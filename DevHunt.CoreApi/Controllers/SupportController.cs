using System;
using System.ComponentModel.DataAnnotations;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing support tickets and helpdesk operations.
/// </summary>
/// <remarks>
/// This controller implements a complete ticket management system for user support:
///
/// Features:
/// - Multi-category ticket creation (question, bug, feature, billing, other)
/// - Message threading with internal/external visibility
/// - Ticket lifecycle management (open → in_progress → resolved → closed)
/// - Assignment to support staff
/// - Priority management (low, medium, high, urgent)
/// - Audit trail and change history
/// - Context linking (tickets can reference projects or users)
///
/// Access control:
/// - Users can create and manage their own tickets
/// - Admins/curators can view and respond to all tickets
/// - Internal messages are visible only to support staff
///
/// Notifications:
/// - Admins are notified of new tickets
/// - Users and admins are notified of replies
/// - Reopened tickets trigger notifications
///
/// Routes: api/support/*
/// </remarks>
[ApiController]
[Route("api/support")]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly DevHuntDbContext _context;
    private readonly IAuditService _auditService;
    private readonly DevHunt.CoreApi.Services.INotificationServiceClient _notificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupportController"/> class.
    /// </summary>
    /// <param name="context">Database context used for tickets, messages, histories, and support staff lookups.</param>
    /// <param name="auditService">Audit service used to record ticket lifecycle events.</param>
    /// <param name="notificationService">Notification client used to alert users and support staff about ticket changes.</param>
    public SupportController(DevHuntDbContext context, IAuditService auditService, DevHunt.CoreApi.Services.INotificationServiceClient notificationService)
    {
        _context = context;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Reads the authenticated user's identifier from claims and fails fast when authentication middleware did not provide it.
    /// </summary>
    /// <returns>The current user's ID.</returns>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Checks whether the current authenticated user has an active support staff role.
    /// </summary>
    /// <param name="ct">Cancellation token for the user-role lookup.</param>
    /// <returns><see langword="true"/> for active admin, curator, or superadmin users.</returns>
    private async Task<bool> IsAdminOrCuratorAsync(CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        return user == "admin" || user == "curator" || user == "superadmin";
    }

    /// <summary>Request to create a support ticket.</summary>
    /// <param name="Category">Ticket category.</param>
    /// <param name="Subject">Ticket subject.</param>
    /// <param name="Description">Ticket description.</param>
    /// <param name="RelatedProjectId">Optional related project ID.</param>
    /// <param name="RelatedUserId">Optional related user ID.</param>
    public record CreateTicketRequest(
        [Required, MaxLength(50)] string Category,
        [Required, MaxLength(200)] string Subject,
        [Required, MaxLength(5000)] string Description,
        Guid? RelatedProjectId,
        Guid? RelatedUserId);

    /// <summary>Ticket summary response.</summary>
    /// <param name="Id">Ticket identifier.</param>
    /// <param name="Category">Ticket category.</param>
    /// <param name="Subject">Ticket subject.</param>
    /// <param name="Status">Current status.</param>
    /// <param name="Priority">Priority level.</param>
    /// <param name="AssignedToUserId">Assigned support user ID.</param>
    /// <param name="CreatedAt">Creation timestamp.</param>
    /// <param name="ResolvedAt">Resolution timestamp.</param>
    /// <param name="MessageCount">Number of messages.</param>
    public record TicketResponse(
        Guid Id, string Category, string Subject, string Status, string Priority,
        Guid? AssignedToUserId, DateTime CreatedAt, DateTime? ResolvedAt, int MessageCount);

    /// <summary>Detailed ticket response with messages.</summary>
    /// <param name="Id">Ticket identifier.</param>
    /// <param name="Category">Ticket category.</param>
    /// <param name="Subject">Ticket subject.</param>
    /// <param name="Description">Ticket description.</param>
    /// <param name="Status">Current status.</param>
    /// <param name="Priority">Priority level.</param>
    /// <param name="AssignedToUserId">Assigned support user ID.</param>
    /// <param name="RelatedProjectId">Related project ID.</param>
    /// <param name="RelatedUserId">Related user ID.</param>
    /// <param name="CreatedAt">Creation timestamp.</param>
    /// <param name="UpdatedAt">Last update timestamp.</param>
    /// <param name="ResolvedAt">Resolution timestamp.</param>
    /// <param name="Messages">Ticket message thread.</param>
    public record TicketDetailResponse(
        Guid Id, string Category, string Subject, string Description, string Status, string Priority,
        Guid? AssignedToUserId, Guid? RelatedProjectId, Guid? RelatedUserId,
        DateTime CreatedAt, DateTime? UpdatedAt, DateTime? ResolvedAt,
        IEnumerable<MessageResponse> Messages);

    /// <summary>Support ticket message response.</summary>
    /// <param name="Id">Message identifier.</param>
    /// <param name="AuthorId">Author user ID.</param>
    /// <param name="AuthorName">Author display name.</param>
    /// <param name="Content">Message content.</param>
    /// <param name="IsInternal">Whether the message is internal.</param>
    /// <param name="CreatedAt">Creation timestamp.</param>
    /// <param name="EditedAt">Last edit timestamp.</param>
    public record MessageResponse(
        Guid Id, Guid AuthorId, string AuthorName, string Content, bool IsInternal,
        DateTime CreatedAt, DateTime? EditedAt);

    /// <summary>Request to add a message to a ticket.</summary>
    /// <param name="Content">Message content.</param>
    /// <param name="IsInternal">Whether the message is internal.</param>
    public record AddMessageRequest([Required, MaxLength(5000)] string Content, bool IsInternal = false);

    /// <summary>
    /// Creates a new support ticket.
    /// </summary>
    /// <remarks>
    /// This endpoint allows authenticated users to open support tickets for assistance.
    ///
    /// Category validation:
    /// - Allowed categories: question, bug, feature, billing, other
    /// - Categories are normalized to lowercase
    ///
    /// Initial state:
    /// - Status: open
    /// - Priority: medium (can be adjusted by support staff later)
    /// - AssignedToUserId: null (assignment happens later)
    ///
    /// Context linking:
    /// - RelatedProjectId: Links ticket to a specific project (optional)
    /// - RelatedUserId: Links ticket to a specific user (optional)
    ///
    /// Side effects:
    /// 1. Creates ticket record
    /// 2. Logs audit event
    /// 3. Sends notifications to all active admins/curators
    /// 4. High/urgent priority tickets trigger high-priority notifications
    /// </remarks>
    /// <param name="request">Ticket data including category, subject, and description.</param>
    /// <param name="ct">Cancellation token for ticket creation and staff lookup.</param>
    /// <returns>The created ticket summary.</returns>
    /// <response code="201">Ticket created successfully with location header.</response>
    /// <response code="400">Invalid category or validation failure.</response>
    /// <response code="401">User not authenticated.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost("tickets")]
    [ProducesResponseType(typeof(TicketResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var allowedCategories = new[] { "question", "bug", "feature", "billing", "other" };
        if (!allowedCategories.Contains(request.Category.ToLowerInvariant()))
        {
            return BadRequest($"Invalid category. Allowed: {string.Join(", ", allowedCategories)}");
        }

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Category = request.Category.ToLowerInvariant(),
            Subject = SecurityHelpers.SanitizeHtml(request.Subject.Trim()),
            Description = SecurityHelpers.SanitizeHtml(request.Description.Trim()),
            Status = "open",
            Priority = "medium",
            RelatedProjectId = request.RelatedProjectId,
            RelatedUserId = request.RelatedUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.SupportTickets.Add(ticket);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "SupportController.CreateTicket", "SupportTicket", ticket.Id,
            $"Created support ticket: {request.Category} - {request.Subject}");

        // Email notification: notify admins about new ticket
        var adminUsers = await _context.Users
            .Where(u => (u.Role == "admin" || u.Role == "curator") && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (adminUsers.Any())
        {
            await _notificationService.SendBulkNotificationsAsync(
                adminUsers,
                "support",
                $"New support ticket: {request.Subject}",
                $"User created support ticket in category '{request.Category}'. Priority: {ticket.Priority}.",
                ticket.Priority == "urgent" || ticket.Priority == "high" ? "high" : "medium"
            );
        }

        return CreatedAtAction(nameof(GetTicket), new { ticketId = ticket.Id },
            new TicketResponse(ticket.Id, ticket.Category, ticket.Subject, ticket.Status, ticket.Priority,
                ticket.AssignedToUserId, ticket.CreatedAt, ticket.ResolvedAt, 0));
    }

    /// <summary>
    /// Retrieves all support tickets created by the authenticated user.
    /// </summary>
    /// <remarks>
    /// Returns a paginated list of the user's support tickets.
    /// Results are ordered by creation date (newest first).
    ///
    /// Filtering:
    /// - By status: open, in_progress, waiting_user, resolved, closed
    /// - By category: question, bug, feature, billing, other
    ///
    /// Includes message count for each ticket to help users identify active conversations.
    /// </remarks>
    /// <param name="status">Optional status filter.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Items per page (default: 20).</param>
    /// <param name="ct">Cancellation token for ticket queries.</param>
    /// <returns>Paginated list of user's tickets.</returns>
    /// <response code="200">Returns the ticket list.</response>
    [HttpGet("tickets")]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var query = _context.SupportTickets
            .Where(t => t.UserId == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category == category);
        }

        var total = await query.CountAsync(ct);
        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketResponse(
                t.Id, t.Category, t.Subject, t.Status, t.Priority,
                t.AssignedToUserId, t.CreatedAt, t.ResolvedAt,
                t.Messages.Count))
            .ToListAsync(ct);

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Data = tickets });
    }

    /// <summary>
    /// Retrieves detailed information about a specific support ticket.
    /// </summary>
    /// <remarks>
    /// Returns complete ticket details including all messages.
    ///
    /// Access control:
    /// - Ticket creator: Can view all non-internal messages
    /// - Admins/curators: Can view all messages including internal notes
    /// - Others: Access denied (403)
    ///
    /// Message visibility:
    /// - External messages: Visible to everyone with access
    /// - Internal messages: Visible only to support staff (admins/curators)
    ///
    /// Messages are ordered chronologically for conversation threading.
    /// </remarks>
    /// <param name="ticketId">The unique identifier of the ticket.</param>
    /// <param name="ct">Cancellation token for ticket and message queries.</param>
    /// <returns>Complete ticket details with messages.</returns>
    /// <response code="200">Returns the ticket details.</response>
    /// <response code="403">If the user is not authorized to view this ticket.</response>
    /// <response code="404">If the ticket is not found.</response>
    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult> GetTicket(Guid ticketId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var isAdmin = await IsAdminOrCuratorAsync();

        var ticket = await _context.SupportTickets
            .Include(t => t.Messages).ThenInclude(m => m.Author)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket == null) return NotFound();

        // Access check: only author or admin
        if (ticket.UserId != userId && !isAdmin)
        {
            return Forbid();
        }

        var messages = ticket.Messages
            .Where(m => !m.IsInternal || isAdmin) // Hide internal messages from users
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MessageResponse(
                m.Id, m.AuthorId, m.Author != null ? m.Author.FullName ?? m.Author.Email : "Unknown",
                m.Content, m.IsInternal, m.CreatedAt, m.EditedAt))
            .ToList();

        var response = new TicketDetailResponse(
            ticket.Id, ticket.Category, ticket.Subject, ticket.Description,
            ticket.Status, ticket.Priority, ticket.AssignedToUserId,
            ticket.RelatedProjectId, ticket.RelatedUserId,
            ticket.CreatedAt, ticket.UpdatedAt, ticket.ResolvedAt, messages);

        return Ok(response);
    }

    /// <summary>
    /// Adds a message to a support ticket thread.
    /// </summary>
    /// <remarks>
    /// This endpoint handles the conversation flow between users and support staff.
    ///
    /// Access control:
    /// - Ticket creator and admins/curators can add messages
    /// - Only admins/curators can create internal messages (staff notes)
    ///
    /// State transitions triggered by messages:
    /// - If ticket is resolved/closed and admin replies: status → open (reopening)
    /// - If ticket is waiting_user and user replies: status → in_progress
    ///
    /// Side effects:
    /// 1. Creates message record
    /// 2. Updates ticket.UpdatedAt timestamp
    /// 3. May change ticket status (see above)
    /// 4. Sends notification to the other party:
    ///    - If admin sends: notifies ticket creator
    ///    - If user sends: notifies assigned admin (if assigned)
    /// 5. High/urgent priority tickets generate high-priority notifications
    /// </remarks>
    /// <param name="ticketId">The unique identifier of the ticket.</param>
    /// <param name="request">Message content and internal flag.</param>
    /// <param name="ct">Cancellation token for ticket, assignment, and message persistence work.</param>
    /// <returns>The created message.</returns>
    /// <response code="200">Returns the created message.</response>
    /// <response code="403">If the user cannot access this ticket or tries to create internal message without permission.</response>
    /// <response code="404">If the ticket is not found.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost("tickets/{ticketId:guid}/messages")]
    public async Task<IActionResult> AddMessage(Guid ticketId, [FromBody] AddMessageRequest request, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var ticket = await _context.SupportTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null) return NotFound();

        var isAdmin = await IsAdminOrCuratorAsync();

        // Access check
        if (ticket.UserId != userId && !isAdmin)
        {
            return Forbid();
        }

        // Only admins can create internal messages
        if (request.IsInternal && !isAdmin)
        {
            return Forbid("Only admins can create internal messages");
        }

        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = userId,
            Content = SecurityHelpers.SanitizeHtml(request.Content.Trim()),
            IsInternal = request.IsInternal,
            CreatedAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(message);

        // Update ticket status when replying
        if (ticket.Status == "resolved" || ticket.Status == "closed")
        {
            ticket.Status = "open"; // Reopen if admin replied
        }
        else if (ticket.Status == "waiting_user" && !isAdmin)
        {
            ticket.Status = "in_progress"; // User replied, ticket in progress
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        // Email notification: notify the other party about new message
        Guid? notifyUserId = null;
        if (isAdmin)
        {
            notifyUserId = ticket.UserId;
        }
        else
        {
            var ticketWithAssignment = await _context.SupportTickets
                .Include(t => t.AssignedToUser)
                .FirstOrDefaultAsync(t => t.Id == ticketId, ct);
            notifyUserId = ticketWithAssignment?.AssignedToUserId;
        }

        if (notifyUserId.HasValue && notifyUserId.Value != userId)
        {
            var ticketSubject = ticket.Subject;
            var authorName = User.FindFirstValue(ClaimTypes.Name) ?? "User";
            await _notificationService.SendNotificationAsync(
                notifyUserId.Value,
                "support",
                $"New message in support ticket: {ticketSubject}",
                $"{authorName} added a message to support ticket '{ticketSubject}'.",
                "SupportTicket",
                ticketId,
                ticket.Priority == "urgent" || ticket.Priority == "high" ? "high" : "medium"
            );
        }

        return Ok(new MessageResponse(message.Id, message.AuthorId,
            User.FindFirstValue(ClaimTypes.Name) ?? "User", message.Content,
            message.IsInternal, message.CreatedAt, null));
    }

    /// <summary>
    /// Closes a support ticket (user-initiated closure).
    /// </summary>
    /// <remarks>
    /// Allows ticket creators to close their own tickets when the issue is resolved.
    ///
    /// Access control:
    /// - Only the ticket creator can close their ticket
    /// - Admins should use admin endpoints for closure (not implemented here)
    ///
    /// Side effects:
    /// 1. Sets status to "closed"
    /// 2. Records ClosedAt timestamp
    /// 3. Updates UpdatedAt timestamp
    ///
    /// Closed tickets can be reopened if the issue resurfaces.
    /// </remarks>
    /// <param name="ticketId">The unique identifier of the ticket.</param>
    /// <param name="ct">Cancellation token for ticket update work.</param>
    /// <returns>Confirmation message with new status.</returns>
    /// <response code="200">Returns success message.</response>
    /// <response code="403">If the user is not the ticket creator.</response>
    /// <response code="404">If the ticket is not found.</response>
    [HttpPut("tickets/{ticketId:guid}/close")]
    public async Task<IActionResult> CloseTicket(Guid ticketId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var ticket = await _context.SupportTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null) return NotFound();

        if (ticket.UserId != userId)
        {
            return Forbid();
        }

        ticket.Status = "closed";
        ticket.ClosedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { Message = "Ticket closed", Status = ticket.Status });
    }

    /// <summary>
    /// Reopens a closed or resolved support ticket.
    /// </summary>
    /// <remarks>
    /// Allows ticket creators to reopen tickets if the issue persists or resurfaces.
    ///
    /// Access control:
    /// - Only the ticket creator can reopen their own tickets
    ///
    /// Validation:
    /// - Ticket must be in "closed" or "resolved" status
    /// - Cannot reopen tickets in other states
    ///
    /// Side effects:
    /// 1. Resets status to "open"
    /// 2. Clears ClosedAt and ResolvedAt timestamps
    /// 3. Updates UpdatedAt timestamp
    /// 4. Creates change history record for audit trail
    /// 5. Logs audit event
    /// 6. Sends notifications:
    ///    - If ticket is assigned: notifies assigned admin
    ///    - If unassigned: notifies all active admins/curators
    /// 7. High/urgent priority tickets generate high-priority notifications
    ///
    /// Optional reason field helps support staff understand why the ticket was reopened.
    /// </remarks>
    /// <param name="ticketId">The unique identifier of the ticket.</param>
    /// <param name="request">Optional reason for reopening.</param>
    /// <param name="ct">Cancellation token for ticket, history, and notification lookups.</param>
    /// <returns>Confirmation message with new status.</returns>
    /// <response code="200">Returns success message.</response>
    /// <response code="400">If the ticket is not closed or resolved.</response>
    /// <response code="403">If the user is not the ticket creator.</response>
    /// <response code="404">If the ticket is not found.</response>
    [HttpPost("tickets/{ticketId:guid}/reopen")]
    public async Task<IActionResult> ReopenTicket(Guid ticketId, [FromBody] ReopenTicketRequest? request = null, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var ticket = await _context.SupportTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null) return NotFound();

        if (ticket.UserId != userId)
        {
            return Forbid();
        }

        if (ticket.Status != "closed" && ticket.Status != "resolved")
        {
            return BadRequest("Ticket is not closed or resolved");
        }

        // Record the change history
        _context.TicketHistories.Add(new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ChangedByUserId = userId,
            ChangeType = "reopen",
            OldValue = ticket.Status,
            NewValue = "open",
            Reason = request?.Reason ?? "Ticket reopened by user",
            CreatedAt = DateTime.UtcNow
        });

        ticket.Status = "open";
        ticket.ClosedAt = null;
        ticket.ResolvedAt = null;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        // Notify admins about ticket being reopened
        if (ticket.AssignedToUserId.HasValue)
        {
            await _notificationService.SendNotificationAsync(
                ticket.AssignedToUserId.Value,
                "support",
                $"Support ticket '{ticket.Subject}' reopened",
                $"User reopened support ticket '{ticket.Subject}'. Response required.",
                "SupportTicket",
                ticketId,
                ticket.Priority == "urgent" || ticket.Priority == "high" ? "high" : "medium"
            );
        }
        else
        {
            // Notify all admins if ticket is unassigned
            var adminUsers = await _context.Users
                .Where(u => (u.Role == "admin" || u.Role == "curator") && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);

            if (adminUsers.Any())
            {
                await _notificationService.SendBulkNotificationsAsync(
                    adminUsers,
                    "support",
                    $"Support ticket '{ticket.Subject}' reopened",
                    $"User reopened support ticket '{ticket.Subject}'. Assignment and response required.",
                    "medium"
                );
            }
        }

        await _auditService.LogActionAsync(userId, "SupportController.ReopenTicket", "SupportTicket", ticketId,
            $"Reopened ticket: {request?.Reason ?? "No reason provided"}");

        return Ok(new { Message = "Ticket reopened", Status = ticket.Status });
    }

    /// <summary>Request to reopen a resolved or closed ticket.</summary>
    /// <param name="Reason">Optional reopen reason.</param>
    public record ReopenTicketRequest(string? Reason);

    /// <summary>
    /// Retrieves the complete change history for a support ticket.
    /// </summary>
    /// <remarks>
    /// Returns an audit trail of all modifications to the ticket, useful for:
    /// - Understanding ticket lifecycle
    /// - Debugging status transitions
    /// - Accountability and transparency
    ///
    /// Access control:
    /// - Ticket creator and admins/curators can view history
    /// - Others receive 403 Forbidden
    ///
    /// History includes:
    /// - Change type (e.g., "reopen", "assign", "priority_change", "status_change")
    /// - Old and new values
    /// - Reason/comment for the change
    /// - User who made the change
    /// - Timestamp
    ///
    /// Results are ordered by timestamp (newest first).
    /// </remarks>
    /// <param name="ticketId">The unique identifier of the ticket.</param>
    /// <param name="ct">Cancellation token for ticket and history queries.</param>
    /// <returns>Change history ordered newest first.</returns>
    /// <response code="200">Returns the history records.</response>
    /// <response code="403">If the user is not authorized to view this ticket's history.</response>
    /// <response code="404">If the ticket is not found.</response>
    [HttpGet("tickets/{ticketId:guid}/history")]
    public async Task<IActionResult> GetTicketHistory(Guid ticketId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var ticket = await _context.SupportTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null) return NotFound();

        var isAdmin = await IsAdminOrCuratorAsync();

        // Access check: only author or admin
        if (ticket.UserId != userId && !isAdmin)
        {
            return Forbid();
        }

        var history = await _context.TicketHistories
            .Include(h => h.ChangedByUser)
            .Where(h => h.TicketId == ticketId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new
            {
                h.Id,
                h.ChangeType,
                h.OldValue,
                h.NewValue,
                h.Reason,
                ChangedByUserId = h.ChangedByUserId,
                ChangedByUserEmail = h.ChangedByUser != null ? h.ChangedByUser.Email : "Unknown",
                h.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { TicketId = ticketId, History = history });
    }
}

