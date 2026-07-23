namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Persisted status values for <see cref="CodeAnalysisEmbeddingJob"/>.
/// </summary>
public static class CodeAnalysisEmbeddingJobStatus
{
    /// <summary>Queued and waiting for the background worker.</summary>
    public const string Pending = "pending";

    /// <summary>Currently being processed by the worker.</summary>
    public const string Processing = "processing";

    /// <summary>Embeddings were generated successfully.</summary>
    public const string Completed = "completed";

    /// <summary>Failed but may be retried while attempts remain.</summary>
    public const string Failed = "failed";

    /// <summary>Exceeded max attempts; requires operator attention.</summary>
    public const string DeadLetter = "dead_letter";
}
