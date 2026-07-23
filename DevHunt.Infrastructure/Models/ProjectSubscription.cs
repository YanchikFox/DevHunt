using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Subscription linking a user to project updates.
/// </summary>
[Table("Project_Subscriptions")]
public class ProjectSubscription
{
    /// <summary>Project identifier.</summary>
    public Guid ProjectId { get; set; }
    /// <summary>User identifier.</summary>
    public Guid UserId { get; set; }
    /// <summary>Subscription creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Related project entity.</summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>Related user entity.</summary>
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
