using System;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(DevHuntDbContext))]
[Migration("20260605120000_AddCodeAnalysisEmbeddingJobs")]
public partial class AddCodeAnalysisEmbeddingJobs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CodeAnalysisEmbeddingJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AnalysisResultId = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CodeAnalysisEmbeddingJobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_CodeAnalysisEmbeddingJobs_CodeAnalysisResults_AnalysisResultId",
                    column: x => x.AnalysisResultId,
                    principalTable: "CodeAnalysisResults",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CodeAnalysisEmbeddingJobs_AnalysisResultId",
            table: "CodeAnalysisEmbeddingJobs",
            column: "AnalysisResultId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CodeAnalysisEmbeddingJobs_Status_NextAttemptAt",
            table: "CodeAnalysisEmbeddingJobs",
            columns: new[] { "Status", "NextAttemptAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CodeAnalysisEmbeddingJobs");
    }
}
