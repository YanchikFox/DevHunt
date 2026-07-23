---
title: Migration timeline — what each EF migration changed
type: data
status: verified
sources:
  - DevHunt.Infrastructure/Migrations/
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## How to use this file

One row per migration, oldest first. The "Changed" column is one
sentence based on the migration filename plus a spot-read of the
`Up()` method for cases where the name was ambiguous. **It is not
a substitute for opening the migration when you need detail** —
treat the row as a hint, not a contract.

If the most recent file in `Migrations/` is newer than the entries
below, this file is stale. The freshest migration at the verified
commit is `20260428120000_AddAiMessageTransparency`.

## Timeline

| Date / time (UTC) | Migration name | Changed |
|---|---|---|
| 2025-01-12 13:00 | `Projects_AddPerformanceIndexes` | Adds non-unique indexes to the `Projects` table for query performance. |
| 2025-10-30 23:55 | `InitialMigration` | Initial schema bootstrap (Users, Projects, basic infrastructure). |
| 2025-10-31 11:55 | `FullFeatureImplementation` | Large follow-up adding most domain tables in one batch. |
| 2025-11-02 00:11 | `ChatModuleImplementation` | Creates `Conversations`, `Conversation_Participants`, `Messages`. |
| 2025-11-02 00:34 | `ChatEnhancements` | Adds extra columns/indexes on top of the chat tables. |
| 2025-11-02 00:45 | `SkillsSystemImplementation` | Creates `Skills`, `UserSkills`, `ProjectTechStacks`. |
| 2025-11-02 00:55 | `ProjectRolesImplementation` | Creates `ProjectRoles` (with the hand-rolled `RequiredSkillsJson` text column). |
| 2025-11-02 01:00 | `ShowcaseProjectsImplementation` | Creates `ShowcaseProjects` (with hand-rolled `ScreenshotsJson` + `MetricsJson` text columns). |
| 2025-11-02 13:36 | `RecommendationsImplementation` | Creates `Recommendations` (with hand-rolled `ReasoningJson` text column). |
| 2025-11-02 13:38 | `IntegrationsImplementation` | Creates `Integrations` (with hand-rolled `ConfigJson` text column). |
| 2025-11-02 14:37 | `AchievementsSystemImplementation` | Creates `Achievements` and `UserAchievements`. |
| 2025-11-03 13:00 | `AddRefreshTokens` | Creates `RefreshTokens` table for auth. |
| 2025-11-03 19:27 | `AddSupportCommunityIssuesFilesPrivacy` | Multi-domain: support tickets, community feedback, project issues, project files, privacy settings — all added in one migration. |
| 2025-11-03 23:28 | `AddTicketHistory` | Adds `TicketHistory` table. |
| 2025-11-04 02:16 | `AddShowcaseCommentsProjectFilesDocs` | Adds `ShowcaseComments` and `ProjectDocuments`. |
| 2025-11-08 13:55 | `ParticipantOnlySeed` | **Misleading name — actually a data cleanup**: `DeleteData` calls remove specific seed rows from `Invitations`, `Tasks`, `TeamMembers`. |
| 2025-11-09 17:04 | `AddInvitationType` | Adds the `Type` column on `Invitations`. |
| 2025-11-12 12:18 | `RefreshToken_AddHashing` | Switches refresh token storage to hashed values. |
| 2025-11-12 13:48 | `OutboxPattern_AddOutboxEvents` | Creates `OutboxEvents` table for the outbox pattern. |
| 2025-11-24 09:49 | `AddActivityProjectNews` | Creates `ActivityRecords` (table `Activity_Records`) and `ProjectNewsPosts`. |
| 2025-11-24 10:56 | `AddUserFollowAndActivityExtensions` | Creates `UserFollows` plus extensions to `Activity_Records`. |
| 2025-11-24 12:44 | `MakeActivityProjectOptional` | Makes `Activity_Records.ProjectId` nullable. **Bundled extra**: also `UpdateData` calls patching the `Skills` array of two specific user rows — schema and seed mixed in one migration. |
| 2025-11-25 00:00 | `AddPrivacyAndPermissions` | Adds privacy / permission columns across multiple tables. |
| 2025-11-25 00:30 | `OptimizeFeedIndexes` | Adds composite indexes used by feed queries. |
| 2025-12-10 18:34 | `AddSocialAuthAndVerification` | Adds `User.GithubId`, `User.GoogleId`, plus email-verification fields. |
| 2025-12-22 17:30 | `AddSkillAliases` | Creates `SkillAliases` table. |
| 2025-12-22 18:25 | `AddUserSkillEntries` | Creates `UserSkillEntries` (free-form skills). |
| 2025-12-23 10:00 | `AddProjectDefaultVisibilities` | Adds project default-visibility columns. |
| 2025-12-29 03:22 | `AddPasswordResetFields` | Adds password-reset token columns on `Users`. |
| 2026-01-05 03:00 | `AddTaskBoardEnhancements` | Creates `TaskColumns`, `TaskLinks`, `TaskAttachments`, `TaskBoardSettings`. |
| 2026-01-10 02:31 | `AddAiPlanning` | Creates `AiPlans` and `AiOperationLogs`. |
| 2026-01-10 03:36 | `AddAiOperationLogMetadata` | Adds metadata column(s) on `AiOperationLogs`. |
| 2026-01-21 16:07 | `AddGitHubSyncFields` | Adds `TaskItem.GitHubIssueId` (and likely related sync fields). |
| 2026-02-09 20:10 | `AddNewsLikesAndComments` | Creates `NewsPostLikes` and `NewsPostComments`. |
| 2026-02-10 18:27 | `AddProjectArtifacts` | Creates `ProjectArtifacts`. |
| 2026-02-18 02:35 | `AddAdminExtensions` | Adds `User.SuspendedUntil` + `User.SuspensionReason` and creates `AdminNotes` (and `PlatformSettings` + `FeatureFlags` per design — but see next row). |
| 2026-02-18 13:00 | `EnsureAdminTables` | Idempotency follow-up using raw `CREATE TABLE IF NOT EXISTS` SQL for `PlatformSettings` and `FeatureFlags` — exists because the previous migration's table creation was unreliable in some environments. |
| 2026-02-19 12:00 | `AddUsernameToUsers` | Adds `User.Username` column. |
| 2026-02-25 12:50 | `AddBadgesUniqueIndexesAndModerationTracking` | Adds unique indexes on badges plus moderation-tracking columns. |
| 2026-03-05 21:40 | `AddCodeAnalysisResults` | Creates `CodeAnalysisResults` (with the composite Project+CreatedAt and Integration+Branch indexes from `OnModelCreating`). |
| 2026-03-10 18:00 | `AddCodeAnalysisEmbeddings` | Creates `CodeAnalysisEmbeddings` (vector embeddings — recall `db` image is `pgvector/pgvector:pg16`). |
| 2026-03-17 12:00 | `AddTotpTwoFactorAuth` | Adds TOTP secret + verification fields on `Users`. |
| 2026-04-21 12:00 | `AddProjectSlugAndBoosts` | Adds `Project.Slug` (filtered unique) and creates `ProjectBoosts` (composite key). |
| 2026-04-22 12:00 | `AddProjectChannels` | Promotes `Conversations` to channel-aware (`Type=2`, `Slug`, per-project filtered unique). |
| 2026-04-22 17:00 | `AddChannelPermissions` | Adds channel-level permission columns on `Conversation_Participants`. |
| 2026-04-22 18:00 | `NormalizeGeneralChannelTitle` | Pure data UPDATE — sets `Conversations.Title = 'general'` for `Type=2 AND Slug='general'` rows where the title diverged. |
| 2026-04-27 12:00 | `AddMessageReactionsAndPins` | Creates `MessageReactions` and adds `Message.IsPinned` / `PinnedByUserId`. |
| 2026-04-27 13:30 | `AddCustomChannelRoles` | Creates `ChannelRoleDefinitions` (table `Channel_Role_Definitions`). |
| 2026-04-27 18:03 | `AddByokAndModelRegistry` | Creates `LlmModels` and `UserApiKeys` (BYOK + provider catalog). |
| 2026-04-28 12:00 | `AddAiMessageTransparency` | Creates `AiMessageDetails` (1:1 to `Message`, jsonb `FullPayloadJson`). |

## What I should NOT assume

- **Filename ≠ scope.** `MakeActivityProjectOptional` and
  `AddAdminExtensions`/`EnsureAdminTables` both bundle changes
  the names don't advertise. Always open the file before
  reasoning about a roll-back or rebase.
- **`ParticipantOnlySeed` is a deletion**, not a seed. The naming
  suggests adding rows; the migration removes them. Don't
  paraphrase from the name.
- **Index dates here are filename timestamps**, not commit
  times. Two migrations sharing a date are not necessarily
  related.
- **Designer/snapshot files are excluded** from this list. They
  are EF-generated companions — never hand-edited. The
  authoritative migration name is the file *without* the
  `.Designer.cs` suffix.
