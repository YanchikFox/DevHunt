using DevHunt.CoreApi.Hubs;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Helper service for creating notifications and sending via SignalR.
/// Simplifies notification delivery from controllers.
/// </summary>
/// <remarks>
/// <para><strong>Production Architecture</strong>:</para>
/// In production, notifications should be sent via external Notification Service (Node.js microservice).
/// For MVP, direct SignalR delivery from Core API is acceptable.
///
/// <para><strong>Workflow</strong>:</para>
/// 1. Persist notification to database (Notification table)
/// 2. Send real-time notification via SignalR to connected clients
/// 3. (Optional) Queue for email/push delivery via Notification Service
///
/// <para><strong>Usage</strong>:</para>
/// Controllers call this service instead of manually creating Notification entities and sending SignalR messages.
/// This ensures consistent notification format and reduces code duplication.
/// </remarks>
public interface INotificationHelperService
{
    /// <summary>
    /// Create and send notification to a single user.
    /// </summary>
    /// <param name="userId">User ID to send notification to</param>
    /// <param name="type">Notification type (project_update, invitation, message, etc.)</param>
    /// <param name="title">Notification title (brief summary)</param>
    /// <param name="content">Notification content (detailed message, optional)</param>
    /// <param name="relatedEntityType">Type of related entity (Project, Task, etc.)</param>
    /// <param name="relatedEntityId">ID of related entity</param>
    /// <param name="priority">Priority level (low, medium, high, critical)</param>
    /// <param name="ct">Cancels notification persistence.</param>
    Task SendNotificationAsync(
        Guid userId,
        string type,
        string title,
        string? content = null,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        string priority = "medium", CancellationToken ct = default);

    /// <summary>
    /// Create and send notifications to multiple users (bulk operation).
    /// </summary>
    Task SendBulkNotificationsAsync(
        IEnumerable<Guid> userIds,
        string type,
        string title,
        string? content = null,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        string priority = "medium", CancellationToken ct = default);
}

/// <summary>
/// Default implementation of <see cref="INotificationHelperService"/>.
/// </summary>
public class NotificationHelperService : INotificationHelperService
{
    private readonly DevHuntDbContext _context;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly ILogger<NotificationHelperService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationHelperService"/> class.
    /// </summary>
    /// <param name="context">Database context for notification persistence.</param>
    /// <param name="notificationHub">SignalR hub used for live delivery.</param>
    /// <param name="logger">Logger for SignalR delivery failures.</param>
    public NotificationHelperService(
        DevHuntDbContext context,
        IHubContext<NotificationHub> notificationHub,
        ILogger<NotificationHelperService> logger)
    {
        _context = context;
        _notificationHub = notificationHub;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendNotificationAsync(
        Guid userId,
        string type,
        string title,
        string? content = null,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        string priority = "medium", CancellationToken ct = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Content = content,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            Priority = priority,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);

        // Send via SignalR in real-time
        var dto = MapToDto(notification);

        try
        {
            await _notificationHub.SendNotificationToUserAsync(userId, dto);
            _logger.LogDebug("Notification sent via SignalR to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification via SignalR to user {UserId}", userId);
            // Continue working even if SignalR unavailable - notification already in DB
        }
    }

    /// <summary>
    /// Persists one notification per user, then attempts real-time delivery for each recipient.
    /// </summary>
    /// <inheritdoc />
    public async Task SendBulkNotificationsAsync(
        IEnumerable<Guid> userIds,
        string type,
        string title,
        string? content = null,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        string priority = "medium", CancellationToken ct = default)
    {
        var notifications = userIds.Select(userId => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Content = content,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            Priority = priority,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync(ct);

        // Send via SignalR to all users
        foreach (var notification in notifications)
        {
            var dto = MapToDto(notification);

            try
            {
                await _notificationHub.SendNotificationToUserAsync(notification.UserId, dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification via SignalR to user {UserId}", notification.UserId);
            }
        }

        _logger.LogDebug("Bulk notifications sent to {Count} users", notifications.Count);
    }

    /// <summary>
    /// Maps a persisted notification entity to the DTO shape sent over SignalR.
    /// </summary>
    private static Controllers.NotificationsController.NotificationDto MapToDto(Notification n) =>
        new(n.Id, n.Type, n.Title, n.Content, n.RelatedEntityType, n.RelatedEntityId, n.Priority, n.CreatedAt, n.IsRead, n.ReadAt);
}

