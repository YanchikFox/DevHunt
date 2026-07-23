using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Durable queue row for post-analysis embedding generation (replaces fire-and-forget Task.Run).
/// </summary>
[Table("CodeAnalysisEmbeddingJobs")]
public class CodeAnalysisEmbeddingJob
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Idempotency key — one job per analysis result.</summary>
    [Required]
    public Guid AnalysisResultId { get; set; }

    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = CodeAnalysisEmbeddingJobStatus.Pending;

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 5;

    public DateTime? NextAttemptAt { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(AnalysisResultId))]
    public CodeAnalysisResult AnalysisResult { get; set; } = null!;
}
