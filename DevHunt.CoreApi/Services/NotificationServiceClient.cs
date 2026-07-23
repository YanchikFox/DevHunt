using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// HTTP client for Notification Service microservice (Node.js).
/// Corresponds to architecture: Core API → Notification Service → delivery channels (email, push, WebSocket, webhooks).
/// </summary>
/// <remarks>
/// <para><strong>Notification Service Responsibilities</strong>:</para>
/// - Email delivery (SendGrid/SMTP)
/// - Push notifications (FCM/APNs for mobile apps)
/// - In-app notifications via WebSocket/SignalR
/// - Webhook notifications to Slack/Discord channels
///
/// <para><strong>MVP vs Production</strong>:</para>
/// For MVP, notifications can be handled directly by Core API (simpler deployment).
/// For production, external Notification Service provides:
/// - Better scalability (dedicated service for high-volume delivery)
/// - Retry logic and delivery tracking
/// - Template management
/// - Delivery rate limiting
///
/// <para><strong>Fallback Behavior</strong>:</para>
/// If service is disabled or unavailable, notifications are only saved to database (no real-time delivery).
/// </remarks>
public interface INotificationServiceClient
{
    /// <summary>
    /// Send notification to a single user.
    /// </summary>
    Task SendNotificationAsync(Guid userId, string type, string title, string content, string? relatedEntityType = null, Guid? relatedEntityId = null, string priority = "medium", CancellationToken ct = default);

    /// <summary>
    /// Send bulk notifications to multiple users (efficient batch delivery).
    /// </summary>
    Task SendBulkNotificationsAsync(List<Guid> userIds, string type, string title, string content, string priority = "medium", CancellationToken ct = default);

    /// <summary>
    /// Mark notification as read (sync with database).
    /// </summary>
    Task MarkNotificationAsReadAsync(Guid notificationId, CancellationToken ct = default);

    /// <summary>
    /// Mark all user's notifications as read (bulk operation).
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// HTTP client implementation for the Notification Service.
/// </summary>
public class NotificationServiceClient : INotificationServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationServiceClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly bool _isEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationServiceClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured when <c>NotificationService:BaseUrl</c> is set.</param>
    /// <param name="logger">Logger for remote delivery failures.</param>
    /// <param name="configuration">Reads <c>NotificationService:BaseUrl</c>.</param>
    public NotificationServiceClient(
        HttpClient httpClient,
        ILogger<NotificationServiceClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        // Configure the HTTP client only when the notification service URL is explicitly set.
        var serviceUrl = _configuration["NotificationService:BaseUrl"];
        _isEnabled = !string.IsNullOrEmpty(serviceUrl);

        if (_isEnabled)
        {
            _httpClient.BaseAddress = new Uri(serviceUrl!);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }
        else
        {
            _logger.LogWarning("Notification Service disabled (NotificationService:BaseUrl not configured). Notifications will be saved to DB only.");
        }
    }

    /// <inheritdoc />
    public async Task SendNotificationAsync(Guid userId, string type, string title, string content, string? relatedEntityType = null, Guid? relatedEntityId = null, string priority = "medium", CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Notification Service disabled, notification will be saved to DB only");
            // In this case, notification should be persisted to DB via NotificationsController
            return;
        }

        try
        {
            var request = new
            {
                UserId = userId,
                Type = type,
                Title = title,
                Content = content,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                Priority = priority
            };

            var response = await _httpClient.PostAsJsonAsync("/api/notifications", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Notification Service returned {StatusCode} for notification to user {UserId}",
                    response.StatusCode, userId);
            }
            else
            {
                _logger.LogDebug("Notification sent to user {UserId} via Notification Service", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId} via Notification Service", userId);
            // Graceful degradation - don't fail if Notification Service unavailable
        }
    }

    /// <inheritdoc />
    public async Task SendBulkNotificationsAsync(List<Guid> userIds, string type, string title, string content, string priority = "medium", CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Notification Service disabled, bulk notifications skipped");
            return;
        }

        try
        {
            var request = new
            {
                UserIds = userIds,
                Type = type,
                Title = title,
                Content = content,
                Priority = priority
            };

            var response = await _httpClient.PostAsJsonAsync("/api/notifications/bulk", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Notification Service returned {StatusCode} for bulk notifications", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bulk notifications via Notification Service");
        }
    }

    /// <inheritdoc />
    public async Task MarkNotificationAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            return;
        }

        try
        {
            var response = await _httpClient.PutAsync($"/api/notifications/{notificationId}/read", null, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Notification Service returned {StatusCode} for mark as read", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification as read in Notification Service");
        }
    }

    /// <inheritdoc />
    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            return;
        }

        try
        {
            var response = await _httpClient.PutAsync($"/api/notifications/user/{userId}/read-all", null, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Notification Service returned {StatusCode} for mark all as read", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications as read in Notification Service");
        }
    }
}

