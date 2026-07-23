using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ChatEnhancements : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 34, 7, 438, DateTimeKind.Utc).AddTicks(2420));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 34, 7, 438, DateTimeKind.Utc).AddTicks(4926));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 34, 7, 436, DateTimeKind.Utc).AddTicks(6806));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 34, 7, 436, DateTimeKind.Utc).AddTicks(6799));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 34, 7, 436, DateTimeKind.Utc).AddTicks(4359));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 11, 6, 95, DateTimeKind.Utc).AddTicks(4679));

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 11, 6, 95, DateTimeKind.Utc).AddTicks(6329));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 11, 6, 94, DateTimeKind.Utc).AddTicks(3364));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 11, 6, 94, DateTimeKind.Utc).AddTicks(3346));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 2, 0, 11, 6, 94, DateTimeKind.Utc).AddTicks(738));
        }
    }

