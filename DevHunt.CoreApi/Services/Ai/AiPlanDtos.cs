using DevHunt.CoreApi.Services;

namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Request to generate and persist a new AI plan for an existing project.
/// </summary>
/// <param name="Idea">Project idea; sanitized before calling the planner.</param>
/// <param name="TechStack">Chosen stack description.</param>
/// <param name="Version">Optional planner strategy version override.</param>
/// <param name="CustomTags">Optional tag hints for generated tasks.</param>
/// <param name="CustomRoles">Optional role hints for the planner.</param>
/// <param name="Goals">Tech-stack selection goals forwarded to the ML service.</param>
public record GenerateAiPlanRequest(
    string Idea,
    string TechStack,
    string? Version = null,
    List<string>? CustomTags = null,
    List<string>? CustomRoles = null,
    TechStackGoals? Goals = null
);

/// <summary>
/// Request to suggest technology stacks for a project idea without creating a plan.
/// </summary>
/// <param name="Idea">Project idea; sanitized before calling the planner.</param>
/// <param name="Version">Optional planner strategy version override.</param>
/// <param name="Goals">Selection goals forwarded to the ML service.</param>
public record GenerateTechStackRequest(
    string Idea,
    string? Version = null,
    TechStackGoals? Goals = null
);

/// <summary>
/// Boolean flags indicating which qualities matter when ranking tech stacks.
/// </summary>
public record TechStackGoals(
    bool Performance = false,
    bool Cost = false,
    bool DeveloperSpeed = false,
    bool Scalability = false,
    bool Security = false,
    bool Maintainability = false
);

/// <summary>
/// One technology stack alternative in an API response.
/// </summary>
/// <param name="Name">Stack label.</param>
/// <param name="Description">Narrative description.</param>
/// <param name="Pros">Advantages.</param>
/// <param name="Cons">Trade-offs.</param>
public record TechStackOptionDto(
    string Name,
    string Description,
    List<string> Pros,
    List<string> Cons
);

/// <summary>
/// Tech-stack suggestion returned to clients, including LLM usage metadata.
/// </summary>
/// <param name="Version">Response schema version.</param>
/// <param name="Options">Ranked alternatives.</param>
/// <param name="PromptVersion">Prompt template version from upstream.</param>
/// <param name="Provider">Provider name.</param>
/// <param name="Model">Model identifier.</param>
/// <param name="PromptTokens">Input tokens consumed.</param>
/// <param name="CompletionTokens">Output tokens generated.</param>
/// <param name="TotalTokens">Total tokens reported.</param>
public record TechStackResponseDto(
    string Version,
    List<TechStackOptionDto> Options,
    string? PromptVersion = null,
    string? Provider = null,
    string? Model = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null
);

/// <summary>
/// Persisted AI plan with embedded draft content for the frontend editor.
/// </summary>
/// <param name="Id">Plan row id.</param>
/// <param name="ProjectId">Owning project.</param>
/// <param name="Status">Plan lifecycle status (draft, applying, applied, ...).</param>
/// <param name="Idea">Stored idea text.</param>
/// <param name="TechStack">Stored stack text.</param>
/// <param name="PlanVersion">Planner version string on the row.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="AppliedAt">UTC apply timestamp when status is applied.</param>
/// <param name="Draft">Deserialized plan phases and tasks.</param>
public record AiPlanResponseDto(
    Guid Id,
    Guid ProjectId,
    string Status,
    string Idea,
    string TechStack,
    string PlanVersion,
    DateTime CreatedAt,
    DateTime? AppliedAt,
    AiPlanDraft Draft
);

/// <summary>
/// Summary returned after applying a plan to the project board.
/// </summary>
/// <param name="Status">Final or conflict status string (for example <c>applied</c> or <c>already_applied</c>).</param>
/// <param name="TaskCount">Tasks created on apply.</param>
/// <param name="LinkCount">Dependency links created on apply.</param>
/// <param name="AppliedAt">UTC timestamp when the plan was marked applied.</param>
public record ApplyAiPlanResponseDto(
    string Status,
    int TaskCount,
    int LinkCount,
    DateTime? AppliedAt
);

/// <summary>
/// Request to refine an in-memory or persisted plan using natural-language instructions.
/// </summary>
/// <param name="Idea">Current project idea.</param>
/// <param name="TechStack">Current tech stack.</param>
/// <param name="CurrentPlan">Existing plan JSON forwarded to the ML service.</param>
/// <param name="Instructions">User refinement instructions.</param>
/// <param name="CustomTags">Optional tag hints.</param>
/// <param name="CustomRoles">Optional role hints.</param>
/// <param name="Goals">Tech-stack goals for the planner context.</param>
public record RefinePlanRequest(
    string Idea,
    string TechStack,
    System.Text.Json.JsonElement CurrentPlan,
    string Instructions,
    List<string>? CustomTags = null,
    List<string>? CustomRoles = null,
    TechStackGoals? Goals = null
);

/// <summary>
/// Request to refine previously suggested tech-stack options.
/// </summary>
/// <param name="Idea">Project idea.</param>
/// <param name="CurrentOptions">Existing options JSON forwarded to the ML service.</param>
/// <param name="Instructions">User refinement instructions.</param>
/// <param name="Goals">Selection goals for ranking.</param>
public record RefineTechStackRequest(
    string Idea,
    System.Text.Json.JsonElement CurrentOptions,
    string Instructions,
    TechStackGoals? Goals = null
);

/// <summary>
/// Request to generate an architecture or other diagram via the ML service.
/// </summary>
/// <param name="TechStack">Stack description to reflect in the diagram.</param>
/// <param name="Idea">Optional project idea for additional context.</param>
/// <param name="Format">Output format (defaults to <c>mermaid</c>).</param>
/// <param name="DiagramType">Diagram kind (defaults to <c>architecture</c>).</param>
/// <param name="ProjectContext">Optional free-text project context.</param>
public record GenerateDiagramRequest(
    string TechStack,
    string? Idea = null,
    string? Format = "mermaid",
    string? DiagramType = "architecture",
    string? ProjectContext = null
);
