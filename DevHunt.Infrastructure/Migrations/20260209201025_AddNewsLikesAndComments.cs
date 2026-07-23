using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DevHunt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsLikesAndComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Records_Projects_ProjectId",
                table: "Activity_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Records_Users_ActorId",
                table: "Activity_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Records_Users_TargetUserId",
                table: "Activity_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Users_InviteeId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Conversations_ConversationId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Projects_ProjectId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_SenderId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_ModerationReports_Users_ReporterId",
                table: "ModerationReports");

            migrationBuilder.DropForeignKey(
                name: "FK_ModerationReports_Users_UserId",
                table: "ModerationReports");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_Documents_Users_AuthorId",
                table: "Project_Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_Files_Users_UploadedById",
                table: "Project_Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_News_Posts_Users_AuthorId",
                table: "Project_News_Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_Tech_Stack_Skills_SkillId",
                table: "Project_Tech_Stack");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Projects_ProjectId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Users_ReviewedUserId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Users_ReviewerId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Showcase_Comments_Showcase_Comments_ParentCommentId",
                table: "Showcase_Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Showcase_Comments_Users_AuthorId",
                table: "Showcase_Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskBoardSettings_TaskColumns_DefaultColumnId",
                table: "TaskBoardSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskLinks_Tasks_TargetTaskId",
                table: "TaskLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_TaskColumns_ColumnId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Users_AssignedToUserId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Achievements_Achievements_AchievementId",
                table: "User_Achievements");

            migrationBuilder.DropForeignKey(
                name: "FK_User_SkillEntries_Skills_SkillId",
                table: "User_SkillEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Skills_Skills_SkillId",
                table: "User_Skills");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_GithubId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_User_Skills_UserId_SkillId",
                table: "User_Skills");

            migrationBuilder.DropIndex(
                name: "IX_UserSkillEntries_User_RawNormalized",
                table: "User_SkillEntries");

            migrationBuilder.DropIndex(
                name: "IX_UserFollows_CreatedAt",
                table: "User_Follows");

            migrationBuilder.DropIndex(
                name: "IX_UserFollows_Follower",
                table: "User_Follows");

            migrationBuilder.DropIndex(
                name: "IX_User_Achievements_UserId_AchievementId",
                table: "User_Achievements");

            migrationBuilder.DropIndex(
                name: "IX_User_Achievements_UserId_EarnedAt",
                table: "User_Achievements");

            migrationBuilder.DropIndex(
                name: "IX_TeamMembers_ProjectId_UserId",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_Column_Position",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_Status",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_TaskLinks_Source",
                table: "TaskLinks");

            migrationBuilder.DropIndex(
                name: "IX_TaskColumns_Project_Position",
                table: "TaskColumns");

            migrationBuilder.DropIndex(
                name: "IX_TaskBoardSettings_Project",
                table: "TaskBoardSettings");

            migrationBuilder.DropIndex(
                name: "IX_Skills_Name",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Skill_Aliases_AliasNormalized",
                table: "Skill_Aliases");

            migrationBuilder.DropIndex(
                name: "IX_Showcase_Projects_Featured_PublishedAt",
                table: "Showcase_Projects");

            migrationBuilder.DropIndex(
                name: "IX_Showcase_Projects_ProjectId",
                table: "Showcase_Projects");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ProjectId_ReviewerId_ReviewedUserId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId_IsRevoked",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Recommendations_MatchScore_GeneratedAt",
                table: "Recommendations");

            migrationBuilder.DropIndex(
                name: "IX_Recommendations_UserId_GeneratedAt",
                table: "Recommendations");

            migrationBuilder.DropIndex(
                name: "IX_Recommendations_UserId_ProjectId",
                table: "Recommendations");

            migrationBuilder.DropIndex(
                name: "IX_Projects_DifficultyLevel",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Listing",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Status_Created",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Visibility_Created",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Project_Tech_Stack_ProjectId_SkillId",
                table: "Project_Tech_Stack");

            migrationBuilder.DropIndex(
                name: "IX_ProjectSubscriptions_CreatedAt",
                table: "Project_Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Project_Roles_ProjectId_RoleName",
                table: "Project_Roles");

            migrationBuilder.DropIndex(
                name: "IX_ProjectNews_Project_Visibility_Created",
                table: "Project_News_Posts");

            migrationBuilder.DropIndex(
                name: "IX_Project_Files_ProjectId_DeletedAt",
                table: "Project_Files");

            migrationBuilder.DropIndex(
                name: "IX_Project_Documents_ProjectId_Path",
                table: "Project_Documents");

            migrationBuilder.DropIndex(
                name: "IX_OutboxEvents_CreatedAt",
                table: "OutboxEvents");

            migrationBuilder.DropIndex(
                name: "IX_OutboxEvents_Status_CreatedAt",
                table: "OutboxEvents");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_ModerationReports_TargetType_TargetId",
                table: "ModerationReports");

            migrationBuilder.DropIndex(
                name: "IX_ModerationReports_UserId",
                table: "ModerationReports");

            migrationBuilder.DropIndex(
                name: "IX_Messages_Conversation_CreatedAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_Project_CreatedAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Invitation_Project_Invitee",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_InviteeId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Integrations_IsActive_ServiceType",
                table: "Integrations");

            migrationBuilder.DropIndex(
                name: "IX_Integrations_ProjectId_ServiceType",
                table: "Integrations");

            migrationBuilder.DropIndex(
                name: "IX_Conversation_Participants_ConversationId_UserId",
                table: "Conversation_Participants");

            migrationBuilder.DropIndex(
                name: "IX_ActivityRecords_Actor_Created",
                table: "Activity_Records");

            migrationBuilder.DropIndex(
                name: "IX_ActivityRecords_EventType",
                table: "Activity_Records");

            migrationBuilder.DropIndex(
                name: "IX_ActivityRecords_TargetUser_Created",
                table: "Activity_Records");

            migrationBuilder.DropIndex(
                name: "IX_ActivityRecords_Visibility_Created",
                table: "Activity_Records");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_Code",
                table: "Achievements");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("0d5a7ca7-35d8-4cf6-9729-a90f43769f8e"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5d4d7082-ec8a-47c0-86b7-29c91ba524a1"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("6e2f9113-d6ef-4829-8714-0a3dc2a3e2f9"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("7fd40108-e6e6-4220-b66b-e5ef1f5c4843"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a3a95f44-25b8-4a39-8c3e-4d6bb5be6db7"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b9c00db5-4b63-4c2f-a0f5-0d11041a5e58"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("bc2dde64-00f2-4d6b-9ca8-9ff4ae3e8d12"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("ee2f8a99-6d7a-47ad-83ac-5d2d0f1b9cd3"));

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ModerationReports");

            migrationBuilder.RenameIndex(
                name: "IX_UserSkillEntries_SkillId",
                table: "User_SkillEntries",
                newName: "IX_User_SkillEntries_SkillId");

            migrationBuilder.RenameIndex(
                name: "IX_UserFollows_Followed",
                table: "User_Follows",
                newName: "IX_User_Follows_FollowedId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskLinks_Unique",
                table: "TaskLinks",
                newName: "IX_TaskLinks_SourceTaskId_TargetTaskId_LinkType");

            migrationBuilder.RenameIndex(
                name: "IX_TaskLinks_Target",
                table: "TaskLinks",
                newName: "IX_TaskLinks_TargetTaskId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskColumns_Project",
                table: "TaskColumns",
                newName: "IX_TaskColumns_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskAttachments_Task",
                table: "TaskAttachments",
                newName: "IX_TaskAttachments_TaskId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskAttachments_ProjectFile",
                table: "TaskAttachments",
                newName: "IX_TaskAttachments_ProjectFileId");

            // IX_ProjectSubscriptions_UserId → IX_Project_Subscriptions_UserId
            // Skipped: target index already exists from migration 20251124094913_AddActivityProjectNews

            migrationBuilder.RenameIndex(
                name: "IX_ActivityRecords_Project",
                table: "Activity_Records",
                newName: "IX_Activity_Records_ProjectId");

            migrationBuilder.AlterColumn<string>(
                name: "AttachmentsJson",
                table: "Project_News_Posts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommentsCount",
                table: "Project_News_Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LikesCount",
                table: "Project_News_Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // IsAiGenerated column already exists on Messages (added outside migrations)
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Messages' AND column_name = 'IsAiGenerated'
                    ) THEN
                        ALTER TABLE "Messages" ADD COLUMN "IsAiGenerated" boolean NOT NULL DEFAULT false;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Invitations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "invite");

            migrationBuilder.AlterColumn<string>(
                name: "EventGroup",
                table: "Activity_Records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "project");

            // AuditLogs table already exists (created outside migrations)
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AuditLogs" (
                    "Id" uuid NOT NULL,
                    "UserId" uuid,
                    "UserRole" character varying(50),
                    "Action" character varying(100) NOT NULL,
                    "EntityType" character varying(50) NOT NULL,
                    "EntityId" uuid,
                    "Details" text,
                    "IpAddress" character varying(45),
                    "Severity" character varying(20) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.CreateTable(
                name: "News_Post_Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NewsPostId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News_Post_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_News_Post_Comments_Project_News_Posts_NewsPostId",
                        column: x => x.NewsPostId,
                        principalTable: "Project_News_Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_News_Post_Comments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "News_Post_Likes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NewsPostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News_Post_Likes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_News_Post_Likes_Project_News_Posts_NewsPostId",
                        column: x => x.NewsPostId,
                        principalTable: "Project_News_Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_News_Post_Likes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_User_Skills_UserId",
                table: "User_Skills",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_User_SkillEntries_UserId",
                table: "User_SkillEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_User_Achievements_UserId",
                table: "User_Achievements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_ProjectId",
                table: "TeamMembers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ColumnId",
                table: "Tasks",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskBoardSettings_ProjectId",
                table: "TaskBoardSettings",
                column: "ProjectId");

            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_Showcase_Projects_ProjectId" ON "Showcase_Projects" ("ProjectId");""");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProjectId",
                table: "Reviews",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_UserId",
                table: "Recommendations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Tech_Stack_ProjectId",
                table: "Project_Tech_Stack",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Roles_ProjectId",
                table: "Project_Roles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_News_Posts_ProjectId",
                table: "Project_News_Posts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ProjectId",
                table: "Messages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InviteeId_Status",
                table: "Invitations",
                columns: new[] { "InviteeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_ProjectId_InviteeId_Status",
                table: "Invitations",
                columns: new[] { "ProjectId", "InviteeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_ProjectId",
                table: "Integrations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversation_Participants_ConversationId",
                table: "Conversation_Participants",
                column: "ConversationId");

            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_Activity_Records_ActorId" ON "Activity_Records" ("ActorId");""");

            migrationBuilder.CreateIndex(
                name: "IX_Activity_Records_TargetUserId",
                table: "Activity_Records",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_News_Post_Comments_AuthorId",
                table: "News_Post_Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_News_Post_Comments_NewsPostId_CreatedAt",
                table: "News_Post_Comments",
                columns: new[] { "NewsPostId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_News_Post_Likes_NewsPostId_UserId",
                table: "News_Post_Likes",
                columns: new[] { "NewsPostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_News_Post_Likes_UserId",
                table: "News_Post_Likes",
                column: "UserId");

            // All 7 FKs below already exist in the DB — make idempotent
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Activity_Records_Projects_ProjectId') THEN
                        ALTER TABLE "Activity_Records" ADD CONSTRAINT "FK_Activity_Records_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects"("Id");
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Activity_Records_Users_ActorId') THEN
                        ALTER TABLE "Activity_Records" ADD CONSTRAINT "FK_Activity_Records_Users_ActorId" FOREIGN KEY ("ActorId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Activity_Records_Users_TargetUserId') THEN
                        ALTER TABLE "Activity_Records" ADD CONSTRAINT "FK_Activity_Records_Users_TargetUserId" FOREIGN KEY ("TargetUserId") REFERENCES "Users"("Id");
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Invitations_Users_InviteeId') THEN
                        ALTER TABLE "Invitations" ADD CONSTRAINT "FK_Invitations_Users_InviteeId" FOREIGN KEY ("InviteeId") REFERENCES "Users"("Id") ON DELETE RESTRICT;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Conversations_ConversationId') THEN
                        ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Conversations_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "Conversations"("Id");
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Messages_ReplyToId') THEN
                        ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Messages_ReplyToId" FOREIGN KEY ("ReplyToId") REFERENCES "Messages"("Id");
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Projects_ProjectId') THEN
                        ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects"("Id");
                    END IF;
                END $$;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_SenderId",
                table: "Messages",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationReports_Users_ReporterId",
                table: "ModerationReports",
                column: "ReporterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Documents_Users_AuthorId",
                table: "Project_Documents",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Files_Users_UploadedById",
                table: "Project_Files",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_News_Posts_Users_AuthorId",
                table: "Project_News_Posts",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Tech_Stack_Skills_SkillId",
                table: "Project_Tech_Stack",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Projects_ProjectId",
                table: "Recommendations",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_ReviewedUserId",
                table: "Reviews",
                column: "ReviewedUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_ReviewerId",
                table: "Reviews",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Showcase_Comments_Showcase_Comments_ParentCommentId",
                table: "Showcase_Comments",
                column: "ParentCommentId",
                principalTable: "Showcase_Comments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Showcase_Comments_Users_AuthorId",
                table: "Showcase_Comments",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskBoardSettings_TaskColumns_DefaultColumnId",
                table: "TaskBoardSettings",
                column: "DefaultColumnId",
                principalTable: "TaskColumns",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskLinks_Tasks_TargetTaskId",
                table: "TaskLinks",
                column: "TargetTaskId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_TaskColumns_ColumnId",
                table: "Tasks",
                column: "ColumnId",
                principalTable: "TaskColumns",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Users_AssignedToUserId",
                table: "Tasks",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_User_Achievements_Achievements_AchievementId",
                table: "User_Achievements",
                column: "AchievementId",
                principalTable: "Achievements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_SkillEntries_Skills_SkillId",
                table: "User_SkillEntries",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_User_Skills_Skills_SkillId",
                table: "User_Skills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Records_Projects_ProjectId",
                table: "Activity_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Records_Users_ActorId",
                table: "Activity_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Records_Users_TargetUserId",
                table: "Activity_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Users_InviteeId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Conversations_ConversationId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Projects_ProjectId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_SenderId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_ModerationReports_Users_ReporterId",
                table: "ModerationReports");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_Documents_Users_AuthorId",
                table: "Project_Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_Files_Users_UploadedById",
                table: "Project_Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_News_Posts_Users_AuthorId",
                table: "Project_News_Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_Tech_Stack_Skills_SkillId",
                table: "Project_Tech_Stack");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Projects_ProjectId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Users_ReviewedUserId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Users_ReviewerId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Showcase_Comments_Showcase_Comments_ParentCommentId",
                table: "Showcase_Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Showcase_Comments_Users_AuthorId",
                table: "Showcase_Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskBoardSettings_TaskColumns_DefaultColumnId",
                table: "TaskBoardSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskLinks_Tasks_TargetTaskId",
                table: "TaskLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_TaskColumns_ColumnId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Users_AssignedToUserId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Achievements_Achievements_AchievementId",
                table: "User_Achievements");

            migrationBuilder.DropForeignKey(
                name: "FK_User_SkillEntries_Skills_SkillId",
                table: "User_SkillEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Skills_Skills_SkillId",
                table: "User_Skills");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "News_Post_Comments");

            migrationBuilder.DropTable(
                name: "News_Post_Likes");

            migrationBuilder.DropIndex(
                name: "IX_User_Skills_UserId",
                table: "User_Skills");

            migrationBuilder.DropIndex(
                name: "IX_User_SkillEntries_UserId",
                table: "User_SkillEntries");

            migrationBuilder.DropIndex(
                name: "IX_User_Achievements_UserId",
                table: "User_Achievements");

            migrationBuilder.DropIndex(
                name: "IX_TeamMembers_ProjectId",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ColumnId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_TaskBoardSettings_ProjectId",
                table: "TaskBoardSettings");

            migrationBuilder.DropIndex(
                name: "IX_Showcase_Projects_ProjectId",
                table: "Showcase_Projects");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ProjectId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Recommendations_UserId",
                table: "Recommendations");

            migrationBuilder.DropIndex(
                name: "IX_Project_Tech_Stack_ProjectId",
                table: "Project_Tech_Stack");

            migrationBuilder.DropIndex(
                name: "IX_Project_Roles_ProjectId",
                table: "Project_Roles");

            migrationBuilder.DropIndex(
                name: "IX_Project_News_Posts_ProjectId",
                table: "Project_News_Posts");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ProjectId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_InviteeId_Status",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_ProjectId_InviteeId_Status",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Integrations_ProjectId",
                table: "Integrations");

            migrationBuilder.DropIndex(
                name: "IX_Conversation_Participants_ConversationId",
                table: "Conversation_Participants");

            migrationBuilder.DropIndex(
                name: "IX_Activity_Records_ActorId",
                table: "Activity_Records");

            migrationBuilder.DropIndex(
                name: "IX_Activity_Records_TargetUserId",
                table: "Activity_Records");

            migrationBuilder.DropColumn(
                name: "CommentsCount",
                table: "Project_News_Posts");

            migrationBuilder.DropColumn(
                name: "LikesCount",
                table: "Project_News_Posts");

            migrationBuilder.DropColumn(
                name: "IsAiGenerated",
                table: "Messages");

            migrationBuilder.RenameIndex(
                name: "IX_User_SkillEntries_SkillId",
                table: "User_SkillEntries",
                newName: "IX_UserSkillEntries_SkillId");

            migrationBuilder.RenameIndex(
                name: "IX_User_Follows_FollowedId",
                table: "User_Follows",
                newName: "IX_UserFollows_Followed");

            migrationBuilder.RenameIndex(
                name: "IX_TaskLinks_TargetTaskId",
                table: "TaskLinks",
                newName: "IX_TaskLinks_Target");

            migrationBuilder.RenameIndex(
                name: "IX_TaskLinks_SourceTaskId_TargetTaskId_LinkType",
                table: "TaskLinks",
                newName: "IX_TaskLinks_Unique");

            migrationBuilder.RenameIndex(
                name: "IX_TaskColumns_ProjectId",
                table: "TaskColumns",
                newName: "IX_TaskColumns_Project");

            migrationBuilder.RenameIndex(
                name: "IX_TaskAttachments_TaskId",
                table: "TaskAttachments",
                newName: "IX_TaskAttachments_Task");

            migrationBuilder.RenameIndex(
                name: "IX_TaskAttachments_ProjectFileId",
                table: "TaskAttachments",
                newName: "IX_TaskAttachments_ProjectFile");

            // IX_Project_Subscriptions_UserId → IX_ProjectSubscriptions_UserId
            // Skipped: matches Up() removal — index was already named correctly

            migrationBuilder.RenameIndex(
                name: "IX_Activity_Records_ProjectId",
                table: "Activity_Records",
                newName: "IX_ActivityRecords_Project");

            migrationBuilder.AlterColumn<string>(
                name: "AttachmentsJson",
                table: "Project_News_Posts",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ModerationReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Invitations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "invite",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "EventGroup",
                table: "Activity_Records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "project",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarUrl", "Bio", "CreatedAt", "Email", "Experience", "FullName", "Github", "GithubId", "GithubUsername", "GoogleId", "IsActive", "IsEmailVerified", "IsVerified", "Language", "LastLogin", "Linkedin", "PasswordHash", "PasswordResetToken", "PasswordResetTokenExpiresAt", "Rating", "Role", "Skills", "Timezone", "UpdatedAt", "VerificationToken", "VerificationTokenExpiresAt", "Website" },
                values: new object[,]
                {
                    { new Guid("0d5a7ca7-35d8-4cf6-9729-a90f43769f8e"), null, "Backend generalist focused on event-driven architectures and clean APIs.", new DateTime(2024, 12, 7, 9, 0, 0, 0, DateTimeKind.Utc), "timur.safin@devhunt.io", 7, "Timur Safin", "timursafin", null, null, null, true, true, true, "ru", new DateTime(2025, 1, 1, 9, 0, 0, 0, DateTimeKind.Utc), "timur-safin", "$2a$11$B5q7gfb21BuWXPfPhsE63uJ.iEZrxisjbk11xlAZVe5uZX6xLKhWW", null, null, 4.8f, "participant", new List<string> { ".NET", "PostgreSQL", "RabbitMQ" }, "UTC+5", new DateTime(2024, 12, 29, 9, 0, 0, 0, DateTimeKind.Utc), null, null, "https://timurs.dev" },
                    { new Guid("5d4d7082-ec8a-47c0-86b7-29c91ba524a1"), null, "Mobile + web hybrid dev building onboarding flows with Expo/Next.", new DateTime(2024, 12, 20, 9, 0, 0, 0, DateTimeKind.Utc), "daria.shevchenko@devhunt.io", 5, "Daria Shevchenko", "dariashevchenko", null, null, null, true, false, true, "en", new DateTime(2024, 12, 31, 9, 0, 0, 0, DateTimeKind.Utc), "daria-shevchenko", "$2a$11$B5q7gfb21BuWXPfPhsE63uJ.iEZrxisjbk11xlAZVe5uZX6xLKhWW", null, null, 4.2f, "participant", new List<string> { "React Native", "Next.js", "UX Writing" }, "UTC+1", new DateTime(2024, 12, 29, 9, 0, 0, 0, DateTimeKind.Utc), null, null, null },
                    { new Guid("6e2f9113-d6ef-4829-8714-0a3dc2a3e2f9"), null, "AI engineer experimenting with lightweight recommendation pipelines.", new DateTime(2024, 12, 17, 9, 0, 0, 0, DateTimeKind.Utc), "arseniy.karpov@devhunt.io", 4, "Arseniy Karpov", "akarpoff", null, null, null, true, false, false, "ru", new DateTime(2025, 1, 1, 3, 0, 0, 0, DateTimeKind.Utc), "arseniy-karpov", "$2a$11$B5q7gfb21BuWXPfPhsE63uJ.iEZrxisjbk11xlAZVe5uZX6xLKhWW", null, null, 4.3f, "participant", new List<string> { "Python", "ML.NET", "Vector Search" }, "UTC+4", new DateTime(2024, 12, 27, 9, 0, 0, 0, DateTimeKind.Utc), null, null, "https://karpov.ai" },
                    { new Guid("7fd40108-e6e6-4220-b66b-e5ef1f5c4843"), null, "UX researcher pairing squads with qualitative insights from student pilots.", new DateTime(2024, 12, 22, 9, 0, 0, 0, DateTimeKind.Utc), "olga.smirnova@devhunt.io", 6, "Olga Smirnova", "olga-smirnova", null, null, null, true, false, false, "ru", new DateTime(2025, 1, 1, 7, 0, 0, 0, DateTimeKind.Utc), "olga-smirnova", "$2a$11$B5q7gfb21BuWXPfPhsE63uJ.iEZrxisjbk11xlAZVe5uZX6xLKhWW", null, null, 4.4f, "participant", new List<string> { "UX Research", "Prototyping", "Storytelling" }, "UTC+3", new DateTime(2024, 12, 30, 9, 0, 0, 0, DateTimeKind.Utc), null, null, null },
                    { new Guid("a3a95f44-25b8-4a39-8c3e-4d6bb5be6db7"), null, "Frontend engineer shipping polished UI kits for civic hackathons.", new DateTime(2024, 12, 14, 9, 0, 0, 0, DateTimeKind.Utc), "anastasia.petrenko@devhunt.io", 5, "Anastasia Petrenko", "anapetrenko", null, null, null, true, true, true, "ru", new DateTime(2024, 12, 31, 9, 0, 0, 0, DateTimeKind.Utc), "anastasia-petrenko", "$2a$11$B5q7gfb21BuWXPfPhsE63uJ.iEZrxisjbk11xlAZVe5uZX6xLKhWW", null, null, 4.7f, "participant", new List<string> { "React", "Next.js", "Design Systems" }, "UTC+3", new DateTime(2024, 12, 28, 9, 0, 0, 0, DateTimeKind.Utc), null, null, "https://apetrenko.dev" },
                    { new Guid("b9c00db5-4b63-4c2f-a0f5-0d11041a5e58"), null, "Product-minded engineer who validates onboarding experiments end-to-end.", new DateTime(2024, 12, 2, 9, 0, 0, 0, DateTimeKind.Utc), "elena.koval@devhunt.io", 6, "Elena Koval", "elenakoval", null, null, null, true, false, false, "en", new DateTime(2024, 12, 30, 9, 0, 0, 0, DateTimeKind.Utc), "elena-koval", "$2a$11$B5q7gfb21BuWXPfPhsE63uJ.iEZrxisjbk11xlAZVe5uZX6xLKhWW", null, null, 4.5f, "participant", new List<string> { "Product Discovery", "TypeScript", "Analytics" }, "UTC+2", new DateTime(2024, 12, 26, 9, 0, 0, 0, DateTimeKind.Utc), null, null, null },
                    { new Guid("bc2dde64-00f2-4d6b-9ca8-9ff4ae3e8d12"), null, "DevOps-minded engineer keeping demo clusters green during hackathons.", new DateTime(2024, 11, 22, 9, 0, 0, 0, DateTimeKind.Utc), "maxim.volkov@devhunt.io", 8, "Maxim Volkov", "mvolkov", null, null, null, true, true, true, "ru", new DateTime(2024, 12, 30, 9, 0, 0, 0, DateTimeKind.Utc), "maxim-volkov", "$2a$11$B5q7gfb21BuWXPfPhsE63uJ.iEZrxisjbk11xlAZVe5uZX6xLKhWW", null, null, 4.9f, "participant", new List<string> { "Docker", "Kubernetes", "Grafana" }, "UTC+3", new DateTime(2024, 12, 25, 9, 0, 0, 0, DateTimeKind.Utc), null, null, "https://maxvolkov.dev" },
                    { new Guid("ee2f8a99-6d7a-47ad-83ac-5d2d0f1b9cd3"), null, "Data engineer prototyping ranking metrics for project recommendations.", new DateTime(2024, 12, 10, 9, 0, 0, 0, DateTimeKind.Utc), "nikita.lebedev@devhunt.io", 5, "Nikita Lebedev", "nikitalebedev", null, null, null, true, true, true, "en", new DateTime(2025, 1, 1, 8, 0, 0, 0, DateTimeKind.Utc), "nikita-lebedev", "$2a$11$B5q7gfb21BuWXPfPhsE63uJ.iEZrxisjbk11xlAZVe5uZX6xLKhWW", null, null, 4.6f, "participant", new List<string> { "Python", "ETL", "dbt" }, "UTC+2", new DateTime(2024, 12, 29, 9, 0, 0, 0, DateTimeKind.Utc), null, null, "https://lebedev.dev" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GithubId",
                table: "Users",
                column: "GithubId",
                unique: true,
                filter: "\"GithubId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true,
                filter: "\"GoogleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_User_Skills_UserId_SkillId",
                table: "User_Skills",
                columns: new[] { "UserId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSkillEntries_User_RawNormalized",
                table: "User_SkillEntries",
                columns: new[] { "UserId", "RawNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFollows_CreatedAt",
                table: "User_Follows",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserFollows_Follower",
                table: "User_Follows",
                column: "FollowerId");

            migrationBuilder.CreateIndex(
                name: "IX_User_Achievements_UserId_AchievementId",
                table: "User_Achievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_Achievements_UserId_EarnedAt",
                table: "User_Achievements",
                columns: new[] { "UserId", "EarnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_ProjectId_UserId",
                table: "TeamMembers",
                columns: new[] { "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Column_Position",
                table: "Tasks",
                columns: new[] { "ColumnId", "PositionInColumn" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Status",
                table: "Tasks",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskLinks_Source",
                table: "TaskLinks",
                column: "SourceTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskColumns_Project_Position",
                table: "TaskColumns",
                columns: new[] { "ProjectId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskBoardSettings_Project",
                table: "TaskBoardSettings",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skill_Aliases_AliasNormalized",
                table: "Skill_Aliases",
                column: "AliasNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Showcase_Projects_Featured_PublishedAt",
                table: "Showcase_Projects",
                columns: new[] { "Featured", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Showcase_Projects_ProjectId",
                table: "Showcase_Projects",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProjectId_ReviewerId_ReviewedUserId",
                table: "Reviews",
                columns: new[] { "ProjectId", "ReviewerId", "ReviewedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_IsRevoked",
                table: "RefreshTokens",
                columns: new[] { "UserId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_MatchScore_GeneratedAt",
                table: "Recommendations",
                columns: new[] { "MatchScore", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_UserId_GeneratedAt",
                table: "Recommendations",
                columns: new[] { "UserId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_UserId_ProjectId",
                table: "Recommendations",
                columns: new[] { "UserId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DifficultyLevel",
                table: "Projects",
                column: "DifficultyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Listing",
                table: "Projects",
                columns: new[] { "Status", "Visibility", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_Created",
                table: "Projects",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Visibility_Created",
                table: "Projects",
                columns: new[] { "Visibility", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Project_Tech_Stack_ProjectId_SkillId",
                table: "Project_Tech_Stack",
                columns: new[] { "ProjectId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubscriptions_CreatedAt",
                table: "Project_Subscriptions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Roles_ProjectId_RoleName",
                table: "Project_Roles",
                columns: new[] { "ProjectId", "RoleName" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNews_Project_Visibility_Created",
                table: "Project_News_Posts",
                columns: new[] { "ProjectId", "Visibility", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Project_Files_ProjectId_DeletedAt",
                table: "Project_Files",
                columns: new[] { "ProjectId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Project_Documents_ProjectId_Path",
                table: "Project_Documents",
                columns: new[] { "ProjectId", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_CreatedAt",
                table: "OutboxEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_Status_CreatedAt",
                table: "OutboxEvents",
                columns: new[] { "Status", "CreatedAt" },
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_TargetType_TargetId",
                table: "ModerationReports",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_UserId",
                table: "ModerationReports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Conversation_CreatedAt",
                table: "Messages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Project_CreatedAt",
                table: "Messages",
                columns: new[] { "ProjectId", "CreatedAt" });

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
                name: "IX_Integrations_IsActive_ServiceType",
                table: "Integrations",
                columns: new[] { "IsActive", "ServiceType" });

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_ProjectId_ServiceType",
                table: "Integrations",
                columns: new[] { "ProjectId", "ServiceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversation_Participants_ConversationId_UserId",
                table: "Conversation_Participants",
                columns: new[] { "ConversationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_Actor_Created",
                table: "Activity_Records",
                columns: new[] { "ActorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_EventType",
                table: "Activity_Records",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_TargetUser_Created",
                table: "Activity_Records",
                columns: new[] { "TargetUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_Visibility_Created",
                table: "Activity_Records",
                columns: new[] { "Visibility", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Code",
                table: "Achievements",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Activity_Records_Projects_ProjectId",
                table: "Activity_Records",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Activity_Records_Users_ActorId",
                table: "Activity_Records",
                column: "ActorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Activity_Records_Users_TargetUserId",
                table: "Activity_Records",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Users_InviteeId",
                table: "Invitations",
                column: "InviteeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Conversations_ConversationId",
                table: "Messages",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_ReplyToId",
                table: "Messages",
                column: "ReplyToId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Projects_ProjectId",
                table: "Messages",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_SenderId",
                table: "Messages",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationReports_Users_ReporterId",
                table: "ModerationReports",
                column: "ReporterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationReports_Users_UserId",
                table: "ModerationReports",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Documents_Users_AuthorId",
                table: "Project_Documents",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Files_Users_UploadedById",
                table: "Project_Files",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_News_Posts_Users_AuthorId",
                table: "Project_News_Posts",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Tech_Stack_Skills_SkillId",
                table: "Project_Tech_Stack",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Projects_ProjectId",
                table: "Recommendations",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_ReviewedUserId",
                table: "Reviews",
                column: "ReviewedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_ReviewerId",
                table: "Reviews",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Showcase_Comments_Showcase_Comments_ParentCommentId",
                table: "Showcase_Comments",
                column: "ParentCommentId",
                principalTable: "Showcase_Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Showcase_Comments_Users_AuthorId",
                table: "Showcase_Comments",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskBoardSettings_TaskColumns_DefaultColumnId",
                table: "TaskBoardSettings",
                column: "DefaultColumnId",
                principalTable: "TaskColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskLinks_Tasks_TargetTaskId",
                table: "TaskLinks",
                column: "TargetTaskId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_TaskColumns_ColumnId",
                table: "Tasks",
                column: "ColumnId",
                principalTable: "TaskColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Users_AssignedToUserId",
                table: "Tasks",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Achievements_Achievements_AchievementId",
                table: "User_Achievements",
                column: "AchievementId",
                principalTable: "Achievements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_User_SkillEntries_Skills_SkillId",
                table: "User_SkillEntries",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Skills_Skills_SkillId",
                table: "User_Skills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
