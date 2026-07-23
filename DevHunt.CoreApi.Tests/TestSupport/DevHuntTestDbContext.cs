using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Tests.TestSupport;

/// <summary>
/// Test <see cref="DevHuntDbContext"/> that omits PostgreSQL-only mappings (jsonb owned types, pgvector).
/// </summary>
internal sealed class DevHuntTestDbContext : DevHuntDbContext
{
    public DevHuntTestDbContext(DbContextOptions<DevHuntDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>().Ignore(p => p.OpenRoles);
        modelBuilder.Ignore<CodeAnalysisEmbedding>();
        base.OnModelCreating(modelBuilder);
    }
}
