---
sidebar_position: 5
title: Data Models Overview
description: Domain models and business logic relationships in DevHunt
---

# Data Models Overview

DevHunt's data models represent the core business domains and encapsulate the platform's domain logic. This document explains why each model exists, how it's used, and the business rules it enforces.

## Introduction

DevHunt's data models are designed around business capabilities rather than technical storage. Each model represents a business concept with well-defined responsibilities, lifecycle, and relationships. Models enforce business rules through EF Core configurations, constraints, and application logic.

**Key Principles:**
- **Domain-Driven Design**: Models reflect business concepts, not database tables
- **Bounded Contexts**: Related models are grouped by business domain
- **Business Rules**: Models enforce invariants and constraints
- **Audit Trail**: Critical changes are tracked for compliance

## User & Identity Domain

**Ownership & Write Rules:**
- **Primary Writer**: Auth Service (user creation, authentication, refresh tokens)
- **Secondary Writer**: Core API (profile updates, role management by admins)
- **Read-Only Services**: All other services (user context for authorization)
- **Write Path**: All changes must go through Auth Service or Core API endpoints

### User Model
**Purpose**: Represents registered platform users and their identity across all DevHunt services.

**Owner Service**: Auth Service (creates/manages authentication), Core API (updates profile)

**Readers**: All services (user context), Frontend (display profiles)

**Lifecycle**:
- **Creation**: Via Auth Service registration or OAuth signup
- **Updates**: Profile changes through Core API, role changes by admins
- **Soft Delete**: `IsActive = false` preserves data for audit/compliance

**Invariants**:
- Email must be unique and verified for login
- Role must be from predefined set (participant, company, curator, admin)
- Social auth IDs must be unique when provided

### RefreshToken Model
**Purpose**: Securely stores JWT refresh tokens for session management with rotation to prevent token theft.

**Owner Service**: Auth Service

**Readers**: Auth Service only (validation/rotation)

**Lifecycle**: Created on login, rotated on refresh, revoked on logout/security events

**Invariants**:
- Token hash must be unique to prevent replay attacks
- User association required and immutable
- Automatic expiration based on security policy

### UserSkill Model
**Purpose**: Junction table linking users to their skills with proficiency levels.

**Owner Service**: Core API

**Readers**: Core API (skill matching), Frontend (user profiles), ML Service (recommendations)

**Lifecycle**: Created when users add skills, updated for proficiency changes

**Invariants**: One skill entry per user-skill pair, proficiency level must be valid

### Skill Model
**Purpose**: Canonical skill definitions used across the platform for standardization.

**Owner Service**: Core API (admin operations)

**Readers**: All services (skill validation and display)

**Lifecycle**: Created by admins, rarely modified to maintain consistency

**Invariants**: Name must be unique, category provides grouping taxonomy

### SkillAlias Model
**Purpose**: Alternative names and spellings for skills to improve matching and discovery.

**Owner Service**: Core API (admin operations)

**Readers**: Core API (skill normalization), ML Service (text processing)

**Lifecycle**: Created to handle common variations, updated for new aliases

**Invariants**: Alias must be unique, maps to exactly one canonical skill

## Projects & Teams Domain

**Ownership & Write Rules:**
- **Primary Writer**: Core API (all project and team operations)
- **Read-Only Services**: Frontend (display), ML Service (analytics), Integration Gateway (context)
- **Write Path**: All team changes must go through Core API with proper authorization checks
- **Event Publishing**: Core API publishes team changes to RabbitMQ for notifications

### Project Model
**Purpose**: Core entity representing collaborative work initiatives with status tracking and visibility controls.

**Owner Service**: Core API

**Readers**: All services (project context), Frontend (display/project management)

**Lifecycle**:
- **Creation**: By authenticated users through Core API
- **Status Flow**: draft → recruiting → active → completed → archived
- **Visibility**: public/private/unlisted controls discovery and access

**Invariants**:
- Owner cannot be changed after creation
- Status transitions must follow business rules
- Tech stack must be valid skill references

### TeamMember Model
**Purpose**: Manages user participation in projects with granular permissions and roles.

**Owner Service**: Core API

**Readers**: Core API (authorization), Frontend (team management)

**Lifecycle**: Created by project owners/leaders, updated for role changes, soft-deleted on removal

**Invariants**:
- One user per project (unique constraint)
- Permissions cascade from roles (IsLeader grants management rights)
- Status tracks active/inactive/left states

### Invitation Model
**Purpose**: Manages project join requests and team invitations with acceptance tracking.

**Owner Service**: Core API

**Readers**: Core API (invitation processing), Frontend (invitation management)

**Lifecycle**: Created by project leaders, accepted/rejected by invitees, expires after timeout

**Invariants**:
- Unique per project-invitee combination
- Inviter must be active team member with invitation rights
- Type must be from predefined set (invite, application)

### ProjectRole Model
**Purpose**: Defines custom roles within projects beyond basic team membership.

**Owner Service**: Core API

**Readers**: Core API (authorization), Frontend (role management)

**Lifecycle**: Created by project owners, assigned to team members, can be modified

**Invariants**: Role name must be unique within project, belongs to one project only

### ProjectSubscription Model
**Purpose**: Tracks user subscriptions to project updates and notifications.

**Owner Service**: Core API

**Readers**: Core API (notification routing), Frontend (subscription status)

**Lifecycle**: Created when users subscribe, removed on unsubscribe or project deletion

**Invariants**: One subscription per user-project pair, prevents duplicate notifications

## Tasks & Workflow Domain

**Ownership & Write Rules:**
- **Primary Writer**: Core API (all task and workflow operations)
- **Read-Only Services**: Frontend (task boards), ML Service (workflow analytics)
- **Write Path**: All task changes must validate project membership and permissions
- **Real-time Updates**: Changes published via SignalR for live board updates

### TaskItem Model
**Purpose**: Represents individual work items in project kanban boards with status and assignment tracking.

**Owner Service**: Core API

**Readers**: Core API (task operations), Frontend (task boards)

**Lifecycle**: Created within projects, assigned to team members, moved through status workflow

**Invariants**:
- Must belong to existing project
- Assignee must be active team member (nullable for unassigned)
- Status must be from workflow states (todo, in_progress, review, done)

### TaskColumn Model
**Purpose**: Defines kanban board columns with ordering for task organization.

**Owner Service**: Core API

**Readers**: Core API, Frontend (board layout)

**Lifecycle**: Created per project, reordered by team members

**Invariants**: Position must be unique within project, belongs to one project only

## Communication Domain

**Ownership & Write Rules:**
- **Primary Writer**: Core API (SignalR integration for real-time messaging)
- **Read-Only Services**: Frontend (message display), ML Service (communication analytics)
- **Write Path**: All messages must go through Core API with SignalR broadcasting
- **Moderation**: Content filtering applied before storage

### Conversation Model
**Purpose**: Chat rooms for project communication and direct messaging between users.

**Owner Service**: Core API (SignalR integration)

**Readers**: Core API, Frontend (real-time messaging)

**Lifecycle**: Created for projects or direct messages, archived when inactive

**Invariants**:
- Project conversations require active project membership
- Direct conversations require mutual following/acquaintance

### ConversationParticipant Model
**Purpose**: Tracks which users are members of which conversations for access control.

**Owner Service**: Core API

**Readers**: Core API (message authorization), Frontend (participant lists)

**Lifecycle**: Added when user joins conversation, removed on leave or conversation archive

**Invariants**: Unique per conversation-user pair, user must have access rights

### Message Model
**Purpose**: Individual chat messages with threading and content management.

**Owner Service**: Core API

**Readers**: Core API, Frontend (message history)

**Lifecycle**: Created in real-time, soft-deleted for moderation

**Invariants**:
- Sender must be conversation participant
- Reply-to references must be valid messages
- Content cannot be empty

## Integrations Domain

**Ownership & Write Rules:**
- **Primary Writer**: Integration Gateway (webhook processing and API calls)
- **Secondary Writer**: Core API (integration setup and configuration)
- **Read-Only Services**: ML Service (integration analytics)
- **Write Path**: Configuration changes must validate API connectivity

### Integration Model
**Purpose**: External service connections (GitHub, GitLab) with webhook handling and API access.

**Owner Service**: Integration Gateway

**Readers**: Integration Gateway (webhook processing), Core API (status checks)

**Lifecycle**: Created by project owners, configured with API keys, deactivated on errors

**Invariants**:
- One integration type per project (unique constraint)
- Service type must be supported (github, gitlab)
- Access tokens encrypted at rest

## Content & Files Domain

**Ownership & Write Rules:**
- **Primary Writer**: Core API (all content operations)
- **Supporting Writer**: Integration Gateway (webhook-generated content)
- **Read-Only Services**: Frontend (content display), ML Service (content analysis)
- **Storage**: File content stored in SeaweedFS, metadata in database

### ProjectFile Model
**Purpose**: Metadata for user-uploaded files with soft deletion and access control.

**Owner Service**: Core API

**Readers**: Core API (file operations), Frontend (file listings)

**Lifecycle**: Created on upload, soft-deleted to preserve references

**Invariants**:
- Must belong to existing project
- Uploader must be active team member
- Size/content-type validation enforced

### ProjectDocument Model
**Purpose**: Rich text documents and wiki pages for project documentation.

**Owner Service**: Core API

**Readers**: Core API, Frontend (document viewing)

**Lifecycle**: Created by team members, versioned through updates

**Invariants**: Must belong to project, author must be team member

### ProjectNewsPost Model
**Purpose**: News feed items and announcements within projects.

**Owner Service**: Core API

**Readers**: Core API, Frontend (news feeds)

**Lifecycle**: Created by authorized team members, visibility-controlled

**Invariants**: Author must have posting permissions, visibility rules enforced

### ActivityRecord Model
**Purpose**: Comprehensive audit trail of all user and project activities for transparency and analytics.

**Owner Service**: Core API (automatic logging)

**Readers**: Core API (analytics), Admin interfaces (audit reports)

**Lifecycle**: Automatically created for significant events, retained indefinitely

**Invariants**:
- Actor must be valid user (nullable for system events)
- Event type from predefined taxonomy
- Payload contains structured event data

## Community & Showcase Domain

**Ownership & Write Rules:**
- **Primary Writer**: Core API (all community operations)
- **Read-Only Services**: Frontend (display), ML Service (analytics), Admin interfaces (moderation)
- **Write Path**: All community content must pass moderation rules
- **Featured Content**: Admin-only controls for highlighting content

### ShowcaseProject Model
**Purpose**: Published project portfolios for community discovery and recognition.

**Owner Service**: Core API

**Readers**: Frontend (showcase listings), ML Service (recommendations)

**Lifecycle**: Created from completed projects, featured by admins

**Invariants**:
- One showcase per project
- Requires completed project status
- Featured status controlled by admins

### Review Model
**Purpose**: User feedback and ratings for project quality assessment.

**Owner Service**: Core API

**Readers**: Frontend (project ratings), Core API (aggregations)

**Lifecycle**: Created by team members for completed projects

**Invariants**:
- Reviewer must be different from reviewed user
- One review per reviewer-reviewed-user-project combination

## System & Events Domain

**Ownership & Write Rules:**
- **Primary Writer**: Core API (outbox events, activity logging)
- **Secondary Writer**: Notification Service (notification creation)
- **Read-Only Services**: Background workers, Admin interfaces
- **Event Flow**: All domain events must use outbox pattern for reliability

### OutboxEvent Model
**Purpose**: Reliable event publishing using the Outbox Pattern for distributed transactions.

**Owner Service**: Core API (transactional writes)

**Readers**: Background workers (event processing)

**Lifecycle**: Created in database transactions, processed by workers, cleaned up after success

**Invariants**:
- Status tracks processing state (pending → processing → completed/failed)
- Event type from predefined set
- Payload contains serializable event data

### Notification Model
**Purpose**: User notifications with delivery tracking and read status.

**Owner Service**: Notification Service (creation), Core API (status updates)

**Readers**: Frontend (user inbox), Notification Service (delivery)

**Lifecycle**: Created by services, marked as read by users, archived after period

**Invariants**:
- User must exist and be active
- Type must be supported notification type
- Related entity references must be valid

### SupportTicket Model
**Purpose**: Customer support requests with conversation threading and resolution tracking.

**Owner Service**: Core API (ticket operations)

**Readers**: Core API, Support interfaces, Admin dashboards

**Lifecycle**: Created by users, assigned to agents, resolved with status updates

**Invariants**: Creator must be authenticated user, assignment requires agent role

### ModerationReport Model
**Purpose**: User-reported content violations requiring admin review and action.

**Owner Service**: Core API

**Readers**: Admin interfaces, Moderation teams

**Lifecycle**: Created by users, reviewed by moderators, resolved with actions

**Invariants**: Reporter must be different from reported user, target must exist

## Cross-Domain Relationships

### User-Project Ecosystem
Users create projects, join teams, work on tasks, communicate, and showcase results. This creates a complete participation lifecycle from discovery to recognition.

### Event-Driven Architecture
Outbox events drive integration processing, notifications, and analytics. This ensures reliable cross-service communication without tight coupling.

### Audit & Compliance
ActivityRecords and Reviews provide transparency and quality signals across all domains. Soft deletes preserve referential integrity.

## Anti-Patterns

### Authorization Bypass
❌ **Direct database queries from frontend applications**
❓ **Why**: Bypasses authentication, authorization, and business rules
✅ **Instead**: Always use Core API endpoints with proper JWT validation and role checks

❌ **Modifying user roles directly in database without audit**
❓ **Why**: Critical security changes must be logged and validated
✅ **Instead**: Use Core API admin endpoints that create ActivityRecords and validate permissions

### Cross-Service Data Modification
❌ **Notification Service directly updating project status**
❓ **Why**: Only Core API owns project lifecycle and business rules
✅ **Instead**: Notification Service should only read data and send notifications via external APIs

❌ **Integration Gateway creating users without Auth Service**
❓ **Why**: User creation requires password hashing, email verification, and role assignment
✅ **Instead**: Integration Gateway should redirect to Auth Service OAuth flows

❌ **ML Service directly modifying user recommendations**
❓ **Why**: Recommendation data is owned by Core API with business validation
✅ **Instead**: ML Service should send recommendation updates via API calls to Core API

### Data Integrity Violations
❌ **Creating team members without checking project membership rules**
❓ **Why**: Business rules require checking project capacity, user roles, and invitation status
✅ **Instead**: Always use Core API TeamMember endpoints that enforce all business constraints

❌ **Soft delete bypass by setting IsActive=true without validation**
❓ **Why**: Deactivated entities may have cascading effects on other business rules
✅ **Instead**: Use reactivation endpoints that validate all dependent data and permissions

❌ **Storing unencrypted sensitive data like API tokens**
❓ **Why**: Security requirement to protect user credentials and service secrets
✅ **Instead**: Always use encrypted fields and proper key management through Core API

This model architecture ensures DevHunt's data layer supports complex business requirements while maintaining performance, integrity, and evolvability.