using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevHunt.CoreApi.Extensions;

/// <summary>
/// Extension methods for database configuration
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Configures Entity Framework Core with PostgreSQL
    /// </summary>
    public static IServiceCollection AddDatabaseContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<DevHuntDbContext>(options =>
            options.UseNpgsql(dataSource));

        return services;
    }
}

