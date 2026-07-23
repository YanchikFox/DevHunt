using System.Text.RegularExpressions;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Chat;

/// <summary>
/// EF-backed implementation of <see cref="IProjectChannelService"/>. Creates and manages
/// <see cref="ConversationType.ProjectChannel"/> rows, auto-enrols viewers into public channels,
/// and delegates effective permission flags to <see cref="ChannelMemberService.ResolveEffective"/>.
/// </summary>
public sealed class ProjectChannelService : IProjectChannelService
{
    private static readonly Regex SlugPattern = new(
        @"^[a-z0-9]([a-z0-9\-_]{0,48}[a-z0-9])?$",
        RegexOptions.Compiled);

    private const string GeneralSlug = "general";

    private readonly DevHuntDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProjectChannelService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectChannelService"/> class.
    /// </summary>
    /// <param name="context">Database context for project conversations, participants, messages, and team membership.</param>
    /// <param name="auditService">Audit writer for channel create, update, and delete events.</param>
    /// <param name="logger">Logger for project channel diagnostics.</param>
    public ProjectChannelService(
        DevHuntDbContext context,
        IAuditService auditService,
        ILogger<ProjectChannelService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Lists project channels visible to the caller. Ensures <c>#general</c> exists, auto-enrols
    /// the viewer into public channels they have not left or been banned from, and hides private
    /// channels from non-participants unless the caller is the project owner.
    /// </summary>
    /// <param name="userId">Viewer whose membership and unread counts are resolved.</param>
    /// <param name="projectId">Project whose channels are listed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered channel DTOs with permission flags, or 403 when the user is not a project member.</returns>
    public async Task<ChatResult<IReadOnlyList<ProjectChannelDto>>> ListAsync(
        Guid userId, Guid projectId, CancellationToken ct = default)
    {
        var access = await ResolveProjectAccessAsync(projectId, userId, ct);
        if (access is null)
            return ChatResult<IReadOnlyList<ProjectChannelDto>>.Failure(
                "You must be a project member to view channels", 403);

        // Ensure a #general always exists before listing — keeps UI free from
        // an "empty channel list" edge case for newly created projects.
        await EnsureGeneralInternalAsync(projectId, userId, ct);

        // Fetch raw channel data + the viewer's participant row (if any) so we
        // can resolve effective permissions locally without extra round trips.
        var rows = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId && c.Type == ConversationType.ProjectChannel)
            .OrderBy(c => c.Position)
            .ThenBy(c => c.Slug)
            .Select(c => new
            {
                Channel = c,
                ViewerParticipant = c.Participants
                    .FirstOrDefault(p => p.UserId == userId),
                UnreadCount = c.Messages.Count(m =>
                    !m.IsDeleted &&
                    m.CreatedAt > (c.Participants
                        .Where(p => p.UserId == userId)
                        .Select(p => p.LastReadAt)
                        .FirstOrDefault() ?? DateTime.MinValue)),
            })
            .ToListAsync(ct);

        // Auto-enrol the viewer into public channels they're not in yet (unless
        // they previously left or were banned). Keeps message send working on
        // first click without requiring an explicit "join" tap.
        var toAdd = new List<ConversationParticipant>();
        foreach (var row in rows)
        {
            if (row.Channel.IsPrivate) continue;
            if (row.ViewerParticipant is not null) continue;
            toAdd.Add(new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = row.Channel.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow,
                Role = access.IsOwner ? ChannelRole.Admin : ChannelRole.Member,
                State = ParticipantState.Active,
            });
        }
        if (toAdd.Count > 0)
        {
            _context.ConversationParticipants.AddRange(toAdd);
            await _context.SaveChangesAsync(ct);
        }

        var roleDefinitionIds = rows
            .Select(r => r.ViewerParticipant?.ChannelRoleDefinitionId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var roleDefinitions = roleDefinitionIds.Count == 0
            ? new Dictionary<Guid, ChannelRoleDefinition>()
            : await _context.ChannelRoleDefinitions
                .AsNoTracking()
                .Where(r => roleDefinitionIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, ct);

        var visible = new List<ProjectChannelDto>(rows.Count);
        foreach (var row in rows)
        {
            // Resolve participant — if we just auto-added one it lives in toAdd.
            var participant = row.ViewerParticipant
                ?? toAdd.FirstOrDefault(p => p.ConversationId == row.Channel.Id);
            if (participant?.ChannelRoleDefinitionId is { } roleId &&
                roleDefinitions.TryGetValue(roleId, out var roleDefinition))
            {
                participant.RoleDefinition = roleDefinition;
            }

            var isMember = participant is not null && participant.State == ParticipantState.Active;
            var isBanned = participant?.State == ParticipantState.Banned;

            // Private channels are invisible to non-participants; banned users
            // never see their channel in the list.
            if (isBanned) continue;
            if (row.Channel.IsPrivate && !isMember && !access.IsOwner) continue;

            visible.Add(MapToDto(row.Channel, participant, access, userId, row.UnreadCount));
        }

        return ChatResult<IReadOnlyList<ProjectChannelDto>>.Success(visible);
    }

    /// <summary>
    /// Creates a channel with a unique slug. Seeds the creator and project owner as admins;
    /// public channels defer bulk team enrolment to the next <see cref="ListAsync"/> call.
    /// </summary>
    /// <param name="userId">Creator; must be an active project member.</param>
    /// <param name="projectId">Project that will own the channel.</param>
    /// <param name="dto">Slug, title, topic, and privacy flag for the new channel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created channel DTO, or 400/403/409 on validation or permission failure.</returns>
    public async Task<ChatResult<ProjectChannelDto>> CreateAsync(
        Guid userId, Guid projectId, CreateProjectChannelDto dto, CancellationToken ct = default)
    {
        var access = await ResolveProjectAccessAsync(projectId, userId, ct);
        if (access is null)
            return ChatResult<ProjectChannelDto>.Failure(
                "You must be a project member to create channels", 403);

        var slug = (dto.Slug ?? string.Empty).Trim().ToLowerInvariant();
        if (!SlugPattern.IsMatch(slug))
            return ChatResult<ProjectChannelDto>.Failure(
                "Channel slug must be 1–50 characters, lowercase, starting and ending with a letter or digit", 400);

        var slugTaken = await _context.Conversations
            .AnyAsync(c => c.ProjectId == projectId &&
                           c.Type == ConversationType.ProjectChannel &&
                           c.Slug == slug, ct);

        if (slugTaken)
            return ChatResult<ProjectChannelDto>.Failure(
                $"Channel #{slug} already exists in this project", 409);

        var nextPosition = await _context.Conversations
            .Where(c => c.ProjectId == projectId && c.Type == ConversationType.ProjectChannel)
            .Select(c => (int?)c.Position)
            .MaxAsync(ct) ?? -1;

        var now = DateTime.UtcNow;
        var channel = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.ProjectChannel,
            ProjectId = projectId,
            Slug = slug,
            Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title!.Trim(),
            Topic = string.IsNullOrWhiteSpace(dto.Topic) ? null : dto.Topic!.Trim(),
            IsPrivate = dto.IsPrivate,
            Position = nextPosition + 1,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _context.Conversations.Add(channel);

        // Seed participants:
        //   - Private channel → just the creator (rest are invited later).
        //   - Public channel → creator + project owner only. Remaining team
        //     members will be auto-enrolled on first channel list call; this
        //     avoids duplicating the enrolment loop and keeps creation cheap.
        var seedIds = new HashSet<Guid> { userId };
        var ownerId = await _context.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.OwnerId)
            .FirstOrDefaultAsync(ct);
        if (ownerId != Guid.Empty) seedIds.Add(ownerId);

        foreach (var mid in seedIds)
        {
            _context.ConversationParticipants.Add(new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = channel.Id,
                UserId = mid,
                JoinedAt = now,
                Role = (mid == userId || mid == ownerId) ? ChannelRole.Admin : ChannelRole.Member,
                State = ParticipantState.Active,
            });
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel created", "Conversation", channel.Id,
            $"{{\"ProjectId\":\"{projectId}\",\"Slug\":\"{slug}\",\"IsPrivate\":{dto.IsPrivate.ToString().ToLowerInvariant()}}}");

        var creatorRow = await _context.ConversationParticipants
            .Include(p => p.RoleDefinition)
            .FirstOrDefaultAsync(p => p.ConversationId == channel.Id && p.UserId == userId, ct);
        return ChatResult<ProjectChannelDto>.Success(MapToDto(channel, creatorRow, access, userId, 0));
    }

    /// <summary>
    /// Patches channel metadata. Requires edit permission via owner/creator status or
    /// <see cref="ChannelMemberService.ResolveEffective"/>. <c>#general</c> cannot be renamed
    /// or made private.
    /// </summary>
    /// <param name="userId">User attempting the update.</param>
    /// <param name="channelId">Target project channel conversation id.</param>
    /// <param name="dto">Partial update; null fields are ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated channel DTO, or 403/404/409 when access or slug rules fail.</returns>
    public async Task<ChatResult<ProjectChannelDto>> UpdateAsync(
        Guid userId, Guid channelId, UpdateProjectChannelDto dto, CancellationToken ct = default)
    {
        var channel = await _context.Conversations.FirstOrDefaultAsync(
            c => c.Id == channelId && c.Type == ConversationType.ProjectChannel, ct);

        if (channel is null || channel.ProjectId is null)
            return ChatResult<ProjectChannelDto>.Failure("Channel not found", 404);

        var access = await ResolveProjectAccessAsync(channel.ProjectId.Value, userId, ct);
        if (access is null)
            return ChatResult<ProjectChannelDto>.Failure("Access denied", 403);

        var participant = await _context.ConversationParticipants
            .Include(p => p.RoleDefinition)
            .FirstOrDefaultAsync(p => p.ConversationId == channelId && p.UserId == userId, ct);

        var canEdit = access.IsOwner || channel.CreatedByUserId == userId ||
            ChannelMemberService.ResolveEffective(participant, ChannelRole.Member, access.OwnerId, userId).CanEditChannel;

        if (!canEdit)
            return ChatResult<ProjectChannelDto>.Failure(
                "You don't have permission to edit this channel", 403);

        if (!string.IsNullOrWhiteSpace(dto.Slug))
        {
            var newSlug = dto.Slug!.Trim().ToLowerInvariant();
            if (!SlugPattern.IsMatch(newSlug))
                return ChatResult<ProjectChannelDto>.Failure(
                    "Channel slug must be 1–50 characters, lowercase, starting and ending with a letter or digit", 400);

            if (channel.Slug == GeneralSlug && newSlug != GeneralSlug)
                return ChatResult<ProjectChannelDto>.Failure("#general cannot be renamed", 400);

            if (newSlug != channel.Slug)
            {
                var taken = await _context.Conversations.AnyAsync(
                    c => c.ProjectId == channel.ProjectId &&
                         c.Type == ConversationType.ProjectChannel &&
                         c.Slug == newSlug &&
                         c.Id != channel.Id, ct);
                if (taken)
                    return ChatResult<ProjectChannelDto>.Failure(
                        $"Channel #{newSlug} already exists in this project", 409);
                channel.Slug = newSlug;
            }
        }

        if (dto.Title is not null)
            channel.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();

        if (dto.Topic is not null)
            channel.Topic = string.IsNullOrWhiteSpace(dto.Topic) ? null : dto.Topic.Trim();

        if (dto.IsPrivate.HasValue && channel.Slug != GeneralSlug)
            channel.IsPrivate = dto.IsPrivate.Value;

        if (dto.Position.HasValue)
            channel.Position = dto.Position.Value;

        channel.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel updated", "Conversation", channel.Id, null);

        var unread = await GetUnreadCountAsync(channel.Id, userId, ct);
        return ChatResult<ProjectChannelDto>.Success(
            MapToDto(channel, participant, access, userId, unread));
    }

    /// <summary>
    /// Hard-deletes a channel and all of its messages. <c>#general</c> is protected; callers
    /// need delete permission from effective channel roles or owner/creator status.
    /// </summary>
    /// <param name="userId">User attempting deletion.</param>
    /// <param name="channelId">Channel conversation id to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or 400/403/404 when the channel is protected or inaccessible.</returns>
    public async Task<ChatResult> DeleteAsync(Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var channel = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == channelId && c.Type == ConversationType.ProjectChannel, ct);

        if (channel is null || channel.ProjectId is null)
            return ChatResult.Failure("Channel not found", 404);

        if (channel.Slug == GeneralSlug)
            return ChatResult.Failure("#general cannot be deleted", 400);

        var access = await ResolveProjectAccessAsync(channel.ProjectId.Value, userId, ct);
        if (access is null)
            return ChatResult.Failure("Access denied", 403);

        var participant = await _context.ConversationParticipants
            .Include(p => p.RoleDefinition)
            .FirstOrDefaultAsync(p => p.ConversationId == channelId && p.UserId == userId, ct);

        var canDelete = access.IsOwner || channel.CreatedByUserId == userId ||
            ChannelMemberService.ResolveEffective(participant, ChannelRole.Member, access.OwnerId, userId).CanDeleteChannel;

        if (!canDelete)
            return ChatResult.Failure("You don't have permission to delete this channel", 403);

        var messages = _context.Messages.Where(m => m.ConversationId == channelId);
        _context.Messages.RemoveRange(messages);
        _context.Conversations.Remove(channel);

        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel deleted", "Conversation", channelId,
            $"{{\"ProjectId\":\"{channel.ProjectId}\",\"Slug\":\"{channel.Slug}\"}}");

        return ChatResult.Success();
    }

    /// <summary>
    /// Adds or reactivates the caller as an active channel participant. Banned users receive 403.
    /// </summary>
    /// <param name="userId">User joining the channel.</param>
    /// <param name="channelId">Target project channel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Channel DTO after join, or 403/404 on access failure.</returns>
    public async Task<ChatResult<ProjectChannelDto>> JoinAsync(
        Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var channel = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == channelId && c.Type == ConversationType.ProjectChannel, ct);

        if (channel is null || channel.ProjectId is null)
            return ChatResult<ProjectChannelDto>.Failure("Channel not found", 404);

        var access = await ResolveProjectAccessAsync(channel.ProjectId.Value, userId, ct);
        if (access is null)
            return ChatResult<ProjectChannelDto>.Failure("Access denied", 403);

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == channelId && p.UserId == userId, ct);

        if (participant?.State == ParticipantState.Banned)
            return ChatResult<ProjectChannelDto>.Failure(
                "You are banned from this channel", 403);

        if (participant is null)
        {
            participant = new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = channelId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow,
                Role = access.IsOwner ? ChannelRole.Admin : ChannelRole.Member,
                State = ParticipantState.Active,
            };
            _context.ConversationParticipants.Add(participant);
        }
        else
        {
            participant.State = ParticipantState.Active;
        }

        await _context.SaveChangesAsync(ct);

        var unread = await GetUnreadCountAsync(channelId, userId, ct);
        return ChatResult<ProjectChannelDto>.Success(
            MapToDto(channel, participant, access, userId, unread));
    }

    /// <summary>
    /// Soft-leaves a channel by setting participant state to <c>Left</c>, which blocks
    /// auto-enrolment on public channels. <c>#general</c> cannot be left.
    /// </summary>
    /// <param name="userId">Participant leaving the channel.</param>
    /// <param name="channelId">Target project channel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or 400/404 when leaving is disallowed or the channel is missing.</returns>
    public async Task<ChatResult> LeaveAsync(Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var channel = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == channelId && c.Type == ConversationType.ProjectChannel, ct);

        if (channel is null || channel.ProjectId is null)
            return ChatResult.Failure("Channel not found", 404);

        if (channel.Slug == GeneralSlug)
            return ChatResult.Failure("#general cannot be left", 400);

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == channelId && p.UserId == userId, ct);

        if (participant is null || participant.State != ParticipantState.Active)
            return ChatResult.Success();

        // Soft-leave: keep the row with State=Left. Auto-enrolment respects it
        // on public channels so the user isn't snapped back in on next list.
        participant.State = ParticipantState.Left;
        await _context.SaveChangesAsync(ct);

        return ChatResult.Success();
    }

    /// <summary>
    /// Resolves or creates the project's <c>#general</c> channel and ensures the caller is an
    /// active participant. Used by the legacy project-chat entry point in <see cref="ChatService"/>.
    /// </summary>
    /// <param name="userId">Project member requesting the default channel.</param>
    /// <param name="projectId">Project whose general channel is ensured.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>General channel DTO, or 403 when the user is not a member or is banned.</returns>
    public async Task<ChatResult<ProjectChannelDto>> EnsureGeneralAsync(
        Guid userId, Guid projectId, CancellationToken ct = default)
    {
        var access = await ResolveProjectAccessAsync(projectId, userId, ct);
        if (access is null)
            return ChatResult<ProjectChannelDto>.Failure(
                "You must be a project member to access project chat", 403);

        var channel = await EnsureGeneralInternalAsync(projectId, userId, ct);

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == channel.Id && p.UserId == userId, ct);

        if (participant?.State == ParticipantState.Banned)
            return ChatResult<ProjectChannelDto>.Failure(
                "You are banned from this channel", 403);

        if (participant is null || participant.State != ParticipantState.Active)
        {
            if (participant is null)
            {
                participant = new ConversationParticipant
                {
                    Id = Guid.NewGuid(),
                    ConversationId = channel.Id,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow,
                    Role = access.IsOwner ? ChannelRole.Admin : ChannelRole.Member,
                    State = ParticipantState.Active,
                };
                _context.ConversationParticipants.Add(participant);
            }
            else
            {
                participant.State = ParticipantState.Active;
            }
            await _context.SaveChangesAsync(ct);
        }

        var unread = await GetUnreadCountAsync(channel.Id, userId, ct);
        return ChatResult<ProjectChannelDto>.Success(
            MapToDto(channel, participant, access, userId, unread));
    }

    // ---------- helpers ----------

    /// <summary>
    /// Cached project title, owner id, and whether the requesting user is that owner.
    /// </summary>
    /// <param name="Title">Project display title.</param>
    /// <param name="OwnerId">Project owner user id.</param>
    /// <param name="IsOwner">True when the caller is the project owner.</param>
    internal sealed record ProjectAccess(string Title, Guid OwnerId, bool IsOwner);

    /// <summary>
    /// Returns project access when the user is the owner or an active team member; otherwise null.
    /// </summary>
    private async Task<ProjectAccess?> ResolveProjectAccessAsync(
        Guid projectId, Guid userId, CancellationToken ct)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Title, p.OwnerId })
            .FirstOrDefaultAsync(ct);

        if (project is null) return null;
        if (project.OwnerId == userId) return new ProjectAccess(project.Title, project.OwnerId, true);

        var isMember = await _context.TeamMembers.AnyAsync(
            tm => tm.ProjectId == projectId &&
                  tm.UserId == userId &&
                  tm.Status == TeamMemberStatus.Active.Value, ct);

        return isMember ? new ProjectAccess(project.Title, project.OwnerId, false) : null;
    }

    /// <summary>
    /// Collects active team member ids plus the project owner when seeding channel participants.
    /// </summary>
    private async Task<List<Guid>> ResolveProjectMembersAsync(Guid projectId, CancellationToken ct)
    {
        var ownerId = await _context.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.OwnerId)
            .FirstOrDefaultAsync(ct);

        var members = await _context.TeamMembers
            .Where(tm => tm.ProjectId == projectId && tm.Status == TeamMemberStatus.Active.Value)
            .Select(tm => tm.UserId)
            .ToListAsync(ct);

        if (ownerId != Guid.Empty && !members.Contains(ownerId))
            members.Add(ownerId);

        return members;
    }

    /// <summary>
    /// Finds or creates the <c>#general</c> channel, upgrading legacy group conversations in place
    /// and enrolling all active project members when a new channel is created.
    /// </summary>
    private async Task<Conversation> EnsureGeneralInternalAsync(
        Guid projectId, Guid userId, CancellationToken ct)
    {
        var channel = await _context.Conversations.FirstOrDefaultAsync(
            c => c.ProjectId == projectId &&
                 c.Type == ConversationType.ProjectChannel &&
                 c.Slug == GeneralSlug, ct);

        if (channel is not null) return channel;

        // Legacy path: a "project chat" conversation (Id == ProjectId) may
        // still be Type=Group if a prior chat existed and the migration
        // somehow didn't upgrade it. Fix-forward inline rather than fail.
        var legacy = await _context.Conversations.FindAsync(new object[] { projectId }, ct);
        if (legacy is not null)
        {
            legacy.Type = ConversationType.ProjectChannel;
            legacy.ProjectId = projectId;
            legacy.Slug = GeneralSlug;
            legacy.Position = 0;
            legacy.IsPrivate = false;
            // Legacy "project chat" conversations were created with Title = project.Name
            // (e.g. "DevHunt"), which leaks into the channel header. Reset it here so
            // the #general channel doesn't show the project name as its identifier.
            legacy.Title = "general";
            legacy.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return legacy;
        }

        var now = DateTime.UtcNow;
        var created = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.ProjectChannel,
            ProjectId = projectId,
            Slug = GeneralSlug,
            Title = "general",
            IsPrivate = false,
            Position = 0,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _context.Conversations.Add(created);

        var ownerId = await _context.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.OwnerId)
            .FirstOrDefaultAsync(ct);

        var memberIds = await ResolveProjectMembersAsync(projectId, ct);
        if (!memberIds.Contains(userId)) memberIds.Add(userId);

        foreach (var mid in memberIds)
        {
            _context.ConversationParticipants.Add(new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = created.Id,
                UserId = mid,
                JoinedAt = now,
                Role = (mid == ownerId || mid == userId) ? ChannelRole.Admin : ChannelRole.Member,
                State = ParticipantState.Active,
            });
        }

        await _context.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>
    /// Counts non-deleted messages created after the viewer's <c>LastReadAt</c> timestamp.
    /// </summary>
    private Task<int> GetUnreadCountAsync(Guid channelId, Guid userId, CancellationToken ct)
    {
        return _context.Messages
            .Where(m => m.ConversationId == channelId && !m.IsDeleted)
            .Where(m => m.CreatedAt > (_context.ConversationParticipants
                .Where(p => p.ConversationId == channelId && p.UserId == userId)
                .Select(p => p.LastReadAt)
                .FirstOrDefault() ?? DateTime.MinValue))
            .CountAsync(ct);
    }

    /// <summary>
    /// Maps a channel and optional participant row to a client DTO, granting owner/creator edit
    /// rights even when legacy participant rows lack admin role data.
    /// </summary>
    private static ProjectChannelDto MapToDto(
        Conversation c,
        ConversationParticipant? participant,
        ProjectAccess access,
        Guid viewerId,
        int unreadCount)
    {
        var isMember = participant is not null && participant.State == ParticipantState.Active;
        var effective = ChannelMemberService.ResolveEffective(participant, ChannelRole.Member, access.OwnerId, viewerId);

        // Creator + project owner always get edit rights even if their row was
        // never backfilled to Admin (e.g. legacy channels without the new
        // columns populated).
        var canEditChannel = effective.CanEditChannel || access.IsOwner || c.CreatedByUserId == viewerId;
        var canDeleteChannel = (effective.CanDeleteChannel || access.IsOwner || c.CreatedByUserId == viewerId)
            && c.Slug != GeneralSlug;
        var canManageMembers = effective.CanManageMembers || access.IsOwner || c.CreatedByUserId == viewerId;

        // Viewer role label: owner is always "admin"; otherwise fall through to
        // the stored role, or null when there's no participant row yet.
        string? viewerRole = null;
        if (access.IsOwner) viewerRole = "admin";
        else if (participant is not null) viewerRole = participant.Role == ChannelRole.Admin ? "admin" : "member";

        return new ProjectChannelDto(
            c.Id,
            c.ProjectId ?? Guid.Empty,
            c.Slug ?? string.Empty,
            c.Title,
            c.Topic,
            c.IsPrivate,
            c.Position,
            c.CreatedAt,
            c.LastMessageAt,
            unreadCount,
            isMember,
            CanManage: canEditChannel || canManageMembers,
            CanPost: effective.CanPost && participant?.State != ParticipantState.Banned,
            CanManageMembers: canManageMembers,
            CanEditChannel: canEditChannel,
            CanDeleteChannel: canDeleteChannel,
            CanPinMessages: effective.CanPinMessages || access.IsOwner || c.CreatedByUserId == viewerId,
            CanDeleteMessages: effective.CanDeleteMessages || access.IsOwner || c.CreatedByUserId == viewerId,
            ViewerRole: viewerRole);
    }
}
