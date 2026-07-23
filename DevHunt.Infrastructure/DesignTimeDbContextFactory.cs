using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DevHunt.Infrastructure.Configuration;

namespace DevHunt.Infrastructure;

/// <summary>
/// Lightweight design-time factory so dotnet-ef can create DevHuntDbContext
/// without executing long-running services or hitting real infrastructure.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DevHuntDbContext>
{
    public DevHuntDbContext CreateDbContext(string[] args)
    {
        EnvLoader.Load();

        var connectionString =
            Environment.GetEnvironmentVariable("DEVHUNT_DB_CONNECTION") ??
            Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTION") ??
            EnvLoader.BuildConnectionStringFromPostgresEnv() ??
            throw new InvalidOperationException(
                "No connection string found. Set DEVHUNT_DB_CONNECTION or CONNECTIONSTRINGS__DEFAULTCONNECTION env var.");

        var optionsBuilder = new DbContextOptionsBuilder<DevHuntDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new DevHuntDbContext(optionsBuilder.Options);
    }
}
