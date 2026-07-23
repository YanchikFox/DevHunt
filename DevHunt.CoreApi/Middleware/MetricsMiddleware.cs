using Prometheus;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DevHunt.CoreApi.Middleware;

/// <summary>
/// Prometheus metrics collection middleware for HTTP requests and business events.
/// </summary>
/// <remarks>
/// <para><strong>Collected Metrics</strong>:</para>
/// - <b>http_requests_total</b>: Counter of all HTTP requests (labeled by method, endpoint, status_code)
/// - <b>http_request_duration_seconds</b>: Histogram of request latencies (labeled by method, endpoint)
/// - <b>support_tickets_created_total</b>: Counter of support ticket creations
/// - <b>feedback_items_created_total</b>: Counter of feedback submissions (labeled by type)
/// - <b>project_issues_created_total</b>: Counter of project issue creations (labeled by type)
/// - <b>avatar_uploads_total</b>: Counter of avatar uploads
///
/// <para><strong>Purpose</strong>:</para>
/// Provides observability into API performance and business KPIs.
/// Metrics are scraped by Prometheus at /metrics endpoint and visualized in Grafana dashboards.
/// </remarks>
public class MetricsMiddleware
{
    private static readonly Counter HttpRequestsTotal = Metrics
        .CreateCounter("http_requests_total", "Total number of HTTP requests", new[] { "method", "endpoint", "status_code" });

    private static readonly Histogram HttpRequestDuration = Metrics
        .CreateHistogram("http_request_duration_seconds", "HTTP request duration in seconds", new[] { "method", "endpoint" });

    private static readonly Counter SupportTicketsCreated = Metrics
        .CreateCounter("support_tickets_created_total", "Total number of support tickets created");

    private static readonly Counter FeedbackItemsCreated = Metrics
        .CreateCounter("feedback_items_created_total", "Total number of feedback items created", new[] { "type" });

    private static readonly Counter ProjectIssuesCreated = Metrics
        .CreateCounter("project_issues_created_total", "Total number of project issues created", new[] { "type" });

    private static readonly Counter AvatarUploads = Metrics
        .CreateCounter("avatar_uploads_total", "Total number of avatar uploads");

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
        var path = GetEndpointPath(context.Request.Path);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode.ToString();

            // General HTTP metrics
            HttpRequestsTotal.WithLabels(method, path, statusCode).Inc();
            HttpRequestDuration.WithLabels(method, path).Observe(stopwatch.Elapsed.TotalSeconds);

            // Specific metrics for business events
            if (context.Request.Path.StartsWithSegments("/api/support/tickets") &&
                context.Request.Method == "POST" &&
                statusCode == "201")
            {
                SupportTicketsCreated.Inc();
            }

            if (context.Request.Path.StartsWithSegments("/api/community/feedback") &&
                context.Request.Method == "POST" &&
                statusCode == "201")
            {
                // Try to get type from query string or headers
                var feedbackType = context.Request.Query["type"].ToString();
                if (string.IsNullOrEmpty(feedbackType))
                {
                    feedbackType = "unknown";
                }
                FeedbackItemsCreated.WithLabels(feedbackType).Inc();
            }

            if (context.Request.Path.Value?.Contains("/issues") == true &&
                context.Request.Method == "POST" &&
                statusCode == "201")
            {
                var issueType = context.Request.Query["type"].ToString();
                if (string.IsNullOrEmpty(issueType))
                {
                    issueType = "unknown";
                }
                ProjectIssuesCreated.WithLabels(issueType).Inc();
            }

            if (context.Request.Path.StartsWithSegments("/api/profile/avatar") &&
                context.Request.Method == "POST" &&
                statusCode == "200")
            {
                AvatarUploads.Inc();
            }
        }
    }

    private static string GetEndpointPath(PathString path)
    {
        var pathValue = path.Value ?? "/";

        // Normalize paths for metric grouping (avoid high cardinality)
        if (pathValue.StartsWith("/api/support/tickets/"))
        {
            return "/api/support/tickets/{id}";
        }
        if (pathValue.StartsWith("/api/community/feedback/"))
        {
            return "/api/community/feedback/{id}";
        }
        if (pathValue.Contains("/issues/"))
        {
            return pathValue.Replace(Regex.Match(pathValue, @"\/issues\/[^\/]+").Value, "/issues/{id}");
        }
        if (pathValue.StartsWith("/api/profile/"))
        {
            if (pathValue.Contains("avatar"))
                return "/api/profile/avatar";
            if (pathValue.Contains("privacy"))
                return "/api/profile/privacy";
        }

        return pathValue;
    }
}

