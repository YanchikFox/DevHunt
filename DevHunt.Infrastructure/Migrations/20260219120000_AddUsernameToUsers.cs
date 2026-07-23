using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    [DbContext(typeof(DevHuntDbContext))]
    [Migration("20260219120000_AddUsernameToUsers")]
    public partial class AddUsernameToUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users""
                ADD COLUMN IF NOT EXISTS ""Username"" character varying(50);
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Username""
                ON ""Users"" (""Username"")
                WHERE ""Username"" IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Users_Username"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""Username"";");
        }
    }
}
