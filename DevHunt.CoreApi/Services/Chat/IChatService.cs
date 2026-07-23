using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DevHunt.CoreApi.Services.Chat;

/// <summary>
/// Core messaging operations for direct, group, and project-channel conversations. Implemented by
/// <see cref="ChatService"/> and consumed by chat controllers and AI streaming flows.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Lists the caller's conversations excluding project channels (those are listed via
    /// <see cref="IProjectChannelService"/>). Includes last-message preview and unread counts.
    /// </summary>
    /// <param name="userId">Authenticated user whose conversations are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Conversation summaries ordered by recent activity.</returns>
    Task<ChatResult<object>> GetMyConversationsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Finds an existing 1:1 conversation or creates one inside a serializable transaction to
    /// prevent duplicate direct threads.
    /// </summary>
    /// <param name="userId">Initiating user.</param>
    /// <param name="otherUserId">Peer user; must differ from <paramref name="userId"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Conversation id on success, or 400 when self-chat is requested.</returns>
    Task<ChatResult<object>> GetOrCreateDirectConversationAsync(Guid userId, Guid otherUserId, CancellationToken ct = default);

    /// <summary>
    /// Legacy entry point that resolves the project's <c>#general</c> channel via
    /// <see cref="IProjectChannelService.EnsureGeneralAsync"/>.
    /// </summary>
    /// <param name="userId">Project member requesting chat access.</param>
    /// <param name="projectId">Project whose default channel id is returned.</param>
    /// <returns>Conversation id of the general channel, or an error from the channel service.</returns>
    Task<ChatResult<object>> GetOrCreateProjectChatAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Returns a paginated, chronological page of decrypted messages and updates the caller's
    /// <c>LastReadAt</c> for the conversation.
    /// </summary>
    /// <param name="userId">Participant requesting messages.</param>
    /// <param name="conversationId">Conversation to read.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Page size clamped to 1–100.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Message DTOs with pagination metadata, or 403 when not a participant.</returns>
    Task<ChatResult<object>> GetMessagesAsync(Guid userId, Guid conversationId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Validates access, censors profanity, encrypts content, persists the message, and optionally
    /// broadcasts via SignalR. Triggers achievements and in-app notifications for other participants.
    /// </summary>
    /// <param name="userId">Sender.</param>
    /// <param name="conversationId">Target conversation.</param>
    /// <param name="dto">Plain-text content, optional reply target, and AI metadata flags.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="broadcastViaHub">When false, skips the <c>ReceiveMessage</c> hub event (used by LLM streaming).</param>
    /// <returns>Saved message id and timestamp as <see cref="MessageSendResult"/>.</returns>
    Task<ChatResult<object>> SendMessageAsync(Guid userId, Guid conversationId, SendMessageDto dto, CancellationToken ct = default, bool broadcastViaHub = true);

    /// <summary>
    /// Replaces message content for the sender's own messages; broadcasts <c>MessageEdited</c> to the conversation group.
    /// </summary>
    /// <param name="userId">Editor; must be the original sender.</param>
    /// <param name="messageId">Message to edit.</param>
    /// <param name="dto">New plain-text content (profanity censored before save).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated message id, or 403/404 on ownership or access failure.</returns>
    Task<ChatResult<object>> EditMessageAsync(Guid userId, Guid messageId, EditMessageDto dto, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a message by clearing encrypted content. Senders may delete their own messages;
    /// project-channel moderators with <c>CanDeleteMessages</c> may delete others' messages.
    /// </summary>
    /// <param name="userId">User performing the deletion.</param>
    /// <param name="messageId">Message to remove from the UI.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success after broadcasting <c>MessageDeleted</c>, or 403/404 on failure.</returns>
    Task<ChatResult> DeleteMessageAsync(Guid userId, Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Adds or removes the caller's emoji reaction and pushes aggregated counts via
    /// <c>MessageReactionsUpdated</c>.
    /// </summary>
    /// <param name="userId">Participant toggling the reaction.</param>
    /// <param name="messageId">Target message.</param>
    /// <param name="dto">Emoji string (1–32 chars after trim).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the reaction was added and the emoji used.</returns>
    Task<ChatResult<object>> ToggleReactionAsync(Guid userId, Guid messageId, MessageReactionDto dto, CancellationToken ct = default);

    /// <summary>
    /// Pins or unpins a message for all conversation participants and broadcasts
    /// <c>MessagePinUpdated</c>.
    /// </summary>
    /// <param name="userId">Participant changing pin state.</param>
    /// <param name="messageId">Message to pin or unpin.</param>
    /// <param name="dto">Desired pin state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Pin metadata payload echoed to clients.</returns>
    Task<ChatResult<object>> SetMessagePinAsync(Guid userId, Guid messageId, PinMessageDto dto, CancellationToken ct = default);

    /// <summary>
    /// Creates a group conversation and enrols the creator plus all validated active invitees.
    /// </summary>
    /// <param name="userId">Group creator.</param>
    /// <param name="dto">Title and participant user ids.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New conversation id, or 400 when title or invitees are invalid.</returns>
    Task<ChatResult<object>> CreateGroupChatAsync(Guid userId, CreateGroupChatDto dto, CancellationToken ct = default);

    /// <summary>
    /// Adds an active user to an existing group chat. Caller must already be a participant.
    /// </summary>
    /// <param name="currentUserId">Participant performing the add.</param>
    /// <param name="conversationId">Group conversation id.</param>
    /// <param name="userId">User to invite.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or 400/403/404/409 when validation fails.</returns>
    Task<ChatResult> AddParticipantAsync(Guid currentUserId, Guid conversationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Removes another user from a group chat. Caller must be a participant; only group chats support this.
    /// </summary>
    /// <param name="currentUserId">Participant performing the removal.</param>
    /// <param name="conversationId">Group conversation id.</param>
    /// <param name="userId">Participant to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or 400/403/404 when the target is missing or chat type is unsupported.</returns>
    Task<ChatResult> RemoveParticipantAsync(Guid currentUserId, Guid conversationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Removes the caller from a group chat. Fails when they are the last remaining participant.
    /// </summary>
    /// <param name="userId">Participant leaving the chat.</param>
    /// <param name="conversationId">Group conversation id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or 400/404 when leaving is not allowed.</returns>
    Task<ChatResult> LeaveGroupChatAsync(Guid userId, Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Sets the caller's per-conversation mute flag on their participant row.
    /// </summary>
    /// <param name="userId">Participant updating mute preference.</param>
    /// <param name="conversationId">Conversation to mute or unmute.</param>
    /// <param name="dto">Desired mute state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Current mute flag after save, or 403 when not a participant.</returns>
    Task<ChatResult<object>> ToggleMuteAsync(Guid userId, Guid conversationId, MuteDto dto, CancellationToken ct = default);

    /// <summary>
    /// Lists participants with profile fields, join/read timestamps, and mute state for a
    /// conversation the caller belongs to.
    /// </summary>
    /// <param name="userId">Requesting participant.</param>
    /// <param name="conversationId">Conversation whose roster is returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Participant rows, or 403 when the caller lacks access.</returns>
    Task<ChatResult<object>> GetParticipantsAsync(Guid userId, Guid conversationId, CancellationToken ct = default);
}
