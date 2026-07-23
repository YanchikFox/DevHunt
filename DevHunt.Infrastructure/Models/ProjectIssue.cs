using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Project Issue - a problem in a project requiring admin intervention
/// Used for various types of issues: technical, legal, conflicts, violations, etc.
/// </summary>
public class ProjectIssue
{
    /// <summary>Unique issue identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Project identifier.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>Reporter user identifier.</summary>
    [Required]
    public Guid ReporterId { get; set; } // Who created the issue

    /// <summary>Issue type (technical, legal, conflict, abuse, violation, security, other).</summary>
    [Required, MaxLength(50)]
    public string Type { get; set; } = "technical"; // technical, legal, conflict, abuse, violation, security, other

    /// <summary>Issue title.</summary>
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Issue description.</summary>
    [Required, MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Issue status (open, investigating, resolved, dismissed, escalated).</summary>
    [MaxLength(50)]
    public string Status { get; set; } = "open"; // open, investigating, resolved, dismissed, escalated

    /// <summary>Priority level (low, medium, high, urgent).</summary>
    [MaxLength(50)]
    public string Priority { get; set; } = "medium"; // low, medium, high, urgent

    /// <summary>Assigned admin user ID.</summary>
    public Guid? AssignedToAdminId { get; set; } // Assigned admin
    /// <summary>Related user ID (optional).</summary>
    public Guid? RelatedUserId { get; set; } // User related to the issue

    /// <summary>Admin resolution text.</summary>
    [MaxLength(5000)]
    public string? AdminResolution { get; set; } // Admin resolution
    /// <summary>Admin who resolved the issue.</summary>
    public Guid? ResolvedByAdminId { get; set; } // Who resolved the issue
    /// <summary>Resolution timestamp.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Escalation timestamp.</summary>
    public DateTime? EscalatedAt { get; set; }

    // Navigation properties
    /// <summary>Related project entity.</summary>
    public Project? Project { get; set; }
    /// <summary>Reporter user entity.</summary>
    public User? Reporter { get; set; }
    /// <summary>Assigned admin user entity.</summary>
    public User? AssignedToAdmin { get; set; }
    /// <summary>Related user entity.</summary>
    public User? RelatedUser { get; set; }
    /// <summary>Admin who resolved the issue.</summary>
    public User? ResolvedByAdmin { get; set; }
}

