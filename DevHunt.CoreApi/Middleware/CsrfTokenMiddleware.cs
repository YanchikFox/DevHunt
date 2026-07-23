using Microsoft.AspNetCore.Antiforgery;

namespace DevHunt.CoreApi.Middleware;

/// <summary>
/// CSRF token middleware (SEC-011).
/// Ensures CSRF token cookie is set for all authenticated requests.
///
/// This middleware generates and sends CSRF tokens to the client:
/// 1. Sets CSRF-TOKEN cookie (httpOnly=false so JS can read it)
/// 2. Client reads cookie and sends value in X-CSRF-TOKEN header
/// 3. Antiforgery validates header matches cookie (Double Submit Cookie pattern)
///
/// Token lifecycle:
/// - Token is regenerated on each request to prevent token fixation
/// - Token is tied to the request (not user session) via cryptographic validation
/// </summary>
public class CsrfTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CsrfTokenMiddleware> _logger;

    public CsrfTokenMiddleware(RequestDelegate next, ILogger<CsrfTokenMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        // Always generate and set CSRF token for non-API documentation paths
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Skip for health/metrics endpoints
        if (path.StartsWith("/health") || path.StartsWith("/metrics"))
        {
            await _next(context);
            return;
        }

        // Generate tokens and set cookie
        var tokens = antiforgery.GetAndStoreTokens(context);

        // Set the request token in a non-HttpOnly cookie so JavaScript can read it
        // The cookie token is HttpOnly (set by AddAntiforgery config)
        if (tokens.RequestToken != null)
        {
            context.Response.Cookies.Append("XSRF-REQUEST-TOKEN", tokens.RequestToken, new CookieOptions
            {
                HttpOnly = false, // JavaScript needs to read this
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                // Token is per-request, short expiry
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for CSRF middleware registration.
/// </summary>
public static class CsrfTokenMiddlewareExtensions
{
    public static IApplicationBuilder UseCsrfToken(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CsrfTokenMiddleware>();
    }
}
