using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Reports degraded health when the event bus runs in noop mode (DEV-23).
/// </summary>
public sealed class EventBusHealthCheck : IHealthCheck
{
    private readonly EventBusRuntimeState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventBusHealthCheck"/> class.
    /// </summary>
    /// <param name="state">Current event bus runtime mode.</param>
    public EventBusHealthCheck(EventBusRuntimeState state)
    {
        _state = state;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_state.IsActive)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("Event bus is active (outbox + RabbitMQ)."));
        }

        return Task.FromResult(
            HealthCheckResult.Degraded(
                "Event bus is in noop mode; domain events are not published.",
                data: new Dictionary<string, object> { ["event_bus_mode"] = _state.Mode }));
    }
}
