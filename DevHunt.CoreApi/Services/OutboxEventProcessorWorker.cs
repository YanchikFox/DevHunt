using System.Text.Json;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// REL-002: Background worker that processes outbox events and publishes them to RabbitMQ.
/// Runs every 5 seconds to check for pending events.
/// </summary>
public class OutboxEventProcessorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxEventProcessorWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxEventProcessorWorker"/> class.
    /// </summary>
    /// <param name="serviceProvider">Creates scopes for outbox and RabbitMQ services each poll cycle.</param>
    /// <param name="logger">Logger for processor lifecycle and per-event failures.</param>
    public OutboxEventProcessorWorker(
        IServiceProvider serviceProvider,
        ILogger<OutboxEventProcessorWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>Polls the outbox every five seconds and publishes pending events to RabbitMQ.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Event Processor Worker started (REL-002)");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox events");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Outbox Event Processor Worker stopped");
    }

    /// <summary>Deserializes pending outbox rows and publishes them via <see cref="RabbitMQEventBusService"/>.</summary>
    private async Task ProcessPendingEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxEventService>();

        // REL-002: Get RabbitMQ service directly (not the decorator) for actual publishing
        var rabbitMqService = scope.ServiceProvider.GetRequiredService<RabbitMQEventBusService>();

        var pendingEvents = await outboxService.GetPendingEventsAsync(batchSize: 100);

        if (pendingEvents.Count == 0)
        {
            return; // No events to process
        }

        _logger.LogDebug("Processing {Count} pending outbox events", pendingEvents.Count);

        foreach (var outboxEvent in pendingEvents)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Deserialize domain event from JSON payload
                var domainEvent = JsonSerializer.Deserialize<DomainEvent>(outboxEvent.Payload);
                if (domainEvent == null)
                {
                    await outboxService.MarkAsFailedAsync(outboxEvent.Id, "Failed to deserialize domain event");
                    continue;
                }

                // Publish to RabbitMQ directly
                await rabbitMqService.PublishAsync(domainEvent);

                // Mark as completed
                await outboxService.MarkAsCompletedAsync(outboxEvent.Id);

                _logger.LogDebug("Published outbox event {EventId} ({EventType}) to RabbitMQ",
                    outboxEvent.Id, domainEvent.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox event {EventId}", outboxEvent.Id);
                await outboxService.MarkAsFailedAsync(outboxEvent.Id, ex.Message);
            }
        }
    }
}
