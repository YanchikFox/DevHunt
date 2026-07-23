using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    /// <summary>
    /// Legacy "project chat" conversations were created with <c>Title = project.Name</c>
    /// (e.g. "DevHunt"). When they were converted to <c>#general</c> project channels,
    /// that stale title was carried over and leaked into the channel header as the
    /// primary identifier instead of the slug. This migration normalises those rows:
    /// every <c>#general</c> channel gets <c>Title = "general"</c>, regardless of
    /// what it was before. Non-<c>general</c> channels are left untouched so any
    /// user-provided display title survives.
    /// </summary>
    [DbContext(typeof(DevHuntDbContext))]
    [Migration("20260422180000_NormalizeGeneralChannelTitle")]
    public partial class NormalizeGeneralChannelTitle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Conversations""
                SET ""Title"" = 'general',
                    ""UpdatedAt"" = NOW() AT TIME ZONE 'UTC'
                WHERE ""Type"" = 2
                  AND ""Slug"" = 'general'
                  AND (""Title"" IS DISTINCT FROM 'general');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally irreversible: we don't know the original project-name
            // titles, and they were junk anyway.
        }
    }
}
