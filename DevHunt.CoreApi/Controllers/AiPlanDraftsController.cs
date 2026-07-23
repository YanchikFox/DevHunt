using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Pre-project AI drafting endpoints. Everything here is ephemeral: no DB
/// writes, no usage metering (which is project-scoped). The workflow is:
/// user sketches an idea → AI drafts a stack + plan → user hits
/// "Create project from plan" which materialises a real project and
/// (separately) applies the plan through <see cref="AiPlansController"/>.
/// </summary>
[ApiController]
[Route("api/ai/plans/draft")]
[Authorize]
[RequireFeatureFlag("ai_features")]
public class AiPlanDraftsController : ControllerBase
{
    private readonly IAiPlanDraftService _draftService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiPlanDraftsController"/> class.
    /// </summary>
    /// <param name="draftService">Service that generates and refines pre-project AI drafts.</param>
    public AiPlanDraftsController(IAiPlanDraftService draftService)
    {
        _draftService = draftService;
    }

    /// <summary>
    /// Generates an ephemeral AI project plan draft for the authenticated user.
    /// </summary>
    /// <param name="request">Initial idea and constraints for the draft plan.</param>
    /// <param name="locale">Optional language header used for localized AI output; defaults to English.</param>
    /// <param name="cancellationToken">Cancellation token for draft generation.</param>
    /// <returns>The generated draft payload, 401 when the user claim is absent, or a mapped AI planning error.</returns>
    [HttpPost]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateAiPlanDraftRequest request,
        [FromHeader(Name = "Accept-Language")] string? locale,
        CancellationToken cancellationToken)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();

        var result = await _draftService.GenerateAsync(userId.Value, request, locale ?? "en", cancellationToken);
        return result.Type == AiPlanningResultType.Ok
            ? Ok(result.Payload)
            : AiControllerHelper.MapErrorResult(result);
    }

    /// <summary>
    /// Refines an ephemeral AI plan draft with follow-up user instructions.
    /// </summary>
    /// <param name="request">Existing draft content plus refinement instructions.</param>
    /// <param name="locale">Optional language header used for localized AI output; defaults to English.</param>
    /// <param name="cancellationToken">Cancellation token for draft refinement.</param>
    /// <returns>The refined draft payload, 401 when the user claim is absent, or a mapped AI planning error.</returns>
    [HttpPost("refine")]
    public async Task<IActionResult> Refine(
        [FromBody] RefineAiPlanDraftRequest request,
        [FromHeader(Name = "Accept-Language")] string? locale,
        CancellationToken cancellationToken)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();

        var result = await _draftService.RefineAsync(userId.Value, request, locale ?? "en", cancellationToken);
        return result.Type == AiPlanningResultType.Ok
            ? Ok(result.Payload)
            : AiControllerHelper.MapErrorResult(result);
    }

    /// <summary>
    /// Suggests an ephemeral technology stack for a pre-project plan draft.
    /// </summary>
    /// <param name="request">Idea and constraints used to choose technologies.</param>
    /// <param name="locale">Optional language header used for localized AI output; defaults to English.</param>
    /// <param name="cancellationToken">Cancellation token for stack suggestion.</param>
    /// <returns>The suggested stack payload, 401 when the user claim is absent, or a mapped AI planning error.</returns>
    [HttpPost("tech-stack")]
    public async Task<IActionResult> SuggestTechStack(
        [FromBody] GenerateAiPlanDraftTechStackRequest request,
        [FromHeader(Name = "Accept-Language")] string? locale,
        CancellationToken cancellationToken)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();

        var result = await _draftService.SuggestTechStackAsync(userId.Value, request, locale ?? "en", cancellationToken);
        return result.Type == AiPlanningResultType.Ok
            ? Ok(result.Payload)
            : AiControllerHelper.MapErrorResult(result);
    }
}
