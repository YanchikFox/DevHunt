namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Billable or quota-gated AI operations checked by <see cref="IAiEntitlementService"/>.
/// </summary>
public enum AiEntitlementOperation
{
    GeneratePlan,
    ApplyPlan
}

/// <summary>
/// Outcome of an entitlement check for a specific operation.
/// </summary>
/// <param name="Allowed">Whether the operation may proceed.</param>
/// <param name="Reason">Denial reason when <paramref name="Allowed"/> is false.</param>
public record AiEntitlementResult(bool Allowed, string? Reason = null);

/// <summary>
/// Determines whether a user is entitled to quota-sensitive AI planning operations.
/// </summary>
public interface IAiEntitlementService
{
    /// <summary>
    /// Checks subscription or credit entitlement before plan generation or apply.
    /// </summary>
    /// <param name="userId">Authenticated user.</param>
    /// <param name="projectId">Target project.</param>
    /// <param name="operation">Operation being requested.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Allow or deny result with optional reason.</returns>
    Task<AiEntitlementResult> CheckAsync(Guid userId, Guid projectId, AiEntitlementOperation operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default entitlement implementation (always allow).
/// Replace with credits/subscription checks later.
/// </summary>
public sealed class AllowAllAiEntitlementService : IAiEntitlementService
{
    /// <inheritdoc />
    public Task<AiEntitlementResult> CheckAsync(Guid userId, Guid projectId, AiEntitlementOperation operation, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AiEntitlementResult(true));
    }
}
