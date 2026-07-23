using DevHunt.CoreApi.Services.Moderation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevHunt.CoreApi.Filters;

/// <summary>
/// Action filter that CENSORS profanity in request DTOs (replaces with ******).
/// Used for documents and other content where we allow posting but sanitize the text.
/// </summary>
public class ProfanityCensorFilter : ActionFilterAttribute
{
    private readonly IProfanityFilterService _profanityService;

    public ProfanityCensorFilter(IProfanityFilterService profanityService)
    {
        _profanityService = profanityService;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            _profanityService.CensorDto(arg);
        }
    }
}
