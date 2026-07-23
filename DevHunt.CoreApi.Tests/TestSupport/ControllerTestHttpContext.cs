using DevHunt.CoreApi.Services.Badges;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DevHunt.CoreApi.Tests.TestSupport;

/// <summary>
/// Builds <see cref="HttpContext"/> instances with a minimal DI graph for controller unit tests.
/// </summary>
internal static class ControllerTestHttpContext
{
    /// <summary>
    /// Creates an HTTP context with optional user principal and achievement-check services.
    /// </summary>
    /// <param name="user">Authenticated user principal, if any.</param>
    /// <returns>Configured HTTP context for controller tests.</returns>
    public static HttpContext Create(System.Security.Claims.ClaimsPrincipal? user = null)
    {
        var services = new ServiceCollection();
        services.AddControllers();
        services.AddLogging();
        services.AddScoped(_ => Mock.Of<IAchievementTriggerService>());
        var provider = services.BuildServiceProvider();

        return new DefaultHttpContext
        {
            User = user ?? new System.Security.Claims.ClaimsPrincipal(),
            RequestServices = provider,
        };
    }
}
