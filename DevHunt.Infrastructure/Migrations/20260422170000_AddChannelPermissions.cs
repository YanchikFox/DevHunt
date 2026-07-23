using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <summary>
    /// Adds channel-level role / permissions / membership state / ban metadata
    /// to <c>Conversation_Participants</c>, and backfills sensible defaults:
    /// <list type="bullet">
    ///   <item>project owner and channel creator get <c>Role = Admin</c>;</item>
    ///   <item>everyone else stays <c>Role = Member</c>;</item>
    ///   <item>all rows start in <c>State = Active</c> with null permission
    ///   overrides (inherit role defaults).</item>
    /// </list>
    /// Also deduplicates any accidental <c>(ConversationId, UserId)</c>
    /// duplicates so the fresh unique index can be applied safely.
    /// </summary>
    [DbContext(typeof(DevHuntDbContext))]
    [Migration("20260422170000_AddChannelPermissions")]
    public partial class AddChannelPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. New columns. Integers for Role/State so EF enums map cleanly.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Conversation_Participants""
                ADD COLUMN IF NOT EXISTS ""Role"" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS ""State"" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS ""CanPost"" boolean NULL,
                ADD COLUMN IF NOT EXISTS ""CanManageMembers"" boolean NULL,
                ADD COLUMN IF NOT EXISTS ""CanEditChannel"" boolean NULL,
                ADD COLUMN IF NOT EXISTS ""CanDeleteChannel"" boolean NULL,
                ADD COLUMN IF NOT EXISTS ""BanReason"" character varying(280) NULL,
                ADD COLUMN IF NOT EXISTS ""BannedAt"" timestamp without time zone NULL,
                ADD COLUMN IF NOT EXISTS ""BannedByUserId"" uuid NULL;
            ");

            // 2. Banner FK — SetNull so deleting the banning moderator's user
            //    row doesn't cascade-remove historical bans.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Conversation_Participants_Users_BannedByUserId'
                    ) THEN
                        ALTER TABLE ""Conversation_Participants""
                        ADD CONSTRAINT ""FK_Conversation_Participants_Users_BannedByUserId""
                        FOREIGN KEY (""BannedByUserId"") REFERENCES ""Users""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END$$;
            ");

            // 3. Deduplicate any stray (ConversationId, UserId) duplicates that
            //    slipped in before the unique index existed. Keep the oldest row.
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT ""Id"",
                           ROW_NUMBER() OVER (
                               PARTITION BY ""ConversationId"", ""UserId""
                               ORDER BY ""JoinedAt"" ASC, ""Id"" ASC
                           ) AS rn
                    FROM ""Conversation_Participants""
                )
                DELETE FROM ""Conversation_Participants""
                WHERE ""Id"" IN (SELECT ""Id"" FROM ranked WHERE rn > 1);
            ");

            // 4. Backfill Role = Admin for project owner on every channel.
            migrationBuilder.Sql(@"
                UPDATE ""Conversation_Participants"" cp
                SET ""Role"" = 1
                FROM ""Conversations"" c
                JOIN ""Projects"" p ON p.""Id"" = c.""ProjectId""
                WHERE cp.""ConversationId"" = c.""Id""
                  AND c.""Type"" = 2
                  AND cp.""UserId"" = p.""OwnerId"";
            ");

            // 5. Backfill Role = Admin for channel creator (owner may also be creator,
            //    no-op in that case). Skip if CreatedByUserId is null (legacy rows).
            migrationBuilder.Sql(@"
                UPDATE ""Conversation_Participants"" cp
                SET ""Role"" = 1
                FROM ""Conversations"" c
                WHERE cp.""ConversationId"" = c.""Id""
                  AND c.""Type"" = 2
                  AND c.""CreatedByUserId"" IS NOT NULL
                  AND cp.""UserId"" = c.""CreatedByUserId"";
            ");

            // 6. Unique index on (ConversationId, UserId) — now safe to create
            //    after dedupe. Prevents future duplicate participations.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Conversation_Participants_ConvUser""
                ON ""Conversation_Participants"" (""ConversationId"", ""UserId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Conversation_Participants_ConvUser"";");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Conversation_Participants""
                DROP CONSTRAINT IF EXISTS ""FK_Conversation_Participants_Users_BannedByUserId"";
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Conversation_Participants""
                DROP COLUMN IF EXISTS ""BannedByUserId"",
                DROP COLUMN IF EXISTS ""BannedAt"",
                DROP COLUMN IF EXISTS ""BanReason"",
                DROP COLUMN IF EXISTS ""CanDeleteChannel"",
                DROP COLUMN IF EXISTS ""CanEditChannel"",
                DROP COLUMN IF EXISTS ""CanManageMembers"",
                DROP COLUMN IF EXISTS ""CanPost"",
                DROP COLUMN IF EXISTS ""State"",
                DROP COLUMN IF EXISTS ""Role"";
            ");
        }
    }
}
