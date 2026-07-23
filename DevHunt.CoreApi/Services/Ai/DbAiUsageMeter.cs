using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Database-backed <see cref="IAiUsageMeter"/> that writes to <c>AiOperationLogs</c>.
/// On start failure returns a scope without a log id so callers can still complete metering gracefully.
/// </summary>
public sealed class DbAiUsageMeter : IAiUsageMeter
{
    private readonly ReadWriteDbContextFactory _dbFactory;
    private readonly ILogger<DbAiUsageMeter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbAiUsageMeter"/> class.
    /// </summary>
    /// <param name="dbFactory">Factory for write-scoped database contexts.</param>
    /// <param name="logger">Logger for persistence warnings.</param>
    public DbAiUsageMeter(ReadWriteDbContextFactory dbFactory, ILogger<DbAiUsageMeter> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AiUsageScope> StartAsync(AiUsageStart start, CancellationToken ct)
    {
        try
        {
            await using var db = _dbFactory.CreateWriteContext();
            var log = BuildLog(start);
            db.AiOperationLogs.Add(log);
            await db.SaveChangesAsync(ct);
            return new AiUsageScope(start, log.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist AI usage start for user {UserId}", start.UserId);
            return new AiUsageScope(start, null);
        }
    }

    /// <inheritdoc />
    public async Task CompleteAsync(AiUsageScope scope, AiUsageResult result, CancellationToken ct)
    {
        try
        {
            await using var db = _dbFactory.CreateWriteContext();
            AiOperationLog? log = null;
            if (scope.LogId.HasValue)
            {
                log = await db.AiOperationLogs.FirstOrDefaultAsync(l => l.Id == scope.LogId.Value, ct);
            }

            if (log == null)
            {
                log = BuildLog(scope.Start);
                db.AiOperationLogs.Add(log);
            }

            ApplyResult(log, result);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist AI usage completion for user {UserId}", scope.Start.UserId);
        }
    }

    /// <summary>
    /// Builds a new log row in <c>started</c> status from the operation start context.
    /// </summary>
    private static AiOperationLog BuildLog(AiUsageStart start)
    {
        return new AiOperationLog
        {
            Id = Guid.NewGuid(),
            ProjectId = start.ProjectId ?? Guid.Empty,
            PlanId = start.PlanId,
            UserId = start.UserId,
            Capability = start.Capability.ToString(),
            StrategyVersion = start.StrategyVersion ?? "unknown",
            OperationType = start.Capability.ToString(),
            Status = "started",
            Success = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps a completed <see cref="AiUsageResult"/> onto an existing log row.
    /// </summary>
    private static void ApplyResult(AiOperationLog log, AiUsageResult result)
    {
        log.Status = result.Success ? "completed" : "failed";
        log.Success = result.Success;
        log.ErrorCode = result.ErrorCode;
        log.ErrorMessage = result.ErrorMessage;
        log.DurationMs = result.LatencyMs;

        if (result.Metadata != null)
        {
            log.PromptVersion = result.Metadata.PromptVersion;
            log.Provider = result.Metadata.Provider;
            log.Model = result.Metadata.Model;
            log.PromptTokens = result.Metadata.InputTokens;
            log.CompletionTokens = result.Metadata.OutputTokens;
            log.TotalTokens = result.Metadata.TotalTokens;
        }

        if (!string.IsNullOrWhiteSpace(result.MetadataJson))
        {
            log.MetadataJson = result.MetadataJson;
        }
    }
}
