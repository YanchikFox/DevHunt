using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DevHunt.DatabaseMigrator;

// ============================================================================
// DevHunt Database Migrator - Standalone Migration Job
// ============================================================================
// This is a separate job that runs database migrations before services start.
// It prevents race conditions when multiple service instances try to migrate
// the database simultaneously (especially in Kubernetes environments).
//
// Usage:
//   - Run as a Kubernetes Job or Docker Compose init container
//   - Should complete before core-api and auth-service start
//   - Exits with code 0 on success, non-zero on failure
// ============================================================================

class Program
{
    static async Task<int> Main(string[] args)
    {
        EnvLoader.Load();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<Program>();

        logger.LogInformation("=== DevHunt Database Migrator Starting ===");

        // Build configuration from environment variables and appsettings
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            logger.LogError("Connection string 'DefaultConnection' is not configured");
            return 1;
        }

        logger.LogInformation("Connecting to database...");

        // Create DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<DevHuntDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseLoggerFactory(loggerFactory);

        var maxAttempts = 10;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                logger.LogInformation("Attempt {Attempt}/{MaxAttempts} to apply migrations", attempt, maxAttempts);

                using var dbContext = new DevHuntDbContext(optionsBuilder.Options);
                await dbContext.Database.MigrateAsync();

                logger.LogInformation("=== Migrations completed successfully ===");
                return 0;
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException or DbUpdateException)
            {
                if (attempt == maxAttempts)
                {
                    logger.LogError(ex, "Failed to apply database migrations after {Attempts} attempts", attempt);
                    return 1;
                }

                var delaySeconds = Math.Pow(2, attempt);
                logger.LogWarning(ex, 
                    "Attempt {Attempt}/{MaxAttempts} to apply migrations failed. Retrying in {DelaySeconds}s...", 
                    attempt, maxAttempts, delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error during migration: {Message}", ex.Message);
                return 1;
            }
        }

        return 1;
    }
}

