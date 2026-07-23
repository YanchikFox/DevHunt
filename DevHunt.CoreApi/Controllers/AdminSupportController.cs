using DevHunt.Infrastructure;
using DevHunt.CoreApi.Models;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevHunt.CoreApi.Security;
using System.ComponentModel.DataAnnotations;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Badges;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for Admin Support Ticket Management.
/// Separated from AdminController to reduce complexity.
/// Routes: api/admin/support/*
/// </summary>
[ApiController]
[Route("api/admin/support")]
[Authorize]
public class AdminSupportController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly IAuditService _auditService;
    private readonly INotificationServiceClient _notificationService;
    private readonly ILogger<AdminSupportController> _logger;

    /// <summary>
    /// Creates the admin support controller with persistence, audit, notification, and logging services.
    /// </summary>
    /// <param name="db">Database context used for support ticket workflow operations.</param>
    /// <param name="auditService">Audit service used to record ticket assignments, resolutions, and priority changes.</param>
    /// <param name="notificationService">Notification client used to notify ticket owners and admins.</param>
    /// <param name="logger">Logger for support workflow diagnostics.</param>
    public AdminSupportController(
        DevHuntDbContext db,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        ILogger<AdminSupportController> logger)
    {
        _db = db;
        _auditService = auditService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Attempts to read the authenticated user identifier from the current claims principal.
    /// </summary>
    private Guid? TryGetUserId()
    {
        return SecurityHelpers.GetUserId(User);
    }

    /// <summary>
    /// Checks the database for an active admin, curator, or superadmin matching the current claims principal.
    /// </summary>
    private async Task<bool> IsAdminOrCuratorAsync(CancellationToken ct = default)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue) return false;

        var role = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId.Value && u.IsActive)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        return role == UserRoles.Admin || role == UserRoles.Curator || role == UserRoles.SuperAdmin;
    }

    /// <summary>
    /// Loads a ticket assignee and verifies that the target user is an admin or curator.
    /// </summary>
    /// <returns>The user when assignable; otherwise <see langword="null"/>.</returns>
    private async Task<User?> GetAdminUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) return null;
        if (!user.IsAdminOrCurator()) return null;
        return user;
    }

    /// <summary>
    /// Adds a ticket history entry attributed to the current admin user.
    /// </summary>
    private void AddTicketHistory(Guid ticketId, TicketHistoryEntry entry)
    {
        _db.TicketHistories.Add(new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ChangedByUserId = GetRequiredUserId(),
            ChangeType = entry.Type,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            Reason = entry.Reason,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Notification payload sent for support ticket workflow updates.</summary>
    private record SupportNotification(Guid UserId, string Subject, string Message, Guid TicketId, string Priority = "medium");

    /// <summary>
    /// Sends an in-app support notification for a ticket.
    /// </summary>
    private async Task SendSupportNotificationAsync(SupportNotification notification)
    {
        await _notificationService.SendNotificationAsync(
            notification.UserId,
            "support",
            notification.Subject,
            notification.Message,
            "SupportTicket",
            notification.TicketId,
            notification.Priority);
    }

    /// <summary>Request to assign a support ticket to an admin or curator.</summary>
    /// <param name="TicketId">Ticket to assign.</param>
    /// <param name="AdminUserId">Admin or curator who should receive the ticket.</param>
    public record AssignTicketRequest(Guid TicketId, Guid AdminUserId);

    /// <summary>Request to resolve a support ticket with a public resolution message.</summary>
    /// <param name="TicketId">Ticket to resolve.</param>
    /// <param name="Resolution">Resolution text added as a ticket message.</param>
    public record ResolveTicketRequest(Guid TicketId, string Resolution);

    /// <summary>Request to reassign a ticket to another admin or curator.</summary>
    /// <param name="NewAdminUserId">New admin or curator assigned to the ticket.</param>
    /// <param name="Reason">Optional reason stored in ticket history.</param>
    public record ReassignTicketRequest(Guid NewAdminUserId, string? Reason);

    /// <summary>Request to update ticket priority and record an optional reason.</summary>
    /// <param name="Priority">New priority; must be low, medium, high, or urgent.</param>
    /// <param name="Reason">Optional reason stored in ticket history.</param>
    public record UpdatePriorityRequest([Required, MaxLength(50)] string Priority, string? Reason);

    /// <summary>Request to escalate a support ticket to urgent priority.</summary>
    /// <param name="Reason">Optional escalation reason stored in ticket history.</param>
    public record EscalateTicketRequest(string? Reason);

    /// <summary>Filter and pagination values for support ticket listing.</summary>
    /// <param name="Status">Optional exact ticket status.</param>
    /// <param name="Priority">Optional exact priority.</param>
    /// <param name="Category">Optional exact category.</param>
    /// <param name="AssignedToUserId">Optional assignee filter.</param>
    /// <param name="Search">Optional text search over subject, description, or user email.</param>
    /// <param name="DateFrom">Optional earliest ticket creation time.</param>
    /// <param name="DateTo">Optional latest ticket creation time.</param>
    /// <param name="Page">Page number to return.</param>
    /// <param name="PageSize">Number of tickets per page.</param>
    public record TicketFilterRequest(
        string? Status = null,
        string? Priority = null,
        string? Category = null,
        Guid? AssignedToUserId = null,
        string? Search = null,
        DateTime? DateFrom = null,
        DateTime? DateTo = null,
        int Page = 1,
        int PageSize = 21); // Default pageSize

    /// <summary>Internal ticket history change to persist with the current admin as actor.</summary>
    /// <param name="Type">History change type, such as assignment, status, priority, or escalate.</param>
    /// <param name="OldValue">Previous value when known.</param>
    /// <param name="NewValue">New value when known.</param>
    /// <param name="Reason">Optional reason supplied by the admin.</param>
    private record TicketHistoryEntry(string Type, string? OldValue = null, string? NewValue = null, string? Reason = null);

    /// <summary>
    /// Lists support tickets for admins and curators with filters, priority sorting, and pagination.
    /// </summary>
    [HttpGet("tickets")]
    public async Task<IActionResult> GetAllSupportTickets([FromQuery] TicketFilterRequest filter, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var query = _db.SupportTickets
            .Include(t => t.User)
            .Include(t => t.AssignedToUser)
            .AsQueryable();

        query = ApplyTicketFilters(query, filter);

        var total = await query.CountAsync(ct);

        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;
        var page = filter.Page > 0 ? filter.Page : 1;

        var tickets = await ApplyTicketSorting(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Category,
                t.Subject,
                t.Status,
                t.Priority,
                UserId = t.UserId,
                UserEmail = t.User != null ? t.User.Email : "Unknown",
                AssignedToUserId = t.AssignedToUserId,
                AssignedToName = t.AssignedToUser != null ? (t.AssignedToUser.FullName ?? t.AssignedToUser.Email) : null,
                t.CreatedAt,
                t.UpdatedAt,
                t.ResolvedAt,
                MessageCount = t.Messages.Count
            })
            .ToListAsync(ct);

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Data = tickets });
    }

    /// <summary>
    /// Assigns a ticket to an admin or curator, moves open tickets to in-progress, records history, notifies the owner, and audits the action.
    /// </summary>
    [HttpPost("tickets/assign")]
    public async Task<IActionResult> AssignTicket([FromBody] AssignTicketRequest request, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var ticket = await _db.SupportTickets.FindAsync(new object[] { request.TicketId }, ct);
        if (ticket == null) return NotFound("Ticket not found");

        var admin = await GetAdminUserAsync(request.AdminUserId);
        if (admin == null) return BadRequest("User is not an admin or curator");

        var adminUserId = GetRequiredUserId();
        var oldStatus = ticket.Status;

        // Record history changes
        AddTicketHistory(ticket.Id, new TicketHistoryEntry("assignment", ticket.AssignedToUserId?.ToString(), request.AdminUserId.ToString()));
        if (oldStatus == "open")
        {
            AddTicketHistory(ticket.Id, new TicketHistoryEntry("status", oldStatus, "in_progress", "Ticket assigned to admin"));
        }

        ticket.AssignedToUserId = request.AdminUserId;
        ticket.Status = "in_progress";
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Notify user
        var priority = ticket.Priority == "urgent" || ticket.Priority == "high" ? "high" : "medium";
        await SendSupportNotificationAsync(new SupportNotification(
            ticket.UserId,
            $"Support ticket '{ticket.Subject}' assigned to administrator",
            $"Your support ticket '{ticket.Subject}' has been assigned to administrator. Await response.",
            request.TicketId,
            priority));

        await _auditService.LogActionAsync(adminUserId, "AdminSupportController.AssignTicket", "SupportTicket", request.TicketId,
            $"Assigned ticket to {admin.Email}");

        return Ok(new { Message = "Ticket assigned", AssignedToUserId = request.AdminUserId });
    }

    /// <summary>
    /// Resolves a support ticket, records history, appends the resolution as a message, audits the action, and triggers achievements.
    /// </summary>
    [HttpPost("tickets/resolve")]
    public async Task<IActionResult> ResolveTicket([FromBody] ResolveTicketRequest request, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var ticket = await _db.SupportTickets.FindAsync(new object[] { request.TicketId }, ct);
        if (ticket == null) return NotFound("Ticket not found");

        var adminUserId = GetRequiredUserId();
        var oldStatus = ticket.Status;

        // Record history change
        AddTicketHistory(request.TicketId, new TicketHistoryEntry("status", oldStatus, "resolved", request.Resolution));

        ticket.Status = "resolved";
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Add message with resolution
        var resolutionMessage = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = request.TicketId,
            AuthorId = adminUserId,
            Content = request.Resolution,
            IsInternal = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.TicketMessages.Add(resolutionMessage);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(adminUserId, "AdminSupportController.ResolveTicket", "SupportTicket", request.TicketId,
            $"Resolved ticket: {request.Resolution}");

        await HttpContext.RequestServices.TriggerAchievementCheckAsync(adminUserId, AchievementTrigger.TicketResolved);

        return Ok(new { Message = "Ticket resolved", Status = ticket.Status });
    }

    /// <summary>
    /// Reassigns a ticket to another admin or curator, records history, and notifies the new and previous assignees.
    /// </summary>
    [HttpPost("tickets/{ticketId:guid}/reassign")]
    public async Task<IActionResult> ReassignTicket(Guid ticketId, [FromBody] ReassignTicketRequest request, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var ticket = await _db.SupportTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null) return NotFound("Ticket not found");

        var newAdmin = await GetAdminUserAsync(request.NewAdminUserId);
        if (newAdmin == null) return BadRequest("User is not an admin or curator");

        var adminUserId = GetRequiredUserId();
        var oldAdminId = ticket.AssignedToUserId;

        // Record history change
        AddTicketHistory(ticketId, new TicketHistoryEntry("assignment", oldAdminId?.ToString(), request.NewAdminUserId.ToString(), request.Reason));

        ticket.AssignedToUserId = request.NewAdminUserId;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Notifications
        await _notificationService.SendNotificationAsync(
            request.NewAdminUserId,
            "support",
            $"Support ticket '{ticket.Subject}' assigned to you",
            $"Support ticket '{ticket.Subject}' transferred to you from another administrator.",
            "SupportTicket",
            ticketId,
            ticket.Priority == "urgent" || ticket.Priority == "high" ? "high" : "medium"
        );

        if (oldAdminId.HasValue && oldAdminId.Value != request.NewAdminUserId)
        {
            await _notificationService.SendNotificationAsync(
                oldAdminId.Value,
                "support",
                $"Support ticket '{ticket.Subject}' transferred to another administrator",
                $"Support ticket '{ticket.Subject}' has been transferred to another administrator.",
                "SupportTicket",
                ticketId,
                "medium"
            );
        }

        await _auditService.LogActionAsync(adminUserId, "AdminSupportController.ReassignTicket", "SupportTicket", ticketId,
            $"Reassigned ticket from {oldAdminId} to {newAdmin.Email}");

        return Ok(new { Message = "Ticket reassigned", AssignedToUserId = request.NewAdminUserId });
    }

    /// <summary>
    /// Changes ticket priority after validating allowed values and notifies the owner when priority is raised to high or urgent.
    /// </summary>
    [HttpPut("tickets/{ticketId:guid}/priority")]
    public async Task<IActionResult> UpdateTicketPriority(Guid ticketId, [FromBody] UpdatePriorityRequest request, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var allowedPriorities = new[] { "low", "medium", "high", "urgent" };
        if (!allowedPriorities.Contains(request.Priority.ToLowerInvariant()))
        {
            return BadRequest($"Invalid priority. Allowed: {string.Join(", ", allowedPriorities)}");
        }

        var ticket = await _db.SupportTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null) return NotFound("Ticket not found");

        var adminUserId = GetRequiredUserId();
        var oldPriority = ticket.Priority;
        var newPriority = request.Priority.ToLowerInvariant();

        // Record history change
        AddTicketHistory(ticketId, new TicketHistoryEntry("priority", oldPriority, newPriority, request.Reason));

        ticket.Priority = newPriority;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Notify user if priority increased
        bool wasLowOrMedium = oldPriority == "low" || oldPriority == "medium";
        bool isHighOrUrgent = newPriority == "high" || newPriority == "urgent";

        if (wasLowOrMedium && isHighOrUrgent)
        {
            await _notificationService.SendNotificationAsync(
                ticket.UserId,
                "support",
                $"Support ticket '{ticket.Subject}' priority raised",
                $"Priority of your support ticket '{ticket.Subject}' has been raised to '{request.Priority}'.",
                "SupportTicket",
                ticketId,
                "high"
            );
        }

        await _auditService.LogActionAsync(adminUserId, "AdminSupportController.UpdateTicketPriority", "SupportTicket", ticketId,
            $"Changed priority from {oldPriority} to {request.Priority}");

        return Ok(new { Message = "Priority updated", Priority = ticket.Priority });
    }

    /// <summary>
    /// Escalates a ticket to urgent priority, notifies active admins and the ticket owner, and audits the action.
    /// </summary>
    [HttpPost("tickets/{ticketId:guid}/escalate")]
    public async Task<IActionResult> EscalateTicket(Guid ticketId, [FromBody] EscalateTicketRequest? request = null, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var ticket = await _db.SupportTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null) return NotFound("Ticket not found");

        var adminUserId = GetRequiredUserId();
        var oldPriority = ticket.Priority;
        var newPriority = "urgent";

        // Record history change
        AddTicketHistory(ticketId, new TicketHistoryEntry("escalate", oldPriority, newPriority, request?.Reason ?? "Ticket escalated by admin"));

        ticket.Priority = newPriority;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Notify all admins about escalation
        var adminUsers = await _db.Users
            .Where(u => (u.Role == UserRoles.Admin || u.Role == UserRoles.Curator || u.Role == UserRoles.SuperAdmin) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        await _notificationService.SendBulkNotificationsAsync(
            adminUsers,
            "support",
            $"Support ticket '{ticket.Subject}' escalated",
            $"Support ticket '{ticket.Subject}' escalated to urgent. Immediate attention required.",
            "high"
        );

        await _notificationService.SendNotificationAsync(
            ticket.UserId,
            "support",
            $"Support ticket '{ticket.Subject}' escalated",
            $"Your support ticket '{ticket.Subject}' has been escalated to maximum priority. Administrators will handle it first.",
            "SupportTicket",
            ticketId,
            "high"
        );

        await _auditService.LogActionAsync(adminUserId, "AdminSupportController.EscalateTicket", "SupportTicket", ticketId,
            "Escalated ticket to urgent");

        return Ok(new { Message = "Ticket escalated", Priority = ticket.Priority });
    }

    /// <summary>
    /// Applies status, priority, category, assignee, search, and date filters to support ticket queries.
    /// </summary>
    private IQueryable<SupportTicket> ApplyTicketFilters(IQueryable<SupportTicket> query, TicketFilterRequest filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(t => t.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.Priority))
            query = query.Where(t => t.Priority == filter.Priority);

        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(t => t.Category == filter.Category);

        if (filter.AssignedToUserId.HasValue)
            query = query.Where(t => t.AssignedToUserId == filter.AssignedToUserId.Value);

        query = ApplySearchFilter(query, filter.Search);

        if (filter.DateFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(t => t.CreatedAt <= filter.DateTo.Value);

        return query;
    }

    /// <summary>
    /// Applies case-insensitive subject, description, and requester-email search to ticket queries.
    /// </summary>
    private IQueryable<SupportTicket> ApplySearchFilter(IQueryable<SupportTicket> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;

        var searchLower = search.ToLowerInvariant();
        return query.Where(t =>
            t.Subject.ToLowerInvariant().Contains(searchLower) ||
            t.Description.ToLowerInvariant().Contains(searchLower) ||
            (t.User != null && t.User.Email.ToLowerInvariant().Contains(searchLower)));
    }

    /// <summary>
    /// Sorts support tickets by priority weight first and creation time second.
    /// </summary>
    private IQueryable<SupportTicket> ApplyTicketSorting(IQueryable<SupportTicket> query)
    {
        return query
            .OrderByDescending(t =>
                t.Priority == "urgent" ? 4 :
                t.Priority == "high" ? 3 :
                t.Priority == "medium" ? 2 : 1
            )
            .ThenByDescending(t => t.CreatedAt);
    }

    /// <summary>
    /// Lists all support tickets for admins and curators using the shared filters and priority sorting.
    /// </summary>
    [HttpGet("tickets/all")]
    public async Task<IActionResult> GetAllTickets([FromQuery] TicketFilterRequest filter, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var query = _db.SupportTickets
            .Include(t => t.User)
            .Include(t => t.AssignedToUser)
            .AsQueryable();

        query = ApplyTicketFilters(query, filter);

        var total = await query.CountAsync(ct);

        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;
        var page = filter.Page > 0 ? filter.Page : 1;

        var tickets = await ApplyTicketSorting(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Category,
                t.Subject,
                t.Status,
                t.Priority,
                UserId = t.UserId,
                UserEmail = t.User != null ? t.User.Email : "Unknown",
                AssignedToUserId = t.AssignedToUserId,
                AssignedToUserEmail = t.AssignedToUser != null ? t.AssignedToUser.Email : (string?)null,
                MessageCount = t.Messages.Count,
                t.CreatedAt,
                t.UpdatedAt,
                t.ResolvedAt,
                t.ClosedAt
            })
            .ToListAsync(ct);

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Data = tickets });
    }

    /// <summary>
    /// Returns support ticket counts by status, priority, category, assignment, and average resolution time.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetSupportStats(CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var stats = new
        {
            TotalTickets = await _db.SupportTickets.CountAsync(ct),
            OpenTickets = await _db.SupportTickets.CountAsync(t => t.Status == "open"),
            InProgressTickets = await _db.SupportTickets.CountAsync(t => t.Status == "in_progress"),
            WaitingUserTickets = await _db.SupportTickets.CountAsync(t => t.Status == "waiting_user"),
            ResolvedTickets = await _db.SupportTickets.CountAsync(t => t.Status == "resolved"),
            ClosedTickets = await _db.SupportTickets.CountAsync(t => t.Status == "closed"),

            ByPriority = new
            {
                Urgent = await _db.SupportTickets.CountAsync(t => t.Priority == "urgent"),
                High = await _db.SupportTickets.CountAsync(t => t.Priority == "high"),
                Medium = await _db.SupportTickets.CountAsync(t => t.Priority == "medium"),
                Low = await _db.SupportTickets.CountAsync(t => t.Priority == "low")
            },

            ByCategory = new
            {
                Question = await _db.SupportTickets.CountAsync(t => t.Category == "question"),
                Bug = await _db.SupportTickets.CountAsync(t => t.Category == "bug"),
                Feature = await _db.SupportTickets.CountAsync(t => t.Category == "feature"),
                Billing = await _db.SupportTickets.CountAsync(t => t.Category == "billing"),
                Other = await _db.SupportTickets.CountAsync(t => t.Category == "other")
            },

            UnassignedTickets = await _db.SupportTickets.CountAsync(t => t.AssignedToUserId == null && t.Status != "closed" && t.Status != "resolved"),

            AvgResolutionTimeHours = await _db.SupportTickets
                .Where(t => t.ResolvedAt != null)
                .Select(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalHours)
                .DefaultIfEmpty(0)
                .AverageAsync(ct)
        };

        return Ok(stats);
    }
}
