using System;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevHunt.Infrastructure.Migrations;

[DbContext(typeof(DevHuntDbContext))]
[Migration("20251125000000_AddPrivacyAndPermissions")]
public partial class AddPrivacyAndPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ActivityVisibility",
            table: "UserPrivacySettings",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "public");

        migrationBuilder.AddColumn<bool>(
            name: "CanManageFiles",
            table: "TeamMembers",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "CanManageGallery",
            table: "TeamMembers",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "CanManageTasks",
            table: "TeamMembers",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "CanPublishNews",
            table: "TeamMembers",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Visibility",
            table: "Project_Files",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "public");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ActivityVisibility",
            table: "UserPrivacySettings");

        migrationBuilder.DropColumn(
            name: "CanManageFiles",
            table: "TeamMembers");

        migrationBuilder.DropColumn(
            name: "CanManageGallery",
            table: "TeamMembers");

        migrationBuilder.DropColumn(
            name: "CanManageTasks",
            table: "TeamMembers");

        migrationBuilder.DropColumn(
            name: "CanPublishNews",
            table: "TeamMembers");

        migrationBuilder.DropColumn(
            name: "Visibility",
            table: "Project_Files");
    }
}
