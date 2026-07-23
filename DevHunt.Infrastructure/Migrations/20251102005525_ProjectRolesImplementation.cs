using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ProjectRolesImplementation : Migration
{
    /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Project_Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false),
                    FilledCount = table.Column<int>(type: "integer", nullable: false),
                    RequiredSkillsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Project_Roles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 55, 24, 118, DateTimeKind.Utc).AddTicks(5788));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 55, 24, 118, DateTimeKind.Utc).AddTicks(7201));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 55, 24, 117, DateTimeKind.Utc).AddTicks(4435));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 55, 24, 117, DateTimeKind.Utc).AddTicks(4430));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 55, 24, 117, DateTimeKind.Utc).AddTicks(2612));

            migrationBuilder.CreateIndex(
                name: "IX_Project_Roles_ProjectId_RoleName",
                table: "Project_Roles",
                columns: new[] { "ProjectId", "RoleName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Project_Roles");

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 45, 36, 642, DateTimeKind.Utc).AddTicks(222));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 45, 36, 642, DateTimeKind.Utc).AddTicks(1905));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 45, 36, 640, DateTimeKind.Utc).AddTicks(4390));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 45, 36, 640, DateTimeKind.Utc).AddTicks(4383));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 45, 36, 640, DateTimeKind.Utc).AddTicks(2253));
        }
    }

