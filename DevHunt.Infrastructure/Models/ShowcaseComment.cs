using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Comment on a showcase project.
/// Allows users to comment on published projects.
/// </summary>
[Table("Showcase_Comments")]
public class ShowcaseComment
{
    /// <summary>
    /// Unique identifier of the comment.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Showcase project identifier.
    /// </summary>
    [Required]
    public Guid ShowcaseProjectId { get; set; }

    /// <summary>
    /// Comment author identifier.
    /// </summary>
    [Required]
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Comment content.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Parent comment identifier (for nested replies).
    /// </summary>
    public Guid? ParentCommentId { get; set; }

    /// <summary>
    /// Comment creation date and time.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Comment last update date and time.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Comment deletion date and time (soft delete).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Flag indicating whether the comment has been edited.
    /// </summary>
    public bool IsEdited { get; set; } = false;

    // Navigation properties

    /// <summary>
    /// Showcase project to which the comment belongs.
    /// </summary>
    [ForeignKey(nameof(ShowcaseProjectId))]
    public ShowcaseProject ShowcaseProject { get; set; } = null!;

    /// <summary>
    /// Comment author.
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public User Author { get; set; } = null!;

    /// <summary>
    /// Parent comment (for nested replies).
    /// </summary>
    [ForeignKey(nameof(ParentCommentId))]
    public ShowcaseComment? ParentComment { get; set; }

    /// <summary>
    /// Child comments (replies to this comment).
    /// </summary>
    public ICollection<ShowcaseComment> Replies { get; set; } = new List<ShowcaseComment>();
}

