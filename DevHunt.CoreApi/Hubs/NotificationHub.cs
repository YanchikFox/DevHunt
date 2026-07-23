using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using DevHunt.CoreApi.Services;

namespace DevHunt.CoreApi.Hubs;

/// <summary>
/// SignalR Hub for real-time in-app notifications.
/// Corresponds to architecture: in-app notifications via WebSocket.
/// </summary>
/// <remarks>
/// <para><strong>Purpose</strong>:</para>
/// Delivers instant notifications to connected users without polling.
/// Used for system notifications, mentions, project updates, etc.
///
/// <para><strong>SignalR Groups</strong>:</para>
/// - <b>user:{userId}</b> - Personal channel for user-specific notifications
///
/// <para><strong>Security</strong>:</para>
/// [Authorize] attribute requires JWT authentication.
/// Each user is automatically added to their personal notification group upon connection.
///
/// <para><strong>Client Integration</strong>:</para>
/// Frontend establishes SignalR connection on login and listens for "Notification" events.
/// Notifications are also persisted to database for retrieval when user is offline.
/// </remarks>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;
    private readonly IPresenceService _presenceService;

    public NotificationHub(ILogger<NotificationHub> logger, IPresenceService presenceService)
    {
        _logger = logger;
        _presenceService = presenceService;
    }

    public override async Task OnConnectedAsync()
    {
        // SECURITY: Verify user is authenticated
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthenticated connection attempt to NotificationHub from {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("Invalid user ID in token for NotificationHub connection {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        // Note: NotificationHub doesn't have DbContext access, so we rely on [Authorize] attribute
        // and JWT token validation. If you need to check user status, inject DbContext here.

        // Add user to their personal group for notifications
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId.Value}");
        await _presenceService.SetOnlineAsync(userId.Value, "notify");
        _logger.LogInformation("User {UserId} connected to NotificationHub with connection {ConnectionId}",
            userId.Value, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId.Value}");
            await _presenceService.SetOfflineAsync(userId.Value, "notify");
            _logger.LogInformation("User {UserId} disconnected from NotificationHub", userId.Value);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid? GetUserId()
    {
        var id = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var guid) ? guid : null;
    }
}

/// <summary>
/// Extension methods for sending notifications via SignalR.
/// </summary>
/// <remarks>
/// Provides convenient methods for sending notifications from controllers/services.
/// Automatically routes notifications to user-specific SignalR groups.
/// </remarks>
public static class NotificationHubExtensions
{
    /// <summary>
    /// Send notification to a specific user.
    /// </summary>
    /// <param name="hubContext">SignalR hub context</param>
    /// <param name="userId">User ID to send notification to</param>
    /// <param name="notification">Notification object (will be serialized to JSON)</param>
    public static async Task SendNotificationToUserAsync(
        this IHubContext<NotificationHub> hubContext,
        Guid userId,
        object notification)
    {
        await hubContext.Clients.Group($"user:{userId}").SendAsync("Notification", notification);
    }

    /// <summary>
    /// Send notification to multiple users (bulk delivery).
    /// </summary>
    /// <param name="hubContext">SignalR hub context</param>
    /// <param name="userIds">User IDs to send notification to</param>
    /// <param name="notification">Notification object (same for all users)</param>
    public static async Task SendNotificationToUsersAsync(
        this IHubContext<NotificationHub> hubContext,
        IEnumerable<Guid> userIds,
        object notification)
    {
        var tasks = userIds.Select(userId =>
            hubContext.Clients.Group($"user:{userId}").SendAsync("Notification", notification));
        await Task.WhenAll(tasks);
    }
}

