using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents user's proficiency level with a skill.
/// Corresponds to `User_Skills` entity in `devhunt_erd.puml`.
/// </summary>
[Table("User_Skills")]
public class UserSkill
{
    /// <summary>
    /// Unique identifier for user-skill relationship.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// User identifier.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Skill identifier.
    /// </summary>
    [Required]
    public Guid SkillId { get; set; }

    /// <summary>
    /// Skill proficiency level: beginner, intermediate, advanced, expert.
    /// </summary>
    [MaxLength(50)]
    public string ProficiencyLevel { get; set; } = "intermediate";

    /// <summary>
    /// Years of experience with this skill.
    /// </summary>
    public int? YearsOfExperience { get; set; }

    /// <summary>
    /// Skill verification flag (e.g., through certificate or review).
    /// </summary>
    public bool Verified { get; set; } = false;

    /// <summary>
    /// Record creation date and time.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>
    /// User who owns the skill.
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>
    /// Skill owned by the user.
    /// </summary>
    [ForeignKey(nameof(SkillId))]
    public Skill Skill { get; set; } = null!;
}

