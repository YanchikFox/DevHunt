using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Admin API for managing conversations and messages.
/// </summary>
[ApiController]
[Route("api/admin/chat")]
[Authorize]
public class AdminChatController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly IAuditService _auditService;
    private readonly IEncryptionService _encryption;

    /// <summary>
    /// Creates the admin chat controller with conversation storage, audit logging, and message decryption services.
    /// </summary>
    /// <param name="db">Database context used to inspect and delete conversations and messages.</param>
    /// <param name="auditService">Audit service used to record destructive chat actions.</param>
    /// <param name="encryption">Encryption service used to decrypt message content for admin review.</param>
    public AdminChatController(DevHuntDbContext db, IAuditService auditService, IEncryptionService encryption)
    {
        _db = db;
        _auditService = auditService;
        _encryption = encryption;
    }

    /// <summary>
    /// Decrypts message content for admin display, returning a placeholder when ciphertext cannot be decrypted.
    /// </summary>
    private string DecryptSafe(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return string.Empty;
        try { return _encryption.Decrypt(cipher); }
        catch { return "[encrypted]"; }
    }

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId() =>
        SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");

    /// <summary>
    /// Checks the database for an active admin, curator, or superadmin user matching the current claims principal.
    /// </summary>
    private async Task<bool> IsAdminOrCuratorAsync(CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return false;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        return user?.IsAdminOrCurator() == true;
    }

    // ========================================================================
    // Stats
    // ========================================================================

    /// <summary>Returns conversation and message counts, including active and orphaned group conversation counts.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var projectIds = _db.Projects.Select(p => p.Id);
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        var total = await _db.Conversations.CountAsync(ct);
        var groups = await _db.Conversations.CountAsync(c => c.Type == ConversationType.Group, ct);
        var directs = await _db.Conversations.CountAsync(c => c.Type == ConversationType.Direct, ct);
        var orphaned = await _db.Conversations
            .CountAsync(c => c.Type == ConversationType.Group && !projectIds.Contains(c.Id), ct);
        var totalMessages = await _db.Messages.CountAsync(ct);
        var activeConversations = await _db.Conversations
            .CountAsync(c => c.LastMessageAt != null && c.LastMessageAt > sevenDaysAgo, ct);

        return Ok(new
        {
            total,
            groups,
            directs,
            orphaned,
            totalMessages,
            activeConversations
        });
    }

    // ========================================================================
    // List Conversations
    // ========================================================================

    /// <summary>Lists conversations with optional type, title-search, orphaned-only filters, and pagination.</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(
        [FromQuery] string? type,
        [FromQuery] string? search,
        [FromQuery] bool? orphaned,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var projectIds = _db.Projects.Select(p => p.Id);

        var query = _db.Conversations
            .Include(c => c.Participants)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(type) && type != "all")
        {
            if (Enum.TryParse<ConversationType>(type, true, out var conversationType))
                query = query.Where(c => c.Type == conversationType);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => c.Title != null && c.Title.ToLower().Contains(search.ToLower()));
        }

        if (orphaned == true)
        {
            query = query.Where(c => c.Type == ConversationType.Group && !projectIds.Contains(c.Id));
        }

        var totalCount = await query.CountAsync(ct);

        var conversations = await query
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                Type = c.Type.ToString(),
                c.Title,
                ParticipantCount = c.Participants.Count,
                MessageCount = _db.Messages.Count(m => m.ConversationId == c.Id),
                c.LastMessageAt,
                c.CreatedAt,
                IsOrphaned = c.Type == ConversationType.Group && !projectIds.Contains(c.Id)
            })
            .ToListAsync(ct);

        return Ok(new
        {
            data = conversations,
            pagination = new
            {
                page,
                pageSize,
                total = totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            }
        });
    }

    // ========================================================================
    // View Messages
    // ========================================================================

    /// <summary>Returns decrypted messages for a conversation with sender metadata and pagination.</summary>
    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var conversation = await _db.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conversation == null) return NotFound();

        var query = _db.Messages
            .Where(m => m.ConversationId == id)
            .AsNoTracking();

        var totalCount = await query.CountAsync(ct);

        var rawMessages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(m => m.Sender)
            .ToListAsync(ct);

        var messages = rawMessages.Select(m => new
        {
            m.Id,
            m.SenderId,
            SenderName = m.Sender?.FullName ?? m.Sender?.Email,
            SenderAvatarUrl = m.Sender?.AvatarUrl,
            Content = DecryptSafe(m.Content),
            m.CreatedAt,
            m.IsEdited,
            m.IsDeleted,
            m.IsAiGenerated
        }).ToList();

        return Ok(new
        {
            data = messages,
            pagination = new
            {
                page,
                pageSize,
                total = totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            }
        });
    }

    // ========================================================================
    // Delete Conversation
    // ========================================================================

    /// <summary>Deletes a conversation with its messages and participants, then audits the removed message count.</summary>
    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid id, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var conversation = await _db.Conversations
            .Include(c => c.Participants)
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversation == null) return NotFound();

        var userId = GetRequiredUserId();
        var messageCount = conversation.Messages.Count;
        var title = conversation.Title ?? conversation.Id.ToString();

        _db.Messages.RemoveRange(conversation.Messages);
        _db.ConversationParticipants.RemoveRange(conversation.Participants);
        _db.Conversations.Remove(conversation);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "AdminChat.DeleteConversation", "Conversation", id,
            $"Deleted conversation \"{title}\" with {messageCount} messages");

        return NoContent();
    }

    // ========================================================================
    // Delete Message
    // ========================================================================

    /// <summary>Deletes a single message from a conversation and records the admin action.</summary>
    [HttpDelete("conversations/{id:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid id, Guid messageId, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == id, ct);
        if (message == null) return NotFound();

        var userId = GetRequiredUserId();

        _db.Messages.Remove(message);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "AdminChat.DeleteMessage", "Message", messageId,
            $"Deleted message in conversation {id}");

        return NoContent();
    }

    // ========================================================================
    // Cleanup Orphaned
    // ========================================================================

    /// <summary>Deletes orphaned group conversations whose project no longer exists and reports deleted totals.</summary>
    [HttpPost("cleanup-orphaned")]
    public async Task<IActionResult> CleanupOrphaned(CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var projectIds = await _db.Projects.Select(p => p.Id).ToListAsync(ct);

        var orphanedConversations = await _db.Conversations
            .Include(c => c.Participants)
            .Include(c => c.Messages)
            .Where(c => c.Type == ConversationType.Group && !projectIds.Contains(c.Id))
            .ToListAsync(ct);

        if (orphanedConversations.Count == 0)
            return Ok(new { deletedCount = 0 });

        var totalMessages = 0;
        foreach (var conv in orphanedConversations)
        {
            totalMessages += conv.Messages.Count;
            _db.Messages.RemoveRange(conv.Messages);
            _db.ConversationParticipants.RemoveRange(conv.Participants);
        }

        _db.Conversations.RemoveRange(orphanedConversations);
        await _db.SaveChangesAsync(ct);

        var userId = GetRequiredUserId();
        await _auditService.LogActionAsync(userId, "AdminChat.CleanupOrphaned", "Conversation", Guid.Empty,
            $"Cleaned up {orphanedConversations.Count} orphaned conversations with {totalMessages} messages");

        return Ok(new
        {
            deletedCount = orphanedConversations.Count,
            deletedMessages = totalMessages
        });
    }
}
