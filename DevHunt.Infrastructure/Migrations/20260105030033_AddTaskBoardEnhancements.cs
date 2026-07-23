using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskBoardEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "CanvasX",
                table: "Tasks",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "CanvasY",
                table: "Tasks",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ColumnId",
                table: "Tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionInColumn",
                table: "Tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TaskAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttachedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAttachments_Project_Files_ProjectFileId",
                        column: x => x.ProjectFileId,
                        principalTable: "Project_Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaskAttachments_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskAttachments_Users_AttachedByUserId",
                        column: x => x.AttachedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CanvasX = table.Column<float>(type: "real", nullable: true),
                    CanvasY = table.Column<float>(type: "real", nullable: true),
                    CanvasWidth = table.Column<float>(type: "real", nullable: true),
                    CanvasHeight = table.Column<float>(type: "real", nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    WipLimit = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskColumns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskLinks_Tasks_SourceTaskId",
                        column: x => x.SourceTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskLinks_Tasks_TargetTaskId",
                        column: x => x.TargetTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskLinks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskBoardSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewMode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CanvasZoom = table.Column<float>(type: "real", nullable: false),
                    CanvasPanX = table.Column<float>(type: "real", nullable: false),
                    CanvasPanY = table.Column<float>(type: "real", nullable: false),
                    ShowCompletedTasks = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultColumnId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskBoardSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskBoardSettings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskBoardSettings_TaskColumns_DefaultColumnId",
                        column: x => x.DefaultColumnId,
                        principalTable: "TaskColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "IX_Tasks_Column_Position",
                table: "Tasks",
                columns: new[] { "ColumnId", "PositionInColumn" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_AttachedByUserId",
                table: "TaskAttachments",
                column: "AttachedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_ProjectFile",
                table: "TaskAttachments",
                column: "ProjectFileId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_Task",
                table: "TaskAttachments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskBoardSettings_DefaultColumnId",
                table: "TaskBoardSettings",
                column: "DefaultColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskBoardSettings_Project",
                table: "TaskBoardSettings",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskColumns_Project",
                table: "TaskColumns",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskColumns_Project_Position",
                table: "TaskColumns",
                columns: new[] { "ProjectId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskLinks_CreatedByUserId",
                table: "TaskLinks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskLinks_Source",
                table: "TaskLinks",
                column: "SourceTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskLinks_Target",
                table: "TaskLinks",
                column: "TargetTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskLinks_Unique",
                table: "TaskLinks",
                columns: new[] { "SourceTaskId", "TargetTaskId", "LinkType" },
                unique: true);

            // Data migration: Create default columns for each existing project
            migrationBuilder.Sql(@"
                INSERT INTO ""TaskColumns"" (""Id"", ""ProjectId"", ""Name"", ""Position"", ""Color"", ""IsDefault"", ""IsCompleted"", ""CreatedAt"")
                SELECT
                    gen_random_uuid(),
                    p.""Id"",
                    col.""Name"",
                    col.""Position"",
                    col.""Color"",
                    col.""IsDefault"",
                    col.""IsCompleted"",
                    NOW()
                FROM ""Projects"" p
                CROSS JOIN (VALUES
                    ('To Do', 0, '#6B7280', true, false),
                    ('In Progress', 1, '#3B82F6', false, false),
                    ('Review', 2, '#F59E0B', false, false),
                    ('Done', 3, '#10B981', false, true),
                    ('Archived', 4, '#9CA3AF', false, true),
                    ('Cancelled', 5, '#EF4444', false, true)
                ) AS col(""Name"", ""Position"", ""Color"", ""IsDefault"", ""IsCompleted"")
            ");

            // Data migration: Map existing tasks to columns based on Status
            migrationBuilder.Sql(@"
                UPDATE ""Tasks"" t
                SET ""ColumnId"" = tc.""Id"",
                    ""PositionInColumn"" = (
                        SELECT COUNT(*)
                        FROM ""Tasks"" t2
                        WHERE t2.""ProjectId"" = t.""ProjectId""
                          AND t2.""Status"" = t.""Status""
                          AND t2.""CreatedAt"" < t.""CreatedAt""
                    )
                FROM ""TaskColumns"" tc
                WHERE tc.""ProjectId"" = t.""ProjectId""
                  AND (
                    (t.""Status"" = 'todo' AND tc.""Name"" = 'To Do') OR
                    (t.""Status"" = 'in_progress' AND tc.""Name"" = 'In Progress') OR
                    (t.""Status"" = 'review' AND tc.""Name"" = 'Review') OR
                    (t.""Status"" = 'done' AND tc.""Name"" = 'Done') OR
                    (t.""Status"" = 'archived' AND tc.""Name"" = 'Archived') OR
                    (t.""Status"" = 'cancelled' AND tc.""Name"" = 'Cancelled')
                  )
            ");

            // Put any unmapped tasks (unknown status) into the first column
            migrationBuilder.Sql(@"
                UPDATE ""Tasks"" t
                SET ""ColumnId"" = (
                    SELECT tc.""Id""
                    FROM ""TaskColumns"" tc
                    WHERE tc.""ProjectId"" = t.""ProjectId""
                    ORDER BY tc.""Position""
                    LIMIT 1
                )
                WHERE t.""ColumnId"" IS NULL
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_TaskColumns_ColumnId",
                table: "Tasks",
                column: "ColumnId",
                principalTable: "TaskColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_TaskColumns_ColumnId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "TaskAttachments");

            migrationBuilder.DropTable(
                name: "TaskBoardSettings");

            migrationBuilder.DropTable(
                name: "TaskLinks");

            migrationBuilder.DropTable(
                name: "TaskColumns");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_Column_Position",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CanvasX",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CanvasY",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ColumnId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "PositionInColumn",
                table: "Tasks");

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
