using DevHunt.CoreApi.Hubs;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Notifications API for retrieving and managing user notifications.
/// </summary>
/// <remarks>
/// Routes: api/notifications/*
/// </remarks>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly IHubContext<NotificationHub> _notificationHub;

    /// <summary>
    /// Creates the notifications controller with notification storage and SignalR delivery services.
    /// </summary>
    /// <param name="db">Database context used to query and mutate notifications.</param>
    /// <param name="notificationHub">SignalR hub context used to push newly created notifications.</param>
    public NotificationsController(DevHuntDbContext db, IHubContext<NotificationHub> notificationHub)
    {
        _db = db;
        _notificationHub = notificationHub;
    }

    /// <summary>Notification item returned to clients.</summary>
    /// <param name="Id">Notification identifier.</param>
    /// <param name="Type">Notification type key.</param>
    /// <param name="Title">Notification title.</param>
    /// <param name="Content">Notification content/body.</param>
    /// <param name="RelatedEntityType">Related entity type (optional).</param>
    /// <param name="RelatedEntityId">Related entity identifier (optional).</param>
    /// <param name="Priority">Priority label.</param>
    /// <param name="CreatedAt">Creation timestamp.</param>
    /// <param name="IsRead">Whether the notification has been read.</param>
    /// <param name="ReadAt">Read timestamp (if read).</param>
    public record NotificationDto(Guid Id, string Type, string Title, string? Content, string? RelatedEntityType, Guid? RelatedEntityId, string? Priority, DateTime CreatedAt, bool IsRead, DateTime? ReadAt);

    /// <summary>Request to create a notification (admin helper).</summary>
    /// <param name="UserId">Target user identifier.</param>
    /// <param name="Type">Notification type.</param>
    /// <param name="Title">Notification title.</param>
    /// <param name="Content">Optional content/body.</param>
    /// <param name="RelatedEntityType">Optional related entity type.</param>
    /// <param name="RelatedEntityId">Optional related entity ID.</param>
    /// <param name="Priority">Priority label.</param>
    public record CreateNotificationRequest(Guid UserId, string Type, string Title, string? Content, string? RelatedEntityType, Guid? RelatedEntityId, string? Priority);

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Retrieves the current user's notifications with optional unread filtering and clamped pagination.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> MyNotifications(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // SECURITY: Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // SECURITY: Limit max page size

        var uid = GetRequiredUserId();
        var q = _db.Notifications.AsNoTracking().Where(n => n.UserId == uid);
        if (unreadOnly == true) q = q.Where(n => !n.IsRead);

        // Get total count for pagination
        var total = await q.CountAsync(ct);

        var list = await q.OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Content, n.RelatedEntityType, n.RelatedEntityId, n.Priority, n.CreatedAt, n.IsRead, n.ReadAt))
            .ToListAsync(ct);

        // Standardized response format with pagination
        return Ok(new
        {
            Data = list,
            Pagination = new
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                TotalPages = (int)Math.Ceiling((double)total / pageSize),
                HasNext = page * pageSize < total,
                HasPrevious = page > 1
            }
        });
    }

    /// <summary>
    /// Marks one of the current user's notifications as read.
    /// </summary>
    /// <param name="id">Notification identifier.</param>
    /// <param name="ct">Cancels the notification lookup and read update.</param>
    /// <returns>Returns not found when the notification does not belong to the current user; otherwise returns OK.</returns>
    [HttpPost("mark-read/{id:guid}")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var uid = GetRequiredUserId();
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct);
        if (n == null) return NotFound();
        n.IsRead = true; n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// <summary>
    /// Marks all unread notifications for the current user as read with a set-based database update.
    /// </summary>
    /// <returns>Returns OK after the update completes, including when no notifications were unread.</returns>
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var uid = GetRequiredUserId();
        var now = DateTime.UtcNow;

        // Mark unread notifications with one database update instead of loading rows into memory.
        await _db.Notifications
            .Where(x => x.UserId == uid && !x.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), ct);

        return Ok();
    }

    // Admin helper for creating in-app notifications.
    /// <summary>
    /// Creates a notification for a user when the caller is an admin or curator and pushes it through SignalR.
    /// </summary>
    /// <param name="req">Notification details.</param>
    /// <param name="ct">Cancels notification persistence.</param>
    /// <returns>Forbids non-admin/curator callers; otherwise returns OK after persistence and SignalR delivery.</returns>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateNotificationRequest req, CancellationToken ct = default)
    {
        if (!SecurityHelpers.IsAdminOrCurator(User)) return Forbid();

        var n = new Notification
        {
            Id = Guid.NewGuid(), UserId = req.UserId, Type = req.Type, Title = req.Title,
            Content = req.Content, RelatedEntityType = req.RelatedEntityType, RelatedEntityId = req.RelatedEntityId,
            Priority = req.Priority, CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync(ct);

        // Send the created notification via SignalR in real time.
        var notificationDto = new NotificationDto(
            n.Id, n.Type, n.Title, n.Content, n.RelatedEntityType, n.RelatedEntityId,
            n.Priority, n.CreatedAt, n.IsRead, n.ReadAt);
        await _notificationHub.SendNotificationToUserAsync(req.UserId, notificationDto);

        return Ok();
    }

    /// <summary>
    /// Returns the current user's unread notification count.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
    {
        var uid = GetRequiredUserId();
        var count = await _db.Notifications
            .CountAsync(n => n.UserId == uid && !n.IsRead, ct);
        return Ok(new { count });
    }

    /// <summary>
    /// Deletes one notification belonging to the current user.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var uid = GetRequiredUserId();
        var affected = await _db.Notifications
            .Where(n => n.Id == id && n.UserId == uid)
            .ExecuteDeleteAsync(ct);
        return affected > 0 ? Ok() : NotFound();
    }

    /// <summary>
    /// Deletes all read notifications belonging to the current user and returns the affected row count.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteAllRead(CancellationToken ct = default)
    {
        var uid = GetRequiredUserId();
        var affected = await _db.Notifications
            .Where(n => n.UserId == uid && n.IsRead)
            .ExecuteDeleteAsync(ct);
        return Ok(new { deleted = affected });
    }
}
