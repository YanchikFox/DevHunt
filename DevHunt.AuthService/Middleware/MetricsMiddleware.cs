using System.Diagnostics;
using Prometheus;

namespace DevHunt.AuthService.Middleware;

/// <summary>
/// Prometheus metrics collection for Auth API.
/// </summary>
public class MetricsMiddleware
{
    private static readonly Counter HttpRequestsTotal = Metrics
        .CreateCounter("auth_http_requests_total", "Total number of HTTP requests", new[] { "method", "endpoint", "status_code" });

    private static readonly Histogram HttpRequestDuration = Metrics
        .CreateHistogram("auth_http_request_duration_seconds", "HTTP request duration in seconds", new[] { "method", "endpoint" });

    private static readonly Counter LoginAttempts = Metrics
        .CreateCounter("auth_login_attempts_total", "Total login attempts", new[] { "result" });

    private static readonly Counter RegistrationAttempts = Metrics
        .CreateCounter("auth_registration_attempts_total", "Total registration attempts", new[] { "result" });

    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(RequestDelegate next, ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = NormalizePath(context.Request.Path);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode.ToString();

            HttpRequestsTotal.WithLabels(method, path, statusCode).Inc();
            HttpRequestDuration.WithLabels(method, path).Observe(stopwatch.Elapsed.TotalSeconds);

            // Auth-specific counters
            if (path == "/api/auth/login" && method == "POST")
            {
                LoginAttempts.WithLabels(statusCode == "200" ? "success" : "failure").Inc();
            }

            if (path == "/api/auth/register" && method == "POST")
            {
                RegistrationAttempts.WithLabels(statusCode == "200" ? "success" : "failure").Inc();
            }
        }
    }

    private static string NormalizePath(PathString path)
    {
        var value = path.Value ?? "/";
        if (value.StartsWith("/api/auth/login"))
            return "/api/auth/login";
        if (value.StartsWith("/api/auth/register"))
            return "/api/auth/register";
        return value;
    }
}
