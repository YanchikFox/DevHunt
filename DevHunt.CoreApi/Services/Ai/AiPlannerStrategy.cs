namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Versioned adapter that calls the external ML service to produce tech stacks and plan drafts.
/// Implementations map ML DTOs into domain models consumed by <see cref="AiPlanningService"/>.
/// </summary>
public interface IAiPlannerStrategy
{
    /// <summary>Strategy version string (for example <c>v1</c>) used for routing and usage logs.</summary>
    string Version { get; }

    /// <summary>
    /// Requests ranked technology stack options for a project idea.
    /// </summary>
    /// <param name="ctx">Caller and goal context.</param>
    /// <param name="idea">Sanitized project idea.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tech-stack draft including token usage metadata.</returns>
    Task<AiTechStackDraft> SuggestTechStackAsync(AiContext ctx, string idea, CancellationToken ct);

    /// <summary>
    /// Generates a full phased plan draft for an idea and tech stack.
    /// </summary>
    /// <param name="ctx">Caller and goal context.</param>
    /// <param name="req">Idea, stack, tags, and roles for the planner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Plan draft to validate and persist.</returns>
    Task<AiPlanDraft> GeneratePlanDraftAsync(AiContext ctx, PlanDraftRequest req, CancellationToken ct);

    /// <summary>
    /// Revises an existing plan JSON using natural-language instructions.
    /// </summary>
    /// <param name="ctx">Caller and goal context.</param>
    /// <param name="req">Base idea and stack context.</param>
    /// <param name="currentPlan">Current plan object forwarded to the ML service.</param>
    /// <param name="instructions">User refinement instructions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Refined plan draft.</returns>
    Task<AiPlanDraft> RefinePlanDraftAsync(AiContext ctx, PlanDraftRequest req, object currentPlan, string instructions, CancellationToken ct);

    /// <summary>
    /// Revises tech-stack options using natural-language instructions.
    /// </summary>
    /// <param name="ctx">Caller and goal context.</param>
    /// <param name="idea">Project idea.</param>
    /// <param name="currentOptions">Current options object forwarded to the ML service.</param>
    /// <param name="instructions">User refinement instructions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Refined tech-stack draft.</returns>
    Task<AiTechStackDraft> RefineTechStackAsync(AiContext ctx, string idea, object currentOptions, string instructions, CancellationToken ct);
}
