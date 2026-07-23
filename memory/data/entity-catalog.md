---
title: Entity catalog — every DbSet → file:line + key relations
type: data
status: verified
sources:
  - DevHunt.Infrastructure/DevHuntDbContext.cs
  - DevHunt.Infrastructure/
  - DevHunt.Infrastructure/Models/
  - DevHunt.Infrastructure/Configuration/EntityConfigurations/
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## How to use this file

One line per DbSet. Format:

`EntityName → path:line — DbSet `Plural`; ↔ <key relations>`

Relations are the FKs and nav-collections most useful for
navigation. Not exhaustive. For full mapping (cascade behavior,
filtered indexes, table-name overrides), open the entity file
**and** the matching `Configuration/EntityConfigurations/*.cs`
when one exists (see "Where mappings live" below).

## Where entity classes live

Entity classes are split across **three** physical locations:

1. `DevHunt.Infrastructure/*.cs` — older entities at project root.
2. `DevHunt.Infrastructure/Models/*.cs` — newer entities.
3. **Inline inside other entity files**:
   - `ProjectBoost` and `OpenRoleEntry` inside `Project.cs`.
   - `AiMessageDetails` and `MessageReaction` inside `Message.cs`.
   - `ChannelRoleDefinition` inside `ConversationParticipant.cs`.

A grep for `class \w+` across both directories is the fastest way
to find a specific entity.

## Where mappings live

Two layers configure entities:

- **Inside `DevHuntDbContext.OnModelCreating`** (lines 132–273) —
  `ModerationReport`, `CodeAnalysisResult`, `Project` (slug
  filter), `ProjectBoost`, `Conversation`,
  `ConversationParticipant`, `ChannelRoleDefinition`, `Message`,
  `AiMessageDetails`, `MessageReaction`. These have the most
  complex constraints (filtered unique indexes, jsonb columns,
  custom cascade behavior).
- **Separate `IEntityTypeConfiguration<T>` classes** under
  [DevHunt.Infrastructure/Configuration/EntityConfigurations/](DevHunt.Infrastructure/Configuration/EntityConfigurations/),
  picked up by `ApplyConfigurationsFromAssembly` at
  [DevHuntDbContext.cs:130](DevHunt.Infrastructure/DevHuntDbContext.cs#L130).
  Files (one per file, may contain multiple `*Configuration` classes):
  `AchievementConfiguration.cs` (Achievement + UserAchievement),
  `ActivityRecordConfiguration.cs`, `AdminConfiguration.cs`
  (ProjectIssue + UserPrivacySettings), `AiConfiguration.cs` (AiPlan +
  AiOperationLog + ProjectArtifact),
  `FeedbackConfiguration.cs` (FeedbackItem + Vote + Comment),
  `InvitationConfiguration.cs`, `LlmConfiguration.cs` (UserApiKey +
  LlmModel), `NewsPostConfiguration.cs` (Like + Comment),
  `ProjectSubscriptionConfiguration.cs`,
  `SupportConfiguration.cs` (SupportTicket + TicketMessage +
  TicketHistory), `TaskBoardConfiguration.cs` (TaskLink +
  TaskAttachment), `UserFollowConfiguration.cs`.

If you need full constraints for an entity, **check both layers**.

## Naming oddities

EF default would name tables after DbSet property names
(`ActivityRecords`, `ConversationParticipants`, …). Several
entities have explicit table names with underscores via the
configuration layer:

- `Activity_Records` (set in `ActivityRecordConfiguration` —
  inferred from migration `MakeActivityProjectOptional`).
- `Channel_Role_Definitions` (set inline in DbContext line 219).
- `Conversation_Participants` (inferred from index name
  `IX_Conversation_Participants_ConvUser` at DbContext line 213).

When writing raw SQL or pgAdmin queries, use the underscored
table name, not the EF DbSet name.

## Conventions to know

- **jsonb columns**: only `Message.AiMetadataJson` and
  `AiMessageDetails.FullPayloadJson`. Others ending in `Json`
  are plain `text` — see
  [gotchas/hand-rolled-json-text-columns.md](../gotchas/hand-rolled-json-text-columns.md).
- **Filtered unique indexes**: `Project.Slug` (when not null);
  `Conversation.Slug` (per-project, only when `Type = 2` channel
  and slug not null). Both rely on PostgreSQL `WHERE` clauses on
  indexes — won't behave the same on other engines.
- **Soft-delete cascades**: `Conversation`'s parent project
  (`SetNull`) and its `CreatedBy` (`SetNull`); `Message.PinnedBy`
  (`SetNull`); `ConversationParticipant.{BannedBy, RoleDefinition}`
  (`SetNull`). Hard cascades: `ProjectBoost ↔ {Project, User}`,
  `ChannelRoleDefinition ↔ Conversation`,
  `MessageReaction ↔ {Message, User}`,
  `AiMessageDetails ↔ Message`.
- **Polymorphic FKs**: `ModerationReport.{TargetType, TargetId}`
  and `Notification.RelatedEntityId` are not real FKs — they're
  raw columns interpreted by application code.

## Catalog (alphabetical)

- `Achievement` → [DevHunt.Infrastructure/Achievement.cs:11](DevHunt.Infrastructure/Achievement.cs#L11) — `Achievements`; ← `UserAchievement` (junction)
- `ActivityRecord` → [DevHunt.Infrastructure/Models/ActivityRecord.cs:11](DevHunt.Infrastructure/Models/ActivityRecord.cs#L11) — `ActivityRecords` (table `Activity_Records`); ↔ `User` (Actor), `Project` (nullable, optional), `User` (TargetUser, nullable)
- `AdminNote` → [DevHunt.Infrastructure/Models/AdminNote.cs:11](DevHunt.Infrastructure/Models/AdminNote.cs#L11) — `AdminNotes`; ↔ `User` (subject, `UserId`), `User` (`AuthorId`)
- `AiMessageDetails` → [DevHunt.Infrastructure/Models/Message.cs:141](DevHunt.Infrastructure/Models/Message.cs#L141) — `AiMessageDetails`; 1:1 ↔ `Message` (PK = `MessageId`, jsonb `FullPayloadJson`, Cascade)
- `AiOperationLog` → [DevHunt.Infrastructure/Models/AiOperationLog.cs:8](DevHunt.Infrastructure/Models/AiOperationLog.cs#L8) — `AiOperationLogs`; ↔ `Project`, `User`, `AiPlan` (nullable)
- `AiPlan` → [DevHunt.Infrastructure/Models/AiPlan.cs:9](DevHunt.Infrastructure/Models/AiPlan.cs#L9) — `AiPlans`; ↔ `Project`, `User` (CreatedBy); → `AiOperationLog` collection
- `AuditLog` → [DevHunt.Infrastructure/AuditLog.cs:11](DevHunt.Infrastructure/AuditLog.cs#L11) — `AuditLogs`; ↔ `User` (nullable); polymorphic `EntityId`
- `ChannelRoleDefinition` → [DevHunt.Infrastructure/Models/ConversationParticipant.cs:151](DevHunt.Infrastructure/Models/ConversationParticipant.cs#L151) — `ChannelRoleDefinitions` (table `Channel_Role_Definitions`); ↔ `Conversation` (Cascade); unique on (`ConversationId`, `Name`)
- `CodeAnalysisEmbedding` → [DevHunt.Infrastructure/Models/CodeAnalysisEmbedding.cs:12](DevHunt.Infrastructure/Models/CodeAnalysisEmbedding.cs#L12) — `CodeAnalysisEmbeddings`; ↔ `CodeAnalysisResult` (`AnalysisResultId`), `Project`
- `CodeAnalysisResult` → [DevHunt.Infrastructure/CodeAnalysisResult.cs:11](DevHunt.Infrastructure/CodeAnalysisResult.cs#L11) — `CodeAnalysisResults`; ↔ `Project`, `Integration` (nullable); composite indexes (Project+CreatedAt, Integration+Branch)
- `Conversation` → [DevHunt.Infrastructure/Models/Conversation.cs:14](DevHunt.Infrastructure/Models/Conversation.cs#L14) — `Conversations`; ↔ `Project` (nullable, SetNull), `User` (CreatedBy, SetNull); → `Participants`, `Messages`
- `ConversationParticipant` → [DevHunt.Infrastructure/Models/ConversationParticipant.cs:14](DevHunt.Infrastructure/Models/ConversationParticipant.cs#L14) — `ConversationParticipants` (table `Conversation_Participants`); ↔ `Conversation`, `User`, `User` (BannedBy, SetNull), `ChannelRoleDefinition` (SetNull); unique (`ConversationId`, `UserId`)
- `FeatureFlag` → [DevHunt.Infrastructure/Models/FeatureFlag.cs:11](DevHunt.Infrastructure/Models/FeatureFlag.cs#L11) — `FeatureFlags`; ↔ `User` (UpdatedBy, nullable); PK = `Key` (string)
- `FeedbackComment` → [DevHunt.Infrastructure/Models/FeedbackComment.cs:8](DevHunt.Infrastructure/Models/FeedbackComment.cs#L8) — `FeedbackComments`; ↔ `FeedbackItem` (`FeedbackId`), `User` (`AuthorId`)
- `FeedbackItem` → [DevHunt.Infrastructure/Models/FeedbackItem.cs:9](DevHunt.Infrastructure/Models/FeedbackItem.cs#L9) — `FeedbackItems`; ↔ `User` (Author), `Project` (Related, nullable), `User` (AssignedTo, nullable); → `Votes`, `Comments`
- `FeedbackVote` → [DevHunt.Infrastructure/Models/FeedbackVote.cs:9](DevHunt.Infrastructure/Models/FeedbackVote.cs#L9) — `FeedbackVotes`; ↔ `FeedbackItem` (`FeedbackId`), `User`
- `Integration` → [DevHunt.Infrastructure/Integration.cs:12](DevHunt.Infrastructure/Integration.cs#L12) — `Integrations`; ↔ `Project`; **plain-text `ConfigJson`** (see hand-rolled-json gotcha)
- `Invitation` → [DevHunt.Infrastructure/Invitation.cs:20](DevHunt.Infrastructure/Invitation.cs#L20) — `Invitations`; ↔ `Project`, `User` (Inviter), `User` (Invitee)
- `LlmModel` → [DevHunt.Infrastructure/Models/LlmModel.cs:10](DevHunt.Infrastructure/Models/LlmModel.cs#L10) — `LlmModels`; standalone catalog table (provider + model registry); seeded on startup by `LlmModelSeeder`
- `Message` → [DevHunt.Infrastructure/Models/Message.cs:12](DevHunt.Infrastructure/Models/Message.cs#L12) — `Messages`; ↔ `Conversation` (nullable), `Project` (nullable), `User` (Sender), `Message` (ReplyTo, nullable), `User` (PinnedBy, SetNull); 1:1 `AiMessageDetails`; **jsonb `AiMetadataJson`**
- `MessageReaction` → [DevHunt.Infrastructure/Models/Message.cs:159](DevHunt.Infrastructure/Models/Message.cs#L159) — `MessageReactions`; ↔ `Message` (Cascade), `User` (Cascade); unique (`MessageId`, `UserId`, `Emoji`)
- `ModerationReport` → [DevHunt.Infrastructure/ModerationReport.cs:6](DevHunt.Infrastructure/ModerationReport.cs#L6) — `ModerationReports`; ↔ `User` (Reporter), `User` (ProcessedBy, nullable); **polymorphic** (`TargetType` + `TargetId`); composite index (TargetType, TargetId, Status)
- `NewsPostComment` → [DevHunt.Infrastructure/Models/NewsPostComment.cs:12](DevHunt.Infrastructure/Models/NewsPostComment.cs#L12) — `NewsPostComments`; ↔ `ProjectNewsPost` (`NewsPostId`), `User` (`AuthorId`)
- `NewsPostLike` → [DevHunt.Infrastructure/Models/NewsPostLike.cs:11](DevHunt.Infrastructure/Models/NewsPostLike.cs#L11) — `NewsPostLikes`; ↔ `ProjectNewsPost` (`NewsPostId`), `User`
- `Notification` → [DevHunt.Infrastructure/Notification.cs:6](DevHunt.Infrastructure/Notification.cs#L6) — `Notifications`; ↔ `User` (recipient); polymorphic `RelatedEntityId`
- `OutboxEvent` → [DevHunt.Infrastructure/OutboxEvent.cs:8](DevHunt.Infrastructure/OutboxEvent.cs#L8) — `OutboxEvents`; standalone (outbox pattern; written by `OutboxEventBusDecorator`, drained by `OutboxEventProcessorWorker`)
- `PlatformSetting` → [DevHunt.Infrastructure/Models/PlatformSetting.cs:11](DevHunt.Infrastructure/Models/PlatformSetting.cs#L11) — `PlatformSettings`; ↔ `User` (UpdatedBy, nullable); PK = `Key` (string)
- `Project` → [DevHunt.Infrastructure/Project.cs:38](DevHunt.Infrastructure/Project.cs#L38) — `Projects`; ↔ `User` (Owner); → `TeamMembers`, `Tasks`, `Invitations`, `TechStacks`, `ProjectRoles`, `ShowcaseProjects`, `ProjectFiles`, `ProjectDocuments`, `ProjectNewsPosts`, `ProjectSubscriptions`, `ActivityRecords`, `Boosts`; filtered-unique `Slug`
- `ProjectArtifact` → [DevHunt.Infrastructure/ProjectArtifact.cs:11](DevHunt.Infrastructure/ProjectArtifact.cs#L11) — `ProjectArtifacts`; ↔ `Project`, `User` (GeneratedBy, nullable)
- `ProjectBoost` → [DevHunt.Infrastructure/Project.cs:115](DevHunt.Infrastructure/Project.cs#L115) — `ProjectBoosts`; composite key (`ProjectId`, `UserId`); both Cascade; `↔ Project.Boosts`
- `ProjectDocument` → [DevHunt.Infrastructure/Models/ProjectDocument.cs:11](DevHunt.Infrastructure/Models/ProjectDocument.cs#L11) — `ProjectDocuments`; ↔ `Project`, `User` (`AuthorId`)
- `ProjectFile` → [DevHunt.Infrastructure/Models/ProjectFile.cs:11](DevHunt.Infrastructure/Models/ProjectFile.cs#L11) — `ProjectFiles`; ↔ `Project`, `User` (UploadedBy)
- `ProjectIssue` → [DevHunt.Infrastructure/Models/ProjectIssue.cs:9](DevHunt.Infrastructure/Models/ProjectIssue.cs#L9) — `ProjectIssues`; ↔ `Project`, `User` (Reporter); admin-side: `User` (AssignedToAdmin, ResolvedByAdmin, RelatedUser — all nullable)
- `ProjectNewsPost` → [DevHunt.Infrastructure/Models/ProjectNewsPost.cs:12](DevHunt.Infrastructure/Models/ProjectNewsPost.cs#L12) — `ProjectNewsPosts`; ↔ `Project`, `User` (Author); → `Likes`, `Comments`
- `ProjectRole` → [DevHunt.Infrastructure/ProjectRole.cs:12](DevHunt.Infrastructure/ProjectRole.cs#L12) — `ProjectRoles`; ↔ `Project`; **plain-text `RequiredSkillsJson`** (hand-rolled JSON)
- `ProjectSubscription` → [DevHunt.Infrastructure/Models/ProjectSubscription.cs:10](DevHunt.Infrastructure/Models/ProjectSubscription.cs#L10) — `ProjectSubscriptions`; ↔ `Project`, `User`
- `ProjectTechStack` → [DevHunt.Infrastructure/ProjectTechStack.cs:11](DevHunt.Infrastructure/ProjectTechStack.cs#L11) — `ProjectTechStacks`; junction `Project ↔ Skill`
- `Recommendation` → [DevHunt.Infrastructure/Recommendation.cs:12](DevHunt.Infrastructure/Recommendation.cs#L12) — `Recommendations`; ↔ `Project`, `User` (target); **plain-text `ReasoningJson`**
- `RefreshToken` → [DevHunt.Infrastructure/RefreshToken.cs:12](DevHunt.Infrastructure/RefreshToken.cs#L12) — `RefreshTokens`; ↔ `User`; values are stored hashed (per migration `RefreshToken_AddHashing`)
- `Review` → [DevHunt.Infrastructure/Review.cs:6](DevHunt.Infrastructure/Review.cs#L6) — `Reviews`; ↔ `Project`, `User` (Reviewer), `User` (Reviewed, nullable)
- `ShowcaseComment` → [DevHunt.Infrastructure/Models/ShowcaseComment.cs:11](DevHunt.Infrastructure/Models/ShowcaseComment.cs#L11) — `ShowcaseComments`; ↔ `ShowcaseProject`, `User` (Author), self (`ParentCommentId`, threading); → `Replies`
- `ShowcaseProject` → [DevHunt.Infrastructure/ShowcaseProject.cs:12](DevHunt.Infrastructure/ShowcaseProject.cs#L12) — `ShowcaseProjects`; ↔ `Project`; → `Comments`; **plain-text `ScreenshotsJson` + `MetricsJson`**
- `Skill` → [DevHunt.Infrastructure/Skill.cs:11](DevHunt.Infrastructure/Skill.cs#L11) — `Skills`; → `UserSkills`, `ProjectTechStacks`
- `SkillAlias` → [DevHunt.Infrastructure/SkillAlias.cs:11](DevHunt.Infrastructure/SkillAlias.cs#L11) — `SkillAliases`; ↔ `Skill` (canonical name → aliases)
- `SupportTicket` → [DevHunt.Infrastructure/Models/SupportTicket.cs:9](DevHunt.Infrastructure/Models/SupportTicket.cs#L9) — `SupportTickets`; ↔ `User` (creator), `User` (AssignedTo, nullable), `Project` (Related, nullable), `User` (RelatedUser, nullable); → `Messages`
- `TaskAttachment` → [DevHunt.Infrastructure/Models/TaskAttachment.cs:9](DevHunt.Infrastructure/Models/TaskAttachment.cs#L9) — `TaskAttachments`; ↔ `TaskItem` (`TaskId`), `User` (AttachedBy), `ProjectFile` (nullable, optional file pointer)
- `TaskBoardSettings` → [DevHunt.Infrastructure/Models/TaskBoardSettings.cs:9](DevHunt.Infrastructure/Models/TaskBoardSettings.cs#L9) — `TaskBoardSettings`; 1:1 ↔ `Project`; → `TaskColumn` (DefaultColumn, nullable)
- `TaskColumn` → [DevHunt.Infrastructure/Models/TaskColumn.cs:9](DevHunt.Infrastructure/Models/TaskColumn.cs#L9) — `TaskColumns`; ↔ `Project`; → `Tasks`
- `TaskItem` → [DevHunt.Infrastructure/TaskItem.cs:23](DevHunt.Infrastructure/TaskItem.cs#L23) — `Tasks` (note DbSet name diverges from class name); ↔ `Project`, `User` (CreatedBy), `User` (AssignedTo, nullable), `TaskColumn` (nullable); `GitHubIssueId` for sync; → `OutgoingLinks`, `IncomingLinks`, `Attachments`
- `TaskLink` → [DevHunt.Infrastructure/Models/TaskLink.cs:9](DevHunt.Infrastructure/Models/TaskLink.cs#L9) — `TaskLinks`; ↔ `TaskItem` (Source), `TaskItem` (Target), `User` (CreatedBy)
- `TeamMember` → [DevHunt.Infrastructure/TeamMember.cs:20](DevHunt.Infrastructure/TeamMember.cs#L20) — `TeamMembers`; junction `Project ↔ User`
- `TicketHistory` → [DevHunt.Infrastructure/Models/TicketHistory.cs:9](DevHunt.Infrastructure/Models/TicketHistory.cs#L9) — `TicketHistories`; ↔ `SupportTicket` (`TicketId`), `User` (`ChangedByUserId`)
- `TicketMessage` → [DevHunt.Infrastructure/Models/TicketMessage.cs:8](DevHunt.Infrastructure/Models/TicketMessage.cs#L8) — `TicketMessages`; ↔ `SupportTicket` (`TicketId`), `User` (`AuthorId`)
- `User` → [DevHunt.Infrastructure/User.cs:23](DevHunt.Infrastructure/User.cs#L23) — `Users`; → `TeamMemberships`, `AssignedTasks`, `SentInvitations`, `ReceivedInvitations`, `Reports`, `UserSkills`, `UserAchievements`, `UserSkillEntries`; auth fields: `GithubId`, `GoogleId`, password-reset, TOTP, suspension
- `UserAchievement` → [DevHunt.Infrastructure/UserAchievement.cs:11](DevHunt.Infrastructure/UserAchievement.cs#L11) — `UserAchievements`; junction `User ↔ Achievement`
- `UserApiKey` → [DevHunt.Infrastructure/Models/UserApiKey.cs:11](DevHunt.Infrastructure/Models/UserApiKey.cs#L11) — `UserApiKeys`; ↔ `User` (BYOK — encrypted provider keys)
- `UserFollow` → [DevHunt.Infrastructure/Models/UserFollow.cs:10](DevHunt.Infrastructure/Models/UserFollow.cs#L10) — `UserFollows`; self-junction `User ↔ User` (`FollowerId`, `FollowedId`)
- `UserPrivacySettings` → [DevHunt.Infrastructure/Models/UserPrivacySettings.cs:9](DevHunt.Infrastructure/Models/UserPrivacySettings.cs#L9) — `UserPrivacySettings`; 1:1 ↔ `User`
- `UserSkill` → [DevHunt.Infrastructure/UserSkill.cs:11](DevHunt.Infrastructure/UserSkill.cs#L11) — `UserSkills`; junction `User ↔ Skill` (canonical skill linkage)
- `UserSkillEntry` → [DevHunt.Infrastructure/UserSkillEntry.cs:11](DevHunt.Infrastructure/UserSkillEntry.cs#L11) — `UserSkillEntries`; ↔ `User`, `Skill` (nullable — free-form skill input pre-canonicalization)

## What I should NOT assume

- **Class location is not predictable from the name.** Newer
  entities are under `Models/`, older at root, several are
  inline. Do not skip the grep.
- **Cascade behavior is sometimes overridden** in the
  configuration layer. The relations listed here are the FKs
  themselves; check the matching `*Configuration.cs` for the
  exact `OnDelete` policy when the answer matters.
- **Some DbSets are written by background workers, not
  controllers.** `OutboxEvents` (worker), `LlmModels` (seeder
  on startup), `AiMessageDetails` (created alongside
  `Message`). Don't expect controller-level audit trails for
  rows in those tables.
- **`Tasks` is the DbSet name for `TaskItem`.** Raw EF queries
  reference `Tasks`; don't refactor the property to "TaskItems"
  without a migration.
