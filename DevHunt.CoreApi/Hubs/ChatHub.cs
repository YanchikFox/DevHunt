using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Chat;

namespace DevHunt.CoreApi.Hubs;

/// <summary>
/// SignalR Hub for real-time chat functionality.
/// Corresponds to architecture: real-time communications via WebSocket.
/// </summary>
/// <remarks>
/// <para><strong>Supported Chat Types</strong>:</para>
/// - Private conversations (1-on-1 messaging)
/// - Project team chats (all team members)
/// - Group conversations
///
/// <para><strong>SignalR Groups</strong>:</para>
/// - <b>conversation:{conversationId}</b> - All participants in a conversation
/// - <b>user:{userId}</b> - Personal channel for user-specific notifications
/// - <b>project:{projectId}</b> - All team members in a project (for project chat)
///
/// <para><strong>Security</strong>:</para>
/// - [Authorize] attribute requires authentication
/// - JoinConversation/JoinProject verify user is participant before allowing access
/// - SendMessage validates sender is conversation participant
/// - Prevents unauthorized users from reading messages or joining conversations
///
/// <para><strong>Message Encryption</strong>:</para>
/// Messages can be end-to-end encrypted using IEncryptionService.
/// Encryption is optional and configured per-conversation.
/// </remarks>
[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly DevHuntDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IPresenceService _presenceService;
    private readonly IChatService _chatService;

    /// <summary>
    /// C-08: In-memory throttle for typing events. Key: (conversationId, userId), Value: last event ticks.
    /// Static so it survives hub instance recreation. Entries are cheap (~32 bytes each).
    /// </summary>
    private static readonly ConcurrentDictionary<(Guid, Guid), long> _typingThrottle = new();
    private const int TypingThrottleMs = 3000;

    public ChatHub(ILogger<ChatHub> logger, DevHuntDbContext context, IEncryptionService encryptionService, IPresenceService presenceService, IChatService chatService)
    {
        _logger = logger;
        _context = context;
        _encryptionService = encryptionService;
        _presenceService = presenceService;
        _chatService = chatService;
    }

    public override async Task OnConnectedAsync()
    {
        // SECURITY: Verify user is authenticated
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthenticated connection attempt to ChatHub from {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("Invalid user ID in token for ChatHub connection {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        // SECURITY: Verify user is active (not blocked/deleted)
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Inactive or non-existent user {UserId} attempted to connect to ChatHub", userId.Value);
            Context.Abort();
            return;
        }

        // Add user to their personal group for notifications
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId.Value}");
        await _presenceService.SetOnlineAsync(userId.Value, "chat");
        _logger.LogInformation("User {UserId} connected to ChatHub with connection {ConnectionId}",
            userId.Value, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId.Value}");
            await _presenceService.SetOfflineAsync(userId.Value, "chat");
            _logger.LogInformation("User {UserId} disconnected from ChatHub", userId.Value);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join conversation group to receive real-time messages.
    /// </summary>
    /// <remarks>
    /// <strong>Security</strong>: Verifies user is a participant in the conversation before allowing access.
    /// Non-participants will receive an "Unauthorized" error.
    /// </remarks>
    /// <param name="conversationId">Conversation ID to join</param>
    public async Task JoinConversation(Guid conversationId)
    {
        var ct = Context.ConnectionAborted;
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized", ct);
            return;
        }

        // SECURITY: Verify user is participant in this conversation
        var isParticipant = await _context.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value, ct);

        if (!isParticipant)
        {
            _logger.LogWarning("User {UserId} attempted to join unauthorized conversation {ConversationId}",
                userId.Value, conversationId);
            await Clients.Caller.SendAsync("Error", "Forbidden: Not a participant in this conversation", ct);
            return;
        }

        var groupName = $"conversation:{conversationId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName, ct);

        _logger.LogDebug("User {UserId} joined conversation {ConversationId}", userId.Value, conversationId);
        await Clients.Group(groupName).SendAsync("UserJoined", userId.Value, ct);
    }

    /// <summary>
    /// Leave conversation group.
    /// </summary>
    public async Task LeaveConversation(Guid conversationId)
    {
        var ct = Context.ConnectionAborted;
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return;
        }

        var groupName = $"conversation:{conversationId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName, ct);

        _logger.LogDebug("User {UserId} left conversation {ConversationId}", userId.Value, conversationId);
        await Clients.Group(groupName).SendAsync("UserLeft", userId.Value, ct);
    }

    /// <summary>
    /// Join project group to receive project notifications and messages.
    /// </summary>
    /// <remarks>
    /// <strong>Security</strong>: Verifies user is a team member or owner of the project.
    /// Non-members will receive a "Forbidden" error.
    /// </remarks>
    /// <param name="projectId">Project ID to join</param>
    public async Task JoinProject(Guid projectId)
    {
        var ct = Context.ConnectionAborted;
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized", ct);
            return;
        }

        // SECURITY: Verify user is team member of this project
        var isMember = await _context.TeamMembers
            .AnyAsync(tm => tm.ProjectId == projectId &&
                           tm.UserId == userId.Value &&
                           tm.Status == TeamMemberStatus.Active.Value, ct);

        // Also check if user is project owner
        var isOwner = await _context.Projects
            .AnyAsync(p => p.Id == projectId && p.OwnerId == userId.Value, ct);

        if (!isMember && !isOwner)
        {
            _logger.LogWarning("User {UserId} attempted to join unauthorized project {ProjectId}",
                userId.Value, projectId);
            await Clients.Caller.SendAsync("Error", "Forbidden: Not a member of this project", ct);
            return;
        }

        var groupName = $"project:{projectId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName, ct);

        _logger.LogDebug("User {UserId} joined project {ProjectId}", userId.Value, projectId);
    }

    /// <summary>
    /// Leave project group.
    /// </summary>
    public async Task LeaveProject(Guid projectId)
    {
        var ct = Context.ConnectionAborted;
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return;
        }

        var groupName = $"project:{projectId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName, ct);

        _logger.LogDebug("User {UserId} left project {ProjectId}", userId.Value, projectId);
    }

    /// <summary>
    /// Send message to a conversation.
    /// </summary>
    /// <remarks>
    /// <strong>Security</strong>: Verifies sender is a participant in the conversation.
    /// Message is persisted to database and broadcast to all conversation participants via SignalR.
    /// Supports optional end-to-end encryption.
    /// </remarks>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="content">Message content (plain text or encrypted)</param>
    public async Task SendMessage(Guid conversationId, string content)
    {
        var ct = Context.ConnectionAborted;
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized", ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(content) || content.Length > 10000)
        {
            await Clients.Caller.SendAsync("Error", "Message content is invalid", ct);
            return;
        }

        // C-03: Delegate to ChatService — ensures audit, events, achievements, and profanity all fire
        var result = await _chatService.SendMessageAsync(
            userId.Value, conversationId, new SendMessageDto { Content = content }, ct);

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.ErrorMessage, ct);
        }

        _logger.LogDebug("User {UserId} sent message to conversation {ConversationId}", userId.Value, conversationId);
    }

    /// <summary>
    /// Mark messages as read in a conversation.
    /// </summary>
    /// <remarks>
    /// Updates participant's LastReadAt timestamp and notifies other participants.
    /// Used to display "read receipts" in chat UI.
    /// </remarks>
    /// <param name="conversationId">Conversation ID</param>
    public async Task MarkAsRead(Guid conversationId)
    {
        var ct = Context.ConnectionAborted;
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return;
        }

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value, ct);

        if (participant != null)
        {
            participant.LastReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            // Notify others that user read messages
            var groupName = $"conversation:{conversationId}";
            await Clients.OthersInGroup(groupName).SendAsync("MessageRead", userId.Value, DateTime.UtcNow, ct);
        }
    }

    /// <summary>
    /// Typing indicator - notify other participants that user is typing.
    /// </summary>
    /// <remarks>
    /// Broadcasts typing status to other conversation participants.
    /// Frontend should send isTyping=false when user stops typing.
    /// </remarks>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="isTyping">True if user is typing, false otherwise</param>
    public async Task Typing(Guid conversationId, bool isTyping)
    {
        var ct = Context.ConnectionAborted;
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return;
        }

        // C-08: In-memory throttle — skip DB query if isTyping=true was sent recently
        var key = (conversationId, userId.Value);
        var now = Environment.TickCount64;
        if (isTyping)
        {
            if (_typingThrottle.TryGetValue(key, out var lastTick) && now - lastTick < TypingThrottleMs)
            {
                return; // Throttled — too soon since last typing event
            }
            _typingThrottle[key] = now;
        }
        else
        {
            // isTyping=false clears throttle so next true is not delayed
            _typingThrottle.TryRemove(key, out _);
        }

        // SECURITY: Verify user is participant
        var isParticipant = await _context.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value, ct);

        if (!isParticipant)
        {
            return;
        }

        var groupName = $"conversation:{conversationId}";
        await Clients.OthersInGroup(groupName).SendAsync("UserTyping", userId.Value, isTyping, ct);
    }

    private Guid? GetUserId()
    {
        var id = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var guid) ? guid : null;
    }
}

