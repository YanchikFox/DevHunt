using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

    /// <inheritdoc />
    public partial class AchievementsSystemImplementation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Achievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IconUrl = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User_Achievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementId = table.Column<Guid>(type: "uuid", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User_Achievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_User_Achievements_Achievements_AchievementId",
                        column: x => x.AchievementId,
                        principalTable: "Achievements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_User_Achievements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 14, 37, 28, 914, DateTimeKind.Utc).AddTicks(1234));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 14, 37, 28, 914, DateTimeKind.Utc).AddTicks(3062));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 14, 37, 28, 912, DateTimeKind.Utc).AddTicks(7470));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 14, 37, 28, 912, DateTimeKind.Utc).AddTicks(7462));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 14, 37, 28, 910, DateTimeKind.Utc).AddTicks(8729));

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Code",
                table: "Achievements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_Achievements_AchievementId",
                table: "User_Achievements",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_User_Achievements_UserId_AchievementId",
                table: "User_Achievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_Achievements_UserId_EarnedAt",
                table: "User_Achievements",
                columns: new[] { "UserId", "EarnedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "User_Achievements");

            migrationBuilder.DropTable(
                name: "Achievements");

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 13, 38, 50, 297, DateTimeKind.Utc).AddTicks(9887));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 13, 38, 50, 298, DateTimeKind.Utc).AddTicks(1878));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 13, 38, 50, 296, DateTimeKind.Utc).AddTicks(4064));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 13, 38, 50, 296, DateTimeKind.Utc).AddTicks(4059));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 13, 38, 50, 296, DateTimeKind.Utc).AddTicks(1098));
        }
    }
