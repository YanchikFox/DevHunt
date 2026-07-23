using System.Text.Json;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// REL-002: Outbox Pattern service for reliable event delivery.
/// Events are first saved to database, then published to RabbitMQ by background worker.
/// This ensures events are not lost if RabbitMQ is temporarily unavailable.
/// </summary>
public interface IOutboxEventService
{
    /// <summary>
    /// Save event to outbox for later delivery.
    /// </summary>
    Task SaveEventAsync(DomainEvent domainEvent, CancellationToken ct = default);

    /// <summary>
    /// Get pending events for processing (used by background worker).
    /// </summary>
    Task<List<OutboxEvent>> GetPendingEventsAsync(int batchSize = 100, CancellationToken ct = default);

    /// <summary>
    /// Mark event as completed after successful publish.
    /// </summary>
    Task MarkAsCompletedAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Mark event as failed and increment retry count.
    /// </summary>
    Task MarkAsFailedAsync(Guid eventId, string errorMessage, CancellationToken ct = default);
}

/// <summary>
/// Default outbox implementation backed by the database.
/// </summary>
public class OutboxEventService : IOutboxEventService
{
    private readonly DevHuntDbContext _dbContext;
    private readonly ILogger<OutboxEventService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxEventService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for <c>OutboxEvents</c> persistence.</param>
    /// <param name="logger">Logger for retry and terminal failure events.</param>
    public OutboxEventService(DevHuntDbContext dbContext, ILogger<OutboxEventService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SaveEventAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var outboxEvent = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            EventType = domainEvent.EventType,
            Payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
            {
                WriteIndented = false
            }),
            CreatedAt = DateTime.UtcNow,
            Status = OutboxEventStatus.Pending,
            RetryCount = 0,
            MaxRetries = 3
        };

        _dbContext.OutboxEvents.Add(outboxEvent);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Saved event {EventType} to outbox with ID {EventId}",
            domainEvent.EventType, outboxEvent.Id);
    }

    /// <inheritdoc />
    public async Task<List<OutboxEvent>> GetPendingEventsAsync(int batchSize = 100, CancellationToken ct = default)
    {
        return await _dbContext.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending)
            .Where(e => e.RetryCount < e.MaxRetries)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task MarkAsCompletedAsync(Guid eventId, CancellationToken ct = default)
    {
        var outboxEvent = await _dbContext.OutboxEvents.FindAsync(new object[] { eventId }, ct);
        if (outboxEvent == null) return;

        outboxEvent.Status = OutboxEventStatus.Completed;
        outboxEvent.ProcessedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Marked outbox event {EventId} as completed", eventId);
    }

    /// <inheritdoc />
    public async Task MarkAsFailedAsync(Guid eventId, string errorMessage, CancellationToken ct = default)
    {
        var outboxEvent = await _dbContext.OutboxEvents.FindAsync(new object[] { eventId }, ct);
        if (outboxEvent == null) return;

        outboxEvent.RetryCount++;
        outboxEvent.ErrorMessage = errorMessage;

        if (outboxEvent.RetryCount >= outboxEvent.MaxRetries)
        {
            outboxEvent.Status = OutboxEventStatus.Failed;
            outboxEvent.ProcessedAt = DateTime.UtcNow;
            _logger.LogError("Outbox event {EventId} failed after {RetryCount} retries: {Error}",
                eventId, outboxEvent.RetryCount, errorMessage);
        }
        else
        {
            outboxEvent.Status = OutboxEventStatus.Pending; // Retry later
            _logger.LogWarning("Outbox event {EventId} failed (attempt {RetryCount}/{MaxRetries}): {Error}",
                eventId, outboxEvent.RetryCount, outboxEvent.MaxRetries, errorMessage);
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
