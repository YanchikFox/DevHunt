using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLlmModels : Migration
    {
        /// <summary>
        /// Creates the <c>UserLlmModels</c> table that stores per-user model catalog entries
        /// discovered via BYOK sync, decoupling individual model discovery from the shared platform catalog.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserLlmModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Tier = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContextWindow = table.Column<int>(type: "integer", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: true),
                    SupportsTools = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsVision = table.Column<bool>(type: "boolean", nullable: false),
                    InputPricePer1M = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    OutputPricePer1M = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLlmModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLlmModels_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLlmModels_UserId",
                table: "UserLlmModels",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLlmModels_UserId_Provider_ModelId",
                table: "UserLlmModels",
                columns: new[] { "UserId", "Provider", "ModelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserLlmModels");
        }
    }
}
