using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DevHunt.CoreApi.Hubs;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Ai.Llm;
using DevHunt.CoreApi.Services.Chat;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Request from the frontend to confirm one or more proposed tool calls.
/// Each entry mirrors the <c>LlmToolCallView</c> the user just saw.
/// <see cref="OriginalRequestId"/> identifies the assistant turn that
/// produced the tool calls; required for the round-trip follow-up so the
/// chat service can pick up the cached pending state.
/// </summary>
/// <param name="OriginalRequestId">Request id of the assistant turn that proposed the tools.</param>
/// <param name="ToolCalls">User-confirmed tool invocations to execute.</param>
public sealed record ConfirmToolCallsRequest(
    Guid? OriginalRequestId,
    IReadOnlyList<ConfirmedToolCall> ToolCalls);

/// <summary>
/// One tool call confirmed by the user for execution.
/// </summary>
/// <param name="Id">Provider tool-call id from the assistant turn.</param>
/// <param name="Name">Tool name registered in <see cref="LlmToolCatalog"/>.</param>
/// <param name="Arguments">JSON arguments object as a string.</param>
public sealed record ConfirmedToolCall(string Id, string Name, string Arguments);

/// <summary>
/// Result of executing one confirmed tool, returned to the client and follow-up LLM turn.
/// </summary>
/// <param name="Id">Tool-call id from the original assistant turn.</param>
/// <param name="Name">Tool name that was executed.</param>
/// <param name="Success">Whether the tool completed successfully.</param>
/// <param name="Summary">Optional user-facing success summary.</param>
/// <param name="Error">Error message when <paramref name="Success"/> is false.</param>
/// <param name="ResultJson">Compact JSON payload for the model feedback loop.</param>
public sealed record ExecutedToolView(
    string Id,
    string Name,
    bool Success,
    string? Summary,
    string? Error,
    string ResultJson);

/// <summary>
/// Response after executing a batch of confirmed tool calls in a conversation.
/// </summary>
/// <param name="ConversationId">Conversation where tools ran.</param>
/// <param name="ProjectId">Bound project id, when the conversation is project-scoped.</param>
/// <param name="AssistantMessageId">Persisted summary message describing tool results.</param>
/// <param name="Results">Per-tool execution outcomes.</param>
/// <param name="FollowUpRequestId">New streaming request id when a natural-language follow-up started.</param>
public sealed record AiToolExecutionResponse(
    Guid ConversationId,
    Guid? ProjectId,
    Guid AssistantMessageId,
    IReadOnlyList<ExecutedToolView> Results,
    Guid? FollowUpRequestId);

/// <summary>
/// Executes user-confirmed LLM tool calls and optionally triggers a follow-up assistant stream.
/// </summary>
public interface IAiToolExecutionService
{
    /// <summary>
    /// Dispatches each confirmed tool, persists an audit chat message, broadcasts <c>AiToolsExecuted</c>,
    /// and starts <see cref="ILlmChatService.StreamFollowUpAsync"/> when pending state exists.
    /// </summary>
    Task<AiToolExecutionResponse> ExecuteAsync(
        Guid userId,
        Guid conversationId,
        ConfirmToolCallsRequest request,
        CancellationToken ct);
}

/// <summary>
/// Orchestrates tool calls confirmed by the user: dispatches each call,
/// gathers results, persists a compact AI-generated audit message, broadcasts
/// an <c>AiToolsExecuted</c> event so boards can refresh, then asks the LLM
/// for a natural-language follow-up when the original turn snapshot is still
/// available.
/// </summary>
public sealed class AiToolExecutionService : IAiToolExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DevHuntDbContext _db;
    private readonly IAiToolDispatcher _dispatcher;
    private readonly IChatService _chat;
    private readonly ILlmChatService _llmChat;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<AiToolExecutionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiToolExecutionService"/> class.
    /// </summary>
    /// <param name="db">Database context for conversation authorization.</param>
    /// <param name="dispatcher">Routes tool names to <see cref="IAiTool"/> implementations.</param>
    /// <param name="chat">Persists the structured tool execution summary message.</param>
    /// <param name="llmChat">Streams optional natural-language follow-up after tools succeed.</param>
    /// <param name="hub">Broadcasts <c>AiToolsExecuted</c> to conversation participants.</param>
    /// <param name="logger">Logger for malformed arguments and follow-up failures.</param>
    public AiToolExecutionService(
        DevHuntDbContext db,
        IAiToolDispatcher dispatcher,
        IChatService chat,
        ILlmChatService llmChat,
        IHubContext<ChatHub> hub,
        ILogger<AiToolExecutionService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _chat = chat;
        _llmChat = llmChat;
        _hub = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AiToolExecutionResponse> ExecuteAsync(
        Guid userId,
        Guid conversationId,
        ConfirmToolCallsRequest request,
        CancellationToken ct)
    {
        if (request.ToolCalls.Count == 0)
        {
            throw new InvalidOperationException("No tool calls provided.");
        }

        // Same conversation-level authorization the chat service uses; never
        // execute a tool against a project the requester can't access.
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(
                c => c.Id == conversationId
                    && c.Participants.Any(p => p.UserId == userId),
                ct)
            ?? throw new UnauthorizedAccessException("Conversation not found or access denied.");

        var execContext = new AiToolExecutionContext(userId, conversationId, conversation.ProjectId);

        var stopwatch = Stopwatch.StartNew();
        var results = new List<ExecutedToolView>(request.ToolCalls.Count);
        foreach (var call in request.ToolCalls)
        {
            JsonElement args;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.Arguments) ? "{}" : call.Arguments);
                args = doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "AI tool {Tool} call had malformed arguments JSON", call.Name);
                results.Add(new ExecutedToolView(
                    call.Id,
                    call.Name,
                    Success: false,
                    Summary: null,
                    Error: "Arguments were not valid JSON.",
                    ResultJson: """{"error":"bad_arguments_json"}"""));
                continue;
            }

            var result = await _dispatcher.ExecuteAsync(call.Name, args, execContext, ct);
            results.Add(new ExecutedToolView(
                call.Id,
                call.Name,
                result.Success,
                result.UserFacingSummary,
                result.ErrorMessage,
                result.ResultJson));
        }

        stopwatch.Stop();
        var summaryMessage = BuildChatMessage(results);
        var aiTransparency = BuildToolExecutionTransparency(results, (int)stopwatch.ElapsedMilliseconds);
        var saveResult = await _chat.SendMessageAsync(userId, conversationId, new SendMessageDto
        {
            Content = summaryMessage,
            IsAiGenerated = true,
            AiMetadataJson = aiTransparency.MetadataJson,
            AiDetailsJson = aiTransparency.DetailsJson,
        }, ct, broadcastViaHub: false);

        if (!saveResult.IsSuccess)
        {
            throw new InvalidOperationException(saveResult.ErrorMessage ?? "Failed to save tool execution summary.");
        }

        var assistantMessageId = saveResult.Data is MessageSendResult m ? m.MessageId : Guid.Empty;

        // Broadcast a structured event so the UI can patch task boards in
        // place rather than relying on text parsing of the chat message.
        Guid? followUpRequestId = null;

        await _hub.Clients.Group($"conversation:{conversationId}").SendAsync("AiToolsExecuted", new
        {
            ConversationId = conversationId,
            ProjectId = conversation.ProjectId,
            MessageId = assistantMessageId,
            Results = results,
        }, ct);

        if (request.OriginalRequestId is Guid originalRequestId)
        {
            try
            {
                var followUp = await _llmChat.StreamFollowUpAsync(userId, conversationId, originalRequestId, results, ct);
                followUpRequestId = followUp?.RequestId;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogInformation("AI tool follow-up was cancelled for conversation {ConversationId}", conversationId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Tool mutations already succeeded and the structured summary
                // was saved. A provider failure in the narration round should
                // not make the whole confirmation endpoint look failed.
                _logger.LogWarning(ex, "AI tool follow-up failed for conversation {ConversationId}", conversationId);
            }
        }

        return new AiToolExecutionResponse(conversationId, conversation.ProjectId, assistantMessageId, results, followUpRequestId);
    }

    /// <summary>Builds the markdown chat message listing each tool result for humans.</summary>
    private static string BuildChatMessage(IReadOnlyList<ExecutedToolView> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Tool execution**");
        foreach (var r in results)
        {
            var icon = r.Success ? "[ok]" : "[warning]";
            var detail = SecurityHelpers.SanitizeHtml(r.Success ? r.Summary ?? r.Name : r.Error ?? "Failed.");
            sb.AppendLine($"{icon} `{r.Name}` - {detail}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Builds AI transparency metadata and detail JSON for the summary message.</summary>
    private static (string MetadataJson, string DetailsJson) BuildToolExecutionTransparency(
        IReadOnlyList<ExecutedToolView> results,
        int latencyMs)
    {
        var toolSummaries = results.Select(result => new
        {
            id = result.Id,
            name = result.Name,
            success = (bool?)result.Success
        });
        var toolDetails = results.Select(result => new
        {
            id = result.Id,
            name = result.Name,
            arguments = (string?)null,
            argumentsParsed = (JsonElement?)null,
            success = (bool?)result.Success,
            summary = result.Summary,
            error = result.Error,
            resultJson = result.ResultJson
        });
        var createdAt = DateTime.UtcNow;

        var metadataJson = JsonSerializer.Serialize(new
        {
            provider = "devhunt",
            modelId = "tool-executor",
            tokens = new
            {
                promptTokens = (int?)null,
                completionTokens = (int?)null,
                totalTokens = (int?)null
            },
            costUsd = (decimal?)null,
            latencyMs,
            finishReason = "ToolExecution",
            toolCalls = toolSummaries,
            detailsRetentionDays = AiMessageDetailsPruneWorker.RetentionDays
        }, JsonOptions);

        var detailsJson = JsonSerializer.Serialize(new
        {
            systemContext = (string?)null,
            projectContext = (string?)null,
            toolCalls = toolDetails,
            providerResponse = new
            {
                finishReason = "ToolExecution"
            },
            createdAt,
            retainedUntil = createdAt.AddDays(AiMessageDetailsPruneWorker.RetentionDays)
        }, JsonOptions);

        return (metadataJson, detailsJson);
    }
}
