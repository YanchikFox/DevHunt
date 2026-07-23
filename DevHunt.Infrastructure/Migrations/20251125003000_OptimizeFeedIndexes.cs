using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

[DbContext(typeof(DevHuntDbContext))]
[Migration("20251125003000_OptimizeFeedIndexes")]
public partial class OptimizeFeedIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_ActivityRecords_Actor_Created",
            table: "Activity_Records",
            columns: new[] { "ActorId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ActivityRecords_EventType",
            table: "Activity_Records",
            column: "EventType");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectSubscriptions_UserId",
            table: "Project_Subscriptions",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserFollows_Follower",
            table: "User_Follows",
            column: "FollowerId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ActivityRecords_Actor_Created",
            table: "Activity_Records");

        migrationBuilder.DropIndex(
            name: "IX_ActivityRecords_EventType",
            table: "Activity_Records");

        migrationBuilder.DropIndex(
            name: "IX_ProjectSubscriptions_UserId",
            table: "Project_Subscriptions");

        migrationBuilder.DropIndex(
            name: "IX_UserFollows_Follower",
            table: "User_Follows");
    }
}
