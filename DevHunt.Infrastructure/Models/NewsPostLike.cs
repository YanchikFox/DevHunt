using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Like on a project news post. One user = one like per post.
/// </summary>
[Table("News_Post_Likes")]
public class NewsPostLike
{
    /// <summary>Unique like identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>News post identifier.</summary>
    [Required]
    public Guid NewsPostId { get; set; }

    /// <summary>User who liked the post.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>Related news post.</summary>
    [ForeignKey(nameof(NewsPostId))]
    public ProjectNewsPost NewsPost { get; set; } = null!;

    /// <summary>User who liked.</summary>
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
