using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeAnalysisResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodeAnalysisResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Repository = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Branch = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CommitSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TotalIssues = table.Column<int>(type: "integer", nullable: false),
                    TotalFiles = table.Column<int>(type: "integer", nullable: false),
                    AnalysisTimeMs = table.Column<int>(type: "integer", nullable: false),
                    SeverityCountsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CategoryCountsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IssuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeAnalysisResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeAnalysisResults_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CodeAnalysisResults_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_TargetType_TargetId_Status",
                table: "ModerationReports",
                columns: new[] { "TargetType", "TargetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeAnalysisResults_IntegrationId_Branch",
                table: "CodeAnalysisResults",
                columns: new[] { "IntegrationId", "Branch" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeAnalysisResults_ProjectId_CreatedAt",
                table: "CodeAnalysisResults",
                columns: new[] { "ProjectId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeAnalysisResults");

            migrationBuilder.DropIndex(
                name: "IX_ModerationReports_TargetType_TargetId_Status",
                table: "ModerationReports");
        }
    }
}
