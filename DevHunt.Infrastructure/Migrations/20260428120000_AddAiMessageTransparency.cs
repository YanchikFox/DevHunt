using System;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DevHuntDbContext))]
    [Migration("20260428120000_AddAiMessageTransparency")]
    public partial class AddAiMessageTransparency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiMetadataJson",
                table: "Messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiMessageDetails",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMessageDetails", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_AiMessageDetails_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiMessageDetails_CreatedAt",
                table: "AiMessageDetails",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AiMessageDetails");

            migrationBuilder.DropColumn(
                name: "AiMetadataJson",
                table: "Messages");
        }
    }
}
