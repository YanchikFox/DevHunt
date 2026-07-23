using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Project document (wiki, README, documentation).
/// Allows the team to store and manage project documentation.
/// </summary>
[Table("Project_Documents")]
public class ProjectDocument
{
    /// <summary>
    /// Unique identifier of the document.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Project identifier.
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Document author identifier.
    /// </summary>
    [Required]
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Document title.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Document content (Markdown or HTML).
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Document type (e.g.: "readme", "wiki", "guide", "changelog", "api-docs").
    /// </summary>
    [MaxLength(50)]
    public string? DocumentType { get; set; }

    /// <summary>
    /// Content format (e.g.: "markdown", "html", "plain").
    /// </summary>
    [MaxLength(20)]
    public string ContentFormat { get; set; } = "markdown";

    /// <summary>
    /// Document path (for organizing hierarchy, e.g.: "docs/getting-started.md").
    /// </summary>
    [MaxLength(500)]
    public string? Path { get; set; }

    /// <summary>
    /// Sort order (for display in list).
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Flag indicating whether the document is public (visible to everyone).
    /// </summary>
    public bool IsPublic { get; set; } = false;

    /// <summary>
    /// Document creation date and time.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update date and time.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deletion date and time (soft delete).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// View count.
    /// </summary>
    public int ViewsCount { get; set; } = 0;

    // Navigation properties

    /// <summary>
    /// Project to which the document belongs.
    /// </summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Document author.
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public User Author { get; set; } = null!;
}

