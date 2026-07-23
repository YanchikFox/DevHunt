using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Activity record captured for user and project feeds.
/// </summary>
[Table("Activity_Records")]
public class ActivityRecord
{
    /// <summary>Unique activity identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Related project ID (optional).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Actor user identifier.</summary>
    [Required]
    public Guid ActorId { get; set; }

    /// <summary>Target user identifier (optional).</summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>Event type key.</summary>
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Human-readable summary.</summary>
    [MaxLength(1000)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Optional JSON payload.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Visibility scope (public, subscribers, members, private).</summary>
    [Required]
    [MaxLength(50)]
    public string Visibility { get; set; } = "public";

    /// <summary>Event grouping label.</summary>
    [Required]
    [MaxLength(50)]
    public string EventGroup { get; set; } = "project";

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>Related project entity.</summary>
    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>Actor user entity.</summary>
    [ForeignKey(nameof(ActorId))]
    public User Actor { get; set; } = null!;

    /// <summary>Target user entity.</summary>
    [ForeignKey(nameof(TargetUserId))]
    public User? TargetUser { get; set; }
}
