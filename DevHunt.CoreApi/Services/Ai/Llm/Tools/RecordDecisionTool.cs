using System.Text.Json;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Implements the <c>record_decision</c> tool: the curated path for
/// project memory writes. The model decides when an action / insight is
/// worth persisting; calling this tool routes through the user-confirmation
/// flow so a noisy model can't silently spam project artifacts.
/// </summary>
public sealed class RecordDecisionTool : IAiTool
{
    private readonly IProjectMemoryService _memory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordDecisionTool"/> class.
    /// </summary>
    /// <param name="memory">Project memory service that persists decisions.</param>
    public RecordDecisionTool(IProjectMemoryService memory)
    {
        _memory = memory;
    }

    /// <inheritdoc />
    public string Name => "record_decision";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("record_decision can only be invoked from a project conversation.");
        }

        if (!args.TryGetProperty("summary", out var summaryProp) || summaryProp.ValueKind != JsonValueKind.String)
        {
            return Fail("Argument 'summary' is required (one short sentence).");
        }

        var summary = (summaryProp.GetString() ?? string.Empty).Trim();
        if (summary.Length == 0) return Fail("Argument 'summary' must be non-empty.");

        var category = ParseCategory(args);

        await _memory.RecordAsync(projectId, ctx.UserId, category, summary, ct);

        var displayCategory = category.ToString().ToLowerInvariant();
        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(new { recorded = true, category = displayCategory, summary }),
            UserFacingSummary: $"Recorded {displayCategory}: \"{summary}\"");
    }

    /// <summary>Maps the optional memory category argument to a known project memory category.</summary>
    private static ProjectMemoryCategory ParseCategory(JsonElement args)
    {
        if (!args.TryGetProperty("category", out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return ProjectMemoryCategory.Decision;
        }
        return prop.GetString()?.ToLowerInvariant() switch
        {
            "decision" => ProjectMemoryCategory.Decision,
            "action" => ProjectMemoryCategory.Action,
            "technical" or "technical_note" or "tech" => ProjectMemoryCategory.Technical,
            _ => ProjectMemoryCategory.Decision,
        };
    }

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);
}
