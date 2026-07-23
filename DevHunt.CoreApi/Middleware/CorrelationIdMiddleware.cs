using System.Diagnostics;

namespace DevHunt.CoreApi.Middleware;

/// <summary>
/// Correlation ID middleware for distributed request tracing.
/// Generates or propagates correlation ID across service calls and logs.
/// </summary>
/// <remarks>
/// <para><strong>Purpose</strong>:</para>
/// Enables end-to-end request tracing across microservices architecture.
/// Each request gets a unique correlation ID that is included in all logs and propagated to downstream services.
///
/// <para><strong>Behavior</strong>:</para>
/// - Reads correlation ID from X-Correlation-ID or X-Request-ID header
/// - If not present, generates new GUID
/// - Adds correlation ID to response headers (both X-Correlation-ID and X-Request-ID)
/// - Sets correlation ID in HttpContext.Items for use in controllers/services
/// - Sets Activity trace ID for distributed tracing (OpenTelemetry)
/// - Adds correlation ID to logging scope (Serilog/NLog will include in all log entries)
///
/// <para><strong>Usage in Logs</strong>:</para>
/// All log entries for a request will include the same correlation ID, making it easy to filter and track request flow.
/// </remarks>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get or generate correlation ID
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                          ?? context.Request.Headers["X-Request-ID"].FirstOrDefault()
                          ?? Guid.NewGuid().ToString();

        // Add to response headers
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        context.Response.Headers["X-Request-ID"] = correlationId; // Also support standard header

        // Add to HttpContext.Items for use in controllers/services
        context.Items["CorrelationId"] = correlationId;

        // Set Activity trace ID for distributed tracing
        Activity.Current?.SetTag("correlation.id", correlationId);
        Activity.Current?.SetTag("request.id", correlationId);

        // Add to logging scope (Serilog will pick this up)
        using var _ = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<CorrelationIdMiddleware>()
            .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        await _next(context);
    }
}

/// <summary>
/// Extension method for easy middleware registration
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}

