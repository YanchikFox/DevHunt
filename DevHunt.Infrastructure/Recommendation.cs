using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents an ML recommendation of a project for a user.
/// Corresponds to the `Recommendations` entity in `devhunt_erd.puml`.
/// </summary>
[Table("Recommendations")]
public class Recommendation
{
    /// <summary>
    /// Unique identifier of the recommendation.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the project being recommended.
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Identifier of the user for whom the recommendation is given.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Compatibility match score from 0.0 to 1.0.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal MatchScore { get; set; }

    /// <summary>
    /// Recommendation reasoning (JSONB).
    /// Stores an explanation of why the project was recommended (e.g., {"skills_match": 0.9, "experience_match": 0.8}).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? ReasoningJson { get; set; }

    /// <summary>
    /// Date and time when the recommendation was generated.
    /// </summary>
    [Required]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Flag indicating whether the user has viewed the recommendation.
    /// </summary>
    public bool Viewed { get; set; } = false;

    /// <summary>
    /// Flag indicating whether the user has acted on the recommendation (e.g., applied).
    /// </summary>
    public bool Actioned { get; set; } = false;

    // Navigation properties

    /// <summary>
    /// Project that is recommended.
    /// </summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>
    /// User for whom the recommendation is given.
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>
    /// Helper property for working with Reasoning as a dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, object>? Reasoning
    {
        get => string.IsNullOrEmpty(ReasoningJson) 
            ? null 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(ReasoningJson);
        set => ReasoningJson = value == null 
            ? null 
            : JsonSerializer.Serialize(value);
    }
}

