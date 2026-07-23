using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// News post published within a project.
/// </summary>
[Table("Project_News_Posts")]
public class ProjectNewsPost
{
    /// <summary>Unique news post identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Project identifier.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>Author user identifier.</summary>
    [Required]
    public Guid AuthorId { get; set; }

    /// <summary>Post title.</summary>
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Post content.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Visibility scope (public, subscribers, members).</summary>
    [Required]
    [MaxLength(50)]
    public string Visibility { get; set; } = "public"; // public | subscribers | members

    /// <summary>Whether the post is pinned.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Serialized attachment metadata.</summary>
    public string? AttachmentsJson { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last update timestamp (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Denormalized likes count.</summary>
    public int LikesCount { get; set; }

    /// <summary>Denormalized comments count.</summary>
    public int CommentsCount { get; set; }

    /// <summary>Related project entity.</summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>Author user entity.</summary>
    [ForeignKey(nameof(AuthorId))]
    public User Author { get; set; } = null!;

    /// <summary>Likes on this post.</summary>
    public ICollection<NewsPostLike> Likes { get; set; } = new List<NewsPostLike>();

    /// <summary>Comments on this post.</summary>
    public ICollection<NewsPostComment> Comments { get; set; } = new List<NewsPostComment>();
}
