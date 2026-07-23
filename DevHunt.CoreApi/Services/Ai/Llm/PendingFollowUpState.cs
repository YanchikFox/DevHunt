using DevHunt.CoreApi.Services.Ai.Llm.Models;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Snapshot of everything the round-trip phase needs to continue an
/// assistant turn that paused on tool calls. Cached by request id so a
/// later POST .../tools/execute can pick it up without re-doing system
/// prompt assembly, history fetch, or provider/model resolution.
///
/// Owned by <see cref="ILlmChatService"/>. Cache TTL is short (10 min);
/// if the user takes longer to confirm we just lose the round-trip and
/// fall back to the structured tool-execution summary message.
/// </summary>
/// <param name="UserId">Owner of the original assistant turn.</param>
/// <param name="ConversationId">Conversation where the turn happened.</param>
/// <param name="ProjectId">Project bound to the conversation, when any.</param>
/// <param name="Provider">Provider id selected for the original turn.</param>
/// <param name="ModelId">Model id selected for the original turn.</param>
/// <param name="Tier">Optional model tier for usage metadata.</param>
/// <param name="SystemPrompt">System prompt used in the original turn.</param>
/// <param name="ProjectContextSnapshot">Truncated project context block shown in transparency details.</param>
/// <param name="HistoryMessages">History messages after system prompt and before the user prompt.</param>
/// <param name="UserMessage">Cleaned user prompt.</param>
/// <param name="AssistantPreamble">Assistant text emitted before tool calls.</param>
/// <param name="ToolCalls">Tool calls proposed by the assistant.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp for diagnostics.</param>
public sealed record PendingFollowUpState(
    Guid UserId,
    Guid ConversationId,
    Guid? ProjectId,
    string Provider,
    string ModelId,
    string? Tier,
    string SystemPrompt,
    string? ProjectContextSnapshot,
    IReadOnlyList<LlmMessage> HistoryMessages,
    string UserMessage,
    string AssistantPreamble,
    IReadOnlyList<LlmToolCall> ToolCalls,
    DateTime CreatedAtUtc);
