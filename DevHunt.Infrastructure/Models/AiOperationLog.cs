using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Audit log for AI operations (generate/apply).
/// </summary>
public class AiOperationLog
{
    /// <summary>Primary key for an AI operation audit entry.</summary>
    public Guid Id { get; set; }

    /// <summary>Project whose AI workflow produced this operation entry.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>Planning draft associated with the operation, when the operation belongs to an <see cref="AiPlan"/>.</summary>
    public Guid? PlanId { get; set; }

    /// <summary>User who requested the AI operation.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Operation category recorded for the AI request, such as plan generation or application.</summary>
    [Required, MaxLength(32)]
    public string OperationType { get; set; } = "generate";

    /// <summary>AI capability area that handled the request.</summary>
    [Required, MaxLength(32)]
    public string Capability { get; set; } = "unknown";

    /// <summary>Strategy implementation version used to shape the AI request.</summary>
    [Required, MaxLength(32)]
    public string StrategyVersion { get; set; } = "v1";

    /// <summary>Prompt template version used for this operation, when tracked.</summary>
    [MaxLength(32)]
    public string? PromptVersion { get; set; }

    /// <summary>LLM provider that served the operation, when available.</summary>
    [MaxLength(32)]
    public string? Provider { get; set; }

    /// <summary>Provider model identifier used for the operation, when available.</summary>
    [MaxLength(64)]
    public string? Model { get; set; }

    /// <summary>Lifecycle status recorded for the AI operation.</summary>
    [Required, MaxLength(32)]
    public string Status { get; set; } = "started";

    /// <summary>Indicates whether the AI operation completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Machine-readable failure code captured when the operation fails.</summary>
    [MaxLength(64)]
    public string? ErrorCode { get; set; }

    /// <summary>Human-readable failure details captured when the operation fails.</summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>Total operation duration in milliseconds, when measured.</summary>
    public int? DurationMs { get; set; }

    /// <summary>Prompt token count reported by the provider, when available.</summary>
    public int? PromptTokens { get; set; }
    /// <summary>Completion token count reported by the provider, when available.</summary>
    public int? CompletionTokens { get; set; }
    /// <summary>Total token count reported by the provider, when available.</summary>
    public int? TotalTokens { get; set; }

    /// <summary>Additional structured operation metadata stored as JSON.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>UTC timestamp when the AI operation entry was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Project whose AI workflow produced this audit entry.</summary>
    public Project? Project { get; set; }
    /// <summary>AI planning draft associated with this audit entry, when applicable.</summary>
    public AiPlan? Plan { get; set; }
    /// <summary>User who requested the AI operation.</summary>
    public User? User { get; set; }
}
