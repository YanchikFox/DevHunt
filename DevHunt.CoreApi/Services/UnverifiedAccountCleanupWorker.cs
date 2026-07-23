using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Background worker that periodically removes unverified user accounts
/// older than 48 hours. Runs every 6 hours.
/// </summary>
public class UnverifiedAccountCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UnverifiedAccountCleanupWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);
    private readonly TimeSpan _staleThreshold = TimeSpan.FromHours(48);
    private const int BatchSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnverifiedAccountCleanupWorker"/> class.
    /// </summary>
    /// <param name="serviceProvider">Creates scoped <see cref="DevHunt.Infrastructure.DevHuntDbContext"/> instances per cleanup run.</param>
    /// <param name="logger">Logger for cleanup progress and per-user failures.</param>
    public UnverifiedAccountCleanupWorker(
        IServiceProvider serviceProvider,
        ILogger<UnverifiedAccountCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>Runs cleanup every six hours after an initial five-minute startup delay.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Unverified Account Cleanup Worker started");

        // Delay startup to let other services initialize
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupStaleAccountsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during unverified account cleanup");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Unverified Account Cleanup Worker stopped");
    }

    /// <summary>
    /// Deletes up to 100 email-only accounts that remain unverified for 48+ hours.
    /// Skips users with OAuth links or rows blocked by foreign-key constraints.
    /// </summary>
    private async Task CleanupStaleAccountsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevHuntDbContext>();

        var cutoff = DateTime.UtcNow - _staleThreshold;

        var staleUsers = await db.Users
            .Where(u => !u.IsEmailVerified
                        && u.CreatedAt < cutoff
                        && u.GithubId == null
                        && u.GoogleId == null)
            .OrderBy(u => u.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (staleUsers.Count == 0)
            return;

        _logger.LogInformation("Found {Count} stale unverified accounts to clean up", staleUsers.Count);

        int deleted = 0;
        foreach (var user in staleUsers)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                db.Users.Remove(user);
                await db.SaveChangesAsync(cancellationToken);
                deleted++;
            }
            catch (DbUpdateException ex)
            {
                // FK constraint — user has related data, skip for now
                _logger.LogWarning("Skipped cleanup of user {UserId} ({Email}): {Error}",
                    user.Id, user.Email, ex.InnerException?.Message ?? ex.Message);
                db.ChangeTracker.Clear();
            }
        }

        _logger.LogInformation("Cleaned up {Deleted}/{Total} stale unverified accounts", deleted, staleUsers.Count);
    }
}
