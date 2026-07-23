using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Feature flag for enabling/disabling platform features.
/// Managed by superadmin via admin panel.
/// </summary>
[Table("FeatureFlags")]
public class FeatureFlag
{
    /// <summary>Stable feature flag key used by application code to gate platform behavior.</summary>
    [Key]
    [MaxLength(128)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Indicates whether the platform feature is currently enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Admin-facing explanation of what the feature flag controls.</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>UTC timestamp when the feature flag was last changed.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User who last changed the feature flag, when tracked.</summary>
    public Guid? UpdatedById { get; set; }
}
