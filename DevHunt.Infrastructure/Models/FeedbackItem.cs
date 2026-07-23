using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Feedback Item - bug, suggestion, idea from the community
/// Allows users to propose improvements and report issues
/// </summary>
public class FeedbackItem
{
    /// <summary>Unique feedback identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Author user ID.</summary>
    [Required]
    public Guid AuthorId { get; set; }

    /// <summary>Feedback type (bug, suggestion, feature, question).</summary>
    [Required, MaxLength(50)]
    public string Type { get; set; } = "suggestion"; // bug, suggestion, feature, question

    /// <summary>Feedback title.</summary>
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Feedback description.</summary>
    [Required, MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Status value (open, under_review, planned, in_progress, completed, rejected, duplicate).</summary>
    [MaxLength(50)]
    public string Status { get; set; } = "open"; // open, under_review, planned, in_progress, completed, rejected, duplicate

    /// <summary>Priority level (low, medium, high).</summary>
    [MaxLength(50)]
    public string Priority { get; set; } = "medium"; // low, medium, high

    /// <summary>Vote count.</summary>
    public int VoteCount { get; set; } = 0; // Vote count
    /// <summary>Comment count.</summary>
    public int CommentCount { get; set; } = 0; // Comment count

    /// <summary>Related project ID (optional).</summary>
    public Guid? RelatedProjectId { get; set; } // If feedback is related to a project
    /// <summary>Assigned user ID for development (optional).</summary>
    public Guid? AssignedToUserId { get; set; } // Assigned for development (admin)

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Completion timestamp.</summary>
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    /// <summary>Author user entity.</summary>
    public User? Author { get; set; }
    /// <summary>Assigned user entity.</summary>
    public User? AssignedToUser { get; set; }
    /// <summary>Related project entity.</summary>
    public Project? RelatedProject { get; set; }
    /// <summary>Votes for this feedback.</summary>
    public ICollection<FeedbackVote> Votes { get; set; } = new List<FeedbackVote>();
    /// <summary>Comments for this feedback.</summary>
    public ICollection<FeedbackComment> Comments { get; set; } = new List<FeedbackComment>();
}

