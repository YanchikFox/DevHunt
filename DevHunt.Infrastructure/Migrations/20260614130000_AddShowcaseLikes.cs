using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

/// <summary>
/// Adds the ShowcaseLikes table (DEV-114) so showcase likes are tracked per user. The composite
/// primary key (ShowcaseId, UserId) prevents like-spam and cross-user counter zeroing that the
/// previous bare LikesCount++/-- allowed.
/// </summary>
public partial class AddShowcaseLikes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ShowcaseLikes",
            columns: table => new
            {
                ShowcaseId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ShowcaseLikes", x => new { x.ShowcaseId, x.UserId });
                table.ForeignKey(
                    name: "FK_ShowcaseLikes_Showcase_Projects_ShowcaseId",
                    column: x => x.ShowcaseId,
                    principalTable: "Showcase_Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ShowcaseLikes_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ShowcaseLikes_UserId",
            table: "ShowcaseLikes",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ShowcaseLikes");
    }
}
