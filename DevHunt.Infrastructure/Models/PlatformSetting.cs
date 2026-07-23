using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Key-value platform configuration setting.
/// Managed by superadmin via admin panel.
/// </summary>
[Table("PlatformSettings")]
public class PlatformSetting
{
    /// <summary>Stable configuration key used by application code to read a platform setting.</summary>
    [Key]
    [MaxLength(128)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Persisted setting value stored for the platform configuration key.</summary>
    [Required]
    [MaxLength(4000)]
    public string Value { get; set; } = string.Empty;

    /// <summary>Admin-facing explanation of what the platform setting controls.</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>UTC timestamp when the platform setting was last changed.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User who last changed the platform setting, when tracked.</summary>
    public Guid? UpdatedById { get; set; }
}
