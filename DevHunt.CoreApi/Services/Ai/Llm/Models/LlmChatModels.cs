using System.Text.Json;

namespace DevHunt.CoreApi.Services.Ai.Llm.Models;

/// <summary>
/// Canonical chat-message role. Modeled after the OpenAI shape because it's
/// the easiest target to translate other providers (Anthropic, Gemini) into
/// without losing information.
/// </summary>
public enum LlmRole
{
    System,
    User,
    Assistant,
    /// <summary>Result of a tool/function call returned to the model.</summary>
    Tool
}

/// <summary>
/// Why generation stopped. Each provider expresses this differently - adapters
/// normalise to one of these.
/// </summary>
public enum LlmFinishReason
{
    /// <summary>Stream not finished yet (used inside chunks).</summary>
    InProgress,
    /// <summary>Model produced an end-of-turn token naturally.</summary>
    Stop,
    /// <summary>Hit max output tokens.</summary>
    Length,
    /// <summary>Model requested tool calls - response is paused for tool execution.</summary>
    ToolCalls,
    /// <summary>Filtered by provider safety/moderation.</summary>
    ContentFilter,
    /// <summary>Adapter couldn't classify; kept for forward compatibility.</summary>
    Other
}

/// <summary>
/// One conversation turn in canonical form. A single <see cref="LlmMessage"/>
/// can carry text, tool calls (assistant-side) or a tool result (tool-side).
/// </summary>
/// <param name="Role">Canonical author role for this turn.</param>
/// <param name="Content">Text content for the turn, when present.</param>
/// <param name="ToolCalls">Tool calls the assistant wants the host to execute.</param>
/// <param name="ToolCallId">For role=<see cref="LlmRole.Tool"/>: id of the call this message answers.</param>
/// <param name="Name">Optional speaker name for multi-user channels.</param>
public sealed record LlmMessage(
    LlmRole Role,
    string? Content,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? Name = null
);

/// <summary>
/// A tool/function call the model wants the host to perform. Arguments are
/// kept as raw JSON so the host can validate against the tool schema before
/// dispatch (rather than guessing the CLR type here).
/// </summary>
/// <param name="Id">Provider-generated call id used to attach the tool result.</param>
/// <param name="Name">Tool/function name.</param>
/// <param name="ArgumentsJson">Raw JSON object string. May be partial during streaming.</param>
public sealed record LlmToolCall(
    string Id,
    string Name,
    string ArgumentsJson
);

/// <summary>
/// Tool/function definition advertised to the model. Schema follows JSON
/// Schema (the de facto LLM tool-calling format). Adapters map this into
/// each provider's own envelope.
/// </summary>
/// <param name="Name">Tool/function name exposed to the model.</param>
/// <param name="Description">Short behavior description shown to the model.</param>
/// <param name="ParametersSchema">JSON Schema describing the arguments object.</param>
public sealed record LlmToolDefinition(
    string Name,
    string Description,
    JsonElement ParametersSchema
);

/// <summary>
/// Token + dollar accounting for one completion. Fields are nullable because
/// not every provider reports all metrics in the streaming path.
/// </summary>
/// <param name="PromptTokens">Input token count reported by the provider.</param>
/// <param name="CompletionTokens">Output token count reported by the provider.</param>
/// <param name="TotalTokens">Total token count reported by the provider.</param>
/// <param name="EstimatedCostUsd">Computed at the registry layer using <c>LlmModel</c> prices.</param>
public sealed record LlmUsage(
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    decimal? EstimatedCostUsd = null
);

/// <summary>
/// Canonical chat completion request. The same shape feeds every provider -
/// the adapter is responsible for translating into provider-specific JSON.
/// </summary>
public sealed record LlmChatRequest
{
    /// <summary>Provider wire-format model id.</summary>
    public required string ModelId { get; init; }

    /// <summary>Conversation turns in canonical <see cref="LlmMessage"/> form.</summary>
    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>Tool definitions advertised to the model for this request.</summary>
    public IReadOnlyList<LlmToolDefinition>? Tools { get; init; }

    /// <summary>Sampling temperature.</summary>
    public double? Temperature { get; init; }

    /// <summary>Nucleus sampling top-p.</summary>
    public double? TopP { get; init; }

    /// <summary>Maximum completion tokens.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>Optional stop sequences.</summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>Caller-supplied id used for logs/metrics correlation.</summary>
    public string? RequestId { get; init; }
}

/// <summary>
/// One streamed delta from the provider. The host concatenates
/// <see cref="DeltaText"/> chunks until <see cref="FinishReason"/> is non-null
/// and assembles tool calls from <see cref="ToolCallDelta"/> updates.
/// </summary>
/// <param name="DeltaText">Incremental text appended to the assistant message. May be empty.</param>
/// <param name="ToolCallDelta">Partial tool-call updates. Arguments stream in as JSON fragments.</param>
/// <param name="FinishReason">Current or final completion state.</param>
/// <param name="Usage">Usage counters, usually present only on the final chunk.</param>
public sealed record LlmChatChunk(
    string? DeltaText = null,
    IReadOnlyList<LlmToolCallDelta>? ToolCallDelta = null,
    LlmFinishReason FinishReason = LlmFinishReason.InProgress,
    LlmUsage? Usage = null
);

/// <summary>
/// Streaming-time partial tool call. The host accumulates by
/// <see cref="Index"/> until the stream finishes.
/// </summary>
/// <param name="Index">Stable tool-call slot within the streamed assistant turn.</param>
/// <param name="Id">Provider-generated tool-call id, when emitted.</param>
/// <param name="Name">Tool/function name, when emitted.</param>
/// <param name="ArgumentsJsonFragment">Append to the running args buffer for this index.</param>
public sealed record LlmToolCallDelta(
    int Index,
    string? Id = null,
    string? Name = null,
    string? ArgumentsJsonFragment = null
);
