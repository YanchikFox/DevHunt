using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Services.Chat;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing chats and messages.
///
/// Use Cases:
/// - Messaging between project participants
/// - Direct messages between users
/// - Project group chats
///
/// Corresponds to ERD: Messages, Conversations, Conversation_Participants (devhunt_erd.puml).
///
/// Security (according to SRS v1.0, section 9.2):
/// - All messages are encrypted with AES-256 before storage in DB (handled by Service)
/// - Automatic moderation via ProfanityFilter (devhunt_moderation.puml)
/// - Audit of all operations (chat creation, sending/editing/deleting messages) (handled by Service)
/// - Access control for conversations (only participants can read/write) (handled by Service)
/// - Rate limiting at API level (via AspNetCoreRateLimit)
/// - JWT authentication mandatory for all operations
/// - TLS 1.3 for all connections (infrastructure level)
/// </summary>
[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatController"/> class.
    /// </summary>
    /// <param name="chatService">Service that enforces chat access and performs conversation/message operations.</param>
    /// <param name="logger">Logger reserved for chat controller diagnostics.</param>
    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Converts a successful chat service payload to <see cref="OkObjectResult"/> or preserves the service error status.
    /// </summary>
    private IActionResult MapResult<T>(ChatResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Converts a successful no-payload chat service result to <see cref="OkResult"/> or preserves the service error status.
    /// </summary>
    private IActionResult MapResult(ChatResult result)
    {
        if (result.IsSuccess)
        {
            return Ok(); // Or NoContent? Using Ok for now to match strict logic unless standardizing
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    #region Conversations

    /// <summary>
    /// Gets conversations that include the authenticated user.
    /// </summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetMyConversations()
    {
        var result = await _chatService.GetMyConversationsAsync(GetRequiredUserId());
        return MapResult(result);
    }

    /// <summary>
    /// Gets or creates a direct conversation between the authenticated user and another user.
    /// </summary>
    [HttpPost("conversations/direct/{otherUserId}")]
    public async Task<IActionResult> GetOrCreateDirectConversation(Guid otherUserId)
    {
        var result = await _chatService.GetOrCreateDirectConversationAsync(GetRequiredUserId(), otherUserId);
        return MapResult(result);
    }

    /// <summary>
    /// Gets or creates the project group chat when the authenticated user has service-level access.
    /// </summary>
    [HttpPost("conversations/project/{projectId}")]
    public async Task<IActionResult> GetOrCreateProjectChat(Guid projectId)
    {
        var result = await _chatService.GetOrCreateProjectChatAsync(GetRequiredUserId(), projectId);
        return MapResult(result);
    }

    #endregion

    #region Messages

    /// <summary>
    /// Gets paginated messages for a conversation when the authenticated user is allowed to read it.
    ///
    /// Query Parameters:
    /// - page: Page number (default: 1)
    /// - pageSize: Page size (default: 50, max: 100)
    ///
    /// Response: List of messages with pagination
    /// </summary>
    [HttpGet("conversations/{conversationId}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _chatService.GetMessagesAsync(GetRequiredUserId(), conversationId, page, pageSize);
        return MapResult(result);
    }

    /// <summary>
    /// Sends a message to a conversation through the chat service, which handles encryption and access checks.
    /// </summary>
    [HttpPost("conversations/{conversationId}/messages")]
    public async Task<IActionResult> SendMessage(Guid conversationId, [FromBody] SendMessageDto dto)
    {
        var result = await _chatService.SendMessageAsync(GetRequiredUserId(), conversationId, dto);
        return MapResult(result);
    }

    /// <summary>
    /// Edits a message when the chat service recognizes the authenticated user as allowed to modify it.
    /// </summary>
    [HttpPut("messages/{messageId}")]
    public async Task<IActionResult> EditMessage(Guid messageId, [FromBody] EditMessageDto dto)
    {
        var result = await _chatService.EditMessageAsync(GetRequiredUserId(), messageId, dto);
        return MapResult(result);
    }

    /// <summary>
    /// Soft-deletes a message and returns no content on success.
    /// </summary>
    [HttpDelete("messages/{messageId}")]
    public async Task<IActionResult> DeleteMessage(Guid messageId)
    {
        var result = await _chatService.DeleteMessageAsync(GetRequiredUserId(), messageId);
        if (result.IsSuccess) return NoContent();
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Toggles the authenticated user's emoji reaction on a message.
    /// </summary>
    [HttpPost("messages/{messageId}/reactions")]
    public async Task<IActionResult> ToggleReaction(Guid messageId, [FromBody] MessageReactionDto dto)
    {
        var result = await _chatService.ToggleReactionAsync(GetRequiredUserId(), messageId, dto);
        return MapResult(result);
    }

    /// <summary>
    /// Pins or unpins a message in its conversation when the service permits it.
    /// </summary>
    [HttpPut("messages/{messageId}/pin")]
    public async Task<IActionResult> SetMessagePin(Guid messageId, [FromBody] PinMessageDto dto)
    {
        var result = await _chatService.SetMessagePinAsync(GetRequiredUserId(), messageId, dto);
        return MapResult(result);
    }

    /// <summary>
    /// Creates a general group chat that is not tied to a project.
    /// </summary>
    [ServiceFilter(typeof(ProfanityCensorFilter))]
    [HttpPost("conversations/group")]
    public async Task<IActionResult> CreateGroupChat([FromBody] CreateGroupChatDto dto)
    {
        var result = await _chatService.CreateGroupChatAsync(GetRequiredUserId(), dto);
        return MapResult(result);
    }

    /// <summary>
    /// Adds a participant to a group chat when the service permits the caller to manage participants.
    /// </summary>
    [HttpPost("conversations/{conversationId}/participants/{userId}")]
    public async Task<IActionResult> AddParticipant(Guid conversationId, Guid userId)
    {
        var result = await _chatService.AddParticipantAsync(GetRequiredUserId(), conversationId, userId);
        return MapResult(result);
    }

    /// <summary>
    /// Removes a participant from a group chat and returns no content on success.
    /// </summary>
    [HttpDelete("conversations/{conversationId}/participants/{userId}")]
    public async Task<IActionResult> RemoveParticipant(Guid conversationId, Guid userId)
    {
        var result = await _chatService.RemoveParticipantAsync(GetRequiredUserId(), conversationId, userId);
        if (result.IsSuccess) return NoContent();
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Removes the authenticated user from a group chat and returns no content on success.
    /// </summary>
    [HttpPost("conversations/{conversationId}/leave")]
    public async Task<IActionResult> LeaveGroupChat(Guid conversationId)
    {
        var result = await _chatService.LeaveGroupChatAsync(GetRequiredUserId(), conversationId);
        if (result.IsSuccess) return NoContent();
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Toggles mute settings for the authenticated user's conversation participant state.
    /// </summary>
    [HttpPut("conversations/{conversationId}/mute")]
    public async Task<IActionResult> ToggleMute(Guid conversationId, [FromBody] MuteDto dto)
    {
        var result = await _chatService.ToggleMuteAsync(GetRequiredUserId(), conversationId, dto);
        return MapResult(result);
    }

    /// <summary>
    /// Gets participants for a conversation visible to the authenticated user.
    /// </summary>
    [HttpGet("conversations/{conversationId}/participants")]
    public async Task<IActionResult> GetParticipants(Guid conversationId)
    {
        var result = await _chatService.GetParticipantsAsync(GetRequiredUserId(), conversationId);
        return MapResult(result);
    }

    #endregion
}
