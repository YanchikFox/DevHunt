using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Exposes project-scoped AI planning endpoints for generating, refining, reading, and applying plans.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/ai/plans")]
[Authorize]
[RequireFeatureFlag("ai_features")]
public class AiPlansController : ControllerBase
{
    private readonly IAiPlanningService _planningService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiPlansController"/> class.
    /// </summary>
    /// <param name="planningService">Service that performs project access checks and AI planning operations.</param>
    public AiPlansController(IAiPlanningService planningService)
    {
        _planningService = planningService;
    }

    /// <summary>
    /// Generates a localized technology stack recommendation for a project.
    /// </summary>
    /// <param name="projectId">Project to plan against.</param>
    /// <param name="request">Idea, constraints, and preferences for the recommendation.</param>
    /// <param name="locale">Optional language header used for localized AI output; defaults to English.</param>
    /// <param name="cancellationToken">Cancellation token for the planning request.</param>
    /// <returns>
    /// The recommended stack when <see cref="AiPlanningResultType.Ok"/> is returned; otherwise an error response from
    /// the shared <see cref="AiControllerHelper"/> mapping.
    /// </returns>
    [HttpPost("tech-stack")]
    public Task<IActionResult> GenerateTechStack(
        Guid projectId,
        [FromBody] GenerateTechStackRequest request,
        [FromHeader(Name = "Accept-Language")] string? locale,
        CancellationToken cancellationToken)
    {
        return AiControllerHelper.ExecuteAiAction(User, (userId, isAdmin) =>
            _planningService.GenerateTechStackAsync(projectId, userId, isAdmin, request, locale ?? "en", cancellationToken));
    }

    /// <summary>
    /// Generates a project implementation plan for the authenticated user.
    /// </summary>
    /// <param name="projectId">Project to plan against.</param>
    /// <param name="request">Planning prompt, selected stack, and project constraints.</param>
    /// <param name="locale">Optional language header used for localized AI output; defaults to English.</param>
    /// <param name="cancellationToken">Cancellation token for the planning request.</param>
    /// <returns>The generated plan, or a mapped not-found, forbidden, bad-request, conflict, rate-limit, or AI-response error.</returns>
    [HttpPost]
    public Task<IActionResult> GeneratePlan(
        Guid projectId,
        [FromBody] GenerateAiPlanRequest request,
        [FromHeader(Name = "Accept-Language")] string? locale,
        CancellationToken cancellationToken)
    {
        return AiControllerHelper.ExecuteAiAction(User, (userId, isAdmin) =>
            _planningService.GeneratePlanAsync(projectId, userId, isAdmin, request, locale ?? "en", cancellationToken));
    }

    /// <summary>
    /// Retrieves a previously generated AI plan for a project.
    /// </summary>
    /// <param name="projectId">Project that owns the plan.</param>
    /// <param name="planId">Plan to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for the lookup.</param>
    /// <returns>The plan payload for authorized users, or a mapped error when the project or plan cannot be used.</returns>
    [HttpGet("{planId:guid}")]
    public Task<IActionResult> GetPlan(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        return AiControllerHelper.ExecuteAiAction(User, (userId, isAdmin) =>
            _planningService.GetPlanAsync(projectId, planId, userId, isAdmin, cancellationToken));
    }

    /// <summary>
    /// Applies a generated AI plan to its project by delegating the write operation to the planning service.
    /// </summary>
    /// <param name="projectId">Project that will receive the plan changes.</param>
    /// <param name="planId">Plan to apply.</param>
    /// <param name="cancellationToken">Cancellation token for the apply operation.</param>
    /// <returns>The apply result, or a mapped error if the user lacks access or the plan is not applicable.</returns>
    [HttpPost("{planId:guid}/apply")]
    public Task<IActionResult> ApplyPlan(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        return AiControllerHelper.ExecuteAiAction(User, (userId, isAdmin) =>
            _planningService.ApplyPlanAsync(projectId, planId, userId, isAdmin, cancellationToken));
    }

    /// <summary>
    /// Refines an existing or proposed project plan using additional user instructions.
    /// </summary>
    /// <param name="projectId">Project whose plan is being refined.</param>
    /// <param name="request">Current plan content and refinement prompt.</param>
    /// <param name="locale">Optional language header used for localized AI output; defaults to English.</param>
    /// <param name="cancellationToken">Cancellation token for the refinement request.</param>
    /// <returns>The refined plan, or a mapped planning error.</returns>
    [HttpPost("refine")]
    public Task<IActionResult> RefinePlan(
        Guid projectId,
        [FromBody] RefinePlanRequest request,
        [FromHeader(Name = "Accept-Language")] string? locale,
        CancellationToken cancellationToken)
    {
        return AiControllerHelper.ExecuteAiAction(User, (userId, isAdmin) =>
            _planningService.RefinePlanAsync(projectId, userId, isAdmin, request, locale ?? "en", cancellationToken));
    }

    /// <summary>
    /// Refines a generated technology stack recommendation with additional constraints or feedback.
    /// </summary>
    /// <param name="projectId">Project whose stack recommendation is being refined.</param>
    /// <param name="request">Current recommendation and refinement prompt.</param>
    /// <param name="locale">Optional language header used for localized AI output; defaults to English.</param>
    /// <param name="cancellationToken">Cancellation token for the refinement request.</param>
    /// <returns>The refined stack recommendation, or a mapped planning error.</returns>
    [HttpPost("tech-stack/refine")]
    public Task<IActionResult> RefineTechStack(
        Guid projectId,
        [FromBody] RefineTechStackRequest request,
        [FromHeader(Name = "Accept-Language")] string? locale,
        CancellationToken cancellationToken)
    {
        return AiControllerHelper.ExecuteAiAction(User, (userId, isAdmin) =>
            _planningService.RefineTechStackAsync(projectId, userId, isAdmin, request, locale ?? "en", cancellationToken));
    }

    /// <summary>
    /// Generates a project diagram from the supplied architecture or planning request.
    /// </summary>
    /// <param name="projectId">Project used for authorization and context.</param>
    /// <param name="request">Diagram prompt and format options.</param>
    /// <param name="cancellationToken">Cancellation token for diagram generation.</param>
    /// <returns>The generated diagram payload, or a mapped planning error.</returns>
    [HttpPost("diagram")]
    public Task<IActionResult> GenerateDiagram(
        Guid projectId,
        [FromBody] GenerateDiagramRequest request,
        CancellationToken cancellationToken)
    {
        // I-03: Moved business logic to service layer; uses standard AiControllerHelper flow
        return AiControllerHelper.ExecuteAiAction(User, (userId, isAdmin) =>
            _planningService.GenerateDiagramAsync(projectId, userId, isAdmin, request, cancellationToken));
    }
}
