namespace DevHunt.CoreApi.Middleware;

/// <summary>
/// Security headers middleware (SEC-009).
/// Adds HTTP security headers to all responses.
/// CSP is configured for API without inline scripts (nonce is not required for APIs).
/// </summary>
/// <remarks>
/// <para><strong>Applied Headers</strong>:</para>
/// - <b>X-Content-Type-Options: nosniff</b> - Prevents MIME type sniffing
/// - <b>X-Frame-Options: DENY</b> - Prevents clickjacking attacks
/// - <b>X-XSS-Protection: 1; mode=block</b> - Enables browser XSS filter
/// - <b>Strict-Transport-Security</b> (HTTPS only) - Forces HTTPS connections
/// - <b>Content-Security-Policy</b> - Restricts resource loading (no inline scripts)
/// - <b>Referrer-Policy: strict-origin-when-cross-origin</b> - Controls referrer information
/// - <b>Permissions-Policy</b> - Disables geolocation, microphone, camera access
///
/// <para><strong>Note on CSP</strong>:</para>
/// Nonce-based CSP is only required for HTML applications with inline JavaScript.
/// This API does not serve HTML, so nonce is not needed.
/// </remarks>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Security headers - use indexer to set headers (avoids duplicate key exception)
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        // HSTS only for HTTPS (in production)
        if (context.Request.IsHttps)
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        // SEC-009: CSP without nonce (API does not use inline scripts)
        // Nonce is only required for HTML applications with inline JavaScript
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

        // Referrer Policy
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions Policy
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        await _next(context);
    }
}
