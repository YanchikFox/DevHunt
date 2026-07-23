using DevHunt.CoreApi.Security;
using Microsoft.Extensions.Options;
using static DevHunt.CoreApi.Services.Ai.AiPlanningMapper;

namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Pre-project plan drafting. Generates, refines and suggests tech-stacks
/// entirely in-memory — no DB rows, no usage-meter rows (metering is keyed
/// by projectId, which we don't have yet). Anyone with
/// <c>ai_features</c> can call it; the volume is guarded by the upstream ML
/// service rate limits plus the controller's <c>[Authorize]</c> attribute.
/// </summary>
public sealed class AiPlanDraftService : IAiPlanDraftService
{
    private readonly IAiStrategyRouter _strategyRouter;
    private readonly IAiPlanValidator _validator;
    private readonly AiPlanningOptions _options;
    private readonly ILogger<AiPlanDraftService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiPlanDraftService"/> class.
    /// </summary>
    /// <param name="strategyRouter">Router for versioned AI planner strategies.</param>
    /// <param name="validator">Validator for AI-generated plan structure.</param>
    /// <param name="options">Configuration options for AI plan limits.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public AiPlanDraftService(
        IAiStrategyRouter strategyRouter,
        IAiPlanValidator validator,
        IOptions<AiPlanningOptions> options,
        ILogger<AiPlanDraftService> logger)
    {
        _strategyRouter = strategyRouter;
        _validator = validator;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generates an ephemeral plan draft, inferring tech stack from the first suggestion when omitted.
    /// Does not persist plan or usage rows (no project id yet).
    /// </summary>
    public async Task<AiPlanningResult<AiPlanDraftResponseDto>> GenerateAsync(
        Guid userId,
        GenerateAiPlanDraftRequest request,
        string locale,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Idea))
            return AiPlanningResult<AiPlanDraftResponseDto>.BadRequest("Idea is required.");

        var sanitizedIdea = SecurityHelpers.SanitizePromptInput(request.Idea);
        var sanitizedTechStack = string.IsNullOrWhiteSpace(request.TechStack)
            ? null
            : SecurityHelpers.SanitizePromptInput(request.TechStack!);

        IAiPlannerStrategy strategy;
        try { strategy = _strategyRouter.Resolve(request.Version); }
        catch (AiStrategyNotFoundException ex)
        {
            return AiPlanningResult<AiPlanDraftResponseDto>.BadRequest(ex.Message);
        }

        var ctx = new AiContext(userId, Guid.Empty, NormalizeLocale(locale), null, request.Goals);

        try
        {
            // If the caller didn't specify a tech stack, the ML service picks the
            // first suggested option — avoids forcing them through a separate
            // suggest → pick → generate round trip for the common "just give me
            // a plan from an idea" case.
            var resolvedTechStack = sanitizedTechStack;
            if (string.IsNullOrWhiteSpace(resolvedTechStack))
            {
                var techStackDraft = await strategy.SuggestTechStackAsync(ctx, sanitizedIdea, cancellationToken);
                var first = techStackDraft.Options.FirstOrDefault();
                resolvedTechStack = string.IsNullOrWhiteSpace(first?.Name)
                    ? "General-purpose web stack"
                    : first!.Name;
            }

            var planDraft = await strategy.GeneratePlanDraftAsync(ctx,
                new PlanDraftRequest(sanitizedIdea, resolvedTechStack!, strategy.Version,
                    request.CustomTags, request.CustomRoles, request.Goals),
                cancellationToken);

            var validation = _validator.Validate(planDraft);
            if (!validation.IsValid)
            {
                _logger.LogWarning("AI plan draft validation failed for user {UserId}: {Errors}",
                    userId, string.Join(" | ", validation.Errors));
                return AiPlanningResult<AiPlanDraftResponseDto>.InvalidAi(
                    "Invalid AI plan response", validation.Errors);
            }

            return AiPlanningResult<AiPlanDraftResponseDto>.Ok(new AiPlanDraftResponseDto(
                TrimToLength(sanitizedIdea.Trim(), _options.MaxIdeaLength),
                TrimToLength(resolvedTechStack!.Trim(), _options.MaxTechStackLength),
                planDraft.Version,
                planDraft));
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "AI rate limit while drafting plan for user {UserId}", userId);
            return AiPlanningResult<AiPlanDraftResponseDto>.RateLimit(
                "AI service rate limit exceeded. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to draft AI plan for user {UserId}", userId);
            return AiPlanningResult<AiPlanDraftResponseDto>.ErrorResult(
                "Unable to draft plan. Please try again.");
        }
    }

    /// <summary>
    /// Refines an in-memory plan draft from user instructions without persisting a project plan row.
    /// </summary>
    public async Task<AiPlanningResult<AiPlanDraftResponseDto>> RefineAsync(
        Guid userId,
        RefineAiPlanDraftRequest request,
        string locale,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Idea) || string.IsNullOrWhiteSpace(request.TechStack))
            return AiPlanningResult<AiPlanDraftResponseDto>.BadRequest("Idea and TechStack are required.");
        if (string.IsNullOrWhiteSpace(request.Instructions))
            return AiPlanningResult<AiPlanDraftResponseDto>.BadRequest("Instructions are required.");

        var sanitizedIdea = SecurityHelpers.SanitizePromptInput(request.Idea);
        var sanitizedTechStack = SecurityHelpers.SanitizePromptInput(request.TechStack);
        var sanitizedInstructions = SecurityHelpers.SanitizePromptInput(request.Instructions);

        IAiPlannerStrategy strategy;
        try { strategy = _strategyRouter.Resolve(request.Version); }
        catch (AiStrategyNotFoundException ex)
        {
            return AiPlanningResult<AiPlanDraftResponseDto>.BadRequest(ex.Message);
        }

        var ctx = new AiContext(userId, Guid.Empty, NormalizeLocale(locale), null, request.Goals);

        try
        {
            var planDraft = await strategy.RefinePlanDraftAsync(ctx,
                new PlanDraftRequest(sanitizedIdea, sanitizedTechStack, strategy.Version,
                    request.CustomTags, request.CustomRoles, request.Goals),
                request.CurrentPlan,
                sanitizedInstructions,
                cancellationToken);

            var validation = _validator.Validate(planDraft);
            if (!validation.IsValid)
            {
                _logger.LogWarning("AI plan draft refine validation failed for user {UserId}: {Errors}",
                    userId, string.Join(" | ", validation.Errors));
                return AiPlanningResult<AiPlanDraftResponseDto>.InvalidAi(
                    "Invalid AI plan response", validation.Errors);
            }

            return AiPlanningResult<AiPlanDraftResponseDto>.Ok(new AiPlanDraftResponseDto(
                TrimToLength(sanitizedIdea.Trim(), _options.MaxIdeaLength),
                TrimToLength(sanitizedTechStack.Trim(), _options.MaxTechStackLength),
                planDraft.Version,
                planDraft));
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "AI rate limit while refining draft for user {UserId}", userId);
            return AiPlanningResult<AiPlanDraftResponseDto>.RateLimit(
                "AI service rate limit exceeded. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refine AI plan draft for user {UserId}", userId);
            return AiPlanningResult<AiPlanDraftResponseDto>.ErrorResult(
                "Unable to refine plan. Please try again.");
        }
    }

    /// <summary>
    /// Suggests tech-stack options for the pre-project draft workspace.
    /// </summary>
    public async Task<AiPlanningResult<TechStackResponseDto>> SuggestTechStackAsync(
        Guid userId,
        GenerateAiPlanDraftTechStackRequest request,
        string locale,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Idea))
            return AiPlanningResult<TechStackResponseDto>.BadRequest("Idea is required.");

        var sanitizedIdea = SecurityHelpers.SanitizePromptInput(request.Idea);

        IAiPlannerStrategy strategy;
        try { strategy = _strategyRouter.Resolve(request.Version); }
        catch (AiStrategyNotFoundException ex)
        {
            return AiPlanningResult<TechStackResponseDto>.BadRequest(ex.Message);
        }

        var ctx = new AiContext(userId, Guid.Empty, NormalizeLocale(locale), null, request.Goals);

        try
        {
            var draft = await strategy.SuggestTechStackAsync(ctx, sanitizedIdea, cancellationToken);
            return AiPlanningResult<TechStackResponseDto>.Ok(MapTechStack(draft));
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "AI rate limit while suggesting tech stack for user {UserId}", userId);
            return AiPlanningResult<TechStackResponseDto>.RateLimit(
                "AI service rate limit exceeded. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suggest tech stack for user {UserId}", userId);
            return AiPlanningResult<TechStackResponseDto>.ErrorResult(
                "Unable to suggest tech stack. Please try again.");
        }
    }
}
