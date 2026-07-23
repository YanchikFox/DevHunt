using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents an achievement (badge).
/// Corresponds to the `Achievements` entity in `devhunt_erd.puml`.
/// </summary>
[Table("Achievements")]
public class Achievement
{
    /// <summary>
    /// Unique achievement identifier.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Unique achievement code (e.g., "first_project", "team_leader").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Achievement title.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Achievement description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Achievement icon URL.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Achievement category (e.g., "projects", "teamwork", "recognition").
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>
    /// Points awarded for this achievement.
    /// </summary>
    public int Points { get; set; } = 0;

    // Navigation properties

    /// <summary>
    /// Users who have received this achievement.
    /// </summary>
    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}

