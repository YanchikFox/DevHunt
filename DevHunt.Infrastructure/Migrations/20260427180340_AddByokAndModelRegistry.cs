using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddByokAndModelRegistry : Migration
    {
        /// <summary>
        /// Adds BYOK (Bring Your Own Key) storage and the LLM model registry.
        ///
        /// Note: the auto-scaffolded version of this migration also tried to
        /// recreate <c>MessageReactions</c>, <c>Channel_Role_Definitions</c>,
        /// <c>IsPinned</c>/<c>PinnedAt</c>/<c>PinnedByUserId</c> columns and a
        /// few related indexes / FKs. Those changes already exist in the
        /// production database (added by hand-written migrations + manual
        /// SQL, with FK names that don't match EF's snapshot) — re-applying
        /// would crash with "already exists". They have been removed from
        /// <see cref="Up"/> and <see cref="Down"/> here. The accompanying
        /// <c>DevHuntDbContextModelSnapshot</c> still records those entities
        /// so future migrations don't keep trying to add them.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EncryptedKey = table.Column<string>(type: "text", nullable: false),
                    KeyHint = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserApiKeys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmModels_Enabled_Provider_Sort",
                table: "LlmModels",
                columns: new[] { "IsEnabled", "Provider", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmModels_Provider_ModelId",
                table: "LlmModels",
                columns: new[] { "Provider", "ModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserApiKeys_UserId_Provider",
                table: "UserApiKeys",
                columns: new[] { "UserId", "Provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LlmModels");
            migrationBuilder.DropTable(name: "UserApiKeys");
        }
    }
}
