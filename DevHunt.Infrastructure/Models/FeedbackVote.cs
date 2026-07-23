using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Vote for a feedback item
/// One user = one vote
/// </summary>
public class FeedbackVote
{
    /// <summary>Unique vote identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Feedback item identifier.</summary>
    [Required]
    public Guid FeedbackId { get; set; }

    /// <summary>Voting user identifier.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>True for upvote, false for downvote.</summary>
    public bool IsUpvote { get; set; } = true; // true = upvote, false = downvote

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>Related feedback item.</summary>
    public FeedbackItem? Feedback { get; set; }
    /// <summary>User who cast the vote.</summary>
    public User? User { get; set; }
}

