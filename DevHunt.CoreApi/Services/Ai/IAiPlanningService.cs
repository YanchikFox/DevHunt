namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Project-scoped AI planning API implemented by <see cref="AiPlanningService"/>.
/// Returns <see cref="AiPlanningResult{T}"/> for uniform authorization, validation, and error handling.
/// </summary>
public interface IAiPlanningService
{
    /// <summary>
    /// Generates a new draft plan for a project, validates it, persists an <c>AiPlan</c> row, and records usage.
    /// </summary>
    Task<AiPlanningResult<AiPlanResponseDto>> GeneratePlanAsync(
        Guid projectId,
        Guid userId,
        bool isAdmin,
        GenerateAiPlanRequest request,
        string locale,
        CancellationToken cancellationToken);

    /// <summary>
    /// Suggests technology stack options for a project idea without creating a plan row.
    /// </summary>
    Task<AiPlanningResult<TechStackResponseDto>> GenerateTechStackAsync(
        Guid projectId,
        Guid userId,
        bool isAdmin,
        GenerateTechStackRequest request,
        string locale,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads a persisted plan by id when the caller can view project tasks.
    /// </summary>
    Task<AiPlanningResult<AiPlanResponseDto>> GetPlanAsync(
        Guid projectId,
        Guid planId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a draft plan to the project board via <see cref="IAiPlanApplier"/> with usage metering.
    /// </summary>
    Task<AiPlanningResult<ApplyAiPlanResponseDto>> ApplyPlanAsync(
        Guid projectId,
        Guid planId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken);

    /// <summary>
    /// Refines an existing plan shape from user instructions and persists a new draft plan row.
    /// </summary>
    Task<AiPlanningResult<AiPlanResponseDto>> RefinePlanAsync(
        Guid projectId,
        Guid userId,
        bool isAdmin,
        RefinePlanRequest request,
        string locale,
        CancellationToken cancellationToken);

    /// <summary>
    /// Refines previously suggested tech-stack options using user instructions.
    /// </summary>
    Task<AiPlanningResult<TechStackResponseDto>> RefineTechStackAsync(
        Guid projectId,
        Guid userId,
        bool isAdmin,
        RefineTechStackRequest request,
        string locale,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates a diagram via the ML service with proper authorization and usage tracking.
    /// I-03: Moved from controller to service layer.
    /// </summary>
    Task<AiPlanningResult<AiDiagramResponseDto>> GenerateDiagramAsync(
        Guid projectId,
        Guid userId,
        bool isAdmin,
        GenerateDiagramRequest request,
        CancellationToken cancellationToken);
}
