using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgesUniqueIndexesAndModerationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_Achievements_UserId",
                table: "User_Achievements");

            // MIGRATION CONFLICT: The Username column was already added
            // in migration 20260219120000_AddUsernameToUsers
            // migrationBuilder.AddColumn<string>(
            //     name: "Username",
            //     table: "Users",
            //     type: "character varying(50)",
            //     maxLength: 50,
            //     nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessedByUserId",
                table: "ModerationReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_Achievements_UserId_AchievementId_Unique",
                table: "User_Achievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Code_Unique",
                table: "Achievements",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_Achievements_UserId_AchievementId_Unique",
                table: "User_Achievements");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_Code_Unique",
                table: "Achievements");

            // migrationBuilder.DropColumn(
            //     name: "Username",
            //     table: "Users");

            migrationBuilder.DropColumn(
                name: "ProcessedByUserId",
                table: "ModerationReports");

            migrationBuilder.CreateIndex(
                name: "IX_User_Achievements_UserId",
                table: "User_Achievements",
                column: "UserId");
        }
    }
}
