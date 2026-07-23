---
sidebar_position: 1
title: Glossary
description: Source-derived glossary for DevHunt services, infrastructure, and core entities.
sidebar_label: Glossary
---

# Glossary

> _If any detail here contradicts the code, trust the code — not this page._

This glossary defines DevHunt terms as they exist in source-derived memory. For full schema details, use the generated [database ERD](../architecture/generated/database-erd) and the entity files under `DevHunt.Infrastructure`.

## Services And Runtime Components

| Term                 | Meaning                                                                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| API Gateway          | Nginx reverse proxy that terminates TLS, routes frontend/API/hub traffic, and strips spoofable `X-User-Id` headers.                              |
| Auth Service         | ASP.NET Core service for registration, login, OAuth, JWT issuance, refresh tokens, TOTP, and email-based account lifecycle.                      |
| Core API             | Main ASP.NET Core business API for projects, tasks, chat, AI planning, showcase, badges, moderation, support, admin, SignalR, and outbox writes. |
| Frontend             | Next.js 16 App Router application using React 19, NextAuth v5 beta, `next-intl`, API proxy rewrites, and route handlers.                         |
| Integration Gateway  | Express 5 service for GitHub/GitLab OAuth, webhooks, repository sync, and code-analysis triggers.                                                |
| ML Service           | FastAPI service for AI generation, recommendations, and project passport synthesis.                                                              |
| Notification Service | Express 5 service for email/SMS/push delivery and OpenObserve alert webhooks. It does not own notification persistence.                          |
| Database Migrator    | One-shot .NET job that runs EF migrations before Auth Service and Core API boot.                                                                 |
| Code Analyzer        | Python HTTP service for tree-sitter/Semgrep analysis, triggered by integration flows.                                                            |

## Infrastructure

| Term         | Meaning                                                                                                                     |
| ------------ | --------------------------------------------------------------------------------------------------------------------------- |
| PostgreSQL   | Primary relational database. Core API and Auth Service use EF Core; ML Service uses asyncpg.                                |
| Redis        | Cache, SignalR backplane, and distributed rate-limit counter backend when enabled.                                          |
| RabbitMQ     | Topic-exchange message broker for event consumers. Core API writes events through the transactional outbox when configured. |
| SeaweedFS    | S3-compatible object storage used for uploaded files and rendered images.                                                   |
| OpenObserve  | Log, trace, and observability backend used by .NET, Node, and Python services.                                              |
| OutboxEvents | Database table used by Core API's outbox worker to publish durable domain events to RabbitMQ.                               |

## Core Domain Entities

| Term               | Entity             | Meaning                                                                                                                                                               |
| ------------------ | ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| User               | `User`             | Platform account with auth fields, profile relations, skills, team memberships, assigned tasks, reports, achievements, and privacy settings.                          |
| Project            | `Project`          | Main collaboration unit owned by a user; linked to team members, tasks, roles, files, documents, news, subscriptions, boosts, showcase entries, and activity records. |
| Team member        | `TeamMember`       | Junction between a project and a user. Status values should be treated as value objects in code, not raw strings.                                                     |
| Invitation         | `Invitation`       | Project/team invitation linking project, inviter, and invitee.                                                                                                        |
| Project role       | `ProjectRole`      | Role/opening definition for a project; `RequiredSkillsJson` is plain text JSON, not jsonb.                                                                            |
| Open role entry    | `OpenRoleEntry`    | Inline project model type used for open role data inside `Project.cs`.                                                                                                |
| Skill              | `Skill`            | Canonical skill record used by user skills and project tech stacks.                                                                                                   |
| Skill alias        | `SkillAlias`       | Alias pointing back to a canonical skill name.                                                                                                                        |
| Project tech stack | `ProjectTechStack` | Junction between project and skill.                                                                                                                                   |
| User skill         | `UserSkill`        | Canonical user-to-skill relation.                                                                                                                                     |
| User skill entry   | `UserSkillEntry`   | Free-form or pre-canonicalized user skill input, optionally linked to `Skill`.                                                                                        |

## Task And Planning Entities

| Term                | Entity              | Meaning                                                                                                                        |
| ------------------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Task                | `TaskItem`          | Work item stored through DbSet `Tasks`; linked to project, creator, assignee, column, GitHub issue id, links, and attachments. |
| Task column         | `TaskColumn`        | Kanban column owned by a project.                                                                                              |
| Task board settings | `TaskBoardSettings` | Per-project board configuration with optional default column.                                                                  |
| Task link           | `TaskLink`          | Relation between two tasks, with source, target, and creator.                                                                  |
| Task attachment     | `TaskAttachment`    | File attachment for a task, optionally pointing at a project file.                                                             |
| AI plan             | `AiPlan`            | AI-generated project plan linked to project and creator.                                                                       |
| AI operation log    | `AiOperationLog`    | Audit/log record for AI operations, optionally linked to an AI plan.                                                           |
| Project artifact    | `ProjectArtifact`   | Generated or uploaded project artifact linked to project and optional generator user.                                          |

## Chat And Notification Entities

| Term                     | Entity                    | Meaning                                                                                                                        |
| ------------------------ | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Conversation             | `Conversation`            | Chat container, optionally linked to a project and creator; owns participants and messages.                                    |
| Conversation participant | `ConversationParticipant` | User membership in a conversation, with optional ban and role-definition links.                                                |
| Channel role definition  | `ChannelRoleDefinition`   | Per-conversation role definition stored in `Channel_Role_Definitions`.                                                         |
| Message                  | `Message`                 | Chat message linked to conversation, project, sender, optional reply target, optional pinning user, reactions, and AI details. |
| AI message details       | `AiMessageDetails`        | One-to-one AI payload details for a message; stores jsonb `FullPayloadJson`.                                                   |
| Message reaction         | `MessageReaction`         | Unique user emoji reaction on a message.                                                                                       |
| Notification             | `Notification`            | In-app notification row linked to recipient user, with polymorphic `RelatedEntityId`.                                          |

## Showcase, Feedback, And Activity Entities

| Term              | Entity            | Meaning                                                                                           |
| ----------------- | ----------------- | ------------------------------------------------------------------------------------------------- |
| Showcase project  | `ShowcaseProject` | Public portfolio entry on top of a project; screenshots and metrics are plain text JSON.          |
| Showcase comment  | `ShowcaseComment` | Threaded comment on a showcase project.                                                           |
| Project news post | `ProjectNewsPost` | Project update post with likes and comments.                                                      |
| News post like    | `NewsPostLike`    | User like on a project news post.                                                                 |
| News post comment | `NewsPostComment` | Comment on a project news post.                                                                   |
| Review            | `Review`          | Review linked to a project, reviewer, and optional reviewed user.                                 |
| Activity record   | `ActivityRecord`  | Timeline record linked to actor, optional project, and optional target user.                      |
| Feedback item     | `FeedbackItem`    | Product feedback item linked to author, optional project, optional assignee, votes, and comments. |
| Feedback vote     | `FeedbackVote`    | User vote on a feedback item.                                                                     |
| Feedback comment  | `FeedbackComment` | Comment on a feedback item.                                                                       |

## Admin, Support, And Moderation Entities

| Term              | Entity             | Meaning                                                                                                            |
| ----------------- | ------------------ | ------------------------------------------------------------------------------------------------------------------ |
| Moderation report | `ModerationReport` | Polymorphic report with `TargetType` and `TargetId`; not a real FK to the reported target.                         |
| Project issue     | `ProjectIssue`     | Issue/report about a project, linked to reporter and optional admin-side users.                                    |
| Support ticket    | `SupportTicket`    | Support case linked to creator, optional assignee, optional project, optional related user, messages, and history. |
| Ticket message    | `TicketMessage`    | Message in a support ticket.                                                                                       |
| Ticket history    | `TicketHistory`    | Audit trail entry for support ticket changes.                                                                      |
| Admin note        | `AdminNote`        | Note about a user, linked to subject and author.                                                                   |
| Audit log         | `AuditLog`         | Audit row with optional user and polymorphic entity id.                                                            |
| Platform setting  | `PlatformSetting`  | Key-value platform setting, including maintenance-mode state.                                                      |
| Feature flag      | `FeatureFlag`      | DB-backed feature flag keyed by string; unknown flags default to enabled in the current service layer.             |

## Integrations, Files, And Recommendations

| Term                    | Entity                  | Meaning                                                                                                     |
| ----------------------- | ----------------------- | ----------------------------------------------------------------------------------------------------------- |
| Integration             | `Integration`           | External integration config linked to a project; `ConfigJson` is plain text JSON.                           |
| Code analysis result    | `CodeAnalysisResult`    | Analyzer output linked to project and optional integration, indexed by project/time and integration/branch. |
| Code analysis embedding | `CodeAnalysisEmbedding` | Vector-style embedding linked to code analysis result and project.                                          |
| Project file            | `ProjectFile`           | File uploaded to a project through object storage.                                                          |
| Project document        | `ProjectDocument`       | Document linked to project and author.                                                                      |
| Recommendation          | `Recommendation`        | Recommendation row linked to target user and project; `ReasoningJson` is plain text JSON.                   |
| LLM model               | `LlmModel`              | Provider/model catalog row seeded by Core API startup.                                                      |
| User API key            | `UserApiKey`            | BYOK provider key linked to a user and encrypted at rest.                                                   |

## Naming And Storage Notes

- `TaskItem` is stored through DbSet `Tasks`; do not assume the DbSet name is `TaskItems`.
- Some table names are underscored through configuration, including `Activity_Records`, `Conversation_Participants`, and `Channel_Role_Definitions`.
- Only `Message.AiMetadataJson` and `AiMessageDetails.FullPayloadJson` are confirmed jsonb columns in the entity catalog. Several other `*Json` fields are plain text.
- `ModerationReport.TargetId` and `Notification.RelatedEntityId` are polymorphic ids, not enforced foreign keys.
