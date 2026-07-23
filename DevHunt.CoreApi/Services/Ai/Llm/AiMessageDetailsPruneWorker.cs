using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Background worker that deletes expired rows from <c>AiMessageDetails</c>
/// after <see cref="RetentionDays"/>, keeping detailed AI transparency payloads bounded.
/// </summary>
public sealed class AiMessageDetailsPruneWorker : BackgroundService
{
    /// <summary>Days to retain full AI message detail JSON before pruning.</summary>
    public const int RetentionDays = 14;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiMessageDetailsPruneWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiMessageDetailsPruneWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory for scoped database access.</param>
    /// <param name="logger">Logger for prune results and failures.</param>
    public AiMessageDetailsPruneWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AiMessageDetailsPruneWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Runs the prune loop every <see cref="Interval"/> until shutdown.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PruneAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>Deletes detail rows older than the retention cutoff.</summary>
    private async Task PruneAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DevHuntDbContext>();
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

            var deleted = await db.AiMessageDetails
                .Where(d => d.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                _logger.LogInformation("Pruned {Count} expired AI message detail payloads", deleted);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prune expired AI message detail payloads");
        }
    }
}
