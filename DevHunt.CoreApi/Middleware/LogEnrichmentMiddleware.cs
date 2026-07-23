using Serilog.Context;

namespace DevHunt.CoreApi.Middleware;

/// <summary>
/// Enriches Serilog context with request identifiers and user info.
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
