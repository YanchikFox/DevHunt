using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents an alternate spelling/synonym for a skill.
/// Used to improve search and autocomplete quality (e.g., "csharp" -> "C#", ".net" -> ".NET").
/// </summary>
[Table("Skill_Aliases")]
public class SkillAlias
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    /// <summary>
    /// Original alias value as stored (one-language).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// Normalized alias for matching (lowercased, stripped punctuation/spaces).
    /// Must be unique.
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string AliasNormalized { get; set; } = string.Empty;

    [ForeignKey(nameof(SkillId))]
    public Skill Skill { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
