using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

public partial class AddTotpTwoFactorAuth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsTotpEnabled",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "TotpSecret",
            table: "Users",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TotpRecoveryCodes",
            table: "Users",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsTotpEnabled", table: "Users");
        migrationBuilder.DropColumn(name: "TotpSecret", table: "Users");
        migrationBuilder.DropColumn(name: "TotpRecoveryCodes", table: "Users");
    }
}
