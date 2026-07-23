namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// One technology stack alternative returned by the ML planner.
/// </summary>
/// <param name="Name">Short stack label shown to the user.</param>
/// <param name="Description">Narrative description of the stack.</param>
/// <param name="Pros">Advantages of choosing this stack.</param>
/// <param name="Cons">Trade-offs or drawbacks.</param>
public record AiTechStackOption(
    string Name,
    string Description,
    List<string> Pros,
    List<string> Cons
);

/// <summary>
/// Tech-stack suggestion payload from a planner strategy, including token usage metadata.
/// Mapped to <see cref="TechStackResponseDto"/> by <see cref="AiPlanningMapper"/>.
/// </summary>
/// <param name="Version">Strategy or response schema version.</param>
/// <param name="Options">Ranked stack alternatives.</param>
/// <param name="PromptVersion">Prompt template version from the ML service.</param>
/// <param name="Provider">Provider that served the request.</param>
/// <param name="Model">Model identifier used.</param>
/// <param name="PromptTokens">Input tokens consumed.</param>
/// <param name="CompletionTokens">Output tokens generated.</param>
/// <param name="TotalTokens">Total tokens reported.</param>
public record AiTechStackDraft(
    string Version,
    List<AiTechStackOption> Options,
    string? PromptVersion = null,
    string? Provider = null,
    string? Model = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null
);

/// <summary>
/// Full project plan draft with phases, tasks, and LLM usage metadata.
/// Validated by <see cref="AiPlanValidator"/> before persistence or apply.
/// </summary>
/// <param name="Version">Plan schema version from the planner.</param>
/// <param name="Idea">Sanitized project idea.</param>
/// <param name="TechStack">Selected or inferred technology stack.</param>
/// <param name="Phases">Ordered plan phases, each containing tasks.</param>
/// <param name="PromptVersion">Prompt template version from the ML service.</param>
/// <param name="Provider">Provider that served the request.</param>
/// <param name="Model">Model identifier used.</param>
/// <param name="PromptTokens">Input tokens consumed.</param>
/// <param name="CompletionTokens">Output tokens generated.</param>
/// <param name="TotalTokens">Total tokens reported.</param>
public record AiPlanDraft(
    string Version,
    string Idea,
    string TechStack,
    List<AiPlanPhase> Phases,
    string? PromptVersion = null,
    string? Provider = null,
    string? Model = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null
);

/// <summary>
/// A named phase grouping related tasks in an <see cref="AiPlanDraft"/>.
/// Applied as a <c>TaskColumn</c> by <see cref="AiPlanApplier"/>.
/// </summary>
/// <param name="Id">Stable phase identifier within the draft JSON.</param>
/// <param name="Name">Column name created or matched in the project board.</param>
/// <param name="Description">Phase overview text.</param>
/// <param name="Goals">High-level goals for the phase.</param>
/// <param name="Tasks">Tasks belonging to this phase.</param>
public record AiPlanPhase(
    string Id,
    string Name,
    string Description,
    List<string> Goals,
    List<AiPlanTask> Tasks
);

/// <summary>
/// One task node in an AI-generated plan, with dependency edges by draft-local id.
/// </summary>
/// <param name="Id">Stable task id referenced by <see cref="DependsOn"/>.</param>
/// <param name="Title">Task title shown on the board.</param>
/// <param name="Description">Optional task body; prefixed with phase name on apply.</param>
/// <param name="DependsOn">Ids of prerequisite tasks within the same draft.</param>
/// <param name="Priority">Priority label normalized by <see cref="AiPlanApplier"/>.</param>
/// <param name="Tags">Optional comma-separated tags applied to the created task.</param>
public record AiPlanTask(
    string Id,
    string Title,
    string Description,
    List<string> DependsOn,
    string Priority = "medium",
    List<string> Tags = null!
);

/// <summary>
/// Input to <see cref="IAiPlannerStrategy.GeneratePlanDraftAsync"/> and refine operations.
/// </summary>
/// <param name="Idea">Project idea text.</param>
/// <param name="TechStack">Chosen or inferred stack description.</param>
/// <param name="Version">Planner strategy version override.</param>
/// <param name="CustomTags">Optional tag hints for generated tasks.</param>
/// <param name="CustomRoles">Optional role hints for the planner.</param>
/// <param name="Goals">Tech-stack selection goals passed to the ML service.</param>
public record PlanDraftRequest(
    string Idea,
    string TechStack,
    string? Version = null,
    List<string>? CustomTags = null,
    List<string>? CustomRoles = null,
    TechStackGoals? Goals = null
);

/// <summary>
/// Caller and project context threaded through planner strategy calls.
/// <see cref="ProjectId"/> may be <see cref="Guid.Empty"/> for pre-project drafts.
/// </summary>
/// <param name="UserId">Authenticated user initiating the planner call.</param>
/// <param name="ProjectId">Target project, or empty for ephemeral drafts.</param>
/// <param name="Locale">Normalized locale for prompts.</param>
/// <param name="Tier">Optional subscription or model tier.</param>
/// <param name="Goals">Tech-stack goals forwarded to the ML service.</param>
public record AiContext(
    Guid UserId,
    Guid ProjectId,
    string Locale,
    string? Tier,
    TechStackGoals? Goals = null
);
