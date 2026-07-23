using DevHunt.CoreApi.Services;

namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// <c>v1</c> planner strategy that delegates to <see cref="IMLServiceClient"/> and maps responses
/// into <see cref="AiPlanDraft"/> and <see cref="AiTechStackDraft"/> models.
/// </summary>
public sealed class V1AiPlannerStrategy : IAiPlannerStrategy
{
    private readonly IMLServiceClient _mlServiceClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="V1AiPlannerStrategy"/> class.
    /// </summary>
    /// <param name="mlServiceClient">HTTP client for the ML planning service.</param>
    public V1AiPlannerStrategy(IMLServiceClient mlServiceClient)
    {
        _mlServiceClient = mlServiceClient;
    }

    /// <inheritdoc />
    public string Version => "v1";

    /// <inheritdoc />
    public async Task<AiTechStackDraft> SuggestTechStackAsync(AiContext ctx, string idea, CancellationToken ct)
    {
        var response = await _mlServiceClient.GenerateTechStackAsync(idea, ctx.Goals);
        return new AiTechStackDraft(
            response.Version,
            response.Options?.Select(MapTechStackOption).ToList() ?? new List<AiTechStackOption>(),
            response.PromptVersion,
            response.Provider,
            response.Model,
            response.PromptTokens,
            response.CompletionTokens,
            response.TotalTokens
        );
    }

    /// <inheritdoc />
    public async Task<AiPlanDraft> GeneratePlanDraftAsync(AiContext ctx, PlanDraftRequest req, CancellationToken ct)
    {
        var draft = await _mlServiceClient.GeneratePlanAsync(
            req.Idea,
            req.TechStack,
            req.CustomTags,
            req.CustomRoles,
            ctx.Goals);
        return MapPlanDraft(draft);
    }

    /// <inheritdoc />
    public async Task<AiPlanDraft> RefinePlanDraftAsync(AiContext ctx, PlanDraftRequest req, object currentPlan, string instructions, CancellationToken ct)
    {
        var draft = await _mlServiceClient.RefinePlanAsync(
            req.Idea,
            req.TechStack,
            currentPlan,
            instructions,
            req.CustomTags,
            req.CustomRoles,
            ctx.Goals);
        return MapPlanDraft(draft);
    }

    /// <inheritdoc />
    public async Task<AiTechStackDraft> RefineTechStackAsync(AiContext ctx, string idea, object currentOptions, string instructions, CancellationToken ct)
    {
        var response = await _mlServiceClient.RefineTechStackAsync(idea, currentOptions, instructions, ctx.Goals);
        return new AiTechStackDraft(
            response.Version,
            response.Options?.Select(MapTechStackOption).ToList() ?? new List<AiTechStackOption>(),
            response.PromptVersion,
            response.Provider,
            response.Model,
            response.PromptTokens,
            response.CompletionTokens,
            response.TotalTokens
        );
    }

    /// <summary>Maps an ML tech-stack option DTO to the domain model.</summary>
    private static AiTechStackOption MapTechStackOption(AITechStackOptionDto option)
    {
        return new AiTechStackOption(
            option.Name,
            option.Description,
            option.Pros ?? new List<string>(),
            option.Cons ?? new List<string>()
        );
    }

    /// <summary>Maps an ML plan draft DTO to <see cref="AiPlanDraft"/>.</summary>
    private static AiPlanDraft MapPlanDraft(AiPlanDraftDto draft)
    {
        return new AiPlanDraft(
            draft.Version,
            draft.Idea,
            draft.TechStack,
            draft.Phases?.Select(MapPhase).ToList() ?? new List<AiPlanPhase>(),
            draft.PromptVersion,
            draft.Provider,
            draft.Model,
            draft.PromptTokens,
            draft.CompletionTokens,
            draft.TotalTokens
        );
    }

    /// <summary>Maps a phase DTO including nested tasks.</summary>
    private static AiPlanPhase MapPhase(AiPlanPhaseDto phase)
    {
        return new AiPlanPhase(
            phase.Id,
            phase.Name,
            phase.Description,
            phase.Goals ?? new List<string>(),
            phase.Tasks?.Select(MapTask).ToList() ?? new List<AiPlanTask>()
        );
    }

    /// <summary>Maps a task DTO with default-empty dependency and tag lists.</summary>
    private static AiPlanTask MapTask(AiPlanTaskDto task)
    {
        return new AiPlanTask(
            task.Id,
            task.Title,
            task.Description,
            task.DependsOn ?? new List<string>(),
            task.Priority,
            task.Tags ?? new List<string>()
        );
    }
}
