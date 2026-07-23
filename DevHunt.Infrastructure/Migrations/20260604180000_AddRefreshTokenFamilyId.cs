using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

/// <summary>
/// Adds token family id for refresh-token rotation reuse detection.
/// </summary>
public partial class AddRefreshTokenFamilyId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "TokenFamilyId",
            table: "RefreshTokens",
            type: "uuid",
            nullable: false,
            defaultValueSql: "gen_random_uuid()");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_TokenFamilyId",
            table: "RefreshTokens",
            column: "TokenFamilyId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RefreshTokens_TokenFamilyId",
            table: "RefreshTokens");

        migrationBuilder.DropColumn(
            name: "TokenFamilyId",
            table: "RefreshTokens");
    }
}
