using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Stored AI planning draft for a project.
/// Contains the generated plan JSON and metadata.
/// </summary>
public class AiPlan
{
    /// <summary>Primary key for a stored AI planning draft.</summary>
    public Guid Id { get; set; }

    /// <summary>Project that the generated plan is intended to help build.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>User who created the AI planning draft.</summary>
    [Required]
    public Guid CreatedByUserId { get; set; }

    /// <summary>User who applied the AI plan to the project, when the draft has been applied.</summary>
    public Guid? AppliedByUserId { get; set; }

    /// <summary>Project idea or prompt that seeded the generated plan.</summary>
    [Required, MaxLength(2000)]
    public string Idea { get; set; } = string.Empty;

    /// <summary>Technology stack context supplied for plan generation.</summary>
    [Required, MaxLength(500)]
    public string TechStack { get; set; } = string.Empty;

    /// <summary>Lifecycle state of the planning draft.</summary>
    [Required, MaxLength(32)]
    public string Status { get; set; } = "draft";

    /// <summary>Version of the persisted plan JSON structure.</summary>
    [Required, MaxLength(32)]
    public string PlanVersion { get; set; } = "v1";

    /// <summary>Generated plan payload stored as JSON.</summary>
    [Required]
    public string PlanJson { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the planning draft was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>UTC timestamp when the planning draft was last changed.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>UTC timestamp when the plan was applied to the project.</summary>
    public DateTime? AppliedAt { get; set; }

    /// <summary>Project that owns this AI planning draft.</summary>
    public Project? Project { get; set; }
    /// <summary>User who created this AI planning draft.</summary>
    public User? CreatedByUser { get; set; }
    /// <summary>User who applied this AI plan to the project, when applicable.</summary>
    public User? AppliedByUser { get; set; }
    /// <summary>Audit entries for AI operations performed against this plan.</summary>
    public ICollection<AiOperationLog> OperationLogs { get; set; } = new List<AiOperationLog>();
}
