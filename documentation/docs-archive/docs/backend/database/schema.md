---
sidebar_position: 2
title: Database Schema
description: Entity-relationship diagram and table relationships in DevHunt database
---

# Database Schema

This document provides a comprehensive view of DevHunt's PostgreSQL database schema, including entity relationships and key table structures.

## Entity Relationship Diagram

*Note: ER diagram based on DbContext relationships and model configurations. Some complex many-to-many relationships may be simplified for clarity.*

```mermaid
erDiagram
    %% Core entities
    User ||--o{ UserSkillEntry : owns
    User ||--o{ UserSkill : owns
    User ||--o{ UserAchievement : earns
    User ||--o{ UserFollow : follows
    User ||--o{ UserFollow : followed_by
    User ||--o{ RefreshToken : has
    User ||--o{ UserPrivacySettings : configures

    %% Projects and teams
    User ||--o{ Project : owns
    Project ||--o{ TeamMember : has_members
    TeamMember }o--|| User : member
    Project ||--o{ ProjectRole : defines_roles
    Project ||--o{ ProjectTechStack : uses_technologies
    Project ||--o{ TaskItem : contains
    Project ||--o{ TaskColumn : organizes
    Project ||--o{ ProjectFile : stores
    Project ||--o{ ProjectDocument : documents
    Project ||--o{ ProjectNewsPost : publishes
    Project ||--o{ ProjectSubscription : followed_by
    Project ||--o{ ActivityRecord : generates
    Project ||--o{ Invitation : receives
    Project ||--o{ ShowcaseProject : showcases
    Project ||--o{ Integration : integrates

    %% Task management
    TaskColumn ||--o{ TaskItem : contains
    TaskItem ||--o{ TaskAttachment : has
    TaskItem ||--o{ TaskLink : blocks
    TaskItem ||--o{ TaskLink : blocked_by
    TaskItem }o--o{ TaskItem : "depends on"

    %% Communication
    Project ||--o{ Conversation : has_rooms
    Conversation ||--o{ ConversationParticipant : includes
    ConversationParticipant }o--|| User : participant
    Conversation ||--o{ Message : contains
    Message }o--o{ Message : replies_to
    Message }o--|| User : sent_by

    %% Skills system
    Skill ||--o{ SkillAlias : has_aliases
    Skill ||--o{ UserSkill : associated_with
    Skill ||--o{ ProjectTechStack : used_in
    Skill ||--o{ UserSkillEntry : proficiency_in

    %% Showcase and community
    ShowcaseProject ||--o{ ShowcaseComment : has
    ShowcaseComment }o--o{ ShowcaseComment : replies_to
    ShowcaseComment }o--|| User : authored_by

    FeedbackItem ||--o{ FeedbackVote : receives
    FeedbackItem ||--o{ FeedbackComment : discussed_in
    FeedbackVote }o--|| User : cast_by
    FeedbackComment }o--|| User : authored_by

    %% Support system
    User ||--o{ SupportTicket : creates
    SupportTicket ||--o{ TicketMessage : contains
    SupportTicket ||--o{ TicketHistory : tracks
    TicketMessage }o--|| User : sent_by
    TicketHistory }o--|| User : changed_by

    %% Administration
    User ||--o{ ModerationReport : files
    User ||--o{ ProjectIssue : reports
    ProjectIssue }o--o{ User : assigned_to

    %% Event sourcing
    Project ||--o{ OutboxEvent : publishes
    User ||--o{ Notification : receives

    %% Relationships
    User {
        uuid id PK
        string email UK
        string password_hash
        string role
        string full_name
        string bio
        string timezone
        string_array skills
        integer experience
        float rating
        string avatar_url
        boolean is_verified
        boolean is_active
        datetime created_at
        datetime updated_at
        datetime last_login
        string language
        string github
        string linkedin
        string website
        string github_id UK
        string google_id UK
    }

    Project {
        uuid id PK
        string title
        string description
        string_array tech_stack
        string status
        string visibility
        uuid owner_id FK
        string short_description
        string difficulty_level
        integer expected_duration_days
        datetime start_date
        datetime end_date
        boolean showcase_published
        boolean featured
        float rating
        integer max_team_size
        string_array required_roles
        datetime created_at
        datetime updated_at
    }

    TeamMember {
        uuid project_id FK,PK
        uuid user_id FK,PK
        string role
        datetime joined_at
        boolean is_active
    }

    TaskItem {
        uuid id PK
        string title
        string description
        string status
        string priority
        uuid project_id FK
        uuid assigned_to_user_id FK
        uuid column_id FK
        integer position_in_column
        datetime due_date
        datetime completed_at
        datetime created_at
        datetime updated_at
    }

    Conversation {
        uuid id PK
        string type
        string title
        uuid project_id FK
        boolean is_active
        datetime created_at
        datetime last_message_at
    }

    Message {
        uuid id PK
        uuid conversation_id FK
        uuid sender_id FK
        string content
        uuid reply_to_id FK
        uuid project_id FK
        datetime created_at
    }

    Skill {
        uuid id PK
        string name UK
        string category
        string description
        datetime created_at
    }

    ShowcaseProject {
        uuid project_id PK,FK
        string title
        string description
        string repository_url
        string demo_url
        datetime published_at
        boolean featured
        integer view_count
        integer like_count
    }
```

## Core Tables Overview

### Users Domain

| Table | Purpose | Key Relationships |
|-------|---------|-------------------|
| **Users** | Platform accounts with authentication and profiles | Owner of projects, member of teams, sender of messages |
| **RefreshTokens** | JWT refresh token storage | One-to-many with Users |
| **UserAchievements** | Gamification system | Many-to-many with Users and Achievements |
| **UserFollows** | Social following relationships | Self-referencing many-to-many |
| **UserPrivacySettings** | Privacy controls | One-to-one with Users |

### Projects Domain

| Table | Purpose | Key Relationships |
|-------|---------|-------------------|
| **Projects** | Main project entities | Owned by User, contains TeamMembers, Tasks, etc. |
| **TeamMembers** | Project membership (many-to-many) | Links Users to Projects with roles |
| **ProjectRoles** | Custom roles within projects | Defined per project |
| **ProjectTechStacks** | Technology associations | Links Projects to Skills |
| **Invitations** | Team join requests | Links Projects to Users |

### Task Management Domain

| Table | Purpose | Key Relationships |
|-------|---------|-------------------|
| **TaskItems** | Kanban-style tasks | Belong to Projects, assigned to Users, organized in Columns |
| **TaskColumns** | Kanban board columns | Contain Tasks within Projects |
| **TaskLinks** | Task dependencies | Self-referencing many-to-many on TaskItems |
| **TaskAttachments** | File attachments | Link Tasks to ProjectFiles |

### Communication Domain

| Table | Purpose | Key Relationships |
|-------|---------|-------------------|
| **Conversations** | Chat rooms and direct messages | Belong to Projects, contain Messages |
| **ConversationParticipants** | Users in conversations | Many-to-many between Users and Conversations |
| **Messages** | Chat messages | Belong to Conversations, sent by Users, can reply to other Messages |

### Content Management Domain

| Table | Purpose | Key Relationships |
|-------|---------|-------------------|
| **ProjectFiles** | File uploads with soft deletion | Belong to Projects, uploaded by Users |
| **ProjectDocuments** | Rich text documents | Belong to Projects, authored by Users |
| **ProjectNewsPosts** | News feed items | Belong to Projects, authored by Users |
| **ActivityRecords** | Audit trail | Generated by Projects and Users |

### Integration Domain

| Table | Purpose | Key Relationships |
|-------|---------|-------------------|
| **Integrations** | External service connections | Belong to Projects, one per service type |
| **Recommendations** | AI-generated suggestions | Link Users to Projects |
| **Skills** | Normalized skill taxonomy | Referenced by UserSkills and ProjectTechStacks |
| **SkillAliases** | Alternative skill names | Map to canonical Skills |

## Key Relationship Patterns

### One-to-Many Relationships
- **User → Projects** (ownership)
- **Project → Tasks** (containment)
- **Project → TeamMembers** (membership)
- **Conversation → Messages** (threading)
- **User → Messages** (authorship)

### Many-to-Many Relationships
- **Users ↔ Projects** via TeamMembers
- **Users ↔ Conversations** via ConversationParticipants
- **Users ↔ Users** via UserFollows (social following)
- **Tasks ↔ Tasks** via TaskLinks (dependencies)

### Self-Referencing Relationships
- **Messages → Messages** (reply threading)
- **ShowcaseComments → ShowcaseComments** (nested replies)
- **Tasks → Tasks** (dependency links)

### Polymorphic Relationships
- **ActivityRecords** can reference different entity types (Projects, Users, Tasks)
- **ModerationReports** can target different content types
- **Notifications** can link to various entities

## Data Flow Patterns

### User Registration Flow
```sql
-- 1. Create user account
INSERT INTO "Users" (id, email, password_hash, role, created_at)
VALUES ($1, $2, $3, 'participant', NOW());

-- 2. Initialize privacy settings
INSERT INTO "UserPrivacySettings" (user_id, profile_visibility, contact_visibility)
VALUES ($1, 'public', 'registered');
```

### Project Creation Flow
```sql
-- 1. Create project
INSERT INTO "Projects" (id, title, description, owner_id, status, visibility, created_at, updated_at)
VALUES ($1, $2, $3, $4, 'draft', 'private', NOW(), NOW());

-- 2. Add owner as team member
INSERT INTO "TeamMembers" (project_id, user_id, role, joined_at, is_active)
VALUES ($1, $4, 'owner', NOW(), true);

-- 3. Create default task board
INSERT INTO "TaskColumns" (id, project_id, title, position, created_at)
VALUES (gen_random_uuid(), $1, 'To Do', 0, NOW());
```

### Message Creation Flow
```sql
-- 1. Insert message
INSERT INTO "Messages" (id, conversation_id, sender_id, content, created_at)
VALUES ($1, $2, $3, $4, NOW());

-- 2. Update conversation last activity
UPDATE "Conversations"
SET last_message_at = NOW()
WHERE id = $2;

-- 3. Publish to outbox for notifications
INSERT INTO "OutboxEvents" (id, event_type, aggregate_id, payload, created_at)
VALUES (gen_random_uuid(), 'message.created', $1, $payload_json, NOW());
```

## Index Strategy

### Primary Performance Indexes (confirmed in DbContext)
- **Users**: `email` (unique), `github_id` (unique), `google_id` (unique)
- **Projects**: `(status, visibility, created_at)`, `(status, created_at)`, `difficulty_level`
- **Messages**: `(conversation_id, created_at)`, `(project_id, created_at)`
- **ActivityRecords**: `(visibility, created_at)`, `project_id`, `(actor_id, created_at)`, `event_type`

### Foreign Key Indexes
- EF Core automatically creates indexes for foreign keys
- Additional composite indexes for query optimization (confirmed in DbContext)

### Specialized Indexes (from DbContext configuration)
- **GIN indexes** on JSONB columns (ActivityRecords.PayloadJson, Integration.ConfigJson)
- **Array containment** support via PostgreSQL native functionality (TechStack arrays)
- **Partial indexes** for active records (GithubId/GoogledId when not null)

## Data Integrity Constraints

### Unique Constraints
- User emails must be unique across the platform
- GitHub/Google IDs must be unique when provided
- Team membership is unique per user-project pair
- Skill aliases must be unique
- Achievement codes must be unique

### Check Constraints
- User roles must be from predefined set
- Project status must follow state machine
- Task priority must be from allowed values
- Conversation types must be valid

### Referential Integrity
- Cascade deletes for dependent entities (messages when conversation deleted)
- Restrict deletes for referenced entities (prevent deleting users with active projects)
- Set null for optional relationships (unassigned tasks when user leaves)

This schema provides a solid foundation for DevHunt's complex domain model while maintaining performance and data consistency.