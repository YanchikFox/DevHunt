using DevHunt.CoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevHunt.CoreApi.Filters;

/// <summary>
/// Blocks a controller or action when the specified feature flag is disabled.
/// Returns 503 Service Unavailable so clients can show a "feature disabled" message.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequireFeatureFlagAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _key;

    public RequireFeatureFlagAttribute(string key) => _key = key;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var flags = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagService>();
        if (!await flags.IsEnabledAsync(_key))
        {
            context.Result = new ObjectResult(new { error = "This feature is currently disabled by the platform." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }
        await next();
    }
}
