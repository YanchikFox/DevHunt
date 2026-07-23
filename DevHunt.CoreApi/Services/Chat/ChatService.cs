using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Microsoft.AspNetCore.SignalR;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Hubs;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Ai.Llm;
using DevHunt.CoreApi.Services.Badges;
using DevHunt.CoreApi.Services.Moderation;
using Microsoft.Extensions.DependencyInjection;

namespace DevHunt.CoreApi.Services.Chat;

/// <summary>
/// EF-backed implementation of <see cref="IChatService"/>. Encrypts message bodies at rest,
/// broadcasts real-time updates through <see cref="ChatHub"/>, and integrates profanity filtering,
/// audit logging, achievements, and notifications.
/// </summary>
public class ChatService : IChatService
{
    private readonly DevHuntDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IEncryptionService _encryptionService;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly IEventBusService _eventBus;
    private readonly ILogger<ChatService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IProfanityFilterService _profanityFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="context">Database context for conversations, participants, messages, users, and project links.</param>
    /// <param name="auditService">Audit writer for message and conversation events.</param>
    /// <param name="encryptionService">Encrypts message content at rest and decrypts it for responses.</param>
    /// <param name="chatHub">SignalR hub context used to broadcast chat updates.</param>
    /// <param name="eventBus">Publishes message domain events for downstream consumers.</param>
    /// <param name="logger">Logger for message processing and decryption failures.</param>
    /// <param name="serviceProvider">Resolves optional collaborators such as channel services, notifications, and achievements.</param>
    /// <param name="profanityFilter">Censors message content before it is encrypted and saved.</param>
    public ChatService(
        DevHuntDbContext context,
        IAuditService auditService,
        IEncryptionService encryptionService,
        IHubContext<ChatHub> chatHub,
        IEventBusService eventBus,
        ILogger<ChatService> logger,
        IServiceProvider serviceProvider,
        IProfanityFilterService profanityFilter)
    {
        _context = context;
        _auditService = auditService;
        _encryptionService = encryptionService;
        _chatHub = chatHub;
        _eventBus = eventBus;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _profanityFilter = profanityFilter;
    }

    /// <summary>
    /// Decrypts stored ciphertext for API responses; returns a placeholder when decryption fails
    /// so ciphertext is never leaked to clients.
    /// </summary>
    private string DecryptMessageSafe(string cipherText, Guid messageId)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        try
        {
            return _encryptionService.Decrypt(cipherText);
        }
        catch (Exception ex)
        {
            // C-05: Never leak ciphertext to the client
            _logger.LogError(ex, "Failed to decrypt message {MessageId}", messageId);
            return "[Message unavailable]";
        }
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> GetMyConversationsAsync(Guid userId, CancellationToken ct = default)
    {
        // Project channels are surfaced via /projects/{id}/channels and are
        // navigated to explicitly in the UI drill-down. Excluding them here
        // prevents duplicates in the flat "Messages" list.
        var raw = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.Type != ConversationType.ProjectChannel &&
                        c.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Type,
                c.Title,
                c.LastMessageAt,
                Participants = c.Participants.Select(p => new
                {
                    p.UserId,
                    p.User.FullName,
                    p.User.AvatarUrl,
                    p.User.LastLogin
                }),
                LastMessage = c.Messages
                    .Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new
                    {
                        m.Id,
                        m.Content,
                        m.SenderId,
                        m.IsAiGenerated,
                        SenderFullName = m.Sender.FullName,
                    })
                    .FirstOrDefault(),
                UnreadCount = c.Messages
                    .Count(m => m.CreatedAt > (c.Participants
                        .Where(p => p.UserId == userId)
                        .Select(p => p.LastReadAt)
                        .FirstOrDefault() ?? DateTime.MinValue) &&
                        m.SenderId != userId)
            })
            .ToListAsync(ct);

        var conversations = raw.Select(c => new
        {
            c.Id,
            c.Type,
            c.Title,
            c.LastMessageAt,
            c.Participants,
            LastMessagePreview = c.LastMessage == null
                ? null
                : BuildMessagePreview(DecryptMessageSafe(c.LastMessage.Content, c.LastMessage.Id)),
            LastMessageSenderId = c.LastMessage?.SenderId,
            LastMessageSenderName = c.LastMessage?.SenderFullName,
            LastMessageIsAi = c.LastMessage?.IsAiGenerated ?? false,
            c.UnreadCount
        }).ToList();

        return ChatResult<object>.Success(conversations);
    }

    /// <summary>Builds a single-line preview (max 120 chars) from decrypted message text.</summary>
    private static string BuildMessagePreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        // Collapse newlines and trim whitespace for single-line preview
        var single = System.Text.RegularExpressions.Regex.Replace(content, "\\s+", " ").Trim();
        const int max = 120;
        return single.Length <= max ? single : single.Substring(0, max - 1) + "\u2026";
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> GetOrCreateDirectConversationAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
    {
        if (userId == otherUserId)
        {
            return ChatResult<object>.Failure("Cannot create conversation with yourself", 400);
        }

        // C-01: Serializable transaction prevents two concurrent requests from creating duplicate
        // conversations. DEV-118: the loser of a serialization race throws SQLSTATE 40001 — retry
        // (the re-query then returns the now-existing conversation) instead of bubbling a 500.
        const int maxAttempts = 3;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await TryGetOrCreateDirectConversationAsync(userId, otherUserId, ct);
            }
            catch (Exception ex) when (attempt < maxAttempts && IsSerializationFailure(ex))
            {
                _logger.LogWarning(
                    "Serialization conflict creating direct conversation for {UserId}/{OtherUserId} (attempt {Attempt}/{Max}); retrying",
                    userId, otherUserId, attempt, maxAttempts);
                // Drop the failed attempt's tracked (Added) entities so the retry starts clean.
                _context.ChangeTracker.Clear();
            }
        }
    }

    private async Task<ChatResult<object>> TryGetOrCreateDirectConversationAsync(Guid userId, Guid otherUserId, CancellationToken ct)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        var existingConversation = await _context.Conversations
            .Where(c => c.Type == ConversationType.Direct &&
                        c.Participants.Any(p => p.UserId == userId) &&
                        c.Participants.Any(p => p.UserId == otherUserId))
            .FirstOrDefaultAsync(ct);

        if (existingConversation != null)
        {
            await tx.CommitAsync(ct);
            return ChatResult<object>.Success(new { ConversationId = existingConversation.Id });
        }

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);

        _context.ConversationParticipants.AddRange(
            new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            },
            new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                UserId = otherUserId,
                JoinedAt = DateTime.UtcNow
            }
        );

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _auditService.LogActionAsync(userId, "Direct conversation created", "Conversation", conversation.Id, $"{{\"OtherUserId\":\"{otherUserId}\"}}");

        return ChatResult<object>.Success(new { ConversationId = conversation.Id });
    }

    /// <summary>True if the exception (or any inner exception) is a PostgreSQL serialization
    /// failure (40001) or deadlock (40P01), which are safe to retry.</summary>
    private static bool IsSerializationFailure(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is PostgresException pg && pg.SqlState is "40001" or "40P01")
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> GetOrCreateProjectChatAsync(Guid userId, Guid projectId)
    {
        // Backwards-compatible entry point for the legacy "project chat" endpoint.
        // With channels, project chat == the #general channel of the project,
        // so we delegate to the channel service rather than manage a parallel
        // Group-type conversation.
        var channelService = _serviceProvider.GetRequiredService<IProjectChannelService>();
        var result = await channelService.EnsureGeneralAsync(userId, projectId);

        return result.IsSuccess
            ? ChatResult<object>.Success(new { ConversationId = result.Data!.Id })
            : ChatResult<object>.Failure(result.ErrorMessage!, result.StatusCode);
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> GetMessagesAsync(Guid userId, Guid conversationId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);

        if (!await IsUserParticipantAsync(conversationId, userId))
        {
            return ChatResult<object>.Failure("You don't have access to this conversation", 403);
        }

        var messagesQuery = _context.Messages
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted);

        var total = await messagesQuery.CountAsync(ct);

        var messagesData = await messagesQuery
            .Include(m => m.Sender)
            .Include(m => m.ReplyTo!)
                .ThenInclude(r => r.Sender)
            .Include(m => m.Reactions)
            .Include(m => m.AiDetails)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        await UpdateLastReadAtAsync(conversationId, userId);

        return ChatResult<object>.Success(new
        {
            Data = messagesData.OrderBy(m => m.CreatedAt).Select(MapMessageToDto),
            Pagination = CreatePaginationMetadata(page, pageSize, total)
        });
    }

    /// <summary>Clamps page to at least 1 and page size to 1–100 (default 50).</summary>
    private (int page, int pageSize) NormalizePagination(int page, int pageSize)
    {
        return (Math.Max(1, page), pageSize < 1 ? 50 : Math.Min(pageSize, 100));
    }


    /// <summary>Sets the participant's <c>LastReadAt</c> to now when fetching messages.</summary>
    private async Task UpdateLastReadAtAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId, ct);

        if (participant != null)
        {
            participant.LastReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>Maps a persisted message to an API DTO with decrypted content and grouped reactions.</summary>
    private object MapMessageToDto(Message m)
    {
        var aiMetadata = m.IsAiGenerated ? BuildAiMetadataNode(m) : null;

        return new
        {
            m.Id,
            Content = DecryptMessageSafe(m.Content ?? string.Empty, m.Id),
            m.CreatedAt,
            m.IsEdited,
            m.IsAiGenerated,
            AiMetadata = aiMetadata,
            m.IsPinned,
            m.PinnedAt,
            m.PinnedByUserId,
            Sender = new { m.Sender?.Id, m.Sender?.FullName, m.Sender?.AvatarUrl },
            ReplyTo = m.ReplyTo == null ? null : new
            {
                m.ReplyTo.Id,
                Content = DecryptMessageSafe(m.ReplyTo.Content ?? string.Empty, m.ReplyTo.Id),
                FullName = m.ReplyTo.Sender?.FullName
            },
            Reactions = m.Reactions
                .GroupBy(r => r.Emoji)
                .Select(g => new
                {
                    Emoji = g.Key,
                    Count = g.Count(),
                    UserIds = g.Select(r => r.UserId)
                })
        };
    }

    /// <summary>Parses AI transparency JSON and attaches client-safe detail metadata when retained.</summary>
    private static JsonObject? BuildAiMetadataNode(Message message)
    {
        if (string.IsNullOrWhiteSpace(message.AiMetadataJson)) return null;

        try
        {
            var node = JsonNode.Parse(message.AiMetadataJson)?.AsObject();
            if (node == null) return null;

            node["detailsAvailable"] = message.AiDetails != null;
            node["detailsRetentionDays"] = AiMessageDetailsPruneWorker.RetentionDays;

            if (message.AiDetails == null)
            {
                node["details"] = null;
                return node;
            }

            node["detailsRetainedUntil"] = message.AiDetails.CreatedAt
                .AddDays(AiMessageDetailsPruneWorker.RetentionDays);

            var detailsNode = JsonNode.Parse(message.AiDetails.FullPayloadJson);
            node["details"] = detailsNode is JsonObject details
                ? BuildClientSafeAiDetails(details)
                : null;
            return node;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Strips sensitive fields from full AI payload before exposing details to clients.</summary>
    private static JsonObject BuildClientSafeAiDetails(JsonObject details)
    {
        var safe = new JsonObject
        {
            ["redacted"] = true
        };

        CopyJsonProperty(details, safe, "createdAt");
        CopyJsonProperty(details, safe, "retainedUntil");

        if (details.TryGetPropertyValue("providerResponse", out var providerResponseNode)
            && providerResponseNode is JsonObject providerResponse)
        {
            var safeProviderResponse = new JsonObject();
            CopyJsonProperty(providerResponse, safeProviderResponse, "finishReason");
            safe["providerResponse"] = safeProviderResponse;
        }

        if (details.TryGetPropertyValue("toolCalls", out var toolCallsNode)
            && toolCallsNode is JsonArray toolCalls)
        {
            safe["toolCalls"] = BuildClientSafeToolCalls(toolCalls);
        }

        return safe;
    }

    /// <summary>Redacts tool-call arguments from AI details while keeping id, name, and outcome.</summary>
    private static JsonArray BuildClientSafeToolCalls(JsonArray toolCalls)
    {
        var safe = new JsonArray();

        foreach (var item in toolCalls)
        {
            if (item is not JsonObject tool) continue;

            var safeTool = new JsonObject();
            CopyJsonProperty(tool, safeTool, "id");
            CopyJsonProperty(tool, safeTool, "name");
            CopyJsonProperty(tool, safeTool, "success");
            CopyJsonProperty(tool, safeTool, "summary");
            CopyJsonProperty(tool, safeTool, "error");
            safe.Add(safeTool);
        }

        return safe;
    }

    /// <summary>Deep-clones a JSON property from source to target when present.</summary>
    private static void CopyJsonProperty(JsonObject source, JsonObject target, string propertyName)
    {
        if (!source.TryGetPropertyValue(propertyName, out var value) || value == null) return;
        target[propertyName] = value.DeepClone();
    }

    /// <summary>Builds standard page/total/has-next metadata for message queries.</summary>
    private object CreatePaginationMetadata(int page, int pageSize, int total)
    {
        return new
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            HasNext = page * pageSize < total,
            HasPrevious = page > 1
        };
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> SendMessageAsync(Guid userId, Guid conversationId, SendMessageDto dto, CancellationToken ct = default, bool broadcastViaHub = true)
    {
        var (authorized, error) = await ValidateMessageRequestAsync(conversationId, userId, dto.ReplyToId, ct);
        if (!authorized) return ChatResult<object>.Failure(error!.Value.Message, error.Value.StatusCode);

        var conversation = await _context.Conversations.FindAsync(new object[] { conversationId }, ct);
        if (conversation == null) return ChatResult<object>.Failure("Conversation not found", 404);

        // Censor profanity in chat messages (allow sending, but replace bad words)
        dto.Content = _profanityFilter.CensorText(dto.Content);

        var message = CreateMessageEntity(conversation, userId, dto);
        _context.Messages.Add(message);

        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        // broadcastViaHub=false skips the ReceiveMessage hub broadcast — used by
        // the LLM streaming flow which delivers the assistant message via its
        // own AiStreamCompleted event and would otherwise duplicate on the UI.
        // Domain events, achievements and persistent notifications still run.
        if (broadcastViaHub)
        {
            await PublishMessageAsync(message, conversation, dto.Content, userId, ct);
        }
        else
        {
            await PublishMessageSilentAsync(message, conversation, userId, ct);
        }
        await _serviceProvider.TriggerAchievementCheckAsync(userId, AchievementTrigger.MessageSent);

        // Notify other participants via persistent in-app notifications
        await NotifyParticipantsAsync(conversationId, userId, dto.Content, ct);

        return ChatResult<object>.Success(new MessageSendResult(message.Id, message.CreatedAt));
    }

    /// <summary>Verifies active participation and that an optional reply target exists in the conversation.</summary>
    private async Task<(bool Success, (int StatusCode, string Message)? Error)> ValidateMessageRequestAsync(
        Guid conversationId, Guid userId, Guid? replyToId, CancellationToken ct = default)
    {
        if (!await IsUserParticipantAsync(conversationId, userId, ct))
        {
            return (false, (403, "You don't have access to this conversation"));
        }

        if (replyToId.HasValue)
        {
            var replyExists = await _context.Messages
                .AnyAsync(m => m.Id == replyToId.Value && m.ConversationId == conversationId, ct);
            if (!replyExists) return (false, (400, "Reply-to message not found"));
        }

        return (true, null);
    }

    /// <summary>Sends low-priority in-app notifications to all participants except the sender.</summary>
    private async Task NotifyParticipantsAsync(Guid conversationId, Guid senderId, string content, CancellationToken ct)
    {
        var recipientIds = await _context.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != senderId)
            .Select(cp => cp.UserId)
            .ToListAsync(ct);

        if (recipientIds.Count == 0) return;

        var senderName = await _context.Users
            .Where(u => u.Id == senderId)
            .Select(u => u.FullName ?? u.Email)
            .FirstOrDefaultAsync(ct) ?? "Someone";

        var preview = content.Length > 80 ? string.Concat(content.AsSpan(0, 77), "...") : content;

        var notifications = _serviceProvider.GetService<INotificationHelperService>();
        if (notifications == null) return;

        await notifications.SendBulkNotificationsAsync(
            recipientIds, "chatMessage",
            $"New message from {senderName}",
            preview,
            relatedEntityType: "Conversation",
            relatedEntityId: conversationId,
            priority: "low", ct: ct);
    }

    /// <summary>Builds an encrypted message entity, linking <c>ProjectId</c> for legacy project group chats.</summary>
    private Message CreateMessageEntity(Conversation conversation, Guid userId, SendMessageDto dto)
    {
        // C-06: Only set ProjectId for project chats (Conversation.Id == Project.Id convention)
        Guid? projectId = null;
        if (conversation.Type == ConversationType.Group)
        {
            var isProjectChat = _context.Projects.Any(p => p.Id == conversation.Id);
            if (isProjectChat) projectId = conversation.Id;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = userId,
            ConversationId = conversation.Id,
            Content = _encryptionService.Encrypt(dto.Content),
            MessageType = conversation.Type == ConversationType.Direct ? MessageType.Direct : MessageType.Group,
            ReplyToId = dto.ReplyToId,
            CreatedAt = DateTime.UtcNow,
            IsAiGenerated = dto.IsAiGenerated,
            AiMetadataJson = dto.IsAiGenerated ? dto.AiMetadataJson : null,
            ProjectId = projectId
        };

        if (dto.IsAiGenerated && !string.IsNullOrWhiteSpace(dto.AiDetailsJson))
        {
            message.AiDetails = new AiMessageDetails
            {
                MessageId = message.Id,
                FullPayloadJson = dto.AiDetailsJson,
                CreatedAt = DateTime.UtcNow
            };
        }

        return message;
    }

    /// <summary>Publishes <c>ReceiveMessage</c> to the conversation SignalR group plus domain and audit events.</summary>
    private async Task PublishMessageAsync(Message message, Conversation conversation, string plainContent, Guid userId, CancellationToken ct = default)
    {
        var sender = await _context.Users.FindAsync(new object[] { userId }, ct);

        var messageDto = new
        {
            message.Id,
            Content = plainContent,
            message.CreatedAt,
            message.IsAiGenerated,
            message.IsPinned,
            Reactions = Array.Empty<object>(),
            Sender = new { sender?.Id, sender?.FullName, sender?.AvatarUrl },
            ConversationId = conversation.Id,
            ReplyToId = message.ReplyToId
        };

        // C-04: Unified event name — Hub and Service both use "ReceiveMessage"
        await _chatHub.Clients.Group($"conversation:{conversation.Id}").SendAsync("ReceiveMessage", messageDto, ct);

        await _eventBus.PublishAsync(DomainEvents.MessageSent(
            message.Id,
            conversation.Id,
            userId,
            message.ProjectId,
            message.MessageType.ToString()));

        await _auditService.LogActionAsync(userId, "Message sent", "Message", message.Id, $"{{\"ConversationId\":\"{conversation.Id}\"}}");
    }

    /// <summary>
    /// Same domain-event + audit wiring as <see cref="PublishMessageAsync"/>, but
    /// without the SignalR <c>ReceiveMessage</c> broadcast. Used by callers
    /// that own their own delivery channel (e.g. LLM streaming).
    /// </summary>
    private async Task PublishMessageSilentAsync(Message message, Conversation conversation, Guid userId, CancellationToken ct = default)
    {
        await _eventBus.PublishAsync(DomainEvents.MessageSent(
            message.Id,
            conversation.Id,
            userId,
            message.ProjectId,
            message.MessageType.ToString()));

        await _auditService.LogActionAsync(userId, "Message sent", "Message", message.Id, $"{{\"ConversationId\":\"{conversation.Id}\"}}");
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> EditMessageAsync(Guid userId, Guid messageId, EditMessageDto dto, CancellationToken ct = default)
    {
        var message = await _context.Messages.FindAsync(new object[] { messageId }, ct);
        if (message == null)
        {
            return ChatResult<object>.Failure("Message not found", 404);
        }

        if (message.SenderId != userId)
        {
            return ChatResult<object>.Failure("You can only edit your own messages", 403);
        }

        if (message.ConversationId is null ||
            !await IsUserParticipantAsync(message.ConversationId.Value, userId, ct))
        {
            return ChatResult<object>.Failure("You don't have access to this conversation", 403);
        }

        // Censor profanity in edited messages
        dto.Content = _profanityFilter.CensorText(dto.Content);

        message.Content = _encryptionService.Encrypt(dto.Content);
        message.IsEdited = true;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "Message edited", "Message", messageId, null);

        if (message.ConversationId.HasValue)
        {
            var messageDto = new
            {
                message.Id,
                Content = dto.Content,
                message.IsEdited,
                EditedAt = DateTime.UtcNow
            };
            await _chatHub.Clients.Group($"conversation:{message.ConversationId.Value}").SendAsync("MessageEdited", messageDto, ct);
        }

        return ChatResult<object>.Success(new { MessageId = message.Id });
    }

    /// <inheritdoc />
    public async Task<ChatResult> DeleteMessageAsync(Guid userId, Guid messageId, CancellationToken ct = default)
    {
        var message = await _context.Messages.FindAsync(new object[] { messageId }, ct);
        if (message == null)
        {
            return ChatResult.Failure("Message not found", 404);
        }

        if (message.ConversationId is null ||
            !await IsUserParticipantAsync(message.ConversationId.Value, userId, ct))
        {
            return ChatResult.Failure("You don't have access to this conversation", 403);
        }

        if (message.SenderId != userId &&
            !await HasChannelPermissionAsync(message.ConversationId.Value, userId, p => p.CanDeleteMessages, false, ct))
        {
            return ChatResult.Failure("You can only delete your own messages", 403);
        }

        message.IsDeleted = true;
        message.Content = _encryptionService.Encrypt(string.Empty);

        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "Message deleted", "Message", messageId, null);

        if (message.ConversationId.HasValue)
        {
            await _chatHub.Clients.Group($"conversation:{message.ConversationId.Value}").SendAsync("MessageDeleted", messageId, ct);
        }

        return ChatResult.Success();
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> ToggleReactionAsync(Guid userId, Guid messageId, MessageReactionDto dto, CancellationToken ct = default)
    {
        var emoji = dto.Emoji.Trim();
        if (string.IsNullOrWhiteSpace(emoji) || emoji.Length > 32)
        {
            return ChatResult<object>.Failure("Reaction is invalid", 400);
        }

        var message = await _context.Messages
            .Include(m => m.Reactions)
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted, ct);

        if (message?.ConversationId is null)
        {
            return ChatResult<object>.Failure("Message not found", 404);
        }

        if (!await IsUserParticipantAsync(message.ConversationId.Value, userId, ct))
        {
            return ChatResult<object>.Failure("You don't have access to this conversation", 403);
        }

        var existing = await _context.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji, ct);

        var added = existing is null;
        if (existing is null)
        {
            _context.MessageReactions.Add(new MessageReaction
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            _context.MessageReactions.Remove(existing);
        }

        await _context.SaveChangesAsync(ct);

        var payload = await BuildMessageReactionsPayloadAsync(messageId, ct);
        await _chatHub.Clients.Group($"conversation:{message.ConversationId.Value}")
            .SendAsync("MessageReactionsUpdated", payload, ct);

        return ChatResult<object>.Success(new { MessageId = messageId, Emoji = emoji, Added = added });
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> SetMessagePinAsync(Guid userId, Guid messageId, PinMessageDto dto, CancellationToken ct = default)
    {
        var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted, ct);
        if (message?.ConversationId is null)
        {
            return ChatResult<object>.Failure("Message not found", 404);
        }

        if (!await IsUserParticipantAsync(message.ConversationId.Value, userId, ct))
        {
            return ChatResult<object>.Failure("You don't have access to this conversation", 403);
        }

        message.IsPinned = dto.IsPinned;
        message.PinnedAt = dto.IsPinned ? DateTime.UtcNow : null;
        message.PinnedByUserId = dto.IsPinned ? userId : null;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, dto.IsPinned ? "Message pinned" : "Message unpinned", "Message", messageId, null);

        var payload = new
        {
            MessageId = message.Id,
            message.IsPinned,
            message.PinnedAt,
            message.PinnedByUserId
        };

        await _chatHub.Clients.Group($"conversation:{message.ConversationId.Value}")
            .SendAsync("MessagePinUpdated", payload, ct);

        return ChatResult<object>.Success(payload);
    }

    /// <summary>Loads emoji reaction counts and participant ids for hub broadcast after a toggle.</summary>
    private async Task<object> BuildMessageReactionsPayloadAsync(Guid messageId, CancellationToken ct)
    {
        var reactions = await _context.MessageReactions
            .Where(r => r.MessageId == messageId)
            .GroupBy(r => r.Emoji)
            .Select(g => new
            {
                Emoji = g.Key,
                Count = g.Count(),
                UserIds = g.Select(r => r.UserId).ToList()
            })
            .ToListAsync(ct);

        return new { MessageId = messageId, Reactions = reactions };
    }

    /// <summary>
    /// Evaluates a project-channel permission via <see cref="ChannelMemberService.ResolveEffective"/>;
    /// non-channel conversations return <paramref name="nonProjectDefault"/>.
    /// </summary>
    private async Task<bool> HasChannelPermissionAsync(
        Guid conversationId,
        Guid userId,
        Func<ChannelMemberService.EffectivePermissions, bool> permission,
        bool nonProjectDefault,
        CancellationToken ct)
    {
        var channel = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.Type, c.ProjectId, c.CreatedByUserId })
            .FirstOrDefaultAsync(ct);

        if (channel is null || channel.Type != ConversationType.ProjectChannel || channel.ProjectId is null)
        {
            return nonProjectDefault;
        }

        var ownerId = await _context.Projects
            .Where(p => p.Id == channel.ProjectId)
            .Select(p => p.OwnerId)
            .FirstOrDefaultAsync(ct);

        if (ownerId == userId || channel.CreatedByUserId == userId)
        {
            return true;
        }

        var participant = await _context.ConversationParticipants
            .AsNoTracking()
            .Include(p => p.RoleDefinition)
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId &&
                                      p.UserId == userId &&
                                      p.State == ParticipantState.Active, ct);

        var effective = ChannelMemberService.ResolveEffective(participant, ChannelRole.Member, ownerId, userId);
        return permission(effective);
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> CreateGroupChatAsync(Guid userId, CreateGroupChatDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ChatResult<object>.Failure("Group title is required", 400);
        }

        var invitedUsers = await _context.Users
            .Where(u => dto.ParticipantIds.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (invitedUsers.Count != dto.ParticipantIds.Count)
        {
            return ChatResult<object>.Failure("Some participants are invalid or inactive", 400);
        }

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Group,
            Title = dto.Title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);

        var participants = new List<ConversationParticipant>
        {
            new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            }
        };

        participants.AddRange(invitedUsers.Select(p => new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            UserId = p,
            JoinedAt = DateTime.UtcNow
        }));

        _context.ConversationParticipants.AddRange(participants);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "Group chat created", "Conversation", conversation.Id, $"{{\"Title\":\"{dto.Title}\",\"Participants\":{dto.ParticipantIds.Count}}}");

        return ChatResult<object>.Success(new { ConversationId = conversation.Id });
    }

    /// <inheritdoc />
    public async Task<ChatResult> AddParticipantAsync(Guid currentUserId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations.FindAsync(new object[] { conversationId }, ct);
        var validationResult = await ValidateAddParticipantRequestAsync(conversation, currentUserId, userId);
        if (!validationResult.IsSuccess) return validationResult;

        _context.ConversationParticipants.Add(new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });

        conversation!.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(currentUserId, "Participant added to group chat", "Conversation", conversationId, $"{{\"AddedUserId\":\"{userId}\"}}");

        return ChatResult.Success();
    }

    /// <summary>Validates group-chat type, caller participation, and invitee eligibility before add.</summary>
    private async Task<ChatResult> ValidateAddParticipantRequestAsync(Conversation? conversation, Guid currentUserId, Guid targetUserId, CancellationToken ct = default)
    {
        if (conversation == null || conversation.Type != ConversationType.Group)
            return ChatResult.Failure("Only group chats support participant management", 400);

        if (!await IsUserParticipantAsync(conversation.Id, currentUserId))
            return ChatResult.Failure("You must be a participant to add others", 403);

        var targetUser = await _context.Users.FindAsync(new object[] { targetUserId }, ct);
        if (targetUser == null || !targetUser.IsActive)
            return ChatResult.Failure("User not found or inactive", 404);

        if (await IsUserParticipantAsync(conversation.Id, targetUserId))
            return ChatResult.Failure("User is already a participant", 409);

        return ChatResult.Success();
    }

    /// <summary>Returns true when the user has an active participant row in the conversation.</summary>
    private async Task<bool> IsUserParticipantAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        return await _context.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId &&
                            cp.UserId == userId &&
                            cp.State == ParticipantState.Active, ct);
    }

    /// <inheritdoc />
    public async Task<ChatResult> RemoveParticipantAsync(Guid currentUserId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations.FindAsync(new object[] { conversationId }, ct);
        if (conversation == null || conversation.Type != ConversationType.Group)
            return ChatResult.Failure("Only group chats support participant management", 400);

        if (!await IsUserParticipantAsync(conversationId, currentUserId))
            return ChatResult.Failure("You must be a participant to remove others", 403);

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId, ct);

        if (participant == null)
            return ChatResult.Failure("Participant not found", 404);

        _context.ConversationParticipants.Remove(participant);
        conversation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(currentUserId, "Participant removed from group chat", "Conversation", conversationId, $"{{\"RemovedUserId\":\"{userId}\"}}");

        return ChatResult.Success();
    }

    /// <inheritdoc />
    public async Task<ChatResult> LeaveGroupChatAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations.FindAsync(new object[] { conversationId }, ct);
        if (conversation == null || conversation.Type != ConversationType.Group)
            return ChatResult.Failure("Only group chats support leaving", 400);

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId, ct);

        if (participant == null)
            return ChatResult.Failure("You are not a participant of this chat", 404);

        var remainingCount = await _context.ConversationParticipants
            .CountAsync(cp => cp.ConversationId == conversationId);

        if (remainingCount <= 1)
            return ChatResult.Failure("Cannot leave as the last participant", 400);

        _context.ConversationParticipants.Remove(participant);
        conversation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "Left group chat", "Conversation", conversationId, null);

        return ChatResult.Success();
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> ToggleMuteAsync(Guid userId, Guid conversationId, MuteDto dto, CancellationToken ct = default)
    {
        // C-02: Single query instead of two — eliminates TOCTOU race condition
        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId, ct);

        if (participant == null)
            return ChatResult<object>.Failure("You don't have access to this conversation", 403);

        participant.IsMuted = dto.Muted;
        await _context.SaveChangesAsync(ct);

        return ChatResult<object>.Success(new { Muted = participant.IsMuted });
    }

    /// <inheritdoc />
    public async Task<ChatResult<object>> GetParticipantsAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        if (!await IsUserParticipantAsync(conversationId, userId))
        {
            return ChatResult<object>.Failure("You don't have access to this conversation", 403);
        }

        var participants = await _context.ConversationParticipants
            .Include(cp => cp.User)
            .Where(cp => cp.ConversationId == conversationId)
            .Select(cp => new
            {
                cp.UserId,
                cp.User.FullName,
                cp.User.AvatarUrl,
                cp.JoinedAt,
                cp.LastReadAt,
                cp.User.LastLogin,
                cp.IsMuted
            })
            .ToListAsync(ct);

        return ChatResult<object>.Success(participants);
    }
}
