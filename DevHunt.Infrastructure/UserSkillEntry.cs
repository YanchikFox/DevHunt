using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents a single skill token entered by a user.
/// It can be linked to a canonical Skill (SkillId) while still preserving the raw text.
/// </summary>
[Table("User_SkillEntries")]
public class UserSkillEntry
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public Guid? SkillId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Raw { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RawNormalized { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(SkillId))]
    public Skill? Skill { get; set; }
}
