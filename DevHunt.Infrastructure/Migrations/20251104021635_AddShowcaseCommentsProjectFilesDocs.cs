using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

    /// <inheritdoc />
    public partial class AddShowcaseCommentsProjectFilesDocs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectFiles_Projects_ProjectId",
                table: "ProjectFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectFiles_Users_UploadedByUserId",
                table: "ProjectFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProjectFiles",
                table: "ProjectFiles");

            migrationBuilder.DropIndex(
                name: "IX_ProjectFiles_ProjectId_DeletedAt_CreatedAt",
                table: "ProjectFiles");

            migrationBuilder.RenameTable(
                name: "ProjectFiles",
                newName: "Project_Files");

            migrationBuilder.RenameColumn(
                name: "UploadedByUserId",
                table: "Project_Files",
                newName: "UploadedById");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Project_Files",
                newName: "UploadedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectFiles_UploadedByUserId",
                table: "Project_Files",
                newName: "IX_Project_Files_UploadedById");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "Project_Files",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Project_Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "Project_Files",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Project_Files",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<int>(
                name: "DownloadCount",
                table: "Project_Files",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Project_Files",
                table: "Project_Files",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Project_Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContentFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ViewsCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Project_Documents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Project_Documents_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Showcase_Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShowcaseProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Showcase_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Showcase_Comments_Showcase_Comments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "Showcase_Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Showcase_Comments_Showcase_Projects_ShowcaseProjectId",
                        column: x => x.ShowcaseProjectId,
                        principalTable: "Showcase_Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Showcase_Comments_Users_AuthorId",
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
                value: new DateTime(2025, 11, 4, 2, 16, 31, 349, DateTimeKind.Utc).AddTicks(2036));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 2, 16, 31, 349, DateTimeKind.Utc).AddTicks(8415));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 2, 16, 31, 343, DateTimeKind.Utc).AddTicks(226));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 2, 16, 31, 343, DateTimeKind.Utc).AddTicks(214));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 2, 16, 31, 342, DateTimeKind.Utc).AddTicks(5640));

            migrationBuilder.CreateIndex(
                name: "IX_Project_Files_ProjectId",
                table: "Project_Files",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Files_ProjectId_DeletedAt",
                table: "Project_Files",
                columns: new[] { "ProjectId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Project_Documents_AuthorId",
                table: "Project_Documents",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Documents_ProjectId",
                table: "Project_Documents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Documents_ProjectId_Path",
                table: "Project_Documents",
                columns: new[] { "ProjectId", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_Showcase_Comments_AuthorId",
                table: "Showcase_Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Showcase_Comments_ParentCommentId",
                table: "Showcase_Comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Showcase_Comments_ShowcaseProjectId",
                table: "Showcase_Comments",
                column: "ShowcaseProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Files_Projects_ProjectId",
                table: "Project_Files",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Files_Users_UploadedById",
                table: "Project_Files",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Project_Files_Projects_ProjectId",
                table: "Project_Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_Files_Users_UploadedById",
                table: "Project_Files");

            migrationBuilder.DropTable(
                name: "Project_Documents");

            migrationBuilder.DropTable(
                name: "Showcase_Comments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Project_Files",
                table: "Project_Files");

            migrationBuilder.DropIndex(
                name: "IX_Project_Files_ProjectId",
                table: "Project_Files");

            migrationBuilder.DropIndex(
                name: "IX_Project_Files_ProjectId_DeletedAt",
                table: "Project_Files");

            migrationBuilder.DropColumn(
                name: "DownloadCount",
                table: "Project_Files");

            migrationBuilder.RenameTable(
                name: "Project_Files",
                newName: "ProjectFiles");

            migrationBuilder.RenameColumn(
                name: "UploadedById",
                table: "ProjectFiles",
                newName: "UploadedByUserId");

            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "ProjectFiles",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Project_Files_UploadedById",
                table: "ProjectFiles",
                newName: "IX_ProjectFiles_UploadedByUserId");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "ProjectFiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ProjectFiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "ProjectFiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "ProjectFiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProjectFiles",
                table: "ProjectFiles",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 23, 28, 21, 112, DateTimeKind.Utc).AddTicks(4658));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 23, 28, 21, 112, DateTimeKind.Utc).AddTicks(9665));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 23, 28, 21, 107, DateTimeKind.Utc).AddTicks(5068));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 23, 28, 21, 107, DateTimeKind.Utc).AddTicks(5051));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 23, 28, 21, 106, DateTimeKind.Utc).AddTicks(4690));

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_ProjectId_DeletedAt_CreatedAt",
                table: "ProjectFiles",
                columns: new[] { "ProjectId", "DeletedAt", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectFiles_Projects_ProjectId",
                table: "ProjectFiles",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectFiles_Users_UploadedByUserId",
                table: "ProjectFiles",
                column: "UploadedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
