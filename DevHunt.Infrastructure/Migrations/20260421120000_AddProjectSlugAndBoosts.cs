using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <summary>
    /// Adds <c>Slug</c> (URL-safe handle, unique when set) and <c>BoostsCount</c> (denormalized
    /// counter) columns to <c>Projects</c>, plus a new <c>ProjectBoosts</c> join table that
    /// records which users have boosted which projects.
    /// </summary>
    [DbContext(typeof(DevHuntDbContext))]
    [Migration("20260421120000_AddProjectSlugAndBoosts")]
    public partial class AddProjectSlugAndBoosts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Projects""
                ADD COLUMN IF NOT EXISTS ""Slug"" character varying(48);
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Projects""
                ADD COLUMN IF NOT EXISTS ""BoostsCount"" integer NOT NULL DEFAULT 0;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Projects_Slug""
                ON ""Projects"" (""Slug"")
                WHERE ""Slug"" IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProjectBoosts"" (
                    ""ProjectId"" uuid NOT NULL,
                    ""UserId"" uuid NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
                    CONSTRAINT ""PK_ProjectBoosts"" PRIMARY KEY (""ProjectId"", ""UserId""),
                    CONSTRAINT ""FK_ProjectBoosts_Projects_ProjectId"" FOREIGN KEY (""ProjectId"")
                        REFERENCES ""Projects"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_ProjectBoosts_Users_UserId"" FOREIGN KEY (""UserId"")
                        REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProjectBoosts_UserId""
                ON ""ProjectBoosts"" (""UserId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ProjectBoosts"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Projects_Slug"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Projects"" DROP COLUMN IF EXISTS ""BoostsCount"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Projects"" DROP COLUMN IF EXISTS ""Slug"";");
        }
    }
}
