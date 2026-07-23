namespace DevHunt.CoreApi.Services;

/// <summary>
/// Captures whether the real outbox/RabbitMQ event bus is active for health checks and metrics.
/// </summary>
public sealed class EventBusRuntimeState
{
    /// <summary>
    /// Initializes runtime state for the configured event bus mode.
    /// </summary>
    /// <param name="isActive">True when outbox + RabbitMQ are registered; false for noop mode.</param>
    public EventBusRuntimeState(bool isActive)
    {
        IsActive = isActive;
    }

    /// <summary>
    /// Gets whether domain events are published through the outbox pipeline.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Gets a stable mode label for logs and metrics.
    /// </summary>
    public string Mode => IsActive ? "active" : "noop";
}
