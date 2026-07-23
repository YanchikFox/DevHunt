using Serilog.Context;

namespace DevHunt.AuthService.Middleware;

/// <summary>
/// Enrich Serilog context with trace/user identifiers for better observability.
/// </summary>
public class LogEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public LogEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.TraceIdentifier;
        var userId = context.User?.FindFirst("sub")?.Value ?? context.User?.Identity?.Name;

        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
        {
            await _next(context);
        }
    }
}
