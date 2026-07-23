using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Middleware;

public class UserActiveCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserActiveCheckMiddleware> _logger;

    public UserActiveCheckMiddleware(RequestDelegate next, ILogger<UserActiveCheckMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, DevHuntDbContext db)
    {
        var ct = context.RequestAborted;
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
                if (user != null)
                {
                    if (!user.IsActive)
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("Account is blocked");
                        return;
                    }

                    // Update last activity to drive live presence/last seen (throttled to 60s)
                    var now = DateTime.UtcNow;
                    if (user.LastLogin == null || (now - user.LastLogin.Value).TotalSeconds > 60)
                    {
                        user.LastLogin = now;
                        try
                        {
                            await db.SaveChangesAsync(ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to update user last activity for {UserId}", userId);
                        }
                    }
                }
            }
        }

        await _next(context);
    }
}
