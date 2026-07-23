using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents a role in a project with metadata.
/// Corresponds to the `Project_Roles` entity in `devhunt_erd.puml`.
/// </summary>
[Table("Project_Roles")]
public class ProjectRole
{
    /// <summary>
    /// Unique identifier of the role.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Project identifier.
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Role name (e.g., "Backend Developer", "UI/UX Designer").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Role description.
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Required number of people for this role.
    /// </summary>
    public int RequiredCount { get; set; } = 1;

    /// <summary>
    /// Current number of filled positions.
    /// </summary>
    public int FilledCount { get; set; } = 0;

    /// <summary>
    /// Required skills for this role (JSONB).
    /// Stores an array of skill IDs or their names.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? RequiredSkillsJson { get; set; }

    /// <summary>
    /// Role creation date and time.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update date and time.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>
    /// Project for which this role was created.
    /// </summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Helper property for working with RequiredSkills as an array.
    /// </summary>
    [NotMapped]
    public string[]? RequiredSkills
    {
        get => string.IsNullOrEmpty(RequiredSkillsJson) 
            ? null 
            : JsonSerializer.Deserialize<string[]>(RequiredSkillsJson);
        set => RequiredSkillsJson = value == null 
            ? null 
            : JsonSerializer.Serialize(value);
    }
}

