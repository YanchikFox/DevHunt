using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Comment on a project news post.
/// Flat structure (no nesting), with soft delete support.
/// </summary>
[Table("News_Post_Comments")]
public class NewsPostComment
{
    /// <summary>Unique comment identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>News post identifier.</summary>
    [Required]
    public Guid NewsPostId { get; set; }

    /// <summary>Comment author identifier.</summary>
    [Required]
    public Guid AuthorId { get; set; }

    /// <summary>Comment content.</summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last update timestamp (UTC).</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Soft delete timestamp (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>Related news post.</summary>
    [ForeignKey(nameof(NewsPostId))]
    public ProjectNewsPost NewsPost { get; set; } = null!;

    /// <summary>Comment author.</summary>
    [ForeignKey(nameof(AuthorId))]
    public User Author { get; set; } = null!;
}
