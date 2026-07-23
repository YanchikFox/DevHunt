namespace DevHunt.Infrastructure;

/// <summary>
/// Outbox pattern entity for reliable event delivery (REL-002).
/// Events are first saved to database, then published to RabbitMQ by background worker.
/// This ensures events are not lost if RabbitMQ is temporarily unavailable.
/// </summary>
public class OutboxEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty; // JSON serialized event data
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public string? ErrorMessage { get; set; }
    public OutboxEventStatus Status { get; set; } = OutboxEventStatus.Pending;
}

/// <summary>
/// Status of outbox event processing.
/// </summary>
public enum OutboxEventStatus
{
    Pending = 0,      // Not yet processed
    Processing = 1,   // Currently being processed
    Completed = 2,    // Successfully published to RabbitMQ
    Failed = 3        // Failed after max retries
}
