using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    [DbContext(typeof(DevHuntDbContext))]
    [Migration("20260427120000_AddMessageReactionsAndPins")]
    public partial class AddMessageReactionsAndPins : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Messages""
                ADD COLUMN IF NOT EXISTS ""IsPinned"" boolean NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS ""PinnedAt"" timestamp with time zone NULL,
                ADD COLUMN IF NOT EXISTS ""PinnedByUserId"" uuid NULL;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Messages_Users_PinnedByUserId'
                    ) THEN
                        ALTER TABLE ""Messages""
                        ADD CONSTRAINT ""FK_Messages_Users_PinnedByUserId""
                        FOREIGN KEY (""PinnedByUserId"") REFERENCES ""Users""(""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END$$;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Messages_ConversationId_IsPinned_CreatedAt""
                ON ""Messages"" (""ConversationId"", ""IsPinned"", ""CreatedAt"");
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""MessageReactions"" (
                    ""Id"" uuid NOT NULL,
                    ""MessageId"" uuid NOT NULL,
                    ""UserId"" uuid NOT NULL,
                    ""Emoji"" character varying(32) NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_MessageReactions"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_MessageReactions_Messages_MessageId"" FOREIGN KEY (""MessageId"")
                        REFERENCES ""Messages"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_MessageReactions_Users_UserId"" FOREIGN KEY (""UserId"")
                        REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MessageReactions_Message_User_Emoji""
                ON ""MessageReactions"" (""MessageId"", ""UserId"", ""Emoji"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MessageReactions_UserId""
                ON ""MessageReactions"" (""UserId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MessageReactions_UserId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MessageReactions_Message_User_Emoji"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""MessageReactions"";");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Messages_ConversationId_IsPinned_CreatedAt"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Messages""
                DROP CONSTRAINT IF EXISTS ""FK_Messages_Users_PinnedByUserId"",
                DROP COLUMN IF EXISTS ""PinnedByUserId"",
                DROP COLUMN IF EXISTS ""PinnedAt"",
                DROP COLUMN IF EXISTS ""IsPinned"";
            ");
        }
    }
}
