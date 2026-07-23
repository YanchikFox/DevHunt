using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DevHunt.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialMigration : Migration
{
    /// <inheritdoc />
    /// <remarks>
    /// NOTE: This migration contains seed data with bcrypt-hashed passwords.
    /// The password hashes are SAFE to store in version control as they are:
    /// 1. Cryptographically hashed using bcrypt (not reversible)
    /// 2. Used only for development/testing environments
    /// 3. Should be changed in production via proper user registration
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    TechStack = table.Column<string[]>(type: "text[]", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DifficultyLevel = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ExpectedDurationDays = table.Column<int>(type: "integer", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShowcasePublished = table.Column<bool>(type: "boolean", nullable: false),
                    Featured = table.Column<bool>(type: "boolean", nullable: false),
                    Rating = table.Column<float>(type: "real", nullable: true),
                    MaxTeamSize = table.Column<int>(type: "integer", nullable: true),
                    RequiredRoles = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: true),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    Timezone = table.Column<string>(type: "text", nullable: true),
                    Skills = table.Column<string[]>(type: "text[]", nullable: false),
                    Experience = table.Column<int>(type: "integer", nullable: true),
                    Rating = table.Column<float>(type: "real", nullable: true),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    Github = table.Column<string>(type: "text", nullable: true),
                    Linkedin = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_InviteeId",
                        column: x => x.InviteeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_InviterId",
                        column: x => x.InviterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModerationReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ActionTaken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationReports_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModerationReports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    ReviewText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_ReviewedUserId",
                        column: x => x.ReviewedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedHours = table.Column<float>(type: "real", nullable: true),
                    ActualHours = table.Column<float>(type: "real", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tasks_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Contribution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    IsLeader = table.Column<bool>(type: "boolean", nullable: false),
                    ContributionScore = table.Column<int>(type: "integer", nullable: true),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "CreatedAt", "Description", "DifficultyLevel", "EndDate", "ExpectedDurationDays", "Featured", "MaxTeamSize", "OwnerId", "Rating", "RequiredRoles", "ShortDescription", "ShowcasePublished", "StartDate", "Status", "TechStack", "Title", "UpdatedAt", "Visibility" },
                values: new object[,]
                {
                    { new Guid("7f01de92-32e5-459d-bd48-3c968ccb67f1"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Platform that helps hackathon organizers manage teams and tasks in real time.", null, null, null, false, null, new Guid("00000000-0000-0000-0000-000000000000"), null, new string[0], null, false, null, "active", new[] { "ASP.NET Core", "React", "PostgreSQL" }, "Hackathon Companion", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "public" },
                    { new Guid("d7d17e84-7a92-4c2f-bc67-bc0612548723"), new DateTime(2024, 12, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Showcase service for AI projects with ratings, feedback, and GitHub integration.", null, null, null, false, null, new Guid("00000000-0000-0000-0000-000000000000"), null, new string[0], null, false, null, "planning", new[] { "Next.js", "Node.js", "Redis" }, "AI Showcase Hub", new DateTime(2024, 12, 30, 0, 0, 0, 0, DateTimeKind.Utc), "public" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarUrl", "Bio", "CreatedAt", "Email", "Experience", "FullName", "Github", "IsActive", "IsVerified", "Language", "LastLogin", "Linkedin", "PasswordHash", "Rating", "Role", "Skills", "Timezone", "UpdatedAt", "Website" },
                values: new object[,]
                {
                    { new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"), null, "Product manager with 6 years of experience.", new DateTime(2025, 10, 30, 23, 55, 23, 559, DateTimeKind.Utc).AddTicks(4694), "product@devhunt.io", null, "Daria Nikolaeva", null, true, false, null, null, null, string.Empty, null, "pm", new string[0], "UTC+1", null, null },
                    { new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"), null, "Full-stack developer working with React and .NET.", new DateTime(2025, 10, 30, 23, 55, 23, 559, DateTimeKind.Utc).AddTicks(4689), "builder@devhunt.io", null, "Ivan Goncharov", null, true, false, null, null, null, string.Empty, null, "participant", new string[0], "UTC+4", null, null },
                    { new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"), null, "Helps teams with solution architecture and code reviews.", new DateTime(2025, 10, 30, 23, 55, 23, 559, DateTimeKind.Utc).AddTicks(3021), "mentor@devhunt.io", null, "Maria Kotova", null, true, false, null, null, null, string.Empty, null, "mentor", new string[0], "UTC+3", null, null }
                });

            migrationBuilder.InsertData(
                table: "Invitations",
                columns: new[] { "Id", "CreatedAt", "InviteeId", "InviterId", "Message", "ProjectId", "RespondedAt", "Role", "Status" },
                values: new object[] { new Guid("6f1645aa-0ad9-4d51-919e-8b332afbbb5f"), new DateTime(2024, 12, 31, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"), new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"), "Need help with architecture choices and feature vetting.", new Guid("d7d17e84-7a92-4c2f-bc67-bc0612548723"), null, "mentor", "pending" });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "Id", "ActualHours", "AssignedToUserId", "CompletedAt", "CreatedAt", "CreatedByUserId", "Deadline", "Description", "EstimatedHours", "IsDeleted", "Priority", "ProjectId", "Status", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"), null, new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"), null, new DateTime(2025, 10, 30, 23, 55, 23, 560, DateTimeKind.Utc).AddTicks(4453), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2025, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, null, false, "medium", new Guid("7f01de92-32e5-459d-bd48-3c968ccb67f1"), "in_progress", "Set up CI/CD", null },
                    { new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"), null, new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"), null, new DateTime(2025, 10, 30, 23, 55, 23, 560, DateTimeKind.Utc).AddTicks(5793), new Guid("00000000-0000-0000-0000-000000000000"), null, null, null, false, "medium", new Guid("d7d17e84-7a92-4c2f-bc67-bc0612548723"), "todo", "Collect early adopter feedback", null }
                });

            migrationBuilder.InsertData(
                table: "TeamMembers",
                columns: new[] { "Id", "Contribution", "ContributionScore", "IsLeader", "JoinedAt", "LeftAt", "ProjectId", "Role", "Status", "UserId" },
                values: new object[,]
                {
                    { new Guid("02efec63-0182-4a60-9088-f13faf9d3651"), "Built the backend and frontend MVP in 72 hours.", null, false, new DateTime(2024, 12, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("7f01de92-32e5-459d-bd48-3c968ccb67f1"), "fullstack", "active", new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2") },
                    { new Guid("29d08d5d-f8c3-4df8-8638-524c2f46d9a6"), "Mentored the team and reviewed architecture decisions.", null, false, new DateTime(2024, 12, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("7f01de92-32e5-459d-bd48-3c968ccb67f1"), "mentor", "active", new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitation_Project_Invitee",
                table: "Invitations",
                columns: new[] { "ProjectId", "InviteeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InviteeId",
                table: "Invitations",
                column: "InviteeId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InviterId",
                table: "Invitations",
                column: "InviterId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_ReporterId",
                table: "ModerationReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_TargetType_TargetId",
                table: "ModerationReports",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_UserId",
                table: "ModerationReports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProjectId_ReviewerId_ReviewedUserId",
                table: "Reviews",
                columns: new[] { "ProjectId", "ReviewerId", "ReviewedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewedUserId",
                table: "Reviews",
                column: "ReviewedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerId",
                table: "Reviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedToUserId",
                table: "Tasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Status",
                table: "Tasks",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_ProjectId_UserId",
                table: "TeamMembers",
                columns: new[] { "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_UserId",
                table: "TeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "ModerationReports");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }


