using DevHunt.CoreApi.Services.Ai.Llm.Tools;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Conversation-scoped BYOK chat orchestration: skill resolution, streaming, tool proposals,
/// usage logging, and round-trip follow-ups after confirmed tool execution.
/// </summary>
public interface ILlmChatService
{
    /// <summary>
    /// Sends a user message in a conversation, streams the assistant reply, and may pause on tool calls.
    /// </summary>
    Task<LlmChatResponseDto> SendConversationMessageAsync(
        Guid userId,
        Guid conversationId,
        LlmChatRequestDto request,
        CancellationToken ct);

    /// <summary>
    /// Regenerates the last assistant turn, optionally with a different model or provider.
    /// </summary>
    Task<LlmChatResponseDto> RegenerateLastConversationMessageAsync(
        Guid userId,
        Guid conversationId,
        LlmRegenerateRequestDto request,
        CancellationToken ct);

    /// <summary>
    /// Estimates token usage and cost for a message without calling the provider.
    /// </summary>
    Task<LlmChatEstimateResponseDto> EstimateConversationMessageAsync(
        Guid userId,
        Guid conversationId,
        LlmChatRequestDto request,
        CancellationToken ct);

    /// <summary>
    /// Round-trip after the user has confirmed and the dispatcher has executed
    /// a batch of tool calls proposed in <paramref name="originalRequestId"/>.
    /// Picks up the cached state from the original turn, appends the assistant
    /// tool-call turn + tool result messages, and streams a final
    /// natural-language reply to the same conversation. Returns null when the
    /// pending state has expired or wasn't found (e.g. process restart).
    /// </summary>
    Task<LlmChatResponseDto?> StreamFollowUpAsync(
        Guid userId,
        Guid conversationId,
        Guid originalRequestId,
        IReadOnlyList<ExecutedToolView> toolResults,
        CancellationToken ct);

}
