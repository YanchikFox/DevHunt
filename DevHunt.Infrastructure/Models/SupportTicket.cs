using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Support Ticket - user support ticket
/// Used for support requests (bugs, questions, feature requests)
/// </summary>
public class SupportTicket
{
    /// <summary>Unique ticket identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Ticket author user ID.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Ticket category (question, bug, feature, billing, other).</summary>
    [MaxLength(50)]
    public string Category { get; set; } = "question"; // question, bug, feature, billing, other

    /// <summary>Ticket subject.</summary>
    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Ticket description.</summary>
    [Required, MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Status (open, in_progress, waiting_user, resolved, closed).</summary>
    [MaxLength(50)]
    public string Status { get; set; } = "open"; // open, in_progress, waiting_user, resolved, closed

    /// <summary>Priority level (low, medium, high, urgent).</summary>
    [MaxLength(50)]
    public string Priority { get; set; } = "medium"; // low, medium, high, urgent

    /// <summary>Assigned support user ID (optional).</summary>
    public Guid? AssignedToUserId { get; set; } // Admin/moderator assigned to the ticket

    /// <summary>Related project ID (optional).</summary>
    public Guid? RelatedProjectId { get; set; } // If ticket is related to a project
    /// <summary>Related user ID (optional).</summary>
    public Guid? RelatedUserId { get; set; } // If ticket is related to another user

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Resolution timestamp.</summary>
    public DateTime? ResolvedAt { get; set; }
    /// <summary>Closed timestamp.</summary>
    public DateTime? ClosedAt { get; set; }

    // Navigation properties
    /// <summary>Ticket author entity.</summary>
    public User? User { get; set; }
    /// <summary>Assigned support user entity.</summary>
    public User? AssignedToUser { get; set; }
    /// <summary>Related project entity.</summary>
    public Project? RelatedProject { get; set; }
    /// <summary>Ticket messages thread.</summary>
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}

