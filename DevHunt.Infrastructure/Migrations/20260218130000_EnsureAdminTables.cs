using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <summary>
    /// Creates missing admin tables (PlatformSettings, FeatureFlags, AdminNotes)
    /// using IF NOT EXISTS — idempotent. Safe to run even if tables already exist.
    ///
    /// Background: The AddAdminExtensions migration was recorded in __EFMigrationsHistory
    /// before the table-creation statements were added to it, so subsequent deploys
    /// skipped the table creation. This migration ensures the tables are present.
    /// </summary>
    [DbContext(typeof(DevHuntDbContext))]
    [Migration("20260218130000_EnsureAdminTables")]
    public partial class EnsureAdminTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""PlatformSettings"" (
                    ""Key"" character varying(128) NOT NULL,
                    ""Value"" character varying(4000) NOT NULL,
                    ""Description"" character varying(500),
                    ""UpdatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedById"" uuid,
                    CONSTRAINT ""PK_PlatformSettings"" PRIMARY KEY (""Key"")
                );
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""FeatureFlags"" (
                    ""Key"" character varying(128) NOT NULL,
                    ""Enabled"" boolean NOT NULL DEFAULT true,
                    ""Description"" character varying(500),
                    ""UpdatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedById"" uuid,
                    CONSTRAINT ""PK_FeatureFlags"" PRIMARY KEY (""Key"")
                );
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""AdminNotes"" (
                    ""Id"" uuid NOT NULL,
                    ""UserId"" uuid NOT NULL,
                    ""AuthorId"" uuid NOT NULL,
                    ""Content"" character varying(2000) NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_AdminNotes"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_AdminNotes_Users_UserId""
                        FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_AdminNotes_Users_AuthorId""
                        FOREIGN KEY (""AuthorId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_AdminNotes_UserId""   ON ""AdminNotes"" (""UserId"");
                CREATE INDEX IF NOT EXISTS ""IX_AdminNotes_AuthorId"" ON ""AdminNotes"" (""AuthorId"");
            ");

            // Seed initial platform settings (skip if already seeded)
            migrationBuilder.Sql(@"
                INSERT INTO ""PlatformSettings"" (""Key"", ""Value"", ""Description"", ""UpdatedAt"")
                VALUES
                    ('maintenance_mode',      'false', 'Enable/disable platform maintenance mode',  NOW()),
                    ('registration_enabled',  'true',  'Allow new user registrations',              NOW()),
                    ('max_projects_per_user', '10',    'Maximum projects a user can own',           NOW())
                ON CONFLICT (""Key"") DO NOTHING;
            ");

            // Seed initial feature flags (skip if already seeded)
            migrationBuilder.Sql(@"
                INSERT INTO ""FeatureFlags"" (""Key"", ""Enabled"", ""Description"", ""UpdatedAt"")
                VALUES
                    ('ai_chat',       true, 'AI chat assistant feature',  NOW()),
                    ('showcase',      true, 'Project showcase feature',   NOW()),
                    ('notifications', true, 'Push notifications feature', NOW())
                ON CONFLICT (""Key"") DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Tables belong to AddAdminExtensions — do not drop here
        }
    }
}
