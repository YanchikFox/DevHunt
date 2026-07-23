using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevHunt.AuthService.Filters;

/// <summary>
/// CSRF protection filter for AuthService (SEC-011).
/// Validates anti-forgery token on POST, PUT, DELETE, PATCH requests.
/// 
/// Auth endpoints have different CSRF requirements:
/// - login/register: Exempt (no session to protect yet)
/// - logout/refresh: Protected (session state change)
/// - profile updates: Protected (authenticated user operations)
/// </summary>
public class AuthCsrfValidationFilter : IAsyncAuthorizationFilter
{
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<AuthCsrfValidationFilter> _logger;
    private readonly IWebHostEnvironment _environment;

    // Auth endpoints exempt from CSRF (initial auth operations)
    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/refresh",
        "/api/auth/github",
        "/api/auth/github-callback",
        "/api/auth/verify-email",
        "/api/auth/resend-verification",
        "/api/auth/forgot-password",
        "/api/auth/reset-password"
    };

    // Safe HTTP methods
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS", "TRACE"
    };

    public AuthCsrfValidationFilter(
        IAntiforgery antiforgery,
        ILogger<AuthCsrfValidationFilter> logger,
        IWebHostEnvironment environment)
    {
        _antiforgery = antiforgery;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Validates CSRF token for unsafe HTTP methods unless exempted.
    /// </summary>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;

        if (ShouldSkipValidation(context, request))
            return;

        await ValidateCsrfTokenAsync(context, request);
    }

    private bool ShouldSkipValidation(AuthorizationFilterContext context, HttpRequest request)
    {
        // Skip for safe HTTP methods
        if (SafeMethods.Contains(request.Method))
            return true;

        // Skip for exempt auth paths
        var path = request.Path.Value?.ToLowerInvariant() ?? "";
        if (ExemptPaths.Any(exempt => path.Equals(exempt, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Skip if action has [IgnoreAntiforgeryToken] attribute
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IgnoreAntiforgeryTokenAttribute>() != null)
            return true;

        // Development mode: Skip if no token provided
        if (_environment.IsDevelopment() && !HasCsrfToken(request))
        {
            _logger.LogDebug(
                "CSRF token not provided for {Method} {Path} (Development mode - allowing)",
                request.Method, request.Path.Value);
            return true;
        }

        return false;
    }

    private static bool HasCsrfToken(HttpRequest request)
    {
        return request.Headers.ContainsKey("X-CSRF-TOKEN") ||
               request.Cookies.ContainsKey("CSRF-TOKEN");
    }

    private async Task ValidateCsrfTokenAsync(AuthorizationFilterContext context, HttpRequest request)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            _logger.LogWarning(ex,
                "CSRF validation failed for {Method} {Path} from IP {IP}",
                request.Method,
                request.Path.Value,
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new ObjectResult(new
            {
                error = "CSRF token validation failed",
                code = "CSRF_VALIDATION_ERROR",
                hint = "Include X-CSRF-TOKEN header with token from /api/auth/csrf-token endpoint"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
