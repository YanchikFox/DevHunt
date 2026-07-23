using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Idea = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TechStack = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlanVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiPlans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiPlans_Users_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiPlans_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiOperationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiOperationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiOperationLogs_AiPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "AiPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiOperationLogs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiOperationLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("0d5a7ca7-35d8-4cf6-9729-a90f43769f8e"),
                column: "Skills",
                value: new List<string> { ".NET", "PostgreSQL", "RabbitMQ" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5d4d7082-ec8a-47c0-86b7-29c91ba524a1"),
                column: "Skills",
                value: new List<string> { "React Native", "Next.js", "UX Writing" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("6e2f9113-d6ef-4829-8714-0a3dc2a3e2f9"),
                column: "Skills",
                value: new List<string> { "Python", "ML.NET", "Vector Search" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("7fd40108-e6e6-4220-b66b-e5ef1f5c4843"),
                column: "Skills",
                value: new List<string> { "UX Research", "Prototyping", "Storytelling" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a3a95f44-25b8-4a39-8c3e-4d6bb5be6db7"),
                column: "Skills",
                value: new List<string> { "React", "Next.js", "Design Systems" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b9c00db5-4b63-4c2f-a0f5-0d11041a5e58"),
                column: "Skills",
                value: new List<string> { "Product Discovery", "TypeScript", "Analytics" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("bc2dde64-00f2-4d6b-9ca8-9ff4ae3e8d12"),
                column: "Skills",
                value: new List<string> { "Docker", "Kubernetes", "Grafana" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("ee2f8a99-6d7a-47ad-83ac-5d2d0f1b9cd3"),
                column: "Skills",
                value: new List<string> { "Python", "ETL", "dbt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiOperationLogs_Plan",
                table: "AiOperationLogs",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_AiOperationLogs_Project_Created",
                table: "AiOperationLogs",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiOperationLogs_UserId",
                table: "AiOperationLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiPlans_AppliedByUserId",
                table: "AiPlans",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiPlans_CreatedByUserId",
                table: "AiPlans",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiPlans_Project_Created",
                table: "AiPlans",
                columns: new[] { "ProjectId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiOperationLogs");

            migrationBuilder.DropTable(
                name: "AiPlans");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("0d5a7ca7-35d8-4cf6-9729-a90f43769f8e"),
                column: "Skills",
                value: new List<string> { ".NET", "PostgreSQL", "RabbitMQ" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5d4d7082-ec8a-47c0-86b7-29c91ba524a1"),
                column: "Skills",
                value: new List<string> { "React Native", "Next.js", "UX Writing" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("6e2f9113-d6ef-4829-8714-0a3dc2a3e2f9"),
                column: "Skills",
                value: new List<string> { "Python", "ML.NET", "Vector Search" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("7fd40108-e6e6-4220-b66b-e5ef1f5c4843"),
                column: "Skills",
                value: new List<string> { "UX Research", "Prototyping", "Storytelling" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a3a95f44-25b8-4a39-8c3e-4d6bb5be6db7"),
                column: "Skills",
                value: new List<string> { "React", "Next.js", "Design Systems" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b9c00db5-4b63-4c2f-a0f5-0d11041a5e58"),
                column: "Skills",
                value: new List<string> { "Product Discovery", "TypeScript", "Analytics" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("bc2dde64-00f2-4d6b-9ca8-9ff4ae3e8d12"),
                column: "Skills",
                value: new List<string> { "Docker", "Kubernetes", "Grafana" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("ee2f8a99-6d7a-47ad-83ac-5d2d0f1b9cd3"),
                column: "Skills",
                value: new List<string> { "Python", "ETL", "dbt" });
        }
    }
}
