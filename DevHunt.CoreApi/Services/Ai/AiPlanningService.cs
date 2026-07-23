using System.Diagnostics;
using System.Text.Json;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static DevHunt.CoreApi.Services.Ai.AiPlanningMapper;

namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Orchestrates project-scoped AI planning: authorization, capability checks, usage metering,
/// strategy dispatch, validation, persistence, and plan apply. Called by AI planning controllers.
/// </summary>
public sealed class AiPlanningService : IAiPlanningService
{
    private readonly DevHuntDbContext _db;
    private readonly IAiStrategyRouter _strategyRouter;
    private readonly IAiCapabilityPolicy _capabilityPolicy;
    private readonly IAiUsageMeter _usageMeter;
    private readonly IProjectPermissionService _permissionService;
    private readonly IAiPlanValidator _validator;
    private readonly IAiPlanApplier _applier;
    private readonly IMLServiceClient _mlServiceClient;
    private readonly AiPlanningOptions _options;
    private readonly ILogger<AiPlanningService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiPlanningService"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="strategyRouter">Router for versioned AI planner strategies.</param>
    /// <param name="capabilityPolicy">Policy that gates AI capability usage.</param>
    /// <param name="usageMeter">Usage meter for AI operation audit logs.</param>
    /// <param name="permissionService">Project permission service for authorization checks.</param>
    /// <param name="validator">Validator for AI-generated plan structure.</param>
    /// <param name="applier">Service that applies validated plans to boards.</param>
    /// <param name="mlServiceClient">Client for the external ML planning service.</param>
    /// <param name="options">Configuration options for AI plan limits.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public AiPlanningService(
        DevHuntDbContext db,
        IAiStrategyRouter strategyRouter,
        IAiCapabilityPolicy capabilityPolicy,
        IAiUsageMeter usageMeter,
        IProjectPermissionService permissionService,
        IAiPlanValidator validator,
        IAiPlanApplier applier,
        IMLServiceClient mlServiceClient,
        IOptions<AiPlanningOptions> options,
        ILogger<AiPlanningService> logger)
    {
        _db = db;
        _strategyRouter = strategyRouter;
        _capabilityPolicy = capabilityPolicy;
        _usageMeter = usageMeter;
        _permissionService = permissionService;
        _validator = validator;
        _applier = applier;
        _mlServiceClient = mlServiceClient;
        _options = options.Value;
        _logger = logger;
    }

    // ── Common validation helper ──────────────────────────────────────────

    /// <summary>Resolved strategy and AI context after permission and capability checks succeed.</summary>
    private sealed record ValidatedContext(IAiPlannerStrategy Strategy, AiContext AiContext);

    /// <summary>
    /// Verifies project membership, task permissions, capability policy, and resolves the planner strategy.
    /// Returns an early <see cref="AiPlanningResult{T}"/> on failure instead of throwing.
    /// </summary>
    private async Task<(ValidatedContext? Ctx, AiPlanningResult<T>? Early)> ValidateAndResolveAsync<T>(
        Guid projectId, Guid userId, bool isAdmin,
        AiCapability capability, string? version, string locale, TechStackGoals? goals)
    {
        var perms = await _permissionService.GetPermissionsAsync(projectId, userId, isAdmin);
        if (perms == null)
            return (null, AiPlanningResult<T>.NotFound("Project not found"));
        if (!perms.CanManageTasks)
            return (null, AiPlanningResult<T>.Forbidden("Insufficient permissions"));

        var cap = await _capabilityPolicy.CheckAsync(userId, projectId, capability);
        if (!cap.Allowed)
            return (null, AiPlanningResult<T>.Forbidden(cap.DenyReason ?? "AI capability denied"));

        IAiPlannerStrategy strategy;
        try { strategy = _strategyRouter.Resolve(version); }
        catch (AiStrategyNotFoundException ex)
        {
            return (null, AiPlanningResult<T>.BadRequest(ex.Message));
        }

        var context = new AiContext(userId, projectId, NormalizeLocale(locale), null, goals);
        return (new ValidatedContext(strategy, context), null);
    }

    /// <summary>
    /// Wraps an AI operation with usage metering, rate-limit handling, and error logging.
    /// </summary>
    private async Task<AiPlanningResult<T>> WithUsageTrackingAsync<T>(
        Guid projectId,
        string operationName,
        Func<Func<AiUsageStart, Task>, Task<(AiPlanningResult<T> result, AiUsageResult usage)>> action,
        CancellationToken cancellationToken)
    {
        AiUsageScope? usageScope = null;
        var stopwatch = new Stopwatch();
        AiUsageResult? usageResult = null;

        try
        {
            var (result, usage) = await action(async start =>
            {
                usageScope = await _usageMeter.StartAsync(start, cancellationToken);
                stopwatch.Start();
            });
            usageResult = usage;
            return result;
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "AI rate limit for project {ProjectId} during {Op}",
                projectId, operationName);
            usageResult = new AiUsageResult(false, null, "rate_limit", ex.Message);
            return AiPlanningResult<T>.RateLimit("AI service rate limit exceeded. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed {Op} for project {ProjectId}", operationName, projectId);
            usageResult = new AiUsageResult(false, null, "exception", ex.Message);
            throw;
        }
        finally
        {
            if (usageScope != null)
            {
                usageResult ??= new AiUsageResult(false, null, "unknown_error", $"{operationName} failed");
                var finalized = usageResult with { LatencyMs = (int)stopwatch.ElapsedMilliseconds };
                await _usageMeter.CompleteAsync(usageScope, finalized, cancellationToken);
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a draft plan, validates it, persists a new <c>AiPlan</c> row, and records token usage.
    /// </summary>
    public async Task<AiPlanningResult<AiPlanResponseDto>> GeneratePlanAsync(
        Guid projectId, Guid userId, bool isAdmin,
        GenerateAiPlanRequest request, string locale, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Idea) || string.IsNullOrWhiteSpace(request.TechStack))
            return AiPlanningResult<AiPlanResponseDto>.BadRequest("Idea and TechStack are required.");

        // I-12: Sanitize user input to mitigate prompt injection
        var sanitizedIdea = Security.SecurityHelpers.SanitizePromptInput(request.Idea);
        var sanitizedTechStack = Security.SecurityHelpers.SanitizePromptInput(request.TechStack);

        var (ctx, early) = await ValidateAndResolveAsync<AiPlanResponseDto>(
            projectId, userId, isAdmin, AiCapability.PlanDraftGenerate, request.Version, locale, request.Goals);
        if (early != null) return early;
        var strategy = ctx!.Strategy;
        var context = ctx.AiContext;

        return await WithUsageTrackingAsync<AiPlanResponseDto>(projectId, "generate_plan", async startTracking =>
        {
            await startTracking(new AiUsageStart(projectId, userId, null,
                AiCapability.PlanDraftGenerate, strategy.Version, context.Locale, context.Tier));

            var planDraft = await strategy.GeneratePlanDraftAsync(context,
                new PlanDraftRequest(sanitizedIdea, sanitizedTechStack, strategy.Version,
                    request.CustomTags, request.CustomRoles, request.Goals),
                cancellationToken);

            var validation = _validator.Validate(planDraft);
            if (!validation.IsValid)
            {
                _logger.LogWarning("AI plan validation failed for project {ProjectId}: {Errors}",
                    projectId, string.Join(" | ", validation.Errors));
                return (AiPlanningResult<AiPlanResponseDto>.InvalidAi("Invalid AI plan response", validation.Errors),
                    new AiUsageResult(false, null, "ai_response_invalid",
                        string.Join(" | ", validation.Errors), BuildUsageMetadata(planDraft)));
            }

            var planJson = JsonSerializer.Serialize(planDraft, PlanJsonOptions);

            var plan = new AiPlan
            {
                Id = Guid.NewGuid(), ProjectId = projectId, CreatedByUserId = userId,
                Idea = TrimToLength(sanitizedIdea.Trim(), _options.MaxIdeaLength),
                TechStack = TrimToLength(sanitizedTechStack.Trim(), _options.MaxTechStackLength),
                Status = AiPlanStatus.Draft.Value,
                PlanVersion = planDraft.Version, PlanJson = planJson, CreatedAt = DateTime.UtcNow
            };
            _db.AiPlans.Add(plan);
            await _db.SaveChangesAsync(cancellationToken);

            return (AiPlanningResult<AiPlanResponseDto>.Ok(MapPlan(plan, planDraft)),
                new AiUsageResult(true, null, Metadata: BuildUsageMetadata(planDraft),
                    MetadataJson: JsonSerializer.Serialize(new {
                        planVersion = plan.PlanVersion,
                        phaseCount = planDraft.Phases.Count,
                        taskCount = planDraft.Phases.Sum(p => p.Tasks?.Count ?? 0)
                    }, PlanJsonOptions)));
        }, cancellationToken);
    }

    /// <summary>
    /// Suggests tech-stack options for an idea without persisting a plan row.
    /// </summary>
    public async Task<AiPlanningResult<TechStackResponseDto>> GenerateTechStackAsync(
        Guid projectId, Guid userId, bool isAdmin,
        GenerateTechStackRequest request, string locale, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Idea))
            return AiPlanningResult<TechStackResponseDto>.BadRequest("Idea is required.");

        // I-12: Sanitize user input to mitigate prompt injection
        var sanitizedIdea = Security.SecurityHelpers.SanitizePromptInput(request.Idea);

        var (ctx, early) = await ValidateAndResolveAsync<TechStackResponseDto>(
            projectId, userId, isAdmin, AiCapability.TechStackSuggest, request.Version, locale, request.Goals);
        if (early != null) return early;
        var strategy = ctx!.Strategy;
        var context = ctx.AiContext;

        return await WithUsageTrackingAsync<TechStackResponseDto>(projectId, "suggest_tech_stack", async startTracking =>
        {
            await startTracking(new AiUsageStart(projectId, userId, null,
                AiCapability.TechStackSuggest, strategy.Version, context.Locale, context.Tier));

            var techStackDraft = await strategy.SuggestTechStackAsync(context, sanitizedIdea, cancellationToken);

            return (AiPlanningResult<TechStackResponseDto>.Ok(MapTechStack(techStackDraft)),
                new AiUsageResult(true, null, Metadata: new AiUsageMetadata(
                    techStackDraft.PromptVersion, techStackDraft.Provider, techStackDraft.Model,
                    techStackDraft.PromptTokens, techStackDraft.CompletionTokens, techStackDraft.TotalTokens)));
        }, cancellationToken);
    }

    /// <summary>
    /// Loads a persisted plan when the caller can view project tasks; deserializes embedded draft JSON.
    /// </summary>
    public async Task<AiPlanningResult<AiPlanResponseDto>> GetPlanAsync(
        Guid projectId,
        Guid planId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var permissions = await _permissionService.GetPermissionsAsync(projectId, userId, isAdmin);
        if (permissions == null) return AiPlanningResult<AiPlanResponseDto>.NotFound("Project not found");
        if (!permissions.CanViewTasks) return AiPlanningResult<AiPlanResponseDto>.Forbidden("Only project members can view AI plans");

        var plan = await _db.AiPlans.FirstOrDefaultAsync(p => p.Id == planId && p.ProjectId == projectId, cancellationToken);
        if (plan == null) return AiPlanningResult<AiPlanResponseDto>.NotFound("AI plan not found");

        var planDraft = DeserializePlanDraft(plan.PlanJson);
        if (planDraft == null) return AiPlanningResult<AiPlanResponseDto>.ErrorResult("Failed to parse AI plan");

        return AiPlanningResult<AiPlanResponseDto>.Ok(MapPlan(plan, planDraft));
    }

    /// <summary>
    /// Validates and applies a draft plan via <see cref="IAiPlanApplier"/>, handling idempotent already-applied paths.
    /// </summary>
    public async Task<AiPlanningResult<ApplyAiPlanResponseDto>> ApplyPlanAsync(
        Guid projectId,
        Guid planId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var permissions = await _permissionService.GetPermissionsAsync(projectId, userId, isAdmin);
        if (permissions == null)
            return AiPlanningResult<ApplyAiPlanResponseDto>.NotFound("Project not found");
        if (!permissions.CanManageTasks)
            return AiPlanningResult<ApplyAiPlanResponseDto>.Forbidden("Only project owner or team members can apply AI plans");

        var capability = await _capabilityPolicy.CheckAsync(userId, projectId, AiCapability.PlanApply);
        if (!capability.Allowed)
            return AiPlanningResult<ApplyAiPlanResponseDto>.Forbidden(capability.DenyReason ?? "AI capability denied");

        var plan = await _db.AiPlans.FirstOrDefaultAsync(p => p.Id == planId && p.ProjectId == projectId, cancellationToken);
        if (plan == null)
            return AiPlanningResult<ApplyAiPlanResponseDto>.NotFound("AI plan not found");

        // I-07: Use WithUsageTrackingAsync for consistent metering and rate-limit handling
        return await WithUsageTrackingAsync<ApplyAiPlanResponseDto>(projectId, "apply_plan", async startTracking =>
        {
            await startTracking(new AiUsageStart(projectId, userId, planId,
                AiCapability.PlanApply, plan.PlanVersion, null, null));

            if (string.Equals(plan.Status, AiPlanStatus.Applied.Value, StringComparison.OrdinalIgnoreCase))
            {
                var counts = await GetAppliedCountsAsync(plan.Id, cancellationToken);
                return (AiPlanningResult<ApplyAiPlanResponseDto>.Ok(
                    new ApplyAiPlanResponseDto("already_applied", counts.taskCount, counts.linkCount, plan.AppliedAt)),
                    new AiUsageResult(true, null, MetadataJson: JsonSerializer.Serialize(new
                    {
                        taskCount = counts.taskCount, linkCount = counts.linkCount, appliedAt = plan.AppliedAt
                    }, PlanJsonOptions)));
            }

            if (!string.Equals(plan.Status, AiPlanStatus.Draft.Value, StringComparison.OrdinalIgnoreCase))
            {
                return (AiPlanningResult<ApplyAiPlanResponseDto>.Conflict("AI plan is currently being applied."),
                    new AiUsageResult(false, null, "plan_not_ready", $"Plan status is {plan.Status}."));
            }

            var planDraft = DeserializePlanDraft(plan.PlanJson);
            if (planDraft == null)
            {
                return (AiPlanningResult<ApplyAiPlanResponseDto>.ErrorResult("Failed to parse AI plan"),
                    new AiUsageResult(false, null, "plan_parse_failed", "Failed to parse AI plan"));
            }

            var validation = _validator.Validate(planDraft);
            if (!validation.IsValid)
            {
                return (AiPlanningResult<ApplyAiPlanResponseDto>.BadRequest("Plan validation failed", validation.Errors),
                    new AiUsageResult(false, null, "plan_validation_failed",
                        string.Join(" | ", validation.Errors), BuildUsageMetadata(planDraft)));
            }

            var applyResult = await _applier.ApplyAsync(projectId, planId, userId, planDraft, cancellationToken);
            if (applyResult.Status == AiPlanApplyStatus.NotFound)
            {
                return (AiPlanningResult<ApplyAiPlanResponseDto>.NotFound("AI plan not found"),
                    new AiUsageResult(false, null, "plan_not_found", "AI plan not found"));
            }

            if (applyResult.Status == AiPlanApplyStatus.AlreadyApplied)
            {
                if (string.Equals(applyResult.ConflictStatus, AiPlanStatus.Applied.Value, StringComparison.OrdinalIgnoreCase))
                {
                    var appliedPlan = await _db.AiPlans
                        .Where(p => p.Id == planId && p.ProjectId == projectId)
                        .Select(p => p.AppliedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                    var counts = await GetAppliedCountsAsync(planId, cancellationToken);

                    return (AiPlanningResult<ApplyAiPlanResponseDto>.Ok(
                        new ApplyAiPlanResponseDto("already_applied", counts.taskCount, counts.linkCount, appliedPlan)),
                        new AiUsageResult(true, null, MetadataJson: JsonSerializer.Serialize(new
                        {
                            taskCount = counts.taskCount, linkCount = counts.linkCount, appliedAt = appliedPlan
                        }, PlanJsonOptions)));
                }

                return (AiPlanningResult<ApplyAiPlanResponseDto>.Conflict("AI plan is currently being applied."),
                    new AiUsageResult(false, null, "plan_conflict", $"Plan status is {applyResult.ConflictStatus}."));
            }

            return (AiPlanningResult<ApplyAiPlanResponseDto>.Ok(
                new ApplyAiPlanResponseDto(AiPlanStatus.Applied.Value, applyResult.TaskCount, applyResult.LinkCount, applyResult.AppliedAt)),
                new AiUsageResult(true, null, MetadataJson: JsonSerializer.Serialize(new
                {
                    taskCount = applyResult.TaskCount, linkCount = applyResult.LinkCount, appliedAt = applyResult.AppliedAt
                }, PlanJsonOptions)));
        }, cancellationToken);
    }

    /// <summary>
    /// Reads task and link counts from the most recent successful apply usage log for a plan.
    /// </summary>
    private async Task<(int taskCount, int linkCount)> GetAppliedCountsAsync(Guid planId, CancellationToken cancellationToken)
    {
        var metadata = await _db.AiOperationLogs
            .Where(l => l.PlanId == planId
                && (l.OperationType == "apply_plan" || l.OperationType == "apply")
                && (l.Success || l.Status == "success" || l.Status == "completed"))
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => l.MetadataJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(metadata))
        {
            return (0, 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(metadata);
            var root = doc.RootElement;
            var taskCount = root.TryGetProperty("taskCount", out var taskElement) ? taskElement.GetInt32() : 0;
            var linkCount = root.TryGetProperty("linkCount", out var linkElement) ? linkElement.GetInt32() : 0;
            return (taskCount, linkCount);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Refines a plan from user instructions, validates the response, and persists a new draft plan row.
    /// </summary>
    public async Task<AiPlanningResult<AiPlanResponseDto>> RefinePlanAsync(
        Guid projectId, Guid userId, bool isAdmin,
        RefinePlanRequest request, string locale, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Idea) || string.IsNullOrWhiteSpace(request.TechStack))
            return AiPlanningResult<AiPlanResponseDto>.BadRequest("Idea and TechStack are required.");
        if (string.IsNullOrWhiteSpace(request.Instructions))
            return AiPlanningResult<AiPlanResponseDto>.BadRequest("Instructions are required.");

        // I-12: Sanitize user input to mitigate prompt injection
        var sanitizedIdea = Security.SecurityHelpers.SanitizePromptInput(request.Idea);
        var sanitizedTechStack = Security.SecurityHelpers.SanitizePromptInput(request.TechStack);
        var sanitizedInstructions = Security.SecurityHelpers.SanitizePromptInput(request.Instructions);

        var (ctx, early) = await ValidateAndResolveAsync<AiPlanResponseDto>(
            projectId, userId, isAdmin, AiCapability.PlanDraftGenerate, null, locale, request.Goals);
        if (early != null) return early;
        var strategy = ctx!.Strategy;
        var context = ctx.AiContext;

        return await WithUsageTrackingAsync<AiPlanResponseDto>(projectId, "refine_plan", async startTracking =>
        {
            await startTracking(new AiUsageStart(projectId, userId, null,
                AiCapability.PlanDraftGenerate, strategy.Version, context.Locale, context.Tier));

            var planDraft = await strategy.RefinePlanDraftAsync(context,
                new PlanDraftRequest(sanitizedIdea, sanitizedTechStack, strategy.Version,
                    request.CustomTags, request.CustomRoles, request.Goals),
                request.CurrentPlan, sanitizedInstructions, cancellationToken);

            var validation = _validator.Validate(planDraft);
            if (!validation.IsValid)
            {
                _logger.LogWarning("AI plan refinement validation failed for project {ProjectId}: {Errors}",
                    projectId, string.Join(" | ", validation.Errors));
                return (AiPlanningResult<AiPlanResponseDto>.InvalidAi("Invalid AI plan response", validation.Errors),
                    new AiUsageResult(false, null, "ai_response_invalid",
                        string.Join(" | ", validation.Errors), BuildUsageMetadata(planDraft)));
            }

            var planJson = JsonSerializer.Serialize(planDraft, PlanJsonOptions);

            var plan = new AiPlan
            {
                Id = Guid.NewGuid(), ProjectId = projectId, CreatedByUserId = userId,
                Idea = TrimToLength(sanitizedIdea.Trim(), _options.MaxIdeaLength),
                TechStack = TrimToLength(sanitizedTechStack.Trim(), _options.MaxTechStackLength),
                Status = AiPlanStatus.Draft.Value,
                PlanVersion = planDraft.Version, PlanJson = planJson, CreatedAt = DateTime.UtcNow
            };
            _db.AiPlans.Add(plan);
            await _db.SaveChangesAsync(cancellationToken);

            return (AiPlanningResult<AiPlanResponseDto>.Ok(MapPlan(plan, planDraft)),
                new AiUsageResult(true, null, Metadata: BuildUsageMetadata(planDraft),
                    MetadataJson: JsonSerializer.Serialize(new {
                        planVersion = plan.PlanVersion,
                        phaseCount = planDraft.Phases.Count,
                        taskCount = planDraft.Phases.Sum(p => p.Tasks?.Count ?? 0),
                        isRefinement = true
                    }, PlanJsonOptions)));
        }, cancellationToken);
    }

    /// <summary>
    /// Refines previously suggested tech-stack options using user instructions.
    /// </summary>
    public async Task<AiPlanningResult<TechStackResponseDto>> RefineTechStackAsync(
        Guid projectId, Guid userId, bool isAdmin,
        RefineTechStackRequest request, string locale, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Idea))
            return AiPlanningResult<TechStackResponseDto>.BadRequest("Idea is required.");
        if (string.IsNullOrWhiteSpace(request.Instructions))
            return AiPlanningResult<TechStackResponseDto>.BadRequest("Instructions are required.");

        var (ctx, early) = await ValidateAndResolveAsync<TechStackResponseDto>(
            projectId, userId, isAdmin, AiCapability.TechStackSuggest, null, locale, request.Goals);
        if (early != null) return early;
        var strategy = ctx!.Strategy;
        var context = ctx.AiContext;

        return await WithUsageTrackingAsync<TechStackResponseDto>(projectId, "refine_tech_stack", async startTracking =>
        {
            await startTracking(new AiUsageStart(projectId, userId, null,
                AiCapability.TechStackSuggest, strategy.Version, context.Locale, context.Tier));

            var techStackDraft = await strategy.RefineTechStackAsync(
                context, request.Idea, request.CurrentOptions, request.Instructions, cancellationToken);

            return (AiPlanningResult<TechStackResponseDto>.Ok(MapTechStack(techStackDraft)),
                new AiUsageResult(true, null, Metadata: new AiUsageMetadata(
                    techStackDraft.PromptVersion, techStackDraft.Provider, techStackDraft.Model,
                    techStackDraft.PromptTokens, techStackDraft.CompletionTokens, techStackDraft.TotalTokens)));
        }, cancellationToken);
    }

    /// <summary>
    /// I-03: Diagram generation moved from controller to service layer.
    /// Validates permissions, delegates to IMLServiceClient, handles errors uniformly.
    /// </summary>
    public async Task<AiPlanningResult<AiDiagramResponseDto>> GenerateDiagramAsync(
        Guid projectId, Guid userId, bool isAdmin,
        GenerateDiagramRequest request, CancellationToken cancellationToken)
    {
        var permissions = await _permissionService.GetPermissionsAsync(projectId, userId, isAdmin);
        if (permissions == null) return AiPlanningResult<AiDiagramResponseDto>.NotFound("Project not found");
        if (!permissions.CanManageTasks) return AiPlanningResult<AiDiagramResponseDto>.Forbidden("Insufficient permissions");

        var capability = await _capabilityPolicy.CheckAsync(userId, projectId, AiCapability.TechStackSuggest);
        if (!capability.Allowed)
            return AiPlanningResult<AiDiagramResponseDto>.Forbidden(capability.DenyReason ?? "AI capability denied");

        try
        {
            var result = await _mlServiceClient.GenerateDiagramAsync(
                request.TechStack,
                request.Idea,
                request.Format ?? "mermaid",
                request.DiagramType ?? "architecture",
                request.ProjectContext);
            return AiPlanningResult<AiDiagramResponseDto>.Ok(result);
        }
        catch (HttpRequestException)
        {
            return AiPlanningResult<AiDiagramResponseDto>.ErrorResult("AI service unavailable");
        }
    }
}
