using Prometheus;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Prometheus gauge for event bus mode (alert when <c>devhunt_event_bus_active == 0</c> in production).
/// </summary>
public static class EventBusMetrics
{
    private static readonly Gauge EventBusActive = Metrics.CreateGauge(
        "devhunt_event_bus_active",
        "1 when outbox/RabbitMQ event bus is active, 0 when running in noop mode");

    /// <summary>
    /// Sets the event bus active gauge for scraping.
    /// </summary>
    /// <param name="isActive">Whether the real event bus is registered.</param>
    public static void Configure(bool isActive)
    {
        EventBusActive.Set(isActive ? 1 : 0);
    }
}
