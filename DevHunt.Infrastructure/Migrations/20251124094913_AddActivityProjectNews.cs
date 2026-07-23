using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddActivityProjectNews : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activity_Records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    Visibility = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activity_Records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activity_Records_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Activity_Records_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Project_News_Posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    AttachmentsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project_News_Posts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Project_News_Posts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Project_News_Posts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Project_Subscriptions",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project_Subscriptions", x => new { x.ProjectId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Project_Subscriptions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Project_Subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activity_Records_ActorId",
                table: "Activity_Records",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_Project",
                table: "Activity_Records",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_Visibility_Created",
                table: "Activity_Records",
                columns: new[] { "Visibility", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Project_News_Posts_AuthorId",
                table: "Project_News_Posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNews_Project_Visibility_Created",
                table: "Project_News_Posts",
                columns: new[] { "ProjectId", "Visibility", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Project_Subscriptions_UserId",
                table: "Project_Subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubscriptions_CreatedAt",
                table: "Project_Subscriptions",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Project_Subscriptions");

            migrationBuilder.DropTable(
                name: "Project_News_Posts");

            migrationBuilder.DropTable(
                name: "Activity_Records");
        }
    }
