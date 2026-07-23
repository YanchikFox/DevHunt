using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Domain event payload published to the event bus.
/// </summary>
/// <param name="EventType">Event name, e.g. <c>created</c> or <c>member.joined</c>.</param>
/// <param name="EntityType">Aggregate name used in the routing key, e.g. <c>Project</c>.</param>
/// <param name="EntityId">Primary entity identifier, if applicable.</param>
/// <param name="Data">Optional JSON-serializable payload with event-specific fields.</param>
/// <param name="OccurredAt">UTC timestamp; defaults to <see cref="DateTime.UtcNow"/> when omitted.</param>
public record DomainEvent(string EventType, string EntityType, Guid? EntityId, object? Data, DateTime OccurredAt = default);

/// <summary>
/// Abstraction for publishing domain events to a message broker.
/// Production traffic is wrapped by <see cref="OutboxEventBusDecorator"/> for at-least-once delivery.
/// </summary>
/// <remarks>
/// <para><strong>Architecture</strong>:</para>
/// Uses <see cref="IEventBusService"/> for broker abstraction — can be replaced with Kafka, Azure Service Bus, AWS SQS, etc.
/// RabbitMQ is preferred for initial implementation due to easy setup, HA clustering, topic routing, and DLX support.
///
/// <para><strong>Event Consumers</strong>:</para>
/// Notification Service, Integration Gateway, ML Service, and analytics workers subscribe asynchronously.
///
/// <para><strong>Routing Strategy</strong>:</para>
/// Topic exchange with routing key pattern <c>{entityType}.{eventType}</c>, e.g. <c>project.created</c>.
/// </remarks>
public interface IEventBusService
{
    /// <summary>Publishes a domain event to the configured broker or outbox.</summary>
    /// <param name="domainEvent">Event payload to publish.</param>
    Task PublishAsync(DomainEvent domainEvent);
}

/// <summary>
/// No-op event bus for development or disabled broker scenarios.
/// </summary>
public class NoOpEventBusService : IEventBusService
{
    private readonly ILogger<NoOpEventBusService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoOpEventBusService"/> class.
    /// </summary>
    /// <param name="logger">Logger for disabled-bus diagnostics and publish failures.</param>
    public NoOpEventBusService(ILogger<NoOpEventBusService> logger)
    {
        _logger = logger;
    }

    /// <summary>Logs and skips publishing when the broker is disabled.</summary>
    /// <param name="domainEvent">Event that would have been published.</param>
    public Task PublishAsync(DomainEvent domainEvent)
    {
        _logger.LogDebug("EventBus disabled, skipping event: {EventType} for {EntityType}:{EntityId}",
            domainEvent.EventType, domainEvent.EntityType, domainEvent.EntityId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// RabbitMQ-backed implementation of <see cref="IEventBusService"/>.
/// </summary>
public class RabbitMQEventBusService : IEventBusService, IDisposable
{
    private readonly ILogger<RabbitMQEventBusService> _logger;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly string? _connectionString;
    private const string ExchangeName = "devhunt.events";

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMQEventBusService"/> class.
    /// </summary>
    /// <param name="logger">Logger for disabled-bus diagnostics and publish failures.</param>
    /// <param name="configuration">RabbitMQ connection and exchange settings.</param>
    public RabbitMQEventBusService(ILogger<RabbitMQEventBusService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        _connectionString = _configuration["RabbitMQ:ConnectionString"];

        // Try connecting on startup, but do not fail the service if RabbitMQ is down.
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                EnsureConnected();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ not reachable at startup. Will retry on publish.");
            }
        }
    }

    /// <summary>
    /// Reuses an open RabbitMQ connection or creates the connection, channel, topic exchange, and dead-letter queue.
    /// </summary>
    private bool EnsureConnected()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return false;
        }

        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
        {
            return true;
        }

        var exchangeName = _configuration["RabbitMQ:ExchangeName"] ?? ExchangeName;

        // Parse connection string: amqp://user:pass@host:port
        var uri = new Uri(_connectionString);
        var factory = new ConnectionFactory
        {
            Uri = uri,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection?.Dispose();
        _channel?.Dispose();

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Create Exchange if it doesn't exist
        _channel.ExchangeDeclare(exchangeName, ExchangeType.Topic, durable: true, autoDelete: false);

        // Create Dead Letter Exchange and Queue for failed events processing
        var dlxName = $"{exchangeName}.dlx";
        var dlqName = $"{exchangeName}.dlq";
        _channel.ExchangeDeclare(dlxName, ExchangeType.Topic, durable: true, autoDelete: false);
        _channel.QueueDeclare(dlqName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(dlqName, dlxName, "#"); // Bind DLQ to DLX with wildcard routing key

        _logger.LogInformation("RabbitMQ connection established, Exchange: {Exchange}, DLX: {DLX}", exchangeName, dlxName);
        return true;
    }

    /// <summary>
    /// Publishes a persistent message to the RabbitMQ topic exchange with up to three retries
    /// and exponential backoff. Routing key is the <c>EventType</c> verbatim
    /// (e.g. <c>project.created</c>), which is what all downstream consumers bind to.
    /// </summary>
    /// <param name="domainEvent">Event to serialize and publish.</param>
    /// <exception cref="InvalidOperationException">Thrown when RabbitMQ is not configured or the channel is unavailable after retries.</exception>
    public async Task PublishAsync(DomainEvent domainEvent)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("RabbitMQ connection string is not configured.");

        const int maxRetries = 3;
        const int retryDelayMs = 1000; // 1 second

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (!EnsureConnected() || _channel == null)
                    throw new InvalidOperationException("RabbitMQ channel is not available.");

                var exchangeName = _configuration["RabbitMQ:ExchangeName"] ?? ExchangeName;
                // EventType already carries the entity prefix (e.g. "project.created"), so it IS the
                // routing key. Re-prefixing with EntityType produced unroutable 3-segment keys
                // (e.g. "project.project.created") that no consumer binding matched. See DEV-94.
                var routingKey = domainEvent.EventType;

                var json = JsonSerializer.Serialize(domainEvent);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true; // Persist messages to disk (survive broker restart)
                properties.MessageId = Guid.NewGuid().ToString();
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.Type = domainEvent.EventType;

                // Add headers for retry tracking
                properties.Headers = new Dictionary<string, object>
                {
                    { "x-retry-count", attempt - 1 },
                    { "x-original-routing-key", routingKey }
                };

                _channel.BasicPublish(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body
                );

                _logger.LogInformation("Event published: {EventType} for {EntityType}:{EntityId} to {Exchange}/{RoutingKey} (attempt {Attempt})",
                    domainEvent.EventType, domainEvent.EntityType, domainEvent.EntityId, exchangeName, routingKey, attempt);

                return; // Success - exit retry loop
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Failed to publish event: {EventType} (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms",
                        domainEvent.EventType, attempt, maxRetries, retryDelayMs * attempt);

                    // Exponential backoff: wait longer on each retry
                    await Task.Delay(retryDelayMs * attempt);

                    // EnsureConnected() will retry connection on the next attempt.
                }
                else
                {
                    _logger.LogError(ex, "Failed to publish event: {EventType} after {MaxRetries} attempts.",
                        domainEvent.EventType, maxRetries);
                    throw;
                }
            }
        }
    }

    /// <summary>Closes and disposes the RabbitMQ channel and connection.</summary>
    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}

/// <summary>
/// Helper methods for creating standard domain events.
/// </summary>
public static class DomainEvents
{
    /// <summary>
    /// Creates an <see cref="DomainEvent"/> when an activity feed record is written.
    /// </summary>
    /// <param name="activityId">Created activity record ID.</param>
    /// <param name="projectId">Related project ID.</param>
    /// <param name="actorId">User who performed the action.</param>
    /// <param name="eventType">Activity event type key.</param>
    /// <param name="visibility">Visibility scope.</param>
    /// <param name="summary">Human-readable summary.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ActivityRecordCreated(Guid activityId, Guid projectId, Guid actorId, string eventType, string visibility, string summary) =>
        new("activity.record.created", "ActivityRecord", activityId, new { ProjectId = projectId, ActorId = actorId, EventType = eventType, Visibility = visibility, Summary = summary }, DateTime.UtcNow);

    // ========== Project Events ==========
    /// <summary>
    /// Creates a domain event when a new project is created.
    /// </summary>
    /// <param name="projectId">Created project ID.</param>
    /// <param name="ownerId">Project owner user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectCreated(Guid projectId, Guid ownerId) =>
        new("project.created", "Project", projectId, new { OwnerId = ownerId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when project metadata changes.
    /// </summary>
    /// <param name="projectId">Updated project ID.</param>
    /// <param name="userId">User who made the change.</param>
    /// <param name="changes">Optional change summary.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectUpdated(Guid projectId, Guid userId, string? changes = null) =>
        new("project.updated", "Project", projectId, new { UserId = userId, Changes = changes }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a project is archived.
    /// </summary>
    /// <param name="projectId">Archived project ID.</param>
    /// <param name="userId">User who archived the project.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectArchived(Guid projectId, Guid userId) =>
        new("project.archived", "Project", projectId, new { UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a project is restored from archive.
    /// </summary>
    /// <param name="projectId">Restored project ID.</param>
    /// <param name="userId">User who unarchived the project.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectUnarchived(Guid projectId, Guid userId) =>
        new("project.unarchived", "Project", projectId, new { UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a project is deleted.
    /// </summary>
    /// <param name="projectId">Deleted project ID.</param>
    /// <param name="userId">User who deleted the project.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectDeleted(Guid projectId, Guid userId) =>
        new("project.deleted", "Project", projectId, new { UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when project ownership changes.
    /// </summary>
    /// <param name="projectId">Project ID.</param>
    /// <param name="oldOwnerId">Previous owner user ID.</param>
    /// <param name="newOwnerId">New owner user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectOwnershipTransferred(Guid projectId, Guid oldOwnerId, Guid newOwnerId) =>
        new("project.ownership.transferred", "Project", projectId, new { OldOwnerId = oldOwnerId, NewOwnerId = newOwnerId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a project becomes publicly visible.
    /// </summary>
    /// <param name="projectId">Published project ID.</param>
    /// <param name="userId">User who published the project.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectPublished(Guid projectId, Guid userId) =>
        new("project.published", "Project", projectId, new { UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a published project is hidden.
    /// </summary>
    /// <param name="projectId">Unpublished project ID.</param>
    /// <param name="userId">User who unpublished the project.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectUnpublished(Guid projectId, Guid userId) =>
        new("project.unpublished", "Project", projectId, new { UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a project enters an active working state.
    /// </summary>
    /// <param name="projectId">Activated project ID.</param>
    /// <param name="userId">User who activated the project.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectActivated(Guid projectId, Guid userId) =>
        new("project.activated", "Project", projectId, new { UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a project is marked completed.
    /// </summary>
    /// <param name="projectId">Completed project ID.</param>
    /// <param name="userId">User who completed the project.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectCompleted(Guid projectId, Guid userId) =>
        new("project.completed", "Project", projectId, new { UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a project is cancelled.
    /// </summary>
    /// <param name="projectId">Cancelled project ID.</param>
    /// <param name="userId">User who cancelled the project.</param>
    /// <param name="reason">Optional cancellation reason.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProjectCancelled(Guid projectId, Guid userId, string? reason = null) =>
        new("project.cancelled", "Project", projectId, new { UserId = userId, Reason = reason }, DateTime.UtcNow);

    // ========== Showcase Events ==========
    /// <summary>
    /// Creates a domain event when a project showcase is submitted for review.
    /// </summary>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">Submitting user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseSubmitted(Guid projectId, Guid userId) =>
        new("showcase.submitted", "Showcase", projectId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a showcase submission is withdrawn.
    /// </summary>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">User who retracted the submission.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseRetracted(Guid projectId, Guid userId) =>
        new("showcase.retracted", "Showcase", projectId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a showcase is published.
    /// </summary>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">Publishing user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcasePublished(Guid projectId, Guid userId) =>
        new("showcase.published", "Showcase", projectId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a showcase is removed from public view.
    /// </summary>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">User who unpublished the showcase.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseUnpublished(Guid projectId, Guid userId) =>
        new("showcase.unpublished", "Showcase", projectId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a showcase is marked featured.
    /// </summary>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">User who featured the showcase.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseFeatured(Guid projectId, Guid userId) =>
        new("showcase.featured", "Showcase", projectId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when featured status is removed from a showcase.
    /// </summary>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">User who removed featured status.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseUnfeatured(Guid projectId, Guid userId) =>
        new("showcase.unfeatured", "Showcase", projectId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a user likes a showcase.
    /// </summary>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">User who liked the showcase.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseLiked(Guid projectId, Guid userId) =>
        new("showcase.liked", "Showcase", projectId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a user removes a showcase like.
    /// </summary>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">User who unliked the showcase.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseUnliked(Guid projectId, Guid userId) =>
        new("showcase.unliked", "Showcase", projectId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a showcase comment is posted.
    /// </summary>
    /// <param name="commentId">Created comment ID.</param>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">Comment author user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseCommentCreated(Guid commentId, Guid projectId, Guid userId) =>
        new("showcase.comment.created", "ShowcaseComment", commentId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a showcase comment is edited.
    /// </summary>
    /// <param name="commentId">Updated comment ID.</param>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">Editing user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseCommentUpdated(Guid commentId, Guid projectId, Guid userId) =>
        new("showcase.comment.updated", "ShowcaseComment", commentId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a showcase comment is deleted.
    /// </summary>
    /// <param name="commentId">Deleted comment ID.</param>
    /// <param name="projectId">Showcase project ID.</param>
    /// <param name="userId">Deleting user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ShowcaseCommentDeleted(Guid commentId, Guid projectId, Guid userId) =>
        new("showcase.comment.deleted", "ShowcaseComment", commentId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    // ========== Profile Events ==========
    /// <summary>
    /// Creates a domain event when a user profile is updated.
    /// </summary>
    /// <param name="userId">Profile owner user ID.</param>
    /// <param name="changes">Optional change summary.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ProfileUpdated(Guid userId, string? changes = null) =>
        new("profile.updated", "User", userId, new { Changes = changes }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a user uploads a new avatar.
    /// </summary>
    /// <param name="userId">Profile owner user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent AvatarUploaded(Guid userId) =>
        new("avatar.uploaded", "User", userId, null, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a user removes their avatar.
    /// </summary>
    /// <param name="userId">Profile owner user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent AvatarDeleted(Guid userId) =>
        new("avatar.deleted", "User", userId, null, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a user completes email verification.
    /// </summary>
    /// <param name="userId">Verified user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent UserVerified(Guid userId) =>
        new("user.verified", "User", userId, null, DateTime.UtcNow);

    // ========== Team Events ==========
    /// <summary>
    /// Creates a domain event when a user joins a project team.
    /// </summary>
    /// <param name="projectId">Project ID.</param>
    /// <param name="userId">Joined user ID.</param>
    /// <param name="role">Assigned team role.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent TeamMemberJoined(Guid projectId, Guid userId, string role) =>
        new("team.member.joined", "TeamMember", projectId, new { UserId = userId, Role = role }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a user is removed from a project team.
    /// </summary>
    /// <param name="projectId">Project ID.</param>
    /// <param name="userId">Removed user ID.</param>
    /// <param name="role">Team role at removal time.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent TeamMemberRemoved(Guid projectId, Guid userId, string role) =>
        new("team.member.removed", "TeamMember", projectId, new { UserId = userId, Role = role }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a team member's role changes.
    /// </summary>
    /// <param name="projectId">Project ID.</param>
    /// <param name="userId">Affected user ID.</param>
    /// <param name="oldRole">Previous role.</param>
    /// <param name="newRole">New role.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent TeamMemberRoleUpdated(Guid projectId, Guid userId, string oldRole, string newRole) =>
        new("team.member.role.updated", "TeamMember", projectId, new { UserId = userId, OldRole = oldRole, NewRole = newRole }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a user leaves a project team voluntarily.
    /// </summary>
    /// <param name="projectId">Project ID.</param>
    /// <param name="userId">User who left the team.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent TeamMemberLeft(Guid projectId, Guid userId) =>
        new("team.member.left", "TeamMember", projectId, new { UserId = userId }, DateTime.UtcNow);

    // ========== Invitation Events ==========
    /// <summary>
    /// Creates a domain event when a project invitation is sent.
    /// </summary>
    /// <param name="invitationId">Invitation ID.</param>
    /// <param name="projectId">Target project ID.</param>
    /// <param name="inviteeId">Invited user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent InvitationSent(Guid invitationId, Guid projectId, Guid inviteeId) =>
        new("invitation.sent", "Invitation", invitationId, new { ProjectId = projectId, InviteeId = inviteeId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when an invitation is accepted.
    /// </summary>
    /// <param name="invitationId">Invitation ID.</param>
    /// <param name="projectId">Project ID.</param>
    /// <param name="userId">Accepting user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent InvitationAccepted(Guid invitationId, Guid projectId, Guid userId) =>
        new("invitation.accepted", "Invitation", invitationId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when an invitation is declined.
    /// </summary>
    /// <param name="invitationId">Invitation ID.</param>
    /// <param name="projectId">Project ID.</param>
    /// <param name="userId">Declining user ID.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent InvitationDeclined(Guid invitationId, Guid projectId, Guid userId) =>
        new("invitation.declined", "Invitation", invitationId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a pending invitation is cancelled.
    /// </summary>
    /// <param name="invitationId">Invitation ID.</param>
    /// <param name="projectId">Project ID.</param>
    /// <param name="userId">User who cancelled the invitation.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent InvitationCancelled(Guid invitationId, Guid projectId, Guid userId) =>
        new("invitation.cancelled", "Invitation", invitationId, new { ProjectId = projectId, UserId = userId }, DateTime.UtcNow);

    // ========== Chat Events ==========
    /// <summary>
    /// Creates a domain event when a chat message is sent.
    /// </summary>
    /// <param name="messageId">Message ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="senderId">Sender user ID.</param>
    /// <param name="projectId">Related project ID, if any.</param>
    /// <param name="messageType">Message type discriminator.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent MessageSent(Guid messageId, Guid conversationId, Guid senderId, Guid? projectId, string messageType) =>
        new("message.sent", "Message", messageId, new { ConversationId = conversationId, SenderId = senderId, ProjectId = projectId, MessageType = messageType }, DateTime.UtcNow);

    /// <summary>
    /// Creates a domain event when a new conversation is created.
    /// </summary>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="conversationType">Conversation type.</param>
    /// <param name="projectId">Related project ID, if any.</param>
    /// <returns>A <see cref="DomainEvent"/> ready for publishing.</returns>
    public static DomainEvent ConversationCreated(Guid conversationId, string conversationType, Guid? projectId) =>
        new("conversation.created", "Conversation", conversationId, new { Type = conversationType, ProjectId = projectId }, DateTime.UtcNow);
}

