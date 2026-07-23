using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Stores vector embeddings for code analysis rule groups.
/// Used for semantic search (RAG) over analysis issues.
/// One embedding per (AnalysisResultId, RuleId) — grouped by rule, not per-issue.
/// </summary>
[Table("CodeAnalysisEmbeddings")]
public class CodeAnalysisEmbedding
{
    /// <summary>Primary key for a persisted code analysis embedding.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Which analysis result this embedding belongs to.</summary>
    [Required]
    public Guid AnalysisResultId { get; set; }

    /// <summary>Project ID for fast lookups.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>Rule ID this embedding represents.</summary>
    [Required]
    [MaxLength(200)]
    public string RuleId { get; set; } = string.Empty;

    /// <summary>Human-readable rule name.</summary>
    [MaxLength(300)]
    public string? RuleName { get; set; }

    /// <summary>Severity of this rule group.</summary>
    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = string.Empty;

    /// <summary>Category of this rule group.</summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Number of issues in this rule group.</summary>
    public int IssueCount { get; set; }

    /// <summary>The text that was embedded (rule_name + message + category + severity + top files).</summary>
    [Required]
    public string EmbeddingText { get; set; } = string.Empty;

    /// <summary>Sample message from an issue in this group.</summary>
    public string? SampleMessage { get; set; }

    /// <summary>Top affected files (JSON array of strings).</summary>
    [Column(TypeName = "jsonb")]
    public string? TopFilesJson { get; set; }

    /// <summary>CWE ID if applicable.</summary>
    [MaxLength(20)]
    public string? CweId { get; set; }

    /// <summary>Sample fix suggestion.</summary>
    public string? SampleSuggestion { get; set; }

    /// <summary>UTC timestamp when this embedding was created.</summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    /// <summary>Code analysis result whose grouped rule text was embedded.</summary>
    [ForeignKey(nameof(AnalysisResultId))]
    public CodeAnalysisResult AnalysisResult { get; set; } = null!;

    /// <summary>Project used to scope semantic search over analysis embeddings.</summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;
}
