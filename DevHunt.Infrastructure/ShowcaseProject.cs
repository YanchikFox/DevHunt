using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents a published project showcase.
/// Corresponds to `Showcase_Projects` entity in `devhunt_erd.puml`.
/// </summary>
[Table("Showcase_Projects")]
public class ShowcaseProject
{
    /// <summary>
    /// Unique showcase identifier.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Project identifier.
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Brief showcase description (summary).
    /// </summary>
    [Required]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Project demo URL.
    /// </summary>
    public string? DemoUrl { get; set; }

    /// <summary>
    /// Project demo video URL.
    /// </summary>
    public string? DemoVideoUrl { get; set; }

    /// <summary>
    /// Project screenshots (JSONB).
    /// Stores array of screenshot URLs.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? ScreenshotsJson { get; set; }

    /// <summary>
    /// Project repository URL (GitHub, GitLab, etc.).
    /// </summary>
    public string? RepositoryUrl { get; set; }

    /// <summary>
    /// Project metrics (JSONB).
    /// Stores arbitrary metrics (e.g., {"users": 1000, "deployments": 50}).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? MetricsJson { get; set; }

    /// <summary>
    /// Showcase publication date and time.
    /// </summary>
    [Required]
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Flag indicating whether the project is featured (promoted).
    /// </summary>
    public bool Featured { get; set; } = false;

    /// <summary>
    /// Showcase views count.
    /// </summary>
    public int ViewsCount { get; set; } = 0;

    /// <summary>
    /// Showcase likes count.
    /// </summary>
    public int LikesCount { get; set; } = 0;

    /// <summary>
    /// Last update date and time.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>
    /// Project for which the showcase was created.
    /// </summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Helper property for working with Screenshots as an array.
    /// </summary>
    [NotMapped]
    public string[]? Screenshots
    {
        get => string.IsNullOrEmpty(ScreenshotsJson) 
            ? null 
            : JsonSerializer.Deserialize<string[]>(ScreenshotsJson);
        set => ScreenshotsJson = value == null 
            ? null 
            : JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// Helper property for working with Metrics as a dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, object>? Metrics
    {
        get => string.IsNullOrEmpty(MetricsJson) 
            ? null 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(MetricsJson);
        set => MetricsJson = value == null 
            ? null 
            : JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// Comments on the showcase project.
    /// </summary>
    public ICollection<ShowcaseComment> Comments { get; set; } = new List<ShowcaseComment>();
}

