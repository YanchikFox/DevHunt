using System.Text.Json;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Per-call context flowed from the chat service into a tool. The tool itself
/// must do its own authorization check against this context. Never trust
/// arguments coming from the LLM at face value.
/// </summary>
public sealed record AiToolExecutionContext(Guid UserId, Guid ConversationId, Guid? ProjectId);

/// <summary>
/// Outcome of one tool invocation. <see cref="Success"/> false means the tool
/// rejected the request (validation, authorization, missing entity, ...).
/// <see cref="ResultJson"/> is what we'd send back to the model in a
/// <c>role: tool</c> message during a feedback loop. Keep it compact.
/// </summary>
public sealed record AiToolResult(
    bool Success,
    string ResultJson,
    string? UserFacingSummary = null,
    string? ErrorMessage = null);

/// <summary>
/// One tool the LLM can invoke. Implementations are registered in DI and
/// indexed by <see cref="Name"/> through <see cref="IAiToolDispatcher"/>.
/// </summary>
public interface IAiTool
{
    /// <summary>Tool name as advertised in <see cref="LlmToolCatalog"/>.</summary>
    string Name { get; }

    /// <summary>
    /// Validates arguments, enforces project-scoped authorization, mutates state when appropriate,
    /// and returns compact JSON for the model feedback loop.
    /// </summary>
    Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct);
}

/// <summary>
/// Routes confirmed tool calls from <see cref="AiToolExecutionService"/> to registered <see cref="IAiTool"/> implementations.
/// </summary>
public interface IAiToolDispatcher
{
    /// <summary>
    /// Routes one (toolName, args) call to the matching <see cref="IAiTool"/>.
    /// Returns a failed <see cref="AiToolResult"/> if the tool isn't
    /// registered. Callers can surface that as an error message rather than
    /// crashing a multi-tool batch.
    /// </summary>
    Task<AiToolResult> ExecuteAsync(string toolName, JsonElement args, AiToolExecutionContext ctx, CancellationToken ct);
}

/// <summary>
/// DI-backed index of all <see cref="IAiTool"/> implementations by <see cref="IAiTool.Name"/>.
/// </summary>
public sealed class AiToolDispatcher : IAiToolDispatcher
{
    private readonly Dictionary<string, IAiTool> _byName;
    private readonly ILogger<AiToolDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiToolDispatcher"/> class.
    /// </summary>
    /// <param name="tools">All tool implementations registered in DI.</param>
    /// <param name="logger">Logger for unknown tools and execution failures.</param>
    public AiToolDispatcher(IEnumerable<IAiTool> tools, ILogger<AiToolDispatcher> logger)
    {
        _byName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(string toolName, JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (!_byName.TryGetValue(toolName, out var tool))
        {
            _logger.LogWarning("AI requested unknown tool {ToolName}", toolName);
            return new AiToolResult(
                Success: false,
                ResultJson: """{"error":"unknown_tool"}""",
                ErrorMessage: $"Tool '{toolName}' is not implemented yet.");
        }

        try
        {
            return await tool.ExecuteAsync(args, ctx, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {ToolName} threw during execution for user {UserId}", toolName, ctx.UserId);
            return new AiToolResult(
                Success: false,
                ResultJson: """{"error":"tool_threw"}""",
                ErrorMessage: $"Tool '{toolName}' failed: {ex.Message}");
        }
    }
}
