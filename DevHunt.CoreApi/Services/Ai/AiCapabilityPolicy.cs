namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// AI capabilities gated by <see cref="IAiCapabilityPolicy"/> before project-scoped operations run.
/// </summary>
public enum AiCapability
{
    TechStackSuggest,
    Chat,
    PlanDraftGenerate,
    PlanApply,
    PlanHistory
}

/// <summary>
/// Result of an <see cref="IAiCapabilityPolicy"/> check.
/// </summary>
/// <param name="Allowed">Whether the capability may proceed.</param>
/// <param name="DenyReason">Explanation surfaced when <paramref name="Allowed"/> is false.</param>
/// <param name="Remaining">Optional remaining quota for the capability window.</param>
public sealed record AiCapabilityDecision(bool Allowed, string? DenyReason = null, int? Remaining = null);

/// <summary>
/// Authorizes AI capabilities per user and project before <see cref="AiPlanningService"/> calls upstream services.
/// </summary>
public interface IAiCapabilityPolicy
{
    /// <summary>
    /// Evaluates whether the user may use the given capability on the project.
    /// </summary>
    /// <param name="userId">Authenticated user.</param>
    /// <param name="projectId">Target project.</param>
    /// <param name="capability">Capability being requested.</param>
    /// <returns>Allow or deny decision with optional reason.</returns>
    Task<AiCapabilityDecision> CheckAsync(Guid userId, Guid projectId, AiCapability capability);
}

/// <summary>
/// Development stub that permits all capabilities. Replace with tier or subscription checks in production.
/// </summary>
public sealed class AllowAllAiCapabilityPolicy : IAiCapabilityPolicy
{
    /// <inheritdoc />
    public Task<AiCapabilityDecision> CheckAsync(Guid userId, Guid projectId, AiCapability capability)
    {
        return Task.FromResult(new AiCapabilityDecision(true));
    }
}
