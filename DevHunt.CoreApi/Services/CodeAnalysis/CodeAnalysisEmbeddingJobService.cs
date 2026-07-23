using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.CodeAnalysis;

/// <summary>
/// Observability DTO for embedding job status.
/// </summary>
public sealed record CodeAnalysisEmbeddingJobStatusDto(
    Guid JobId,
    Guid AnalysisResultId,
    Guid ProjectId,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTime? NextAttemptAt,
    string? LastError,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt);

/// <summary>
/// Enqueues and reports durable embedding generation jobs.
/// </summary>
public interface ICodeAnalysisEmbeddingJobService
{
    /// <summary>
    /// Creates a pending job for the analysis result, or returns the existing job id (idempotent).
    /// </summary>
    Task<Guid> EnqueueAsync(Guid analysisResultId, Guid projectId, CancellationToken ct);

    /// <summary>
    /// Returns job status for observability, or null when not found.
    /// </summary>
    Task<CodeAnalysisEmbeddingJobStatusDto?> GetStatusAsync(Guid jobId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class CodeAnalysisEmbeddingJobService : ICodeAnalysisEmbeddingJobService
{
    private readonly DevHuntDbContext _db;
    private readonly ILogger<CodeAnalysisEmbeddingJobService> _logger;

    /// <summary>
    /// Creates the embedding job service.
    /// </summary>
    public CodeAnalysisEmbeddingJobService(
        DevHuntDbContext db,
        ILogger<CodeAnalysisEmbeddingJobService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> EnqueueAsync(Guid analysisResultId, Guid projectId, CancellationToken ct)
    {
        var existing = await _db.CodeAnalysisEmbeddingJobs
            .AsNoTracking()
            .Where(j => j.AnalysisResultId == analysisResultId)
            .Select(j => j.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != Guid.Empty)
        {
            return existing;
        }

        var job = new CodeAnalysisEmbeddingJob
        {
            Id = Guid.NewGuid(),
            AnalysisResultId = analysisResultId,
            ProjectId = projectId,
            Status = CodeAnalysisEmbeddingJobStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.CodeAnalysisEmbeddingJobs.Add(job);

        try
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Enqueued embedding job {JobId} for analysis result {AnalysisResultId}",
                job.Id, analysisResultId);
            return job.Id;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogDebug(ex, "Embedding job already exists for analysis {AnalysisResultId}", analysisResultId);
            var raced = await _db.CodeAnalysisEmbeddingJobs
                .AsNoTracking()
                .Where(j => j.AnalysisResultId == analysisResultId)
                .Select(j => j.Id)
                .FirstAsync(ct);
            return raced;
        }
    }

    /// <inheritdoc />
    public async Task<CodeAnalysisEmbeddingJobStatusDto?> GetStatusAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.CodeAnalysisEmbeddingJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        return job == null
            ? null
            : new CodeAnalysisEmbeddingJobStatusDto(
                job.Id,
                job.AnalysisResultId,
                job.ProjectId,
                job.Status,
                job.AttemptCount,
                job.MaxAttempts,
                job.NextAttemptAt,
                job.LastError,
                job.CreatedAt,
                job.UpdatedAt,
                job.CompletedAt);
    }
}
