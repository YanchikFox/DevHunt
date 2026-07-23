namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Context captured when an AI operation begins, written to <c>AiOperationLogs</c>
/// by <see cref="IAiUsageMeter"/> implementations such as <see cref="DbAiUsageMeter"/>.
/// </summary>
/// <param name="ProjectId">Project the operation targets, when applicable.</param>
/// <param name="UserId">User who initiated the operation.</param>
/// <param name="PlanId">AI plan id when the operation relates to a persisted plan.</param>
/// <param name="Capability">Capability gate that authorized the operation.</param>
/// <param name="StrategyVersion">Planner strategy version (for example <c>v1</c>).</param>
/// <param name="Locale">Normalized locale used for prompts.</param>
/// <param name="Tier">Optional model or subscription tier for usage metadata.</param>
public sealed record AiUsageStart(
    Guid? ProjectId,
    Guid UserId,
    Guid? PlanId,
    AiCapability Capability,
    string? StrategyVersion,
    string? Locale,
    string? Tier
);

/// <summary>
/// Token and provider metadata recorded when an AI operation completes.
/// </summary>
/// <param name="PromptVersion">Prompt template version reported by the upstream service.</param>
/// <param name="Provider">LLM or ML provider name.</param>
/// <param name="Model">Model identifier used for the call.</param>
/// <param name="InputTokens">Prompt token count, when reported.</param>
/// <param name="OutputTokens">Completion token count, when reported.</param>
/// <param name="TotalTokens">Total token count, when reported.</param>
public sealed record AiUsageMetadata(
    string? PromptVersion,
    string? Provider,
    string? Model,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens
);

/// <summary>
/// Correlates a started operation with its persisted log row, if any.
/// </summary>
/// <param name="Start">Original start context passed to <see cref="IAiUsageMeter.StartAsync"/>.</param>
/// <param name="LogId">Database id of the <c>AiOperationLog</c> row, or <see langword="null"/> when persistence failed.</param>
public sealed record AiUsageScope(
    AiUsageStart Start,
    Guid? LogId
);

/// <summary>
/// Outcome of an AI operation, merged into the usage log on completion.
/// </summary>
/// <param name="Success">Whether the operation finished successfully.</param>
/// <param name="LatencyMs">Wall-clock duration in milliseconds.</param>
/// <param name="ErrorCode">Machine-readable failure code when <paramref name="Success"/> is false.</param>
/// <param name="ErrorMessage">Human-readable failure message.</param>
/// <param name="Metadata">Structured token and provider metadata.</param>
/// <param name="MetadataJson">Additional JSON metadata (for example task counts on apply).</param>
public sealed record AiUsageResult(
    bool Success,
    int? LatencyMs,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    AiUsageMetadata? Metadata = null,
    string? MetadataJson = null
);

/// <summary>
/// Persists AI operation start and completion records for auditing and analytics.
/// Called by <see cref="AiPlanningService"/> around each metered operation.
/// </summary>
public interface IAiUsageMeter
{
    /// <summary>
    /// Creates a started <c>AiOperationLog</c> row and returns a scope for later completion.
    /// Implementations should not throw on persistence failure; they return a scope with a null log id instead.
    /// </summary>
    /// <param name="start">Operation context to record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Scope containing the start context and optional persisted log id.</returns>
    Task<AiUsageScope> StartAsync(AiUsageStart start, CancellationToken ct);

    /// <summary>
    /// Updates or creates the log row identified by <paramref name="scope"/> with the final result.
    /// Swallows persistence errors after logging a warning.
    /// </summary>
    /// <param name="scope">Scope returned from <see cref="StartAsync"/>.</param>
    /// <param name="result">Final outcome including latency and token metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CompleteAsync(AiUsageScope scope, AiUsageResult result, CancellationToken ct);
}
