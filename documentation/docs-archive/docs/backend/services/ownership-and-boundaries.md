---
sidebar_position: 1
title: Service Ownership & Boundaries
description: Internal service boundaries, ownership rules, and interaction guidelines for DevHunt backend
---

# Service Ownership & Boundaries

**INTERNAL DOCUMENT - NDA REQUIRED**

This document defines the boundaries and ownership rules for DevHunt's backend services. It serves as the single source of truth for service responsibilities and interaction patterns.

## Service Overview

DevHunt's backend consists of six primary services plus utility services:

- **Core API**: Central business logic and data management
- **Auth Service**: Authentication and authorization
- **Integration Gateway**: External service integrations
- **Notification Service**: Multi-channel notifications
- **ML Service**: AI-powered recommendations and analytics
- **Database Migrator**: Schema management utility
- **Database Seeder**: Test data management utility

## Domain → Owning Service Map

| Domain | Primary Owner | Secondary Writers | Readers |
|--------|---------------|-------------------|---------|
| **User & Identity** | Auth Service | Core API (profile updates) | All services |
| **Projects & Teams** | Core API | - | Frontend, ML Service, Integration Gateway |
| **Tasks & Workflow** | Core API | - | Frontend |
| **Communication** | Core API | - | Frontend |
| **Integrations** | Integration Gateway | Core API (config) | ML Service |
| **Content & Files** | Core API | Integration Gateway | Frontend |
| **Community & Showcase** | Core API | - | Frontend, ML Service |
| **System & Events** | Core API | Notification Service | Background workers |

## Core API Service

### Responsibilities
- Project lifecycle management (creation, updates, status changes)
- Team formation and membership management
- Task and workflow management
- User profile management and updates
- Real-time communication (SignalR hubs)
- Content management (files, documents, news)
- Community features (reviews, showcases)
- Administrative operations

### Owns
- **All project data** (Projects, TeamMembers, Tasks, etc.)
- **User profile updates** (bio, skills, experience)
- **Content and files** (ProjectFiles, ProjectDocuments)
- **Real-time communication** (Conversations, Messages)
- **Community features** (Reviews, ShowcaseProjects)
- **Audit trail** (ActivityRecords)
- **Administrative data** (ModerationReports, SupportTickets)

### Does NOT Own
- User creation and authentication (Auth Service)
- External integrations (Integration Gateway)
- Notification delivery (Notification Service)
- AI recommendations (ML Service)
- Database schema changes (Database Migrator)

### Public Interfaces
- **REST API**: `/api/*` endpoints for all business operations
- **SignalR Hubs**: `/chatHub`, `/notificationHub` for real-time features
- **Events Published**: `project.created`, `team.member.joined`, `task.updated`, `user.activity`

### Inbound Dependencies
- Auth Service (JWT validation)
- PostgreSQL (primary data store)
- Redis (caching, SignalR backplane)
- RabbitMQ (event publishing)

### Service Interfaces

#### Inbound Interfaces
- **HTTP REST**: `/api/projects/*`, `/api/teams/*`, `/api/tasks/*`, `/api/users/profile/*`
- **SignalR Hubs**: `/chatHub`, `/notificationHub`
- **RabbitMQ Consumers**: `devhunt.events.*` (all events)

#### Outbound Interfaces
- **HTTP Calls**: Auth Service JWT validation, ML Service recommendations
- **Event Publishing**: `project.created`, `team.updated`, `task.assigned`, `user.activity`
- **Database Writes**: PostgreSQL (primary), Redis (cache)

### Outbound Dependencies
- Frontend (direct API calls)
- Notification Service (event-driven)
- ML Service (event-driven)
- Integration Gateway (event-driven)

## Auth Service

### Responsibilities
- User registration and account creation
- Password-based authentication
- JWT token generation and validation
- OAuth integration with external providers
- Password reset and email verification
- Refresh token rotation and security

### Owns
- **User accounts** (creation, basic profile setup)
- **Authentication credentials** (passwords, OAuth tokens)
- **JWT tokens** (generation, validation, rotation)
- **Refresh tokens** (secure storage, revocation)
- **Email verification** (tokens, workflow)

### Does NOT Own
- User profile updates beyond authentication
- Project-related user data
- User skills and experience updates
- Social features and relationships

### Public Interfaces
- **REST API**: `/auth/*` endpoints for auth operations
- **Events Published**: `user.registered`, `user.login`, `auth.token_revoked`

### Inbound Dependencies
- PostgreSQL (user accounts, refresh tokens)
- Redis (rate limiting, session data)
- Email service (SMTP configuration)

### Service Interfaces

#### Inbound Interfaces
- **HTTP REST**: `/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/oauth/*`
- **RabbitMQ Consumers**: None (stateless service)

#### Outbound Interfaces
- **HTTP Calls**: Core API (user creation callback)
- **Event Publishing**: `user.registered`, `auth.login`, `auth.token_revoked`
- **Database Writes**: PostgreSQL (users, refresh tokens)

### Outbound Dependencies
- Core API (user profile sync after registration)
- Frontend (redirects after OAuth)

## Integration Gateway

### Responsibilities
- GitHub/GitLab OAuth authentication flows
- Webhook processing and validation
- External API calls to integrated services
- Repository synchronization and metadata updates
- CI/CD pipeline integration
- Event translation and forwarding

### Owns
- **External integrations** (GitHub, GitLab connections)
- **Webhook processing** (signature validation, payload parsing)
- **Integration metadata** (repository links, sync status)
- **OAuth flows** (token exchange, user authorization)

### Does NOT Own
- User accounts or authentication (Auth Service)
- Project business logic (Core API)
- Notification delivery (Notification Service)
- Internal event processing

### Public Interfaces
- **REST API**: `/api/oauth/*`, `/api/webhooks/*` endpoints
- **Events Published**: `integration.webhook_received`, `integration.sync_completed`

### Inbound Dependencies
- RabbitMQ (internal events for triggering syncs)
- External APIs (GitHub/GitLab REST APIs)
- PostgreSQL (integration configuration)

### Service Interfaces

#### Inbound Interfaces
- **HTTP REST**: `/api/oauth/github/url`, `/api/webhooks/github`, `/api/webhooks/gitlab`
- **RabbitMQ Consumers**: `devhunt.integrations.*`
- **External Webhooks**: GitHub/GitLab webhook endpoints

#### Outbound Interfaces
- **HTTP Calls**: GitHub/GitLab APIs, Core API (integration updates)
- **Event Publishing**: `integration.webhook_received`, `integration.sync_completed`
- **Database Writes**: PostgreSQL (integrations, webhooks)

### Outbound Dependencies
- Core API (project updates from webhooks)
- RabbitMQ (forwarded events to other services)

## Notification Service

### Responsibilities
- Multi-channel notification delivery (email, SMS, push)
- Notification preference management
- Template rendering and personalization
- Delivery tracking and retry logic
- Bounce handling and unsubscribe management

### Owns
- **Notification delivery** (email, SMS, push messages)
- **Delivery tracking** (status, retry counts, failures)
- **User preferences** (notification settings)
- **Message templates** (content personalization)

### Does NOT Own
- Notification creation logic (other services)
- User account management
- Content that triggers notifications
- Business rules for when to notify

### Public Interfaces
- **REST API**: `/api/notifications/*` endpoints
- **Events Consumed**: `*.created`, `*.updated`, `*.deleted` patterns

### Inbound Dependencies
- RabbitMQ (notification events from other services)
- PostgreSQL (notification history)
- External APIs (SMTP, SMS, FCM services)

### Service Interfaces

#### Inbound Interfaces
- **HTTP REST**: `/api/notifications/send`, `/api/notifications/batch`
- **RabbitMQ Consumers**: `devhunt.notifications.*`
- **Cron Jobs**: None

#### Outbound Interfaces
- **HTTP Calls**: SMTP services, SMS providers, FCM push APIs
- **Event Publishing**: `notification.delivered`, `notification.failed`
- **Database Writes**: PostgreSQL (notification history)

### Outbound Dependencies
- Email/SMS/Push providers (delivery)
- Redis (rate limiting, deduplication)

## ML Service

### Responsibilities
- User-project recommendation generation
- Skill matching and compatibility analysis
- Activity pattern analysis
- Recommendation personalization
- Performance metrics calculation

### Owns
- **Recommendation algorithms** (matching logic, scoring)
- **Analytics data** (usage patterns, performance metrics)
- **Recommendation cache** (generated suggestions)
- **ML model artifacts** (if applicable)

### Does NOT Own
- User or project business data (Core API)
- Recommendation display logic (Frontend)
- Recommendation triggering events (other services)
- User interaction with recommendations

### Public Interfaces
- **REST API**: `/api/recommendations/*` endpoints
- **Events Consumed**: `project.created`, `user.skill_updated`, `team.activity`

### Inbound Dependencies
- PostgreSQL (read-only access to user/project data)
- RabbitMQ (activity events for model training)
- Redis (recommendation caching)

### Service Interfaces

#### Inbound Interfaces
- **HTTP REST**: `/api/recommendations/user/*`, `/api/recommendations/project/*`
- **RabbitMQ Consumers**: `devhunt.activity.*`, `devhunt.user.skill_updated`
- **Cron Jobs**: Daily model retraining, weekly analytics

#### Outbound Interfaces
- **HTTP Calls**: Core API (recommendation storage)
- **Event Publishing**: None
- **Database Writes**: Redis (cached recommendations)

### Outbound Dependencies
- Core API (recommendation updates via API calls)

## Database Migrator

### Responsibilities
- Database schema deployment and versioning
- Migration execution in production environments
- Migration rollback capabilities
- Schema validation and health checks

### Owns
- **Database schema** (table structures, constraints, indexes)
- **Migration scripts** (EF Core generated migrations)
- **Schema versioning** (migration history tracking)

### Does NOT Own
- Application data or business logic
- Runtime database operations
- Data seeding or test data

### Public Interfaces
- **Command Line**: Executable for migration operations
- **Health Checks**: Database connectivity validation

### Service Interfaces

#### Inbound Interfaces
- **Command Line**: `dotnet ef database update`, `dotnet ef migrations list`
- **CI/CD Integration**: Automated migration scripts
- **Cron Jobs**: None

#### Outbound Interfaces
- **Database Writes**: PostgreSQL (schema changes)
- **Event Publishing**: None
- **File System**: Migration file generation

### Inbound Dependencies
- PostgreSQL (target database)
- EF Core migration files

### Outbound Dependencies
- None (utility service)

## Database Seeder

### Responsibilities
- Test data generation for development environments
- Realistic data population for testing
- Data cleanup and reset operations
- Seed data versioning and consistency

### Owns
- **Test data generation** (realistic user/project data)
- **Development fixtures** (consistent test scenarios)
- **Data cleanup scripts** (reset operations)

### Does NOT Own
- Production data or schemas
- User-generated content
- Business-critical data

### Public Interfaces
- **Command Line**: Executable for seeding operations
- **Scripts**: PowerShell/Bash utilities for data management

### Service Interfaces

#### Inbound Interfaces
- **Command Line**: `./scripts/database/reset-and-seed.ps1`, `dotnet run --seed`
- **CI/CD Integration**: Automated test data setup
- **Cron Jobs**: None

#### Outbound Interfaces
- **Database Writes**: PostgreSQL (test data insertion)
- **Event Publishing**: None
- **File System**: Seed data templates

### Inbound Dependencies
- PostgreSQL (target database)
- Migration completion (must run after migrator)

### Outbound Dependencies
- None (utility service)

## Communication Rules

### Synchronous vs Asynchronous

**Use Synchronous (Direct API Calls):**
- User-initiated operations requiring immediate response
- Authentication and authorization checks
- Data validation and business rule enforcement
- Real-time user interactions

**Use Asynchronous (Events):**
- Background processing and notifications
- Cross-service data synchronization
- Analytics and logging
- External system integrations

### Event Usage Guidelines

**Publish Events When:**
- Business state changes that other services need to react to
- Audit-worthy operations (user actions, system changes)
- Background processing can be deferred
- Multiple services need the same data change notification

**Do NOT Publish Events For:**
- Purely internal state changes
- Real-time user responses (use direct API)
- Sensitive data that shouldn't be broadcast
- High-frequency operations that would flood the bus

### Direct API Call Guidelines

**Acceptable For:**
- Service-to-service authentication checks
- Real-time data validation
- Immediate business rule enforcement
- Critical path operations

**Avoid For:**
- Long-running operations (use async)
- Cross-service business logic coupling
- Operations that can fail independently

## Event & Consistency Appendix

### Event Publishers
- **Core API**: `user.activity`, `project.created`, `task.updated`, `team.member.joined`, `conversation.message_sent`
- **Auth Service**: `user.registered`, `auth.login`, `auth.token_revoked`
- **Integration Gateway**: `integration.webhook_received`, `integration.sync_completed`

### Event Consumers
- **Notification Service**: All events (for user notifications)
- **ML Service**: `user.skill_updated`, `project.created`, `team.activity` (for recommendations)
- **Integration Gateway**: `project.updated` (for external sync triggers)

### Outbox Pattern Requirements
**MUST Use Outbox For:**
- User registration (welcome notifications)
- Project creation (team invites)
- Task assignments (assignee notifications)
- Integration setup (webhook confirmations)
- Security events (login failures, access changes)

**Implementation**: Store events in same transaction as business data, background workers publish to RabbitMQ

### Eventual Consistency Scenarios
**Acceptable For:**
- Recommendation updates (stale recommendations OK for minutes)
- Analytics data (batch processing with hourly delays)
- Integration sync status (webhook retries acceptable)
- Notification delivery status (tracking can be delayed)

### Example Event Contracts

**User Activity Event:**
```json
{
  "id": "uuid",
  "type": "user.activity",
  "occurredAt": "2024-01-01T12:00:00Z",
  "aggregateId": "user-uuid",
  "payload": {
    "action": "project_created",
    "projectId": "project-uuid"
  }
}
```

**Project Created Event:**
```json
{
  "id": "uuid",
  "type": "project.created",
  "occurredAt": "2024-01-01T12:00:00Z",
  "aggregateId": "project-uuid",
  "payload": {
    "ownerId": "user-uuid",
    "visibility": "public"
  }
}
```

**Task Assigned Event:**
```json
{
  "id": "uuid",
  "type": "task.assigned",
  "occurredAt": "2024-01-01T12:00:00Z",
  "aggregateId": "task-uuid",
  "payload": {
    "assigneeId": "user-uuid",
    "projectId": "project-uuid"
  }
}
```

## Forbidden Interactions

❌ **Core API directly calling Notification Service APIs**
❓ **Why**: Creates tight coupling and synchronous dependencies in notification flow
✅ **Instead**: Core API publishes events, Notification Service consumes them asynchronously

❌ **Integration Gateway modifying user data in Core API**
❓ **Why**: Authentication and user management is owned by Auth Service
✅ **Instead**: Integration Gateway redirects to Auth Service OAuth flows

❌ **ML Service directly writing to PostgreSQL**
❓ **Why**: All data mutations must go through owning services for consistency
✅ **Instead**: ML Service calls Core API REST endpoints for updates

❌ **Notification Service querying user preferences from database**
❓ **Why**: User preference management is owned by Core API
✅ **Instead**: Notification Service receives preferences via events or API calls

❌ **Frontend directly accessing Auth Service**
❓ **Why**: Creates multiple authentication entry points and security risks
✅ **Instead**: Frontend uses Core API as single entry point with JWT passthrough

❌ **Using JWT claims as source of truth instead of database**
❓ **Why**: Claims can be stale, tampered, or revoked - always verify against authoritative source
✅ **Instead**: Use claims for optimization, but always validate against database on critical operations

❌ **Using application logs or metrics as business data source**
❓ **Why**: Logs are for debugging, not reliable data storage - can be rotated, filtered, or incomplete
✅ **Instead**: Store business data in PostgreSQL, use logs only for monitoring and debugging

❌ **Calling internal service endpoints from internet-exposed surfaces**
❓ **Why**: Internal APIs lack proper authentication, rate limiting, and input validation for external traffic
✅ **Instead**: All external traffic goes through properly secured API Gateway endpoints

❌ **ML Service directly updating user skill levels in database**
❓ **Why**: User profile management is owned by Core API, cross-service ownership creates inconsistency
✅ **Instead**: ML Service sends skill suggestions via API calls, users confirm changes through Core API

❌ **Notification Service storing user email preferences in its own database**
❓ **Why**: User preference management belongs to Core API, creates data duplication and sync issues
✅ **Instead**: Notification Service queries preferences via API calls or receives them via events

❌ **Integration Gateway bypassing OAuth flows for API access**
❓ **Why**: Security requirement - all external API access must go through proper OAuth validation
✅ **Instead**: Always use OAuth flows to obtain and refresh access tokens before API calls

❌ **Database Seeder running with production environment variables**
❓ **Why**: Test data must never contaminate production systems, environment isolation is critical
✅ **Instead**: Seeder execution is blocked in production environments via environment checks

❌ **Database Seeder running in production environments**
❓ **Why**: Test data should never contaminate production systems
✅ **Instead**: Seeder is restricted to Development environment checks

❌ **Integration Gateway storing decrypted API tokens**
❓ **Why**: Security requirement to protect external service credentials
✅ **Instead**: Always use encrypted storage and runtime decryption

## Data Consistency Strategy

### Strong Consistency Requirements
- **User Authentication**: Must be strongly consistent across all services
- **Project Membership**: Team changes must be immediately visible
- **Financial/Critical Operations**: Any monetary or legal implications
- **Security Events**: Authentication failures, access changes

**Implementation**: Direct API calls, immediate database transactions, synchronous validation

### Eventual Consistency Acceptable
- **Notification Delivery**: Can be delayed without business impact
- **Recommendation Updates**: Stale recommendations acceptable for short periods
- **Analytics Data**: Batch processing with acceptable delays
- **External Sync Status**: Webhook processing can be retried

**Implementation**: Event-driven architecture, outbox pattern, background processing

### Outbox Pattern Usage
- **When to Use**: Any domain event that needs guaranteed delivery
- **Implementation**: Events stored in same transaction as business data
- **Processing**: Background workers consume and publish to RabbitMQ
- **Guarantees**: At-least-once delivery, eventual consistency

**Critical Events Using Outbox:**
- User registration (for welcome notifications)
- Project creation (for team notifications)
- Task assignments (for assignee notifications)
- Integration setup (for webhook confirmations)

This document serves as the authoritative guide for service interactions in DevHunt. All architectural decisions must align with these ownership boundaries and communication rules.