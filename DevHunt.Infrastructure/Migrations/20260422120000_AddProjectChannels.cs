using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <summary>
    /// Introduces Discord/Slack-style <b>project channels</b> by extending the
    /// <c>Conversations</c> table with channel-specific columns and backfilling
    /// a <c>#general</c> channel for every existing project.
    ///
    /// Strategy:
    ///   1. Add columns (<c>ProjectId</c>, <c>Slug</c>, <c>Topic</c>,
    ///      <c>IsPrivate</c>, <c>Position</c>, <c>CreatedByUserId</c>).
    ///   2. Convert legacy "project chats" (Conversations whose <c>Id</c>
    ///      matches a <c>Projects.Id</c>) into <c>#general</c> channels with
    ///      <c>Type = 2 (ProjectChannel)</c> — their messages stay linked via
    ///      the same conversation id, zero message rewrites.
    ///   3. Create a fresh <c>#general</c> for any project that has no chat
    ///      yet, and enrol the project owner + active team members as
    ///      participants so the Chat tab has content on day one.
    ///   4. Add FKs and a filtered unique index guaranteeing one slug per
    ///      project (channels only; Direct/Group rows are untouched).
    /// </summary>
    [DbContext(typeof(DevHuntDbContext))]
    [Migration("20260422120000_AddProjectChannels")]
    public partial class AddProjectChannels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Schema: new channel columns on Conversations.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Conversations""
                ADD COLUMN IF NOT EXISTS ""ProjectId"" uuid NULL,
                ADD COLUMN IF NOT EXISTS ""Slug"" character varying(50) NULL,
                ADD COLUMN IF NOT EXISTS ""Topic"" character varying(280) NULL,
                ADD COLUMN IF NOT EXISTS ""IsPrivate"" boolean NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS ""Position"" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS ""CreatedByUserId"" uuid NULL;
            ");

            // 2. FKs — project deletion nulls the link (channels become orphaned
            //    rather than cascade-deleted, so we can archive cleanly later).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Conversations_Projects_ProjectId'
                    ) THEN
                        ALTER TABLE ""Conversations""
                        ADD CONSTRAINT ""FK_Conversations_Projects_ProjectId""
                        FOREIGN KEY (""ProjectId"") REFERENCES ""Projects""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END$$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Conversations_Users_CreatedByUserId'
                    ) THEN
                        ALTER TABLE ""Conversations""
                        ADD CONSTRAINT ""FK_Conversations_Users_CreatedByUserId""
                        FOREIGN KEY (""CreatedByUserId"") REFERENCES ""Users""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END$$;
            ");

            // 3. Backfill — convert "project chat" rows (Conversation.Id == Project.Id)
            //    into channels. We keep the same row ID so every existing Message
            //    stays attached via ConversationId without any rewrite.
            migrationBuilder.Sql(@"
                UPDATE ""Conversations"" c
                SET
                    ""Type"" = 2,
                    ""ProjectId"" = c.""Id"",
                    ""Slug"" = 'general',
                    ""Position"" = 0,
                    ""IsPrivate"" = false
                FROM ""Projects"" p
                WHERE c.""Id"" = p.""Id""
                  AND c.""Type"" = 1
                  AND c.""ProjectId"" IS NULL;
            ");

            // 4. Create #general for every project that has no chat at all yet
            //    (projects with zero messages / no legacy conversation).
            migrationBuilder.Sql(@"
                INSERT INTO ""Conversations""
                    (""Id"", ""Type"", ""Title"", ""ProjectId"", ""Slug"",
                     ""IsPrivate"", ""Position"", ""CreatedAt"", ""UpdatedAt"")
                SELECT
                    gen_random_uuid(), 2, 'general', p.""Id"", 'general',
                    false, 0, (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""Projects"" p
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""Conversations"" c
                    WHERE c.""ProjectId"" = p.""Id"" AND c.""Type"" = 2
                );
            ");

            // 5. Enrol participants for the freshly-created #general channels —
            //    project owner + active team members. Skip channels that already
            //    have participants from the legacy conversion path.
            migrationBuilder.Sql(@"
                INSERT INTO ""Conversation_Participants""
                    (""Id"", ""ConversationId"", ""UserId"", ""JoinedAt"", ""IsMuted"")
                SELECT
                    gen_random_uuid(), c.""Id"", p.""OwnerId"", (now() AT TIME ZONE 'UTC'), false
                FROM ""Conversations"" c
                JOIN ""Projects"" p ON p.""Id"" = c.""ProjectId""
                WHERE c.""Type"" = 2
                  AND c.""Slug"" = 'general'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""Conversation_Participants"" cp
                      WHERE cp.""ConversationId"" = c.""Id"" AND cp.""UserId"" = p.""OwnerId""
                  );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Conversation_Participants""
                    (""Id"", ""ConversationId"", ""UserId"", ""JoinedAt"", ""IsMuted"")
                SELECT
                    gen_random_uuid(), c.""Id"", tm.""UserId"", (now() AT TIME ZONE 'UTC'), false
                FROM ""Conversations"" c
                JOIN ""TeamMembers"" tm ON tm.""ProjectId"" = c.""ProjectId""
                WHERE c.""Type"" = 2
                  AND c.""Slug"" = 'general'
                  AND tm.""Status"" = 'active' /* TeamMemberStatus.Active */
                  AND NOT EXISTS (
                      SELECT 1 FROM ""Conversation_Participants"" cp
                      WHERE cp.""ConversationId"" = c.""Id"" AND cp.""UserId"" = tm.""UserId""
                  );
            ");

            // 6. Indexes — fast listing by project, plus a filtered unique
            //    (ProjectId, Slug) for channels. Direct/Group rows have NULL
            //    ProjectId or NULL Slug and are excluded by the filter.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Conversations_ProjectId""
                ON ""Conversations"" (""ProjectId"");
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Conversations_ProjectId_Slug""
                ON ""Conversations"" (""ProjectId"", ""Slug"")
                WHERE ""Type"" = 2 AND ""Slug"" IS NOT NULL AND ""ProjectId"" IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Conversations_ProjectId_Slug"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Conversations_ProjectId"";");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Conversations""
                DROP CONSTRAINT IF EXISTS ""FK_Conversations_Users_CreatedByUserId"",
                DROP CONSTRAINT IF EXISTS ""FK_Conversations_Projects_ProjectId"";
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Conversations""
                DROP COLUMN IF EXISTS ""CreatedByUserId"",
                DROP COLUMN IF EXISTS ""Position"",
                DROP COLUMN IF EXISTS ""IsPrivate"",
                DROP COLUMN IF EXISTS ""Topic"",
                DROP COLUMN IF EXISTS ""Slug"",
                DROP COLUMN IF EXISTS ""ProjectId"";
            ");

            // Revert channel type back to Group for rows that were converted.
            // Channels that were created *anew* by this migration cannot be
            // reliably distinguished from legacy rows post-drop; we leave them
            // as Type=1 (Group) which is the closest semantic fallback.
            migrationBuilder.Sql(@"
                UPDATE ""Conversations""
                SET ""Type"" = 1
                WHERE ""Type"" = 2;
            ");
        }
    }
}
