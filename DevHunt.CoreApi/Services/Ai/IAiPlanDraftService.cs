namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Pre-project (ephemeral) AI plan drafting. Lets any signed-in user sketch
/// a plan in the "AI Plan" workspace without first owning a project row —
/// results are returned in-memory and materialised via a separate
/// "Create project from plan" action.
/// </summary>
public interface IAiPlanDraftService
{
    Task<AiPlanningResult<AiPlanDraftResponseDto>> GenerateAsync(
        Guid userId,
        GenerateAiPlanDraftRequest request,
        string locale,
        CancellationToken cancellationToken);

    Task<AiPlanningResult<AiPlanDraftResponseDto>> RefineAsync(
        Guid userId,
        RefineAiPlanDraftRequest request,
        string locale,
        CancellationToken cancellationToken);

    Task<AiPlanningResult<TechStackResponseDto>> SuggestTechStackAsync(
        Guid userId,
        GenerateAiPlanDraftTechStackRequest request,
        string locale,
        CancellationToken cancellationToken);
}
