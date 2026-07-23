using DevHunt.CoreApi.Services.Moderation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevHunt.CoreApi.Filters;

/// <summary>
/// Action filter that BLOCKS requests containing profanity (for forms: bio, description, reviews, etc.).
/// Returns 400 with a clear error message.
/// </summary>
public class ProfanityFilter : ActionFilterAttribute
{
    private readonly IProfanityFilterService _profanityService;

    public ProfanityFilter(IProfanityFilterService profanityService)
    {
        _profanityService = profanityService;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var violatedFields = new List<string>();

        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;

            var violations = _profanityService.CheckDto(arg);
            foreach (var v in violations)
            {
                violatedFields.Add(v.FieldName);
            }
        }

        if (violatedFields.Count > 0)
        {
            var fields = string.Join(", ", violatedFields.Distinct());
            context.Result = new BadRequestObjectResult(new
            {
                error = "profanity_detected",
                message = $"Your text contains inappropriate language. Please revise the following fields: {fields}",
                fields = violatedFields.Distinct()
            });
        }
    }
}
