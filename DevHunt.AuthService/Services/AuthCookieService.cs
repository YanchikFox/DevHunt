using Microsoft.AspNetCore.Http;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Service for managing authentication cookies.
/// </summary>
public interface IAuthCookieService
{
    /// <summary>Sets httpOnly cookies for access and refresh tokens.</summary>
    void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken);

    /// <summary>Clears authentication cookies on logout.</summary>
    void ClearAuthCookies(HttpResponse response);
}

/// <summary>
/// Implementation of authentication cookie service.
/// R7: Secure token storage with httpOnly cookies to prevent XSS attacks.
/// </summary>
public class AuthCookieService : IAuthCookieService
{
    private readonly bool _isProduction;

    /// <summary>
    /// Determines whether auth cookies should require HTTPS based on the hosting environment.
    /// </summary>
    public AuthCookieService(IWebHostEnvironment env)
    {
        // L-10: Use IWebHostEnvironment instead of config key
        _isProduction = !env.IsDevelopment();
    }

    /// <inheritdoc />
    public void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken)
    {
        // Access token cookie (30 minutes)
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,  // XSS protection - JS cannot access
            Secure = _isProduction,  // HTTPS only in production
            SameSite = SameSiteMode.Lax,  // Allow cross-site on top-level navigation
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        response.Cookies.Append("access_token", accessToken, accessCookieOptions);

        // Refresh token cookie (7 days)
        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = _isProduction,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        response.Cookies.Append("refresh_token", refreshToken, refreshCookieOptions);
    }

    /// <inheritdoc />
    public void ClearAuthCookies(HttpResponse response)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = _isProduction,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(-1)  // Expired = delete
        };

        response.Cookies.Append("access_token", "", cookieOptions);
        response.Cookies.Append("refresh_token", "", cookieOptions);
    }
}
