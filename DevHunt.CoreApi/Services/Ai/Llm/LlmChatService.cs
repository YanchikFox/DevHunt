using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DevHunt.CoreApi.Hubs;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Ai;
using DevHunt.CoreApi.Services.Chat;
using DevHunt.CoreApi.Services.Ai.Llm.Models;
using DevHunt.CoreApi.Services.Ai.Llm.Providers;
using DevHunt.CoreApi.Services.Ai.Llm.Tools;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// BYOK conversation chat service: resolves skills and project context, streams provider responses,
/// proposes tools for confirmation, meters usage, and supports post-tool follow-up streaming.
/// </summary>
public sealed class LlmChatService : ILlmChatService
{
    private const int HistoryTokenBudget = 3500;
    private const int MaxAssistantMessageChars = 9500;
    private const int ContextSnapshotMaxChars = 4096;
    private const int DefaultEstimateMaxOutputTokens = 1024;
    private static readonly string[] ToolIntentMarkers =
    [
        "create", "add", "task", "card", "move", "delete", "remove", "update",
        "change", "document", "architecture", "roadmap", "decision", "remember",
        "создай", "создать", "добавь", "добавить", "задач", "таск", "карточ",
        "перемести", "удали", "обнови", "измени", "прочитай", "документ",
        "архитектур", "решили", "запомни"
    ];

    /// <summary>
    /// When the caller doesn't pin a specific provider, we prefer the
    /// aggregator (OpenRouter) so a single key unlocks the most models. Pure
    /// UX heuristic; change here when the preference shifts.
    /// </summary>
    private const string PreferredProvider = "openrouter";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DevHuntDbContext _db;
    private readonly IUserApiKeyService _keys;
    private readonly ILlmProviderRegistry _providers;
    private readonly IChatService _chat;
    private readonly IAiUsageMeter _usageMeter;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IEncryptionService _encryption;
    private readonly IAiRateLimiter _rateLimiter;
    private readonly IAiInFlightRegistry _inFlight;
    private readonly IAiSkillResolver _skills;
    private readonly IProjectContextBuilder _projectContext;
    private readonly Services.ICacheService _cache;
    private readonly ILogger<LlmChatService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmChatService"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="keys">Service for storing and resolving user LLM API keys.</param>
    /// <param name="providers">Registry of supported LLM providers.</param>
    /// <param name="chat">Chat service used to persist generated messages.</param>
    /// <param name="usageMeter">Usage meter for AI operation audit logs.</param>
    /// <param name="hub">SignalR hub used to broadcast AI chat events.</param>
    /// <param name="encryption">Encryption service for protected chat/key data.</param>
    /// <param name="rateLimiter">Per-user request limiter for AI chat calls.</param>
    /// <param name="inFlight">Registry for cancellable in-flight AI streams.</param>
    /// <param name="skills">Resolver for chat skill markers and tool policy.</param>
    /// <param name="projectContext">Builder for markdown project context blocks.</param>
    /// <param name="cache">Cache used for project context and pending follow-up state.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public LlmChatService(
        DevHuntDbContext db,
        IUserApiKeyService keys,
        ILlmProviderRegistry providers,
        IChatService chat,
        IAiUsageMeter usageMeter,
        IHubContext<ChatHub> hub,
        IEncryptionService encryption,
        IAiRateLimiter rateLimiter,
        IAiInFlightRegistry inFlight,
        IAiSkillResolver skills,
        IProjectContextBuilder projectContext,
        Services.ICacheService cache,
        ILogger<LlmChatService> logger)
    {
        _db = db;
        _keys = keys;
        _providers = providers;
        _chat = chat;
        _usageMeter = usageMeter;
        _hub = hub;
        _encryption = encryption;
        _rateLimiter = rateLimiter;
        _inFlight = inFlight;
        _skills = skills;
        _projectContext = projectContext;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LlmChatResponseDto> SendConversationMessageAsync(
        Guid userId,
        Guid conversationId,
        LlmChatRequestDto request,
        CancellationToken ct)
    {
        return await ExecuteConversationMessageAsync(
            userId,
            conversationId,
            request,
            persistUserCommand: true,
            existingUserMessageId: null,
            historyBeforeUtc: null,
            ct);
    }

    /// <inheritdoc />
    public async Task<LlmChatResponseDto> RegenerateLastConversationMessageAsync(
        Guid userId,
        Guid conversationId,
        LlmRegenerateRequestDto request,
        CancellationToken ct)
    {
        await LoadAuthorizedConversationAsync(userId, conversationId, ct);

        var recentUserMessages = await _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId
                && m.SenderId == userId
                && !m.IsAiGenerated
                && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(40)
            .Select(m => new { m.Id, m.Content, m.CreatedAt })
            .ToListAsync(ct);

        foreach (var candidate in recentUserMessages)
        {
            var plain = DecryptMessageSafe(candidate.Content, candidate.Id);
            var replay = TryStripAiCommand(plain);
            if (replay == null) continue;

            var replayRequest = new LlmChatRequestDto
            {
                Message = replay,
                Provider = request.Provider,
                ModelId = request.ModelId,
                Temperature = request.Temperature,
                MaxOutputTokens = request.MaxOutputTokens,
                UseHistory = request.UseHistory,
                EnableTools = request.EnableTools,
            };

            return await ExecuteConversationMessageAsync(
                userId,
                conversationId,
                replayRequest,
                persistUserCommand: false,
                existingUserMessageId: candidate.Id,
                historyBeforeUtc: candidate.CreatedAt,
                ct);
        }

        throw new InvalidOperationException("No previous /ai message found to regenerate.");
    }

    /// <inheritdoc />
    public async Task<LlmChatEstimateResponseDto> EstimateConversationMessageAsync(
        Guid userId,
        Guid conversationId,
        LlmChatRequestDto request,
        CancellationToken ct)
    {
        var (skill, cleanedMessage) = _skills.Resolve(request.Message);
        var conversation = await LoadAuthorizedConversationAsync(userId, conversationId, ct);
        var selection = await ResolveSelectionAsync(userId, request.Provider, request.ModelId, ct);

        var includeHistory = request.UseHistory
            ?? (conversation.Type == ConversationType.Direct || skill.DefaultUseHistory);
        var history = includeHistory
            ? await BuildHistoryAsync(conversationId, beforeUtc: null, ct)
            : new List<LlmMessage>();

        var systemPrompt = await ComposeSystemPromptAsync(skill.SystemPrompt, conversation.ProjectId, ct);
        var toolsForRequest = ResolveToolsForRequest(request.EnableTools, selection.Model, skill, cleanedMessage);
        var messages = BuildProviderMessages(systemPrompt.Prompt, history, cleanedMessage);
        var promptTokens = EstimateRequestTokens(messages, toolsForRequest);
        var maxOutputTokens = ResolveEstimateMaxOutputTokens(request.MaxOutputTokens, selection.Model);

        var minCostUsd = EstimateInputCost(selection.Model, promptTokens);
        var maxCostUsd = EstimateTotalCost(selection.Model, promptTokens, maxOutputTokens);

        return new LlmChatEstimateResponseDto(
            selection.Provider,
            selection.Model.ModelId,
            promptTokens,
            maxOutputTokens,
            minCostUsd,
            maxCostUsd,
            IsApproximate: true);
    }

    /// <summary>
    /// Core send/regenerate path: rate limit, resolve skill and model, stream from provider,
    /// persist messages, cache pending state on tool calls, and record usage.
    /// </summary>
    private async Task<LlmChatResponseDto> ExecuteConversationMessageAsync(
        Guid userId,
        Guid conversationId,
        LlmChatRequestDto request,
        bool persistUserCommand,
        Guid? existingUserMessageId,
        DateTime? historyBeforeUtc,
        CancellationToken ct)
    {
        // Minute quota first; every other check below either touches the DB
        // or burns a network round-trip, so a runaway script gets cut here
        // before it costs us anything.
        var quota = await _rateLimiter.CheckAndIncrementAsync(userId, ct);
        if (!quota.Allowed)
        {
            throw new AiRateLimitExceededException(quota);
        }

        var (skill, cleanedMessage) = _skills.Resolve(request.Message);

        var conversation = await LoadAuthorizedConversationAsync(userId, conversationId, ct);
        var selection = await ResolveSelectionAsync(userId, request.Provider, request.ModelId, ct);
        var provider = _providers.Get(selection.Provider)
            ?? throw new InvalidOperationException($"Provider '{selection.Provider}' is not supported.");
        var apiKey = await _keys.ResolveDecryptedKeyAsync(userId, selection.Provider, ct)
            ?? throw new InvalidOperationException($"No active API key saved for provider '{selection.Provider}'.");

        // Privacy-aware history default: in a DM the participants are the AI
        // requester + the other person they're already talking to, so context
        // is fine. In Group / ProjectChannel rooms the history may include
        // messages from people who didn't consent to the requester's BYOK
        // provider seeing their text; make that explicit opt-in only. The
        // skill profile (e.g. /summarize, /standup) can also force history on.
        var includeHistory = request.UseHistory
            ?? (conversation.Type == ConversationType.Direct || skill.DefaultUseHistory);
        var history = includeHistory
            ? await BuildHistoryAsync(conversationId, historyBeforeUtc, ct)
            : new List<LlmMessage>();

        // Stitch project context onto the skill's base prompt for project
        // channels; gives the AI a stable awareness of stack / active tasks /
        // memory artifacts / available documents without ceremonial markers.
        var systemPrompt = await ComposeSystemPromptAsync(skill.SystemPrompt, conversation.ProjectId, ct);

        var userMessage = persistUserCommand
            ? await PersistUserCommandAsync(userId, conversationId, request.Message, ct)
            : existingUserMessageId ?? Guid.Empty;
        var requestId = Guid.NewGuid();
        var scope = await _usageMeter.StartAsync(new AiUsageStart(
            conversation.ProjectId,
            userId,
            PlanId: null,
            AiCapability.Chat,
            StrategyVersion: "llm-v1",
            Locale: null,
            Tier: selection.Model.Tier), ct);

        var stopwatch = Stopwatch.StartNew();
        var fullText = new StringBuilder();
        var toolCallBuffers = new Dictionary<int, ToolCallBuffer>();
        LlmUsage? usage = null;
        var finishReason = LlmFinishReason.InProgress;

        // Register before the first network byte so a /cancel that arrives
        // a few ms later still finds us. Disposed in the outer scope so
        // the entry is removed even on the throw paths below.
        using var inFlight = _inFlight.Register(requestId, userId, conversationId, ct, out var streamCt);

        await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamStarted", new
        {
            RequestId = requestId,
            UserId = userId,
            ConversationId = conversationId,
            selection.Provider,
            selection.Model.ModelId,
            Skill = skill.Skill.ToString(),
        }, streamCt);

        try
        {
            // Tools are gated by both the model's capability AND the skill's
            // allowlist. A /summarize call must not get task-mutation tools
            // even when the model supports them.
            var toolsForRequest = ResolveToolsForRequest(request.EnableTools, selection.Model, skill, cleanedMessage);

            var llmRequest = new LlmChatRequest
            {
                ModelId = selection.Model.ModelId,
                Messages = BuildProviderMessages(systemPrompt.Prompt, history, cleanedMessage),
                Tools = toolsForRequest is { Count: > 0 } ? toolsForRequest : null,
                Temperature = request.Temperature,
                MaxOutputTokens = request.MaxOutputTokens,
                RequestId = requestId.ToString()
            };

            await foreach (var chunk in provider.StreamChatAsync(llmRequest, apiKey, streamCt))
            {
                if (!string.IsNullOrEmpty(chunk.DeltaText))
                {
                    fullText.Append(chunk.DeltaText);
                    await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamDelta", new
                    {
                        RequestId = requestId,
                        UserId = userId,
                        Delta = chunk.DeltaText
                    }, streamCt);
                }

                if (chunk.ToolCallDelta is { Count: > 0 })
                {
                    foreach (var toolDelta in chunk.ToolCallDelta)
                    {
                        var buffer = GetToolCallBuffer(toolCallBuffers, toolDelta.Index);
                        if (!string.IsNullOrWhiteSpace(toolDelta.Id)) buffer.Id = toolDelta.Id;
                        if (!string.IsNullOrWhiteSpace(toolDelta.Name)) buffer.Name = toolDelta.Name;
                        if (!string.IsNullOrEmpty(toolDelta.ArgumentsJsonFragment))
                        {
                            buffer.ArgumentsJson.Append(toolDelta.ArgumentsJsonFragment);
                        }
                    }
                }

                if (chunk.Usage != null)
                {
                    usage = chunk.Usage;
                }

                if (chunk.FinishReason != LlmFinishReason.InProgress)
                {
                    finishReason = chunk.FinishReason;
                }
            }

            var assistantText = NormalizeAssistantText(fullText.ToString());
            var toolCalls = BuildToolCallViews(toolCallBuffers);
            var estimatedCost = EstimateCost(selection.Model, usage);

            stopwatch.Stop();
            var latencyMs = (int)stopwatch.ElapsedMilliseconds;
            var aiMetadataJson = BuildAiMetadataJson(
                selection.Provider,
                selection.Model.ModelId,
                usage,
                estimatedCost,
                latencyMs,
                finishReason,
                toolCalls.Select(t => new
                {
                    id = t.Id,
                    name = t.Function.Name,
                    success = (bool?)null
                }));
            var aiDetailsJson = BuildAiDetailsJson(
                systemPrompt,
                toolCalls.Select(t => new
                {
                    id = t.Id,
                    name = t.Function.Name,
                    arguments = t.Function.Arguments,
                    argumentsParsed = t.Function.ArgumentsParsed,
                    success = (bool?)null,
                    summary = (string?)null,
                    error = (string?)null,
                    resultJson = (string?)null
                }),
                finishReason);
            var assistantMessageId = await PersistAssistantMessageAsync(
                userId,
                conversationId,
                assistantText,
                aiMetadataJson,
                aiDetailsJson,
                ct);

            await _usageMeter.CompleteAsync(scope, new AiUsageResult(
                Success: true,
                LatencyMs: latencyMs,
                Metadata: new AiUsageMetadata(
                    PromptVersion: "chat-v1",
                    Provider: selection.Provider,
                    Model: selection.Model.ModelId,
                    InputTokens: usage?.PromptTokens,
                    OutputTokens: usage?.CompletionTokens,
                    TotalTokens: usage?.TotalTokens),
                MetadataJson: JsonSerializer.Serialize(new
                {
                    conversationId,
                    requestId,
                    estimatedCostUsd = estimatedCost,
                    finishReason = finishReason.ToString(),
                    toolCalls = toolCalls.Select(t => t.Function.Name)
                }, JsonOptions)), ct);

            var usageView = new LlmChatUsageView(
                usage?.PromptTokens,
                usage?.CompletionTokens,
                usage?.TotalTokens,
                estimatedCost);

            await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamCompleted", new
            {
                RequestId = requestId,
                UserId = userId,
                MessageId = assistantMessageId,
                Usage = usageView,
                FinishReason = finishReason.ToString(),
                HasToolCalls = toolCalls.Count > 0,
                ToolCalls = toolCalls
            }, ct);

            if (toolCalls.Count > 0)
            {
                // Cache the snapshot the round-trip will need: same system
                // prompt, history, user message, plus the assistant turn the
                // model just produced. POST .../tools/execute will pick this
                // up after the user confirms. Without the snapshot we'd
                // have to rebuild the system prompt and re-fetch history,
                // which can race with new chat messages.
                var pending = new PendingFollowUpState(
                    UserId: userId,
                    ConversationId: conversationId,
                    ProjectId: conversation.ProjectId,
                    Provider: selection.Provider,
                    ModelId: selection.Model.ModelId,
                    Tier: selection.Model.Tier,
                    SystemPrompt: systemPrompt.Prompt,
                    ProjectContextSnapshot: systemPrompt.ProjectContextSnapshot,
                    HistoryMessages: history,
                    UserMessage: cleanedMessage,
                    AssistantPreamble: assistantText,
                    ToolCalls: toolCalls.Select(t => new LlmToolCall(t.Id, t.Function.Name, t.Function.Arguments)).ToList(),
                    CreatedAtUtc: DateTime.UtcNow);

                await _cache.SetAsync(PendingCacheKey(requestId), pending, TimeSpan.FromMinutes(10));

                await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiToolCallsProposed", new
                {
                    RequestId = requestId,
                    UserId = userId,
                    MessageId = assistantMessageId,
                    ToolCalls = toolCalls
                }, ct);
            }

            return new LlmChatResponseDto(
                requestId,
                userMessage,
                assistantMessageId,
                selection.Provider,
                selection.Model.ModelId,
                assistantText,
                usageView,
                toolCalls.Count > 0,
                toolCalls);
        }
        catch (OperationCanceledException) when (streamCt.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // User-initiated /cancel, distinct from "the HTTP request was
            // aborted". We still want to record usage as cancelled and let
            // the UI replace its spinner with a "stopped" state.
            stopwatch.Stop();
            await _usageMeter.CompleteAsync(scope, new AiUsageResult(
                Success: false,
                LatencyMs: (int)stopwatch.ElapsedMilliseconds,
                ErrorCode: "cancelled",
                ErrorMessage: "Generation cancelled by user.",
                Metadata: new AiUsageMetadata("chat-v1", selection.Provider, selection.Model.ModelId, usage?.PromptTokens, usage?.CompletionTokens, usage?.TotalTokens)), ct);

            await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamCancelled", new
            {
                RequestId = requestId,
                UserId = userId,
                PartialText = fullText.ToString(),
            }, ct);

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "LLM chat failed for user {UserId} conversation {ConversationId}", userId, conversationId);
            await _usageMeter.CompleteAsync(scope, new AiUsageResult(
                Success: false,
                LatencyMs: (int)stopwatch.ElapsedMilliseconds,
                ErrorCode: ex is LlmProviderException providerEx ? $"provider_{providerEx.StatusCode}" : "llm_chat_failed",
                ErrorMessage: ex.Message,
                Metadata: new AiUsageMetadata("chat-v1", selection.Provider, selection.Model.ModelId, null, null, null)), ct);

            await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamFailed", new
            {
                RequestId = requestId,
                UserId = userId,
                Error = "AI request failed. Please try again."
            }, ct);

            throw;
        }
    }

    /// <summary>Loads a conversation only when the user is a participant.</summary>
    private async Task<Conversation> LoadAuthorizedConversationAsync(Guid userId, Guid conversationId, CancellationToken ct)
    {
        // Single query with the participant filter inlined: avoids loading
        // hundreds of ConversationParticipants for a busy channel just to
        // pick out one user, and conflates "not found" with "no access" so
        // the response doesn't leak conversation existence to outsiders.
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(
                c => c.Id == conversationId
                    && c.Participants.Any(p => p.UserId == userId),
                ct);

        if (conversation == null)
        {
            throw new UnauthorizedAccessException("Conversation not found or access denied.");
        }

        return conversation;
    }

    /// <summary>Resolves provider and model from the request or the user's preferred stored key.</summary>
    private async Task<(string Provider, LlmModel Model)> ResolveSelectionAsync(
        Guid userId,
        string? provider,
        string? modelId,
        CancellationToken ct)
    {
        var activeKeys = await _keys.ListAsync(userId, ct);
        var requestedProvider = provider?.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(requestedProvider) && activeKeys.All(k => k.Provider != requestedProvider))
        {
            throw new InvalidOperationException($"No active API key saved for provider '{requestedProvider}'.");
        }

        var query = _db.LlmModels.AsNoTracking().Where(m => m.IsEnabled);
        if (!string.IsNullOrWhiteSpace(requestedProvider))
        {
            query = query.Where(m => m.Provider == requestedProvider);
        }
        else
        {
            var providersWithKeys = activeKeys.Select(k => k.Provider).ToList();
            query = query.Where(m => providersWithKeys.Contains(m.Provider));
        }

        if (!string.IsNullOrWhiteSpace(modelId))
        {
            var requestedModel = modelId.Trim();
            query = query.Where(m => m.ModelId == requestedModel);
        }

        var model = await query
            .OrderBy(m => m.Provider == PreferredProvider ? 0 : 1)
            .ThenBy(m => m.SortOrder)
            .ThenBy(m => m.DisplayName)
            .FirstOrDefaultAsync(ct);

        if (model == null)
        {
            throw new InvalidOperationException("No enabled LLM model matches this request.");
        }

        return (model.Provider, model);
    }

    /// <summary>Builds token-budgeted prior turns for the provider prompt, decrypting stored content.</summary>
    private async Task<List<LlmMessage>> BuildHistoryAsync(Guid conversationId, DateTime? beforeUtc, CancellationToken ct)
    {
        var query = _db.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted);

        if (beforeUtc.HasValue)
        {
            query = query.Where(m => m.CreatedAt < beforeUtc.Value);
        }

        var rows = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(80)
            .ToListAsync(ct);

        // Walk newest to oldest, accept while under budget, then reverse so
        // the LLM sees the chronological order. The previous "clear on overflow"
        // logic threw away earlier turns whenever a single fat message blew the
        // budget mid-iteration, producing a confusing partial-suffix history.
        var collected = new List<LlmMessage>(rows.Count);
        var usedTokens = 0;

        foreach (var message in rows)
        {
            var plain = DecryptMessageSafe(message.Content, message.Id);
            if (string.IsNullOrWhiteSpace(plain)) continue;

            var estimatedTokens = EstimateTokens(plain);
            if (usedTokens + estimatedTokens > HistoryTokenBudget) break;
            usedTokens += estimatedTokens;

            collected.Add(new LlmMessage(
                message.IsAiGenerated ? LlmRole.Assistant : LlmRole.User,
                message.IsAiGenerated
                    ? plain
                    : $"{message.Sender?.FullName ?? "User"}: {plain}"));
        }

        collected.Reverse();
        return collected;
    }

    /// <summary>Assembles system, history, and user messages into the provider request list.</summary>
    private static IReadOnlyList<LlmMessage> BuildProviderMessages(string systemPrompt, List<LlmMessage> history, string userPrompt)
    {
        var messages = new List<LlmMessage>
        {
            new(LlmRole.System, systemPrompt),
        };
        messages.AddRange(history);
        messages.Add(new LlmMessage(LlmRole.User, userPrompt));
        return messages;
    }

    /// <summary>Persists the user's chat command when this turn is not reusing an existing message id.</summary>
    private async Task<Guid> PersistUserCommandAsync(Guid userId, Guid conversationId, string message, CancellationToken ct)
    {
        // The user's "/ai ..." command is a normal chat turn; broadcast it
        // through the hub so other participants see it in real time.
        var result = await _chat.SendMessageAsync(userId, conversationId, new SendMessageDto
        {
            Content = $"/ai {message}",
            IsAiGenerated = false
        }, ct);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to save user message.");
        }

        return ExtractMessageId(result.Data);
    }

    /// <summary>Persists the assistant reply with AI transparency metadata and optional tool-call payload.</summary>
    private async Task<Guid> PersistAssistantMessageAsync(
        Guid userId,
        Guid conversationId,
        string message,
        string aiMetadataJson,
        string aiDetailsJson,
        CancellationToken ct)
    {
        // broadcastViaHub:false. We already drove this message to clients via
        // AiStreamDelta + AiStreamCompleted. A second ReceiveMessage would
        // surface it twice in the UI.
        var result = await _chat.SendMessageAsync(userId, conversationId, new SendMessageDto
        {
            Content = message,
            IsAiGenerated = true,
            AiMetadataJson = aiMetadataJson,
            AiDetailsJson = aiDetailsJson
        }, ct, broadcastViaHub: false);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to save AI message.");
        }

        return ExtractMessageId(result.Data);
    }

    /// <summary>Extracts the persisted message id from a <see cref="MessageSendResult"/> wrapper.</summary>
    private static Guid ExtractMessageId(object? data) =>
        data is MessageSendResult m ? m.MessageId : Guid.Empty;

    /// <summary>Decrypts message content for history replay; returns empty string on failure.</summary>
    private string DecryptMessageSafe(string cipherText, Guid messageId)
    {
        try
        {
            return _encryption.Decrypt(cipherText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt chat history message {MessageId}", messageId);
            return string.Empty;
        }
    }

    /// <summary>Removes a leading <c>/ai</c> command marker and returns null when no command is present.</summary>
    private static string? TryStripAiCommand(string message)
    {
        var trimmed = message.TrimStart();
        if (trimmed.Equals("/ai", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return trimmed.StartsWith("/ai ", StringComparison.OrdinalIgnoreCase)
            ? trimmed[4..].TrimStart()
            : null;
    }

    /// <summary>Normalizes empty assistant output and truncates oversized responses before persistence.</summary>
    private static string NormalizeAssistantText(string text)
    {
        var normalized = string.IsNullOrWhiteSpace(text)
            ? "I could not generate a response."
            : text.Trim();

        return normalized.Length <= MaxAssistantMessageChars
            ? normalized
            : normalized[..MaxAssistantMessageChars];
    }

    /// <summary>Approximates tokens from character count for pre-flight estimates.</summary>
    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    /// <summary>Estimates prompt tokens from messages plus advertised tool schemas.</summary>
    private static int EstimateRequestTokens(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition>? tools)
    {
        var text = new StringBuilder();
        foreach (var message in messages)
        {
            text.Append(message.Role).Append(':').Append(message.Content).Append('\n');
        }

        if (tools is { Count: > 0 })
        {
            foreach (var tool in tools)
            {
                text.Append(tool.Name)
                    .Append(':')
                    .Append(tool.Description)
                    .Append(':')
                    .Append(tool.ParametersSchema.GetRawText())
                    .Append('\n');
            }
        }

        return EstimateTokens(text.ToString());
    }

    /// <summary>Filters skill-filtered tools and optionally all tools when the message looks mutation-oriented.</summary>
    private static IReadOnlyList<LlmToolDefinition>? ResolveToolsForRequest(
        bool enableTools,
        LlmModel model,
        AiSkillProfile skill,
        string message)
    {
        if (!enableTools || !model.SupportsTools || skill.AllowedTools.Count == 0)
        {
            return null;
        }

        if (skill.Skill == AiSkill.General && !LooksLikeToolIntent(message))
        {
            return null;
        }

        return LlmToolCatalog.GetProjectTools()
            .Where(t => skill.AllowedTools.Contains(t.Name))
            .ToList();
    }

    /// <summary>Detects whether the user message contains known task/project mutation intent markers.</summary>
    private static bool LooksLikeToolIntent(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return ToolIntentMarkers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Chooses a positive max-output token count from request, model default, or service default.</summary>
    private static int ResolveEstimateMaxOutputTokens(int? requestedMaxOutputTokens, LlmModel model)
    {
        var tokens = requestedMaxOutputTokens
            ?? model.MaxOutputTokens
            ?? DefaultEstimateMaxOutputTokens;
        return Math.Max(1, tokens);
    }

    /// <summary>Estimates prompt-side cost from model input pricing, returning null when pricing is missing.</summary>
    private static decimal? EstimateInputCost(LlmModel model, int promptTokens)
    {
        if (!model.InputPricePer1M.HasValue) return null;
        return decimal.Round(promptTokens * model.InputPricePer1M.Value / 1_000_000m, 8);
    }

    /// <summary>Estimates worst-case request cost using prompt tokens and max output tokens.</summary>
    private static decimal? EstimateTotalCost(LlmModel model, int promptTokens, int maxOutputTokens)
    {
        if (!model.InputPricePer1M.HasValue || !model.OutputPricePer1M.HasValue) return null;

        var inputCost = promptTokens * model.InputPricePer1M.Value / 1_000_000m;
        var outputCost = maxOutputTokens * model.OutputPricePer1M.Value / 1_000_000m;
        return decimal.Round(inputCost + outputCost, 8);
    }

    /// <summary>Computes actual turn cost from provider usage counters and registry pricing.</summary>
    private static decimal? EstimateCost(LlmModel model, LlmUsage? usage)
    {
        if (usage == null) return null;
        if (!model.InputPricePer1M.HasValue || !model.OutputPricePer1M.HasValue) return null;

        var inputCost = (usage.PromptTokens ?? 0) * model.InputPricePer1M.Value / 1_000_000m;
        var outputCost = (usage.CompletionTokens ?? 0) * model.OutputPricePer1M.Value / 1_000_000m;
        return decimal.Round(inputCost + outputCost, 8);
    }

    /// <summary>Serializes compact AI metadata stored on the chat message row.</summary>
    private static string BuildAiMetadataJson(
        string provider,
        string modelId,
        LlmUsage? usage,
        decimal? costUsd,
        int latencyMs,
        LlmFinishReason finishReason,
        IEnumerable<object> toolCalls)
    {
        return JsonSerializer.Serialize(new
        {
            provider,
            modelId,
            tokens = new
            {
                promptTokens = usage?.PromptTokens,
                completionTokens = usage?.CompletionTokens,
                totalTokens = usage?.TotalTokens
            },
            costUsd,
            latencyMs,
            finishReason = finishReason.ToString(),
            toolCalls,
            detailsRetentionDays = AiMessageDetailsPruneWorker.RetentionDays
        }, JsonOptions);
    }

    /// <summary>Serializes retained AI transparency details including context and provider finish reason.</summary>
    private static string BuildAiDetailsJson(
        SystemPromptContext systemPrompt,
        IEnumerable<object> toolCalls,
        LlmFinishReason finishReason)
    {
        var createdAt = DateTime.UtcNow;
        return JsonSerializer.Serialize(new
        {
            contextSummary = new
            {
                projectContextIncluded = !string.IsNullOrWhiteSpace(systemPrompt.ProjectContextSnapshot)
            },
            toolCalls,
            providerResponse = new
            {
                finishReason = finishReason.ToString()
            },
            createdAt,
            retainedUntil = createdAt.AddDays(AiMessageDetailsPruneWorker.RetentionDays)
        }, JsonOptions);
    }

    /// <summary>Projects tool execution results into the compact metadata form.</summary>
    private static IEnumerable<object> BuildFollowUpToolSummary(IReadOnlyList<ExecutedToolView> toolResults)
    {
        return toolResults.Select(result => new
        {
            id = result.Id,
            name = result.Name,
            success = (bool?)result.Success
        });
    }

    /// <summary>Combines original tool calls with execution results for retained transparency details.</summary>
    private static IEnumerable<object> BuildFollowUpToolDetails(
        IReadOnlyList<LlmToolCall> toolCalls,
        IReadOnlyList<ExecutedToolView> toolResults)
    {
        return toolCalls.Select(call =>
        {
            var result = toolResults.FirstOrDefault(r => r.Id == call.Id);
            return new
            {
                id = call.Id,
                name = call.Name,
                arguments = call.ArgumentsJson,
                argumentsParsed = TryParseJson(call.ArgumentsJson),
                success = result?.Success,
                summary = result?.Summary,
                error = result?.Error,
                resultJson = result?.ResultJson
            };
        });
    }

    /// <summary>Truncates project context snapshots before storing transparency details.</summary>
    private static string? TruncateSnapshot(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= ContextSnapshotMaxChars
            ? value
            : value[..ContextSnapshotMaxChars];
    }

    /// <summary>Accumulates streaming tool-call fragments keyed by provider index.</summary>
    private static ToolCallBuffer GetToolCallBuffer(Dictionary<int, ToolCallBuffer> buffers, int index)
    {
        if (buffers.TryGetValue(index, out var existing))
        {
            return existing;
        }

        var created = new ToolCallBuffer();
        buffers[index] = created;
        return created;
    }

    /// <summary>Converts accumulated tool buffers into confirmation views for the client.</summary>
    private static IReadOnlyList<LlmToolCallView> BuildToolCallViews(Dictionary<int, ToolCallBuffer> buffers)
    {
        return buffers
            .OrderBy(kvp => kvp.Key)
            .Select((kvp, i) =>
            {
                var args = kvp.Value.ArgumentsJson.Length == 0
                    ? "{}"
                    : kvp.Value.ArgumentsJson.ToString();
                var id = string.IsNullOrWhiteSpace(kvp.Value.Id)
                    ? $"tool-{i}"
                    : kvp.Value.Id!;
                var name = string.IsNullOrWhiteSpace(kvp.Value.Name)
                    ? "unknown_tool"
                    : kvp.Value.Name!;

                return new LlmToolCallView(
                    id,
                    "function",
                    new LlmToolCallFunctionView(name, args, TryParseJson(args)));
            })
            .ToList();
    }

    /// <summary>Parses JSON for UI/tool previews and returns null on malformed input.</summary>
    private static JsonElement? TryParseJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>SignalR group name for a conversation stream.</summary>
    private static string GroupName(Guid conversationId) => $"conversation:{conversationId}";

    /// <summary>Cache key for <see cref="PendingFollowUpState"/> between tool confirmation and follow-up stream.</summary>
    private static string PendingCacheKey(Guid requestId) => $"ai:pending:{requestId:N}";

    /// <summary>
    /// Tacks the pre-built project-context block onto a skill's static system
    /// prompt. Non-project conversations get the skill prompt unchanged.
    /// </summary>
    private async Task<SystemPromptContext> ComposeSystemPromptAsync(
        string baseSystemPrompt,
        Guid? projectId,
        CancellationToken ct)
    {
        if (projectId is not Guid pid || pid == Guid.Empty)
        {
            return new SystemPromptContext(baseSystemPrompt, null);
        }

        var projectBlock = await _projectContext.BuildAsync(pid, ct);
        if (string.IsNullOrEmpty(projectBlock))
        {
            return new SystemPromptContext(baseSystemPrompt, null);
        }

        return new SystemPromptContext(
            baseSystemPrompt + "\n\n" + projectBlock,
            TruncateSnapshot(projectBlock));
    }

    /// <inheritdoc />
    public async Task<LlmChatResponseDto?> StreamFollowUpAsync(
        Guid userId,
        Guid conversationId,
        Guid originalRequestId,
        IReadOnlyList<ExecutedToolView> toolResults,
        CancellationToken ct)
    {
        var pending = await _cache.GetAsync<PendingFollowUpState>(PendingCacheKey(originalRequestId));
        if (pending == null) return null;

        // Authorization is doubled-up: (a) the cache binds state to a user
        // already, but (b) we re-check here so a leaked request id from
        // another user can't be redeemed even if the cache key was guessed.
        if (pending.UserId != userId || pending.ConversationId != conversationId)
        {
            _logger.LogWarning(
                "Follow-up request id {RequestId} requested by user {UserId} but pending state belongs to {OwnerUserId}",
                originalRequestId, userId, pending.UserId);
            return null;
        }

        var provider = _providers.Get(pending.Provider)
            ?? throw new InvalidOperationException($"Provider '{pending.Provider}' is not supported.");
        var apiKey = await _keys.ResolveDecryptedKeyAsync(userId, pending.Provider, ct)
            ?? throw new InvalidOperationException($"No active API key saved for provider '{pending.Provider}'.");

        var followUpRequestId = Guid.NewGuid();

        // Conversation: [system, history..., user, assistant(with tool_calls), tool_result*]
        var messages = new List<LlmMessage>
        {
            new(LlmRole.System, pending.SystemPrompt),
        };
        messages.AddRange(pending.HistoryMessages);
        messages.Add(new LlmMessage(LlmRole.User, pending.UserMessage));
        messages.Add(new LlmMessage(
            Role: LlmRole.Assistant,
            Content: string.IsNullOrEmpty(pending.AssistantPreamble) ? null : pending.AssistantPreamble,
            ToolCalls: pending.ToolCalls));

        foreach (var result in toolResults)
        {
            messages.Add(new LlmMessage(
                Role: LlmRole.Tool,
                Content: result.ResultJson,
                ToolCallId: result.Id,
                Name: result.Name));
        }

        var stopwatch = Stopwatch.StartNew();
        var fullText = new StringBuilder();
        LlmUsage? usage = null;
        var finishReason = LlmFinishReason.InProgress;

        var scope = await _usageMeter.StartAsync(new AiUsageStart(
            pending.ProjectId,
            userId,
            PlanId: null,
            AiCapability.Chat,
            StrategyVersion: "llm-v1-followup",
            Locale: null,
            Tier: pending.Tier), ct);

        using var inFlight = _inFlight.Register(followUpRequestId, userId, conversationId, ct, out var streamCt);

        await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamStarted", new
        {
            RequestId = followUpRequestId,
            OriginalRequestId = originalRequestId,
            UserId = userId,
            ConversationId = conversationId,
            pending.Provider,
            ModelId = pending.ModelId,
            FollowUp = true,
        }, streamCt);

        try
        {
            // No tools advertised on the follow-up. The model has already
            // called what it wanted, this round is for narration only.
            var llmRequest = new LlmChatRequest
            {
                ModelId = pending.ModelId,
                Messages = messages,
                Tools = null,
                RequestId = followUpRequestId.ToString(),
            };

            await foreach (var chunk in provider.StreamChatAsync(llmRequest, apiKey, streamCt))
            {
                if (!string.IsNullOrEmpty(chunk.DeltaText))
                {
                    fullText.Append(chunk.DeltaText);
                    await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamDelta", new
                    {
                        RequestId = followUpRequestId,
                        OriginalRequestId = originalRequestId,
                        UserId = userId,
                        Delta = chunk.DeltaText,
                        FollowUp = true,
                    }, streamCt);
                }

                if (chunk.Usage != null) usage = chunk.Usage;
                if (chunk.FinishReason != LlmFinishReason.InProgress) finishReason = chunk.FinishReason;
            }

            var finalText = NormalizeAssistantText(fullText.ToString());

            // We can't get the LlmModel row from cache; look up cost data only
            // if registry has it; otherwise skip the EstimatedCostUsd field.
            decimal? cost = null;
            var modelRow = await _db.LlmModels
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Provider == pending.Provider && m.ModelId == pending.ModelId, ct);
            if (modelRow != null)
            {
                cost = EstimateCost(modelRow, usage);
            }

            stopwatch.Stop();
            var latencyMs = (int)stopwatch.ElapsedMilliseconds;
            var aiMetadataJson = BuildAiMetadataJson(
                pending.Provider,
                pending.ModelId,
                usage,
                cost,
                latencyMs,
                finishReason,
                BuildFollowUpToolSummary(toolResults));
            var aiDetailsJson = BuildAiDetailsJson(
                new SystemPromptContext(pending.SystemPrompt, pending.ProjectContextSnapshot),
                BuildFollowUpToolDetails(pending.ToolCalls, toolResults),
                finishReason);
            var assistantMessageId = await PersistAssistantMessageAsync(
                userId,
                conversationId,
                finalText,
                aiMetadataJson,
                aiDetailsJson,
                ct);

            await _usageMeter.CompleteAsync(scope, new AiUsageResult(
                Success: true,
                LatencyMs: latencyMs,
                Metadata: new AiUsageMetadata(
                    PromptVersion: "chat-v1-followup",
                    Provider: pending.Provider,
                    Model: pending.ModelId,
                    InputTokens: usage?.PromptTokens,
                    OutputTokens: usage?.CompletionTokens,
                    TotalTokens: usage?.TotalTokens),
                MetadataJson: JsonSerializer.Serialize(new
                {
                    conversationId,
                    requestId = followUpRequestId,
                    originalRequestId,
                    estimatedCostUsd = cost,
                    finishReason = finishReason.ToString(),
                }, JsonOptions)), ct);

            var usageView = new LlmChatUsageView(
                usage?.PromptTokens,
                usage?.CompletionTokens,
                usage?.TotalTokens,
                cost);

            await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamCompleted", new
            {
                RequestId = followUpRequestId,
                OriginalRequestId = originalRequestId,
                UserId = userId,
                MessageId = assistantMessageId,
                Usage = usageView,
                FinishReason = finishReason.ToString(),
                FollowUp = true,
            }, ct);

            // Once the round-trip succeeds the snapshot is dead weight. Remove
            // proactively rather than leaning on TTL, which keeps Redis tidy.
            await _cache.RemoveAsync(PendingCacheKey(originalRequestId));

            return new LlmChatResponseDto(
                followUpRequestId,
                Guid.Empty, // no new user message in the follow-up
                assistantMessageId,
                pending.Provider,
                pending.ModelId,
                finalText,
                usageView,
                HasToolCalls: false,
                ToolCalls: Array.Empty<LlmToolCallView>());
        }
        catch (OperationCanceledException) when (streamCt.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            await _usageMeter.CompleteAsync(scope, new AiUsageResult(
                Success: false,
                LatencyMs: (int)stopwatch.ElapsedMilliseconds,
                ErrorCode: "cancelled",
                ErrorMessage: "Follow-up cancelled by user.",
                Metadata: new AiUsageMetadata("chat-v1-followup", pending.Provider, pending.ModelId, usage?.PromptTokens, usage?.CompletionTokens, usage?.TotalTokens)), ct);

            await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamCancelled", new
            {
                RequestId = followUpRequestId,
                OriginalRequestId = originalRequestId,
                UserId = userId,
                PartialText = fullText.ToString(),
                FollowUp = true,
            }, ct);

            await _cache.RemoveAsync(PendingCacheKey(originalRequestId));
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "LLM follow-up failed for user {UserId} conversation {ConversationId}", userId, conversationId);
            await _usageMeter.CompleteAsync(scope, new AiUsageResult(
                Success: false,
                LatencyMs: (int)stopwatch.ElapsedMilliseconds,
                ErrorCode: ex is LlmProviderException providerEx ? $"provider_{providerEx.StatusCode}" : "llm_followup_failed",
                ErrorMessage: ex.Message,
                Metadata: new AiUsageMetadata("chat-v1-followup", pending.Provider, pending.ModelId, null, null, null)), ct);

            await _hub.Clients.Group(GroupName(conversationId)).SendAsync("AiStreamFailed", new
            {
                RequestId = followUpRequestId,
                OriginalRequestId = originalRequestId,
                UserId = userId,
                Error = "AI follow-up failed. The tool actions still completed.",
                FollowUp = true,
            }, ct);

            await _cache.RemoveAsync(PendingCacheKey(originalRequestId));
            throw;
        }
    }

    /// <summary>Accumulates streaming tool-call deltas until the provider finishes the turn.</summary>
    private sealed class ToolCallBuffer
    {
        /// <summary>Provider tool-call id, set when first emitted in the stream.</summary>
        public string? Id { get; set; }

        /// <summary>Tool name, set when first emitted in the stream.</summary>
        public string? Name { get; set; }
        /// <summary>Streaming JSON arguments buffer for this tool-call slot.</summary>
        public StringBuilder ArgumentsJson { get; } = new();
    }

    /// <summary>Final system prompt plus truncated project context snapshot for transparency details.</summary>
    private sealed record SystemPromptContext(string Prompt, string? ProjectContextSnapshot);
}
