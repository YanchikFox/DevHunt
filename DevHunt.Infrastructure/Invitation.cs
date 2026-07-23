using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Invitation entity for project team membership requests.
/// </summary>
/// <remarks>
/// Two invitation flows:
/// - "invite": Owner/leader invites a user to join (InviteeId = target user)
/// - "request": User requests to join a project (InviteeId = requesting user)
///
/// Status lifecycle:
/// - pending → accepted | declined | cancelled
///
/// When accepted, a TeamMember record is created automatically.
///
/// Corresponds to ERD diagram: Invitations table.
/// </remarks>
public class Invitation
{
    /// <summary>Unique invitation identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Target project ID.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>User who initiated the invitation/request.</summary>
    [Required]
    public Guid InviterId { get; set; }

    /// <summary>User being invited (for invites) or requesting (for requests).</summary>
    [Required]
    public Guid InviteeId { get; set; }

    /// <summary>Current status: pending, accepted, declined, cancelled.</summary>
    [Required, MaxLength(32)]
    public string Status { get; set; } = "pending";

    /// <summary>Type: "invite" (owner invites user) or "request" (user asks to join).</summary>
    [Required, MaxLength(50)]
    public string Type { get; set; } = "invite";

    /// <summary>Proposed team role for the invitee.</summary>
    [Required, MaxLength(100)]
    public string Role { get; set; } = "member";

    /// <summary>Optional message from inviter/requester.</summary>
    [MaxLength(2000)]
    public string? Message { get; set; }

    /// <summary>When invitation was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When invitation was accepted/declined (null = pending).</summary>
    public DateTime? RespondedAt { get; set; }

    // Navigation properties
    /// <summary>Target project.</summary>
    public Project? Project { get; set; }

    /// <summary>User who sent the invitation.</summary>
    public User? Inviter { get; set; }

    /// <summary>User who received the invitation.</summary>
    public User? Invitee { get; set; }
}
