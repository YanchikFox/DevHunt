using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents a skill/technology in the system.
/// Corresponds to `Skills` entity in `devhunt_erd.puml`.
/// </summary>
[Table("Skills")]
public class Skill
{
    /// <summary>
    /// Unique skill identifier.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Skill name (unique).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Skill category (e.g., "Backend", "Frontend", "DevOps", "Design").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Skill description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Skill icon URL.
    /// </summary>
    [Column("IconUrl")]
    public string? IconUrl { get; set; }

    /// <summary>
    /// Record creation date and time.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>
    /// Relationships with users (who owns this skill).
    /// </summary>
    public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();

    /// <summary>
    /// Relationships with projects (which projects require this skill).
    /// </summary>
    public ICollection<ProjectTechStack> ProjectTechStacks { get; set; } = new List<ProjectTechStack>();
}

