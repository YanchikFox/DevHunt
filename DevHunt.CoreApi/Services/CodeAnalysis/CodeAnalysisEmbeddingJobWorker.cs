using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.CodeAnalysis;

/// <summary>
/// Processes pending <see cref="CodeAnalysisEmbeddingJob"/> rows with retries and dead-letter handling.
/// </summary>
public sealed class CodeAnalysisEmbeddingJobWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 5;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CodeAnalysisEmbeddingJobWorker> _logger;

    /// <summary>
    /// Creates the background worker.
    /// </summary>
    public CodeAnalysisEmbeddingJobWorker(
        IServiceProvider serviceProvider,
        ILogger<CodeAnalysisEmbeddingJobWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Code analysis embedding job worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing embedding jobs");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevHuntDbContext>();
        var generation = scope.ServiceProvider.GetRequiredService<ICodeAnalysisEmbeddingGenerationService>();

        var now = DateTime.UtcNow;
        var candidateIds = await db.CodeAnalysisEmbeddingJobs
            .Where(j =>
                j.Status == CodeAnalysisEmbeddingJobStatus.Pending
                || (j.Status == CodeAnalysisEmbeddingJobStatus.Failed
                    && j.AttemptCount < j.MaxAttempts
                    && (j.NextAttemptAt == null || j.NextAttemptAt <= now)))
            .OrderBy(j => j.CreatedAt)
            .Take(BatchSize)
            .Select(j => j.Id)
            .ToListAsync(ct);

        foreach (var jobId in candidateIds)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await ProcessJobAsync(db, generation, jobId, ct);
        }
    }

    private async Task ProcessJobAsync(
        DevHuntDbContext db,
        ICodeAnalysisEmbeddingGenerationService generation,
        Guid jobId,
        CancellationToken ct)
    {
        var job = await db.CodeAnalysisEmbeddingJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null)
        {
            return;
        }

        if (job.Status is not (CodeAnalysisEmbeddingJobStatus.Pending or CodeAnalysisEmbeddingJobStatus.Failed))
        {
            return;
        }

        job.Status = CodeAnalysisEmbeddingJobStatus.Processing;
        job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        try
        {
            var count = await generation.GenerateForAnalysisResultAsync(job.AnalysisResultId, ct);
            job.Status = CodeAnalysisEmbeddingJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.LastError = count == 0 ? "No issues to embed" : null;
            job.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Embedding job {JobId} completed ({Count} rule groups)",
                job.Id, count);
        }
        catch (Exception ex)
        {
            job.AttemptCount++;
            job.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            job.UpdatedAt = DateTime.UtcNow;

            if (job.AttemptCount >= job.MaxAttempts)
            {
                job.Status = CodeAnalysisEmbeddingJobStatus.DeadLetter;
                _logger.LogError(
                    ex,
                    "Embedding job {JobId} moved to dead letter after {Attempts} attempts",
                    job.Id, job.AttemptCount);
            }
            else
            {
                job.Status = CodeAnalysisEmbeddingJobStatus.Failed;
                var delayMinutes = Math.Min(60, Math.Pow(2, job.AttemptCount));
                job.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
                _logger.LogWarning(
                    ex,
                    "Embedding job {JobId} failed (attempt {Attempt}/{Max}), next retry at {NextAttempt}",
                    job.Id, job.AttemptCount, job.MaxAttempts, job.NextAttemptAt);
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
