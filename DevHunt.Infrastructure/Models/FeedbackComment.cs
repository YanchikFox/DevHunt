using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Comment on a feedback item
/// </summary>
public class FeedbackComment
{
    /// <summary>Unique comment identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Related feedback item ID.</summary>
    [Required]
    public Guid FeedbackId { get; set; }

    /// <summary>Author user ID.</summary>
    [Required]
    public Guid AuthorId { get; set; }

    /// <summary>Comment content.</summary>
    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Soft delete timestamp.</summary>
    public DateTime? DeletedAt { get; set; } // Soft delete

    // Navigation properties
    /// <summary>Related feedback item.</summary>
    public FeedbackItem? Feedback { get; set; }
    /// <summary>Author user entity.</summary>
    public User? Author { get; set; }
}

