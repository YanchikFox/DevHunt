using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Message in a support ticket
/// </summary>
public class TicketMessage
{
    /// <summary>Unique message identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Support ticket identifier.</summary>
    [Required]
    public Guid TicketId { get; set; }

    /// <summary>Author user identifier.</summary>
    [Required]
    public Guid AuthorId { get; set; }

    /// <summary>Message content.</summary>
    [Required, MaxLength(5000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Whether the message is internal (staff-only).</summary>
    public bool IsInternal { get; set; } = false; // Visible only to admins (internal notes)

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Last edit timestamp.</summary>
    public DateTime? EditedAt { get; set; }

    // Navigation properties
    /// <summary>Related ticket entity.</summary>
    public SupportTicket? Ticket { get; set; }
    /// <summary>Author user entity.</summary>
    public User? Author { get; set; }
}

