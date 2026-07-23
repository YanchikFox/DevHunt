namespace DevHunt.CoreApi.Services;

/// <summary>
/// REL-002: Decorator that wraps EventBusService with Outbox Pattern.
/// All events are first saved to database, then published by background worker.
/// This ensures reliable delivery even if RabbitMQ is temporarily unavailable.
/// </summary>
public class OutboxEventBusDecorator : IEventBusService
{
    private readonly IOutboxEventService _outboxService;
    private readonly ILogger<OutboxEventBusDecorator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxEventBusDecorator"/> class.
    /// </summary>
    /// <param name="outboxService">Outbox writer used by the background processor.</param>
    /// <param name="logger">Logger for saved-event diagnostics.</param>
    public OutboxEventBusDecorator(
        IOutboxEventService outboxService,
        ILogger<OutboxEventBusDecorator> logger)
    {
        _outboxService = outboxService;
        _logger = logger;
    }

    /// <summary>Writes the event to the outbox via <see cref="IOutboxEventService"/> instead of publishing directly.</summary>
    /// <param name="domainEvent">Event to persist for later delivery.</param>
    public async Task PublishAsync(DomainEvent domainEvent)
    {
        // REL-002: Save to outbox instead of publishing directly
        // Background worker will publish to RabbitMQ
        await _outboxService.SaveEventAsync(domainEvent);

        _logger.LogDebug("Event {EventType} saved to outbox for reliable delivery",
            domainEvent.EventType);
    }
}
