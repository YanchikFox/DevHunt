namespace DevHunt.CoreApi.Services;

/// <summary>
/// Validates event bus configuration at application startup.
/// </summary>
public static class EventBusStartupValidation
{
    /// <summary>
    /// Ensures production deployments do not start with a silent noop event bus when events are required.
    /// </summary>
    /// <param name="isProduction">Whether the host environment is production.</param>
    /// <param name="eventBusFeatureEnabled">Value of <c>Features:EventBus:Enabled</c>.</param>
    /// <param name="rabbitMqConnectionString">Configured RabbitMQ connection string, if any.</param>
    /// <exception cref="InvalidOperationException">Thrown when production requires RabbitMQ but it is missing.</exception>
    public static void EnsureProductionConfiguration(
        bool isProduction,
        bool eventBusFeatureEnabled,
        string? rabbitMqConnectionString)
    {
        if (!isProduction || !eventBusFeatureEnabled)
            return;

        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
        {
            throw new InvalidOperationException(
                "RabbitMQ:ConnectionString is required in production when Features:EventBus:Enabled is true. " +
                "Domain events would otherwise be silently dropped by NoOpEventBusService.");
        }
    }
}
