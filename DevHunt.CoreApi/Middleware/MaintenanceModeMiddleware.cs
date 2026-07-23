using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace DevHunt.CoreApi.Middleware;

/// <summary>
/// Blocks all non-admin API traffic when maintenance_mode=true in PlatformSettings.
/// Superadmin and admin roles bypass the check transparently.
/// The setting is cached for 30 seconds to avoid a DB hit on every request.
/// Cache is invalidated immediately when the setting is toggled via the admin API.
/// </summary>
public class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MaintenanceModeMiddleware> _logger;

    /// <summary>Public constant so controllers can invalidate the cache on toggle.</summary>
    public const string CacheKey = "platform:maintenance_mode";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    // Paths that must remain accessible regardless of maintenance mode:
    // health checks, CSRF token endpoint, metrics, SignalR hubs
    private static readonly string[] AlwaysAllowedPrefixes =
    [
        "/health",
        "/metrics",
        "/api/csrf-token",
        "/chatHub",
        "/notificationHub",
    ];

    public MaintenanceModeMiddleware(RequestDelegate next, ILogger<MaintenanceModeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, DevHuntDbContext db, IMemoryCache cache)
    {
        var ct = context.RequestAborted;
        if (!cache.TryGetValue(CacheKey, out bool isMaintenanceOn))
        {
            var setting = await db.PlatformSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "maintenance_mode", ct);

            isMaintenanceOn = setting?.Value == "true";
            cache.Set(CacheKey, isMaintenanceOn, CacheDuration);
        }

        if (!isMaintenanceOn)
        {
            await _next(context);
            return;
        }

        // Always allow admin/superadmin users through
        var role = context.User?.FindFirstValue(ClaimTypes.Role);
        if (role is "superadmin" or "admin")
        {
            await _next(context);
            return;
        }

        // Always allow infrastructure paths
        var path = context.Request.Path.Value ?? "";
        foreach (var prefix in AlwaysAllowedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        _logger.LogInformation(
            "Maintenance mode: blocked {Method} {Path} (role={Role})",
            context.Request.Method, path, role ?? "anonymous");

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Service Unavailable",
            status = 503,
            message = "The platform is currently under maintenance. Please try again later."
        });
    }
}
