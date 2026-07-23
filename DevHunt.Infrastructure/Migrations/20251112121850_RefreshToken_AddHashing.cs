using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

/// <inheritdoc />
/// <summary>
/// SECURITY FIX (R4): Update RefreshToken to store HMAC hash instead of plain text
/// Adds fields for reuse detection and revocation tracking
/// WARNING: This migration will revoke all existing refresh tokens (users must re-login)
/// </summary>
public partial class RefreshToken_AddHashing : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SECURITY: Delete all existing refresh tokens (users will need to re-login)
            // This is necessary because we can't convert plain text tokens to hashes
            migrationBuilder.Sql("DELETE FROM \"RefreshTokens\";");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "RefreshTokens");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                table: "RefreshTokens",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "RefreshTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UsageCount",
                table: "RefreshTokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens");

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

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "UsageCount",
                table: "RefreshTokens");

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);
        }
    }
