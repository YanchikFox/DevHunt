using System.Text.Json;

namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Request to generate an ephemeral plan draft before a project exists.
/// </summary>
/// <param name="Idea">Project idea; required.</param>
/// <param name="TechStack">Optional stack; inferred from the first suggestion when omitted.</param>
/// <param name="Version">Optional planner strategy version.</param>
/// <param name="CustomTags">Optional tag hints for generated tasks.</param>
/// <param name="CustomRoles">Optional role hints for the planner.</param>
/// <param name="Goals">Tech-stack goals when inferring a stack.</param>
public record GenerateAiPlanDraftRequest(
    string Idea,
    string? TechStack = null,
    string? Version = null,
    List<string>? CustomTags = null,
    List<string>? CustomRoles = null,
    TechStackGoals? Goals = null
);

/// <summary>
/// Request to refine an in-memory plan draft using natural-language instructions.
/// </summary>
/// <param name="Idea">Current project idea.</param>
/// <param name="TechStack">Current tech stack.</param>
/// <param name="CurrentPlan">Existing plan JSON from the client.</param>
/// <param name="Instructions">Refinement instructions.</param>
/// <param name="Version">Optional planner strategy version.</param>
/// <param name="CustomTags">Optional tag hints.</param>
/// <param name="CustomRoles">Optional role hints.</param>
/// <param name="Goals">Tech-stack goals for the planner context.</param>
public record RefineAiPlanDraftRequest(
    string Idea,
    string TechStack,
    JsonElement CurrentPlan,
    string Instructions,
    string? Version = null,
    List<string>? CustomTags = null,
    List<string>? CustomRoles = null,
    TechStackGoals? Goals = null
);

/// <summary>
/// Request to suggest tech stacks for a pre-project draft workspace.
/// </summary>
/// <param name="Idea">Project idea; required.</param>
/// <param name="Version">Optional planner strategy version.</param>
/// <param name="Goals">Selection goals forwarded to the ML service.</param>
public record GenerateAiPlanDraftTechStackRequest(
    string Idea,
    string? Version = null,
    TechStackGoals? Goals = null
);

/// <summary>
/// Ephemeral plan draft response. Mirrors <see cref="AiPlanResponseDto"/> but
/// omits DB-only fields (no plan id, no project id, no applied timestamp) so
/// the frontend can render the plan without assuming persistence.
/// </summary>
/// <param name="Idea">Trimmed idea text.</param>
/// <param name="TechStack">Resolved or provided stack text.</param>
/// <param name="PlanVersion">Planner version on the draft.</param>
/// <param name="Draft">Full phased plan content.</param>
public record AiPlanDraftResponseDto(
    string Idea,
    string TechStack,
    string PlanVersion,
    AiPlanDraft Draft
);
