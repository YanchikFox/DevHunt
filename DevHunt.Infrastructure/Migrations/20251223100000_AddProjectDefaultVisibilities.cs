using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevHunt.Infrastructure.Migrations;

[DbContext(typeof(DevHuntDbContext))]
[Migration("20251223100000_AddProjectDefaultVisibilities")]
public partial class AddProjectDefaultVisibilities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // These columns exist in the EF model but were missing from migrations.
        // Use IF NOT EXISTS to be safe across environments.
        migrationBuilder.Sql(
            "ALTER TABLE \"Projects\" ADD COLUMN IF NOT EXISTS \"DefaultNewsVisibility\" character varying(50) NOT NULL DEFAULT 'public';");
        migrationBuilder.Sql(
            "ALTER TABLE \"Projects\" ADD COLUMN IF NOT EXISTS \"DefaultFilesVisibility\" character varying(50) NOT NULL DEFAULT 'public';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE \"Projects\" DROP COLUMN IF EXISTS \"DefaultNewsVisibility\";");
        migrationBuilder.Sql(
            "ALTER TABLE \"Projects\" DROP COLUMN IF EXISTS \"DefaultFilesVisibility\";");
    }
}
