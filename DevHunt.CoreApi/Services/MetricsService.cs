using Microsoft.Extensions.Configuration;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Metrics service abstraction for counters, histograms, and gauges.
/// The current implementation logs metric calls instead of exporting custom Prometheus collectors.
/// </summary>
/// <remarks>
/// <para><strong>Metric Types</strong>:</para>
/// - <b>Counter</b>: Monotonically increasing values (requests, errors, events)
/// - <b>Histogram</b>: Distribution of values (response time, request size)
/// - <b>Gauge</b>: Point-in-time values (active connections, queue depth)
///
/// <para><strong>Monitored Metrics</strong>:</para>
/// - Technical: requests/sec, error rate, response time, database connections
/// - Business: projects created, users registered, team invitations, showcases published
///
/// <para><strong>Implementation</strong>:</para>
/// The service currently logs metric calls when enabled; ASP.NET Prometheus middleware exposes runtime metrics separately.
/// Prometheus scrapes the /metrics endpoint at regular intervals (default: 15 seconds).
/// Grafana visualizes metrics via pre-configured dashboards.
/// </remarks>
public interface IMetricsService
{
    /// <summary>
    /// Records a counter increment.
    /// </summary>
    /// <param name="name">The counter name.</param>
    /// <param name="value">The increment amount.</param>
    /// <param name="labels">The metric labels.</param>
    void IncrementCounter(string name, double value = 1, params (string Key, string Value)[] labels);

    /// <summary>
    /// Records a histogram observation.
    /// </summary>
    /// <param name="name">The histogram name.</param>
    /// <param name="value">Measurement sample added to the histogram distribution.</param>
    /// <param name="labels">The metric labels.</param>
    void RecordHistogram(string name, double value, params (string Key, string Value)[] labels);

    /// <summary>
    /// Records a gauge value.
    /// </summary>
    /// <param name="name">The gauge name.</param>
    /// <param name="value">Point-in-time measurement stored for the gauge.</param>
    /// <param name="labels">The metric labels.</param>
    void SetGauge(string name, double value, params (string Key, string Value)[] labels);
}

/// <summary>
/// Metrics implementation that logs custom metric calls instead of emitting dedicated collectors.
/// </summary>
public class PrometheusMetricsService : IMetricsService
{
    private readonly ILogger<PrometheusMetricsService> _logger;
    private readonly bool _isEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusMetricsService"/> class.
    /// </summary>
    /// <param name="logger">Logger used when custom metrics are recorded as trace output.</param>
    /// <param name="configuration">Enables logging when <c>Prometheus:Endpoint</c> is configured.</param>
    public PrometheusMetricsService(ILogger<PrometheusMetricsService> logger, IConfiguration configuration)
    {
        _logger = logger;
        // Custom metric logging is enabled when a Prometheus endpoint is configured.
        _isEnabled = !string.IsNullOrEmpty(configuration["Prometheus:Endpoint"]);
    }

    /// <inheritdoc />
    public void IncrementCounter(string name, double value = 1, params (string Key, string Value)[] labels)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Metrics disabled: counter {Name} += {Value}", name, value);
            return;
        }

        // This implementation records metrics as trace logs; Prometheus middleware is registered separately.
        _logger.LogTrace("Counter incremented: {Name} += {Value}", name, value);
    }

    /// <inheritdoc />
    public void RecordHistogram(string name, double value, params (string Key, string Value)[] labels)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Metrics disabled: histogram {Name} = {Value}", name, value);
            return;
        }

        // This implementation records metrics as trace logs; Prometheus middleware is registered separately.
        _logger.LogTrace("Histogram recorded: {Name} = {Value}", name, value);
    }

    /// <inheritdoc />
    public void SetGauge(string name, double value, params (string Key, string Value)[] labels)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Metrics disabled: gauge {Name} = {Value}", name, value);
            return;
        }

        // This implementation records metrics as trace logs; Prometheus middleware is registered separately.
        _logger.LogTrace("Gauge set: {Name} = {Value}", name, value);
    }
}

