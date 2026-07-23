using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GithubUsername",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GitHubIssueId",
                table: "Tasks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GitHubIssueNumber",
                table: "Tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubIssueUrl",
                table: "Tasks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("0d5a7ca7-35d8-4cf6-9729-a90f43769f8e"),
                columns: new[] { "GithubUsername", "Skills" },
                values: new object[] { null, new List<string> { ".NET", "PostgreSQL", "RabbitMQ" } });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5d4d7082-ec8a-47c0-86b7-29c91ba524a1"),
                columns: new[] { "GithubUsername", "Skills" },
                values: new object[] { null, new List<string> { "React Native", "Next.js", "UX Writing" } });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("6e2f9113-d6ef-4829-8714-0a3dc2a3e2f9"),
                columns: new[] { "GithubUsername", "Skills" },
                values: new object[] { null, new List<string> { "Python", "ML.NET", "Vector Search" } });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("7fd40108-e6e6-4220-b66b-e5ef1f5c4843"),
                columns: new[] { "GithubUsername", "Skills" },
                values: new object[] { null, new List<string> { "UX Research", "Prototyping", "Storytelling" } });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a3a95f44-25b8-4a39-8c3e-4d6bb5be6db7"),
                columns: new[] { "GithubUsername", "Skills" },
                values: new object[] { null, new List<string> { "React", "Next.js", "Design Systems" } });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b9c00db5-4b63-4c2f-a0f5-0d11041a5e58"),
                columns: new[] { "GithubUsername", "Skills" },
                values: new object[] { null, new List<string> { "Product Discovery", "TypeScript", "Analytics" } });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("bc2dde64-00f2-4d6b-9ca8-9ff4ae3e8d12"),
                columns: new[] { "GithubUsername", "Skills" },
                values: new object[] { null, new List<string> { "Docker", "Kubernetes", "Grafana" } });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("ee2f8a99-6d7a-47ad-83ac-5d2d0f1b9cd3"),
                columns: new[] { "GithubUsername", "Skills" },
                values: new object[] { null, new List<string> { "Python", "ETL", "dbt" } });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GithubUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GitHubIssueId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "GitHubIssueNumber",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "GitHubIssueUrl",
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
