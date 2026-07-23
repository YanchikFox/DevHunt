using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevHunt.CoreApi.Security;

/// <summary>
/// Action filter that restricts access to requests from whitelisted IP addresses.
/// Used to protect superadmin endpoints with an additional layer of security.
/// Configure allowed IPs via SuperAdmin:AllowedIPs in appsettings.
/// Empty/missing list means no IP restriction (useful for development).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class IpWhitelistAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>
    /// Validates the request IP address before executing the protected action.
    /// </summary>
    /// <param name="context">The action execution context.</param>
    /// <param name="next">The next action delegate.</param>
    /// <returns>The asynchronous filter operation.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var allowedIps = configuration.GetSection("SuperAdmin:AllowedIPs").Get<string[]>();

        // If no whitelist configured, allow all (dev mode)
        if (allowedIps == null || allowedIps.Length == 0)
        {
            await next();
            return;
        }

        // Resolve the real client IP, accounting for NGINX reverse proxy.
        // NGINX sets X-Real-IP with the original client IP; fall back to direct connection IP.
        var remoteIp =
            context.HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault()
            ?? context.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? context.HttpContext.Connection.RemoteIpAddress?.ToString();

        // Normalise IPv6 loopback and IPv4-mapped IPv6 (::ffff:x.x.x.x → x.x.x.x)
        if (remoteIp == "::1") remoteIp = "127.0.0.1";
        if (remoteIp != null && remoteIp.StartsWith("::ffff:")) remoteIp = remoteIp[7..];

        if (string.IsNullOrEmpty(remoteIp) || !allowedIps.Contains(remoteIp))
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<IpWhitelistAttribute>>();
            logger.LogWarning("SECURITY: Blocked superadmin access from IP {RemoteIp}. Allowed: {AllowedIps}",
                remoteIp, string.Join(", ", allowedIps));

            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await next();
    }
}
