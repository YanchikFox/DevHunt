using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DevHunt.Infrastructure;

/// <summary>
/// Factory for creating DbContext with Read/Write Replicas support
/// Corresponds to devhunt_deployment.puml: PostgreSQL Cluster with DB_RW and DB_RO endpoints
/// 
/// Usage:
/// - Write operations → DB_RW (Primary)
/// - Read operations → DB_RO (Replicas for scaling)
/// </summary>
public class ReadWriteDbContextFactory
{
    private readonly IConfiguration _configuration;
    private readonly string? _writeConnectionString;
    private readonly string? _readConnectionString;

    public ReadWriteDbContextFactory(IConfiguration configuration)
    {
        _configuration = configuration;
        _writeConnectionString = _configuration.GetConnectionString("DefaultConnection"); // Primary
        _readConnectionString = _configuration.GetConnectionString("ReadOnlyConnection"); // Replica(s)
    }

    /// <summary>
    /// Create DbContext for writing (Primary)
    /// </summary>
    public DevHuntDbContext CreateWriteContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DevHuntDbContext>();
        optionsBuilder.UseNpgsql(_writeConnectionString);
        return new DevHuntDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Create DbContext for reading (Replica)
    /// Used for read-heavy operations: searching projects, listing users, etc.
    /// </summary>
    public DevHuntDbContext CreateReadContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DevHuntDbContext>();
        // If read replica exists - use it, otherwise fallback to primary
        var connectionString = _readConnectionString ?? _writeConnectionString;
        optionsBuilder.UseNpgsql(connectionString);
        // Read-only queries optimization
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        return new DevHuntDbContext(optionsBuilder.Options);
    }
}

