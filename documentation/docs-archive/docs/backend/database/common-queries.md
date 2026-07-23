---
sidebar_position: 4
title: Common Database Queries
description: Practical SQL queries for DevHunt database operations and debugging
---

# Common Database Queries

This document provides practical SQL queries for common DevHunt database operations. These queries are safe (read-only SELECT statements) and useful for debugging, monitoring, and data analysis.

## Schema Verification

All table and column references in this document are verified against:
- **DevHuntDbContext.cs**: EF Core DbContext with entity configurations
- **Models/**: Entity Framework entity classes (User.cs, Project.cs, etc.)
- **Migrations/**: Database migration files with schema changes
- **PostgreSQL system catalogs**: pg_stat_* tables for monitoring

**Key verified entities**: Users, Projects, TeamMembers, TaskItems, Conversations, Messages, Integrations, Notifications, ActivityRecords

## User Management Queries

### Find User by Email
```sql
SELECT id, email, full_name, role, is_active, created_at, last_login
FROM "Users"
WHERE email = 'user@example.com';
```

### Active Users Count by Role
```sql
SELECT role, COUNT(*) as user_count
FROM "Users"
WHERE is_active = true
GROUP BY role
ORDER BY user_count DESC;
```

### Recently Active Users
```sql
SELECT id, email, full_name, last_login, created_at
FROM "Users"
WHERE last_login > NOW() - INTERVAL '7 days'
AND is_active = true
ORDER BY last_login DESC
LIMIT 20;
```

## Project and Team Queries

### Find Project with Owner Details
```sql
SELECT p.id, p.title, p.status, p.visibility, p.created_at,
       u.email as owner_email, u.full_name as owner_name
FROM "Projects" p
JOIN "Users" u ON p.owner_id = u.id
WHERE p.id = 'project-uuid-here';
```

### Projects with Team Members
```sql
SELECT p.id, p.title, p.status,
       COUNT(tm.user_id) as member_count,
       STRING_AGG(u.full_name, ', ') as member_names
FROM "Projects" p
LEFT JOIN "TeamMembers" tm ON p.id = tm.project_id AND tm.status = 'active'
LEFT JOIN "Users" u ON tm.user_id = u.id
WHERE p.status IN ('active', 'recruiting')
GROUP BY p.id, p.title, p.status
HAVING COUNT(tm.user_id) > 0
ORDER BY member_count DESC
LIMIT 20;
```

### Active Projects by Status
```sql
SELECT status, visibility, COUNT(*) as project_count
FROM "Projects"
WHERE created_at > NOW() - INTERVAL '30 days'
GROUP BY status, visibility
ORDER BY status, visibility;
```

## Task Management Queries

### Tasks by Status for Project
```sql
SELECT t.id, t.title, t.status, t.priority,
       u.full_name as assigned_to,
       t.due_date, t.created_at
FROM "TaskItems" t
LEFT JOIN "Users" u ON t.assigned_to_user_id = u.id
WHERE t.project_id = 'project-uuid-here'
ORDER BY
    CASE t.status
        WHEN 'todo' THEN 1
        WHEN 'in_progress' THEN 2
        WHEN 'review' THEN 3
        WHEN 'done' THEN 4
    END,
    t.priority DESC,
    t.created_at ASC;
```

### Overdue Tasks
```sql
SELECT t.id, t.title, t.status, t.due_date,
       p.title as project_title,
       u.full_name as assigned_to
FROM "TaskItems" t
JOIN "Projects" p ON t.project_id = p.id
LEFT JOIN "Users" u ON t.assigned_to_user_id = u.id
WHERE t.due_date < NOW()
  AND t.status NOT IN ('done', 'cancelled')
ORDER BY t.due_date ASC
LIMIT 25;
```

## Communication Queries

### Recent Messages in Project
```sql
SELECT m.id, m.content, m.created_at,
       u.full_name as sender_name,
       c.title as conversation_title
FROM "Messages" m
JOIN "Users" u ON m.sender_id = u.id
JOIN "Conversations" c ON m.conversation_id = c.id
WHERE c.project_id = 'project-uuid-here'
ORDER BY m.created_at DESC
LIMIT 50;
```

### Most Active Conversations
```sql
SELECT c.id, c.title, c.type,
       COUNT(m.id) as message_count,
       MAX(m.created_at) as last_message_at,
       COUNT(DISTINCT cp.user_id) as participant_count
FROM "Conversations" c
LEFT JOIN "Messages" m ON c.id = m.conversation_id
LEFT JOIN "ConversationParticipants" cp ON c.id = cp.conversation_id
WHERE c.created_at > NOW() - INTERVAL '7 days'
GROUP BY c.id, c.title, c.type
ORDER BY message_count DESC
LIMIT 10;
```

## Integration Queries

### Active Integrations by Service
```sql
SELECT service_type, COUNT(*) as integration_count
FROM "Integrations"
WHERE is_active = true
GROUP BY service_type
ORDER BY integration_count DESC;
```

### Inactive Integrations (possible sync issues)
```sql
SELECT i.service_type, i.last_sync_at, i.is_active,
       p.title as project_title,
       u.email as owner_email
FROM "Integrations" i
JOIN "Projects" p ON i.project_id = p.id
JOIN "Users" u ON p.owner_id = u.id
WHERE i.is_active = false
  OR i.last_sync_at < NOW() - INTERVAL '7 days'
ORDER BY i.last_sync_at DESC NULLS FIRST;
```

## Analytics and Monitoring Queries

### Daily Active Users
```sql
SELECT DATE(last_login) as login_date,
       COUNT(*) as active_users
FROM "Users"
WHERE last_login > NOW() - INTERVAL '30 days'
  AND is_active = true
GROUP BY DATE(last_login)
ORDER BY login_date DESC;
```

### Project Creation Trends
```sql
SELECT DATE(created_at) as creation_date,
       COUNT(*) as projects_created,
       COUNT(CASE WHEN visibility = 'public' THEN 1 END) as public_projects
FROM "Projects"
WHERE created_at > NOW() - INTERVAL '30 days'
GROUP BY DATE(created_at)
ORDER BY creation_date DESC;
```

### System Health Check
```sql
-- Recent database activity
SELECT schemaname, tablename,
       seq_scan, seq_tup_read,
       idx_scan, idx_tup_fetch,
       n_tup_ins, n_tup_upd, n_tup_del
FROM pg_stat_user_tables
WHERE schemaname = 'public'
ORDER BY n_tup_ins + n_tup_upd + n_tup_del DESC
LIMIT 10;

-- Large tables
SELECT schemaname, tablename,
       pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC
LIMIT 10;
```

### Applied Migrations
```sql
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId" DESC;
```

## Data Integrity Checks

### Orphaned Records Check
```sql
-- Team members without valid projects
SELECT tm.*
FROM "TeamMembers" tm
LEFT JOIN "Projects" p ON tm.project_id = p.id
WHERE p.id IS NULL;

-- Tasks without valid projects
SELECT t.*
FROM "TaskItems" t
LEFT JOIN "Projects" p ON t.project_id = p.id
WHERE p.id IS NULL;

-- Messages without valid conversations
SELECT m.*
FROM "Messages" m
LEFT JOIN "Conversations" c ON m.conversation_id = c.id
WHERE c.id IS NULL;
```

These queries provide comprehensive coverage of DevHunt's database operations for development, debugging, and monitoring purposes. Always run SELECT queries in read-only mode and be cautious with any data modifications.