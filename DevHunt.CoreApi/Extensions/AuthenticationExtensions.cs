using DevHunt.CoreApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DevHunt.CoreApi.Extensions;

/// <summary>
/// Extension methods for authentication and authorization configuration
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Configures JWT authentication
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
                };

                // JWT configuration for SignalR (token is passed via query string or header)
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        // R7: Priority 1 - Check httpOnly cookie (most secure)
                        if (string.IsNullOrEmpty(context.Token))
                        {
                            context.Token = context.Request.Cookies["access_token"];
                        }

                        // SECURITY: Extract token from query string for SignalR hubs
                        // SignalR WebSocket connections can't set Authorization header or cookies, so token is passed via query string
                        if (string.IsNullOrEmpty(context.Token) && !string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/notificationHub")))
                        {
                            context.Token = accessToken;
                        }
                        // Priority 3 - Authorization header for HTTP requests (backward compatibility with mobile apps/APIs)
                        else if (string.IsNullOrEmpty(context.Token))
                        {
                            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                            {
                                context.Token = authHeader.Substring("Bearer ".Length).Trim();
                            }
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        // SECURITY: Log authentication failures for SignalR
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning("JWT authentication failed for SignalR connection: {Error}", context.Exception.Message);
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Configures authorization policies
    /// </summary>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRoles.Admin));
            options.AddPolicy("AdminOrCurator", policy => policy.RequireRole(UserRoles.Admin, UserRoles.Curator));
            options.AddPolicy("ProjectOwner", policy => policy.RequireAssertion(context =>
                context.User.HasClaim(System.Security.Claims.ClaimTypes.Role, UserRoles.Admin) ||
                context.User.HasClaim(System.Security.Claims.ClaimTypes.Role, UserRoles.Curator) ||
                context.User.Identity?.IsAuthenticated == true)); // detailed check is performed in the controller
        });

        return services;
    }
}

