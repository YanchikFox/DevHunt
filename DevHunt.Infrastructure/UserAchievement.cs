using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents user's relationship with an achievement.
/// Corresponds to `User_Achievements` entity in `devhunt_erd.puml`.
/// </summary>
[Table("User_Achievements")]
public class UserAchievement
{
    /// <summary>
    /// Unique record identifier.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// User identifier.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Achievement identifier.
    /// </summary>
    [Required]
    public Guid AchievementId { get; set; }

    /// <summary>
    /// Achievement earning date and time.
    /// </summary>
    [Required]
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Completion progress (0-100), if achievement has progress.
    /// </summary>
    public int? Progress { get; set; }

    // Navigation properties

    /// <summary>
    /// User who earned the achievement.
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>
    /// The achievement itself.
    /// </summary>
    [ForeignKey(nameof(AchievementId))]
    public Achievement Achievement { get; set; } = null!;
}

