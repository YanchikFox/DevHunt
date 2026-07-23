using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents a technology/skill used in a project.
/// Corresponds to the `Project_Tech_Stack` entity in `devhunt_erd.puml`.
/// </summary>
[Table("Project_Tech_Stack")]
public class ProjectTechStack
{
    /// <summary>
    /// Unique identifier of the project-skill association.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Project identifier.
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Skill/technology identifier.
    /// </summary>
    [Required]
    public Guid SkillId { get; set; }

    /// <summary>
    /// Flag indicating whether this skill is required for the project.
    /// </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// Required skill proficiency level (beginner, intermediate, advanced, expert).
    /// </summary>
    [MaxLength(50)]
    public string? ProficiencyRequired { get; set; }

    // Navigation properties

    /// <summary>
    /// Project using this technology.
    /// </summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Skill/technology used in the project.
    /// </summary>
    [ForeignKey(nameof(SkillId))]
    public Skill Skill { get; set; } = null!;
}

