using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Stores code analysis results from the DevHunt Analyzer service.
/// Each record corresponds to one analysis run (triggered by push webhook or manual).
/// </summary>
[Table("CodeAnalysisResults")]
public class CodeAnalysisResult
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Project that owns the analyzed repository.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>Integration that triggered the analysis.</summary>
    public Guid? IntegrationId { get; set; }

    /// <summary>Repository in "owner/repo" format.</summary>
    [Required]
    [MaxLength(300)]
    public string Repository { get; set; } = string.Empty;

    /// <summary>Branch analyzed (e.g., "main").</summary>
    [MaxLength(300)]
    public string? Branch { get; set; }

    /// <summary>Commit SHA that triggered the analysis.</summary>
    [MaxLength(40)]
    public string? CommitSha { get; set; }

    /// <summary>Total issues found by the analyzer.</summary>
    public int TotalIssues { get; set; }

    /// <summary>Total files scanned.</summary>
    public int TotalFiles { get; set; }

    /// <summary>Analysis duration in milliseconds.</summary>
    public int AnalysisTimeMs { get; set; }

    /// <summary>Severity breakdown as JSONB ({"critical": 2, "high": 5, ...}).</summary>
    [Column(TypeName = "jsonb")]
    public string? SeverityCountsJson { get; set; }

    /// <summary>Category breakdown as JSONB ({"security": 3, "performance": 1, ...}).</summary>
    [Column(TypeName = "jsonb")]
    public string? CategoryCountsJson { get; set; }

    /// <summary>Full list of issues as JSONB array.</summary>
    [Column(TypeName = "jsonb")]
    public string? IssuesJson { get; set; }

    /// <summary>Analysis status: "completed", "failed", "in_progress".</summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "completed";

    /// <summary>Error message if analysis failed.</summary>
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    [ForeignKey(nameof(IntegrationId))]
    public Integration? Integration { get; set; }
}
