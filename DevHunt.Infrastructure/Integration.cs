using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents a project integration with an external service (GitHub, GitLab, Jira, etc.).
/// Corresponds to the `Integrations` entity in `devhunt_erd.puml`.
/// </summary>
[Table("Integrations")]
public class Integration
{
    /// <summary>
    /// Unique integration identifier.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the project to which the integration belongs.
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Integration service type (e.g., "github", "gitlab", "jira").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty;

    /// <summary>
    /// Integration configuration (JSONB).
    /// Stores integration settings (e.g., {"repository": "owner/repo", "webhook_secret": "..."}).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? ConfigJson { get; set; }

    /// <summary>
    /// Encrypted access token for external API access.
    /// </summary>
    public string? AccessTokenEncrypted { get; set; }

    /// <summary>
    /// Flag indicating whether the integration is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Integration creation date and time.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time of the last synchronization.
    /// </summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>
    /// Date and time of the last update.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>
    /// Project to which the integration belongs.
    /// </summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Helper property for working with Config as a dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, object>? Config
    {
        get
        {
            if (string.IsNullOrEmpty(ConfigJson)) return null;
            var raw = JsonSerializer.Deserialize<Dictionary<string, object>>(ConfigJson);
            if (raw == null) return null;
            return new Dictionary<string, object>(raw, StringComparer.OrdinalIgnoreCase);
        }
        set => ConfigJson = value == null 
            ? null 
            : JsonSerializer.Serialize(value);
    }
}

