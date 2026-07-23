using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Client request to send a BYOK chat message in a conversation.
/// </summary>
public sealed record LlmChatRequestDto
{
    /// <summary>User message text; may include a leading skill marker parsed by <see cref="IAiSkillResolver"/>.</summary>
    [Required]
    [MaxLength(8000)]
    public string Message { get; init; } = string.Empty;

    /// <summary>Provider id override; defaults to the user's preferred stored key.</summary>
    [MaxLength(32)]
    public string? Provider { get; init; }

    /// <summary>Model id override from the local registry.</summary>
    [MaxLength(128)]
    public string? ModelId { get; init; }

    /// <summary>Sampling temperature forwarded to the provider.</summary>
    public double? Temperature { get; init; }

    /// <summary>Maximum completion tokens for this turn.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// When the caller wants to override the per-conversation default. If null
    /// (the usual case), the service resolves a privacy-aware default: DMs
    /// include history, multi-user channels do NOT — sending other people's
    /// messages to a third-party LLM without their consent is a privacy leak.
    /// The UI should expose an explicit opt-in toggle for channels.
    /// </summary>
    public bool? UseHistory { get; init; }

    /// <summary>When true, exposes skill-filtered tools from <see cref="LlmToolCatalog"/> to the model.</summary>
    public bool EnableTools { get; init; }
}

/// <summary>
/// Options for regenerating the last assistant message in a conversation.
/// </summary>
public sealed record LlmRegenerateRequestDto
{
    /// <summary>Provider id override for the regeneration.</summary>
    [MaxLength(32)]
    public string? Provider { get; init; }

    /// <summary>Model id override for the regeneration.</summary>
    [MaxLength(128)]
    public string? ModelId { get; init; }

    /// <summary>Sampling temperature forwarded to the provider.</summary>
    public double? Temperature { get; init; }

    /// <summary>Maximum completion tokens for the regenerated turn.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>Whether to include prior conversation history in the prompt.</summary>
    public bool? UseHistory { get; init; }

    /// <summary>Whether to expose tools during regeneration.</summary>
    public bool EnableTools { get; init; }
}

/// <summary>
/// Token and cost summary returned with a completed chat turn.
/// </summary>
/// <param name="PromptTokens">Input tokens reported by the provider.</param>
/// <param name="CompletionTokens">Output tokens reported by the provider.</param>
/// <param name="TotalTokens">Total tokens reported by the provider.</param>
/// <param name="EstimatedCostUsd">Estimated USD cost from registry pricing.</param>
public sealed record LlmChatUsageView(
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    decimal? EstimatedCostUsd);

/// <summary>
/// Pre-flight token and cost estimate for a message without calling the provider.
/// </summary>
/// <param name="Provider">Resolved provider id.</param>
/// <param name="ModelId">Resolved model id.</param>
/// <param name="PromptTokens">Estimated prompt tokens from assembled context.</param>
/// <param name="MaxOutputTokens">Configured or default max completion tokens.</param>
/// <param name="MinCostUsd">Lower-bound USD estimate using output minimum.</param>
/// <param name="MaxCostUsd">Upper-bound USD estimate using max output tokens.</param>
/// <param name="IsApproximate">True when pricing or token counts are heuristic.</param>
public sealed record LlmChatEstimateResponseDto(
    string Provider,
    string ModelId,
    int PromptTokens,
    int MaxOutputTokens,
    decimal? MinCostUsd,
    decimal? MaxCostUsd,
    bool IsApproximate);

/// <summary>
/// Tool call proposed by the assistant for user confirmation before execution.
/// </summary>
/// <param name="Id">Provider tool-call id.</param>
/// <param name="Type">Wire type (typically <c>function</c>).</param>
/// <param name="Function">Function name and JSON arguments.</param>
public sealed record LlmToolCallView(
    string Id,
    string Type,
    LlmToolCallFunctionView Function);

/// <summary>
/// Function invocation details within a <see cref="LlmToolCallView"/>.
/// </summary>
/// <param name="Name">Tool name matching <see cref="LlmToolCatalog"/>.</param>
/// <param name="Arguments">Raw JSON arguments string.</param>
/// <param name="ArgumentsParsed">Parsed arguments for UI preview, when valid JSON.</param>
public sealed record LlmToolCallFunctionView(
    string Name,
    string Arguments,
    JsonElement? ArgumentsParsed);

/// <summary>
/// Completed chat turn response, including streaming-assembled text and optional tool proposals.
/// </summary>
/// <param name="RequestId">Correlation id for cancellation and follow-up tool rounds.</param>
/// <param name="UserMessageId">Persisted user message id.</param>
/// <param name="AssistantMessageId">Persisted assistant message id (may be partial when tool calls pending).</param>
/// <param name="Provider">Provider that served the turn.</param>
/// <param name="ModelId">Model used for the turn.</param>
/// <param name="Message">Assistant text content assembled from the stream.</param>
/// <param name="Usage">Token and cost summary.</param>
/// <param name="HasToolCalls">True when the model paused with tool calls awaiting confirmation.</param>
/// <param name="ToolCalls">Proposed tool calls for the confirmation UI.</param>
public sealed record LlmChatResponseDto(
    Guid RequestId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string Provider,
    string ModelId,
    string Message,
    LlmChatUsageView Usage,
    bool HasToolCalls,
    IReadOnlyList<LlmToolCallView> ToolCalls);
