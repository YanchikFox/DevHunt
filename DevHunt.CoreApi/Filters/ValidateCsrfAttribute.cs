using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevHunt.CoreApi.Filters;

/// <summary>
/// CSRF protection filter for state-changing operations (SEC-011).
/// Validates anti-forgery token on POST, PUT, DELETE, PATCH requests.
///
/// Usage:
/// - Apply [ValidateCsrf] to controllers or actions that modify data
/// - Exempt specific actions with [IgnoreAntiforgeryToken]
///
/// Token flow:
/// 1. Client calls GET /api/auth/csrf-token to get token
/// 2. Token is set in CSRF-TOKEN cookie (httpOnly=false for JS access)
/// 3. Client sends token in X-CSRF-TOKEN header on state-changing requests
/// 4. This filter validates the token matches
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ValidateCsrfAttribute : Attribute, IAsyncAuthorizationFilter
{
    // HTTP methods that don't change state (safe methods per RFC 7231)
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS", "TRACE"
    };

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpMethod = context.HttpContext.Request.Method;

        // Skip CSRF validation for safe methods (GET, HEAD, OPTIONS, TRACE)
        if (SafeMethods.Contains(httpMethod))
        {
            return;
        }

        // Check if action has [IgnoreAntiforgeryToken] attribute
        var endpoint = context.HttpContext.GetEndpoint();
        var ignoreAttribute = endpoint?.Metadata.GetMetadata<IgnoreAntiforgeryTokenAttribute>();
        if (ignoreAttribute != null)
        {
            return;
        }

        // Get antiforgery service
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ValidateCsrfAttribute>>();

        try
        {
            // Validate the anti-forgery token
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning(ex,
                "CSRF validation failed for {Method} {Path} from IP {IP}",
                httpMethod,
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new ObjectResult(new { error = "CSRF token validation failed" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}

/// <summary>
/// Auto-validate CSRF tokens on all state-changing requests globally.
/// This filter runs on all controllers and validates CSRF for POST/PUT/DELETE/PATCH.
///
/// Unlike [ValidateCsrf] attribute, this applies globally via AddMvc configuration.
/// </summary>
public class GlobalCsrfValidationFilter : IAsyncAuthorizationFilter
{
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<GlobalCsrfValidationFilter> _logger;
    private readonly IWebHostEnvironment _environment;

    // Paths that are exempt from CSRF validation (public endpoints, OAuth callbacks, etc.)
    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/refresh",
        "/api/auth/github-callback",
        "/api/auth/forgot-password",
        "/api/auth/reset-password",
        "/api/integrations/callback", // OAuth callbacks
        "/api/webhooks" // Webhook endpoints have their own signature validation
    };

    // HTTP methods that don't change state
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS", "TRACE"
    };

    public GlobalCsrfValidationFilter(
        IAntiforgery antiforgery,
        ILogger<GlobalCsrfValidationFilter> logger,
        IWebHostEnvironment environment)
    {
        _antiforgery = antiforgery;
        _logger = logger;
        _environment = environment;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        var httpMethod = request.Method;
        var path = request.Path.Value?.ToLowerInvariant() ?? "";

        // Skip CSRF validation for safe methods
        if (SafeMethods.Contains(httpMethod))
        {
            return;
        }

        // Skip for exempt paths
        if (ExemptPaths.Any(exempt => path.StartsWith(exempt, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        // Skip for webhook endpoints (they have signature-based validation)
        if (path.Contains("/webhooks/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Check if action has [IgnoreAntiforgeryToken] attribute
        var endpoint = context.HttpContext.GetEndpoint();
        var ignoreAttribute = endpoint?.Metadata.GetMetadata<IgnoreAntiforgeryTokenAttribute>();
        if (ignoreAttribute != null)
        {
            return;
        }

        // In Development, allow requests without CSRF token but log warning
        // This helps during API testing with tools like Postman/curl
        if (_environment.IsDevelopment())
        {
            var hasToken = request.Headers.ContainsKey("X-CSRF-TOKEN") ||
                          request.Cookies.ContainsKey("CSRF-TOKEN");

            if (!hasToken)
            {
                _logger.LogDebug(
                    "CSRF token not provided for {Method} {Path} (Development mode - allowing)",
                    httpMethod, path);
                return;
            }
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            // DEV-119: ANY antiforgery failure rejects the state-changing request with 403.
            // The previous special-case for "different claims-based user" let the request through,
            // bypassing CSRF protection — JWT in an httpOnly cookie is auto-attached by the browser,
            // which is exactly the attack CSRF tokens defend against. Regenerate tokens first so a
            // legitimate client (e.g. after re-login) succeeds on retry.
            _antiforgery.GetAndStoreTokens(context.HttpContext);

            _logger.LogWarning(ex,
                "CSRF validation failed for {Method} {Path} from IP {IP}",
                httpMethod,
                path,
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new ObjectResult(new
            {
                error = "CSRF token validation failed",
                code = "CSRF_VALIDATION_ERROR",
                hint = "Include X-CSRF-TOKEN header with token from /api/csrf-token endpoint"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
