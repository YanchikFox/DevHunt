using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Constants;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace DevHunt.CoreApi.Controllers;

// I-03: Status and type constants to eliminate string literals
/// <summary>Invitation lifecycle status strings persisted on project invitation rows.</summary>
static class InvitationStatusValues
{
    /// <summary>Invitation or join request is waiting for a user or project decision.</summary>
    public const string Pending = "pending";

    /// <summary>Invitation or join request was accepted and should correspond to team membership.</summary>
    public const string Accepted = "accepted";

    /// <summary>Invitation or join request was rejected by the recipient or project owner.</summary>
    public const string Declined = "declined";

    /// <summary>Pending invitation was withdrawn before acceptance.</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>Invitation direction strings that distinguish owner invites from user join requests.</summary>
static class InvitationTypeValues
{
    /// <summary>Project owner or leader invited a user to join.</summary>
    public const string Invite = "invite";

    /// <summary>User asked to join a project and is waiting for project approval.</summary>
    public const string Request = "request";
}

/// <summary>
/// Controller for managing project invitations and join requests.
/// </summary>
/// <remarks>
/// Supports two invitation flows:
/// 1. **Invite** - Project owner/leader invites a user to join
/// 2. **Request** - User requests to join a project (pending owner approval)
///
/// Invitation lifecycle:
/// - pending → accepted (user joins team) | declined | cancelled
///
/// Access control:
/// - Send invites: Project owner or team leaders only
/// - Send requests: Any authenticated user
/// - Accept/decline: Depends on invitation type
///
/// Routes: api/invitations/*
/// </remarks>
[ApiController]
[Route("api/invitations")]
[Authorize]
public class InvitationsController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly IEventBusService _eventBus;
    private readonly ICacheService _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvitationsController"/> class.
    /// </summary>
    /// <param name="db">Database context used to manage invitations, team membership, chats, and notifications.</param>
    /// <param name="eventBus">Event bus used to publish invitation lifecycle events after persistence.</param>
    /// <param name="cache">Cache service used to invalidate project data after accepted invitations.</param>
    public InvitationsController(DevHuntDbContext db, IEventBusService eventBus, ICacheService cache)
    {
        _db = db;
        _eventBus = eventBus;
        _cache = cache;
    }

    // ========================================================================
    // DTOs
    // ========================================================================

    /// <summary>Request to send an invitation or join request.</summary>
    /// <param name="ProjectId">Target project ID.</param>
    /// <param name="UserId">Target user ID (for invites, mutually exclusive with Username).</param>
    /// <param name="Username">Target user's email (for invites, mutually exclusive with UserId).</param>
    /// <param name="Role">Proposed role for the user (developer, designer, etc.).</param>
    /// <param name="Type">"invite" (owner invites user) or "request" (user requests to join).</param>
    /// <param name="Message">Optional message to include with invitation.</param>
    public record InviteRequest(Guid ProjectId, Guid? UserId, string? Username, string Role, string Type, string? Message);

    /// <summary>Request to respond to an invitation.</summary>
    /// <param name="InvitationId">Invitation to respond to.</param>
    /// <param name="Action">"accept" or "decline".</param>
    public record RespondRequest(Guid InvitationId, string Action);

    /// <summary>Invitation data transfer object.</summary>
    /// <param name="Id">Unique invitation identifier.</param>
    /// <param name="ProjectId">Target project.</param>
    /// <param name="InviteeId">User being invited (or requesting to join).</param>
    /// <param name="InviterId">User who created the invitation.</param>
    /// <param name="Type">"invite" or "request".</param>
    /// <param name="Role">Proposed team role.</param>
    /// <param name="Status">Current status: pending, accepted, declined, cancelled.</param>
    /// <param name="Message">Optional message.</param>
    /// <param name="CreatedAt">When invitation was sent.</param>
    /// <param name="RespondedAt">When invitation was accepted/declined (null if pending).</param>
    public record InvitationDto(
        Guid Id, Guid ProjectId, Guid InviteeId, Guid InviterId, string Type, string Role, string Status, string? Message, DateTime CreatedAt, DateTime? RespondedAt
    );

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Sends either a project invite or a join request, validating project access, target user lookup, duplicates, and role eligibility.
    /// </summary>
    /// <param name="req">Invitation or join request details.</param>
    /// <param name="ct">Cancellation token for database operations.</param>
    /// <returns>The created invitation, or a validation, authorization, not found, or conflict result.</returns>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] InviteRequest req, CancellationToken ct = default)
    {
        var initiator = GetRequiredUserId();
        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == req.ProjectId, ct);
        if (project == null) return NotFound("Project not found!");

        // BUGFIX: Allow owner OR team leaders to invite
        if (req.Type == InvitationTypeValues.Invite)
        {
            var isOwner = project.OwnerId == initiator;
            var isLeader = await _db.TeamMembers.AnyAsync(tm =>
                tm.ProjectId == req.ProjectId &&
                tm.UserId == initiator &&
                tm.Status == TeamMemberStatus.Active &&
                tm.IsLeader, ct);

            if (!isOwner && !isLeader)
                return StatusCode(403, "Only project owner or team leaders can send invitations");
        }

        // Determine target user based on invitation type
        Guid targetUserId;
        if (req.Type == InvitationTypeValues.Request)
        {
            // Request-to-join: always target project owner (who can approve/decline)
            if (project.OwnerId == initiator)
                return BadRequest("Project owner cannot request to join their own project");
            targetUserId = project.OwnerId;
        }
        else
        {
            // FEATURE: Support username-based invitations
            if (req.UserId.HasValue)
            {
                targetUserId = req.UserId.Value;
            }
            else if (!string.IsNullOrWhiteSpace(req.Username))
            {
                // Find user by email (username)
                var userByEmail = await _db.Users.FirstOrDefaultAsync(x => x.Email == req.Username, ct);
                if (userByEmail == null)
                    return NotFound($"User with email '{req.Username}' not found");
                targetUserId = userByEmail.Id;
            }
            else
            {
                return BadRequest("Either UserId or Username must be provided");
            }
        }

        var target = await _db.Users.FirstOrDefaultAsync(x => x.Id == targetUserId, ct);
        if (target == null) return NotFound("User not found!");

        // Check if joining user is already a team member
        var joiningUserId = req.Type == InvitationTypeValues.Request ? initiator : targetUserId;
        var alreadyMember = await _db.TeamMembers.AnyAsync(tm =>
            tm.ProjectId == req.ProjectId &&
            tm.UserId == joiningUserId &&
            tm.Status == TeamMemberStatus.Active, ct);
        if (alreadyMember)
            return Conflict("User is already a team member");

        // Already has active invitation/request
        Invitation? existing;
        if (req.Type == InvitationTypeValues.Request)
        {
            // Prevent duplicate requests from same user for same project
            existing = await _db.Invitations.FirstOrDefaultAsync(x =>
                x.ProjectId == req.ProjectId &&
                x.Type == InvitationTypeValues.Request &&
                x.InviterId == initiator &&
                x.Status == InvitationStatusValues.Pending, ct);
        }
        else
        {
            // Prevent multiple pending invites to the same user for the same project
            existing = await _db.Invitations.FirstOrDefaultAsync(x =>
                x.ProjectId == req.ProjectId &&
                x.InviteeId == targetUserId &&
                x.Status == InvitationStatusValues.Pending, ct);
        }
        if (existing != null) return Conflict("Already has active invite/request");

        // I-06: Validate role against project's RequiredRoles
        var validRoles = project.RequiredRoles ?? new List<string>();
        if (validRoles.Count > 0 && !validRoles.Contains(req.Role, StringComparer.OrdinalIgnoreCase))
            return BadRequest($"Role '{req.Role}' is not in the project's required roles");

        var inv = new Invitation {
            Id = Guid.NewGuid(),
            ProjectId = req.ProjectId,
            InviterId = initiator,
            InviteeId = targetUserId,
            Role = req.Role,
            Type = req.Type,
            Status = InvitationStatusValues.Pending,
            Message = req.Message,
            CreatedAt = DateTime.UtcNow
        };
        _db.Invitations.Add(inv);

        // Create or get direct chat between initiator and target
        var existingDirectChat = await _db.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c =>
                c.Type == ConversationType.Direct &&
                c.Participants.Any(p => p.UserId == initiator) &&
                c.Participants.Any(p => p.UserId == targetUserId), ct);

        Guid conversationId;
        if (existingDirectChat == null)
        {
            // Create new direct chat
            var directChat = new Conversation
            {
                Id = Guid.NewGuid(),
                Type = ConversationType.Direct,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Conversations.Add(directChat);

            // Add both participants
            _db.ConversationParticipants.AddRange(new[]
            {
                new ConversationParticipant
                {
                    Id = Guid.NewGuid(),
                    ConversationId = directChat.Id,
                    UserId = initiator,
                    JoinedAt = DateTime.UtcNow
                },
                new ConversationParticipant
                {
                    Id = Guid.NewGuid(),
                    ConversationId = directChat.Id,
                    UserId = targetUserId,
                    JoinedAt = DateTime.UtcNow
                }
            });

            conversationId = directChat.Id;
        }
        else
        {
            conversationId = existingDirectChat.Id;
        }

        // Send system message with invitation/request
        var systemContent = req.Type == InvitationTypeValues.Request
            ? $"Request to join project \"{project.Title}\" as {req.Role}\n\n{req.Message ?? ""}"
            : $"Invitation to project \"{project.Title}\" as {req.Role}\n\n{req.Message ?? ""}";
        var systemMessage = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = initiator,
            ConversationId = conversationId,
            MessageType = MessageType.Direct,
            Content = systemContent,
            CreatedAt = DateTime.UtcNow,
            IsEdited = false,
            IsDeleted = false,
            ReplyToId = null
        };
        _db.Messages.Add(systemMessage);

        // notify invitee/request target
        var notificationTitle = req.Type == InvitationTypeValues.Request ? "New request" : "New invitation";
        var notificationContent = req.Type == InvitationTypeValues.Request
            ? $"User requested to join the project for role: {req.Role}"
            : $"Project invites you for role: {req.Role}";
        _db.Notifications.Add(new Notification {
            Id = Guid.NewGuid(), UserId = targetUserId, Type = "invitation", Title = notificationTitle,
            Content = notificationContent, RelatedEntityType = "Project", RelatedEntityId = req.ProjectId,
            Priority = "medium", CreatedAt = DateTime.UtcNow
        });

        // Save all invitation changes before publishing the event to avoid ghost events.
        await _db.SaveChangesAsync(ct);
        await _eventBus.PublishAsync(DomainEvents.InvitationSent(inv.Id, req.ProjectId, targetUserId));
        return Ok(new InvitationDto(inv.Id, inv.ProjectId, inv.InviteeId, inv.InviterId, inv.Type, inv.Role, inv.Status, inv.Message, inv.CreatedAt, null));
    }

    /// <summary>
    /// Gets pending invitations or join requests addressed to the authenticated user with clamped pagination.
    /// </summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Requested page size, capped at 100.</param>
    /// <param name="ct">Cancellation token for database operations.</param>
    /// <returns>Pending incoming invitations plus pagination metadata.</returns>
    [HttpGet("incoming")]
    public async Task<IActionResult> Incoming([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        // Validate pagination
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var uid = GetRequiredUserId();
        var query = _db.Invitations
            .Where(x => x.InviteeId == uid && x.Status == InvitationStatusValues.Pending);

        var totalCount = await query.CountAsync(ct);
        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InvitationDto(x.Id, x.ProjectId, x.InviteeId, x.InviterId, x.Type, x.Role, x.Status, x.Message, x.CreatedAt, x.RespondedAt))
            .ToListAsync(ct);

        return Ok(new
        {
            Items = list,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            HasNext = page * pageSize < totalCount,
            HasPrevious = page > 1
        });
    }

    /// <summary>
    /// Gets pending invitations or join requests created by the authenticated user with clamped pagination.
    /// </summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Requested page size, capped at 100.</param>
    /// <param name="ct">Cancellation token for database operations.</param>
    /// <returns>Pending sent invitations plus pagination metadata.</returns>
    [HttpGet("sent")]
    public async Task<IActionResult> Sent([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        // Validate pagination
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var uid = GetRequiredUserId();
        var query = _db.Invitations
            .Where(x => x.InviterId == uid && x.Status == InvitationStatusValues.Pending);

        var totalCount = await query.CountAsync(ct);
        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InvitationDto(x.Id, x.ProjectId, x.InviteeId, x.InviterId, x.Type, x.Role, x.Status, x.Message, x.CreatedAt, x.RespondedAt))
            .ToListAsync(ct);

        return Ok(new
        {
            Items = list,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            HasNext = page * pageSize < totalCount,
            HasPrevious = page > 1
        });
    }

    /// <summary>
    /// Accepts or declines a pending invitation, enforcing different responders for join requests and direct invites.
    /// </summary>
    /// <param name="req">Invitation identifier and response action.</param>
    /// <param name="ct">Cancellation token for database operations.</param>
    /// <returns>The updated invitation, or not found, forbidden, conflict, or validation results for invalid branches.</returns>
    [HttpPost("respond")]
    public async Task<IActionResult> Respond([FromBody] RespondRequest req, CancellationToken ct = default)
    {
        var uid = GetRequiredUserId();
        var inv = await _db.Invitations.FirstOrDefaultAsync(x => x.Id == req.InvitationId, ct);
        if (inv == null) return NotFound();
        if (inv.Type == InvitationTypeValues.Request)
        {
            // For requests, project owner or team leaders can approve/decline
            var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == inv.ProjectId, ct);
            if (project == null) return NotFound("Project not found!");

            var isOwner = project.OwnerId == uid;
            var isLeader = await _db.TeamMembers.AnyAsync(tm =>
                tm.ProjectId == inv.ProjectId &&
                tm.UserId == uid &&
                tm.Status == TeamMemberStatus.Active &&
                tm.IsLeader, ct);
            if (!isOwner && !isLeader) return Forbid();
        }
        else
        {
            if (inv.InviteeId != uid) return Forbid();
        }
        if (inv.Status != InvitationStatusValues.Pending) return Conflict();
        if (req.Action == "accept") {
            inv.Status = InvitationStatusValues.Accepted;
            inv.RespondedAt = DateTime.UtcNow;
            var joiningUserId = inv.Type == InvitationTypeValues.Request ? inv.InviterId : uid;

            // I-01: Remove TOCTOU check — just add, rely on single SaveChanges + unique constraint
            _db.TeamMembers.Add(new TeamMember {
                Id = Guid.NewGuid(), ProjectId = inv.ProjectId, UserId = joiningUserId,
                Role = inv.Role, Status = TeamMemberStatus.Active, IsLeader = false, JoinedAt = DateTime.UtcNow
            });

            // Add user to project chat if it exists (only Add, no separate SaveChanges)
            var projectChat = await _db.Conversations.FirstOrDefaultAsync(
                c => c.Id == inv.ProjectId && c.Type == ConversationType.Group, ct);
            if (projectChat != null)
            {
                var alreadyInChat = await _db.ConversationParticipants.AnyAsync(
                    cp => cp.ConversationId == inv.ProjectId && cp.UserId == joiningUserId, ct);
                if (!alreadyInChat)
                {
                    _db.ConversationParticipants.Add(new ConversationParticipant
                    {
                        Id = Guid.NewGuid(),
                        ConversationId = inv.ProjectId,
                        UserId = joiningUserId,
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }

            // Invalidate project cache - important for team members list update
            var projectCacheKey = $"project:{inv.ProjectId}";
            await _cache.RemoveAsync(projectCacheKey);

            // notify requester/inviter about acceptance
            var acceptedTitle = inv.Type == InvitationTypeValues.Request ? "Request accepted" : "Invitation accepted";
            var acceptedContent = inv.Type == InvitationTypeValues.Request
                ? "Your request to join the project has been accepted."
                : "User accepted the project invitation.";
            _db.Notifications.Add(new Notification {
                Id = Guid.NewGuid(), UserId = inv.InviterId, Type = "invitation", Title = acceptedTitle,
                Content = acceptedContent, RelatedEntityType = "Project", RelatedEntityId = inv.ProjectId, Priority = "low", CreatedAt = DateTime.UtcNow
            });
        } else if (req.Action == "decline") {
            inv.Status = InvitationStatusValues.Declined;
            inv.RespondedAt = DateTime.UtcNow;
            var declinedTitle = inv.Type == InvitationTypeValues.Request ? "Request declined" : "Invitation declined";
            var declinedContent = inv.Type == InvitationTypeValues.Request
                ? "Your request to join the project has been declined."
                : "User declined the project invitation.";
            _db.Notifications.Add(new Notification {
                Id = Guid.NewGuid(), UserId = inv.InviterId, Type = "invitation", Title = declinedTitle,
                Content = declinedContent, RelatedEntityType = "Project", RelatedEntityId = inv.ProjectId, Priority = "low", CreatedAt = DateTime.UtcNow
            });
        } else {
            return BadRequest("invalid action");
        }

        // I-01/I-02: Single atomic SaveChanges — all mutations committed together
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Conflict("User is already a team member");
        }

        // Publish events only after a successful commit to avoid ghost events.
        if (req.Action == "accept")
        {
            var acceptSubject = inv.Type == InvitationTypeValues.Request ? inv.InviterId : uid;
            await _eventBus.PublishAsync(DomainEvents.InvitationAccepted(inv.Id, inv.ProjectId, acceptSubject));
        }
        else if (req.Action == "decline")
        {
            var declineSubject = inv.Type == InvitationTypeValues.Request ? inv.InviterId : uid;
            await _eventBus.PublishAsync(DomainEvents.InvitationDeclined(inv.Id, inv.ProjectId, declineSubject));
        }

        return Ok(new InvitationDto(inv.Id, inv.ProjectId, inv.InviteeId, inv.InviterId, inv.Type, inv.Role, inv.Status, inv.Message, inv.CreatedAt, inv.RespondedAt));
    }

    /// <summary>
    /// Cancels a pending invitation when requested by its sender and notifies the invitee.
    /// </summary>
    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> CancelInvitation(Guid invitationId, CancellationToken ct = default)
    {
        var uid = GetRequiredUserId();
        var inv = await _db.Invitations.FirstOrDefaultAsync(x => x.Id == invitationId, ct);
        if (inv == null) return NotFound();

        // Only sender can cancel invitation
        if (inv.InviterId != uid) return Forbid();

        // Cannot cancel already accepted/rejected invitation
        if (inv.Status != InvitationStatusValues.Pending) return Conflict("Cannot cancel non-pending invitation");

        inv.Status = InvitationStatusValues.Cancelled;

        // Notify recipient about cancellation
        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = inv.InviteeId,
            Type = "invitation",
            Title = "Invitation cancelled",
            Content = "Sender cancelled the project invitation.",
            RelatedEntityType = "Project",
            RelatedEntityId = inv.ProjectId,
            Priority = "low",
            CreatedAt = DateTime.UtcNow
        });

        // Save invitation changes before publishing the event to avoid ghost events.
        await _db.SaveChangesAsync(ct);
        await _eventBus.PublishAsync(DomainEvents.InvitationCancelled(invitationId, inv.ProjectId, uid));

        return Ok(new { Message = "Invitation cancelled" });
    }
}
