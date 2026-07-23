using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

    /// <inheritdoc />
    public partial class AddSupportCommunityIssuesFilesPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    RelatedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackItems_Projects_RelatedProjectId",
                        column: x => x.RelatedProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FeedbackItems_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FeedbackItems_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectFiles_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedToAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminResolution = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    ResolvedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EscalatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectIssues_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectIssues_Users_AssignedToAdminId",
                        column: x => x.AssignedToAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectIssues_Users_RelatedUserId",
                        column: x => x.RelatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectIssues_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectIssues_Users_ResolvedByAdminId",
                        column: x => x.ResolvedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Projects_RelatedProjectId",
                        column: x => x.RelatedProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserPrivacySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileVisibility = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ShowEmail = table.Column<bool>(type: "boolean", nullable: false),
                    ShowSkills = table.Column<bool>(type: "boolean", nullable: false),
                    ShowExperience = table.Column<bool>(type: "boolean", nullable: false),
                    ShowRating = table.Column<bool>(type: "boolean", nullable: false),
                    ShowProjects = table.Column<bool>(type: "boolean", nullable: false),
                    ShowSocialLinks = table.Column<bool>(type: "boolean", nullable: false),
                    ShowAchievements = table.Column<bool>(type: "boolean", nullable: false),
                    AllowEmailSearch = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnMessages = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnInvitations = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPrivacySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPrivacySettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackComments_FeedbackItems_FeedbackId",
                        column: x => x.FeedbackId,
                        principalTable: "FeedbackItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedbackComments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsUpvote = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackVotes_FeedbackItems_FeedbackId",
                        column: x => x.FeedbackId,
                        principalTable: "FeedbackItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedbackVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketMessages_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketMessages_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 19, 27, 35, 948, DateTimeKind.Utc).AddTicks(6252));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 19, 27, 35, 949, DateTimeKind.Utc).AddTicks(297));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 19, 27, 35, 943, DateTimeKind.Utc).AddTicks(9526));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 19, 27, 35, 943, DateTimeKind.Utc).AddTicks(9512));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 19, 27, 35, 943, DateTimeKind.Utc).AddTicks(3188));

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackComments_AuthorId",
                table: "FeedbackComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackComments_FeedbackId_CreatedAt",
                table: "FeedbackComments",
                columns: new[] { "FeedbackId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackItems_AssignedToUserId",
                table: "FeedbackItems",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackItems_AuthorId",
                table: "FeedbackItems",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackItems_RelatedProjectId",
                table: "FeedbackItems",
                column: "RelatedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackItems_Type_Status_CreatedAt",
                table: "FeedbackItems",
                columns: new[] { "Type", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackItems_VoteCount_CreatedAt",
                table: "FeedbackItems",
                columns: new[] { "VoteCount", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackVotes_FeedbackId_UserId",
                table: "FeedbackVotes",
                columns: new[] { "FeedbackId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackVotes_UserId",
                table: "FeedbackVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_ProjectId_DeletedAt_CreatedAt",
                table: "ProjectFiles",
                columns: new[] { "ProjectId", "DeletedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_UploadedByUserId",
                table: "ProjectFiles",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIssues_AssignedToAdminId",
                table: "ProjectIssues",
                column: "AssignedToAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIssues_ProjectId_Status_CreatedAt",
                table: "ProjectIssues",
                columns: new[] { "ProjectId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIssues_RelatedUserId",
                table: "ProjectIssues",
                column: "RelatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIssues_ReporterId",
                table: "ProjectIssues",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIssues_ResolvedByAdminId",
                table: "ProjectIssues",
                column: "ResolvedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIssues_Status_Priority_CreatedAt",
                table: "ProjectIssues",
                columns: new[] { "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_AssignedToUserId",
                table: "SupportTickets",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_RelatedProjectId",
                table: "SupportTickets",
                column: "RelatedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId_Status_CreatedAt",
                table: "SupportTickets",
                columns: new[] { "UserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_AuthorId",
                table: "TicketMessages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_TicketId_CreatedAt",
                table: "TicketMessages",
                columns: new[] { "TicketId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPrivacySettings_UserId",
                table: "UserPrivacySettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackComments");

            migrationBuilder.DropTable(
                name: "FeedbackVotes");

            migrationBuilder.DropTable(
                name: "ProjectFiles");

            migrationBuilder.DropTable(
                name: "ProjectIssues");

            migrationBuilder.DropTable(
                name: "TicketMessages");

            migrationBuilder.DropTable(
                name: "UserPrivacySettings");

            migrationBuilder.DropTable(
                name: "FeedbackItems");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 13, 0, 49, 266, DateTimeKind.Utc).AddTicks(8515));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 13, 0, 49, 267, DateTimeKind.Utc).AddTicks(202));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 13, 0, 49, 265, DateTimeKind.Utc).AddTicks(3343));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 13, 0, 49, 265, DateTimeKind.Utc).AddTicks(3338));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 13, 0, 49, 265, DateTimeKind.Utc).AddTicks(1552));
        }
    }
