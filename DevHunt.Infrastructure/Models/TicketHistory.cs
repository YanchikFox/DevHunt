using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Ticket History - ticket change history
/// Logs all changes to status, priority, admin assignment, etc.
/// </summary>
public class TicketHistory
{
    /// <summary>Unique history entry identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Support ticket identifier.</summary>
    [Required]
    public Guid TicketId { get; set; }

    /// <summary>User who made the change.</summary>
    [Required]
    public Guid ChangedByUserId { get; set; } // Who made the change

    /// <summary>Change type (status, priority, assignment, category, reopen, escalate).</summary>
    [MaxLength(50)]
    public string ChangeType { get; set; } = string.Empty; // status, priority, assignment, category, reopen, escalate

    /// <summary>Previous value (optional).</summary>
    [MaxLength(100)]
    public string? OldValue { get; set; } // Old value

    /// <summary>New value (optional).</summary>
    [MaxLength(100)]
    public string? NewValue { get; set; } // New value

    /// <summary>Reason for the change (optional).</summary>
    [MaxLength(500)]
    public string? Reason { get; set; } // Reason for change (optional)

    /// <summary>Change timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>Related ticket entity.</summary>
    public SupportTicket? Ticket { get; set; }
    /// <summary>User who made the change.</summary>
    public User? ChangedByUser { get; set; }
}

