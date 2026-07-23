namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Configuration limits for AI plan validation and field truncation.
/// Bound from configuration and consumed by <see cref="AiPlanValidator"/> and apply paths.
/// </summary>
public sealed class AiPlanningOptions
{
    /// <summary>Maximum number of phases allowed in a single plan draft.</summary>
    public int MaxPhases { get; set; } = 12;

    /// <summary>Maximum total tasks across all phases.</summary>
    public int MaxTasks { get; set; } = 500;

    /// <summary>Maximum <see cref="AiPlanTask.DependsOn"/> entries per task.</summary>
    public int MaxDependencies { get; set; } = 10;

    /// <summary>Maximum stored length for the project idea string.</summary>
    public int MaxIdeaLength { get; set; } = 2000;

    /// <summary>Maximum stored length for the tech stack string.</summary>
    public int MaxTechStackLength { get; set; } = 500;

    /// <summary>Maximum task title length when applying a plan to the board.</summary>
    public int MaxTaskTitleLength { get; set; } = 200;

    /// <summary>Maximum task description length when applying a plan to the board.</summary>
    public int MaxTaskDescriptionLength { get; set; } = 1000;
}
