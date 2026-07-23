using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

/// <summary>
/// Restores the unique constraint on active team memberships that was silently dropped in
/// <c>20260209201025_AddNewsLikesAndComments</c> and never recreated (DEV-97).
///
/// Uses a partial unique index scoped to <c>Status = 'active'</c> so that:
/// - concurrent double-accept of an invitation can no longer create duplicate active memberships
///   (the dead <c>DbUpdateException "duplicate key"</c> catch in InvitationsController is live again);
/// - a user who left a project (rows with <c>Status = 'left'</c>) can rejoin without conflict.
/// </summary>
public partial class RestoreTeamMemberUniqueIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Defensive de-duplication: the constraint was absent for ~25 migrations, so duplicate
        // active memberships may already exist and would make the unique index creation fail.
        // Keep the earliest membership per (ProjectId, UserId) active; mark the rest as 'left'.
        migrationBuilder.Sql(
            """
            UPDATE "TeamMembers" t
            SET "Status" = 'left',
                "LeftAt" = COALESCE("LeftAt", NOW() AT TIME ZONE 'UTC')
            FROM (
                SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "ProjectId", "UserId"
                           ORDER BY "JoinedAt" ASC, "Id" ASC
                       ) AS rn
                FROM "TeamMembers"
                WHERE "Status" = 'active'
            ) dup
            WHERE t."Id" = dup."Id" AND dup.rn > 1;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_TeamMembers_ProjectId_UserId_Active",
            table: "TeamMembers",
            columns: new[] { "ProjectId", "UserId" },
            unique: true,
            filter: "\"Status\" = 'active'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TeamMembers_ProjectId_UserId_Active",
            table: "TeamMembers");
    }
}
