using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Chat;

/// <summary>
/// EF-backed implementation of <see cref="IChannelMemberService"/>. Enforces channel-scoped
/// membership, custom roles, and permission overrides; exposes
/// <see cref="ResolveEffective"/> for other chat services.
/// </summary>
public sealed class ChannelMemberService : IChannelMemberService
{
    private const string GeneralSlug = "general";

    private readonly DevHuntDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<ChannelMemberService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelMemberService"/> class.
    /// </summary>
    /// <param name="context">Database context for conversations, participants, roles, projects, and users.</param>
    /// <param name="auditService">Audit writer for membership and role-management changes.</param>
    /// <param name="logger">Logger for channel membership diagnostics.</param>
    public ChannelMemberService(
        DevHuntDbContext context,
        IAuditService auditService,
        ILogger<ChannelMemberService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChatResult<IReadOnlyList<ChannelMemberDto>>> ListActiveAsync(
        Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null)
            return ChatResult<IReadOnlyList<ChannelMemberDto>>.Failure(ctx.Error, ctx.ErrorCode);

        var list = await BuildMemberListAsync(ctx.Channel!, ctx.OwnerId,
            filter: p => p.State == ParticipantState.Active, ct);
        return ChatResult<IReadOnlyList<ChannelMemberDto>>.Success(list);
    }

    /// <inheritdoc />
    public async Task<ChatResult<IReadOnlyList<ChannelMemberDto>>> ListBannedAsync(
        Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null)
            return ChatResult<IReadOnlyList<ChannelMemberDto>>.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult<IReadOnlyList<ChannelMemberDto>>.Failure(
                "Only channel admins can view the ban list", 403);

        var list = await BuildMemberListAsync(ctx.Channel!, ctx.OwnerId,
            filter: p => p.State == ParticipantState.Banned, ct);
        return ChatResult<IReadOnlyList<ChannelMemberDto>>.Success(list);
    }

    /// <inheritdoc />
    public async Task<ChatResult<IReadOnlyList<ChannelCandidateDto>>> ListCandidatesAsync(
        Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null)
            return ChatResult<IReadOnlyList<ChannelCandidateDto>>.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult<IReadOnlyList<ChannelCandidateDto>>.Failure(
                "Only channel admins can invite members", 403);

        var channel = ctx.Channel!;
        var projectId = channel.ProjectId!.Value;

        // Active + banned participant user ids — those can't be invited.
        var blockedUserIds = await _context.ConversationParticipants
            .Where(p => p.ConversationId == channel.Id &&
                        (p.State == ParticipantState.Active || p.State == ParticipantState.Banned))
            .Select(p => p.UserId)
            .ToListAsync(ct);

        var blockedSet = blockedUserIds.ToHashSet();

        var teamMembers = await _context.TeamMembers
            .AsNoTracking()
            .Where(tm => tm.ProjectId == projectId && tm.Status == TeamMemberStatus.Active.Value)
            .Select(tm => new { tm.UserId })
            .ToListAsync(ct);

        var candidateIds = teamMembers.Select(t => t.UserId).ToHashSet();
        candidateIds.Add(ctx.OwnerId);
        candidateIds.ExceptWith(blockedSet);

        if (candidateIds.Count == 0)
            return ChatResult<IReadOnlyList<ChannelCandidateDto>>.Success(Array.Empty<ChannelCandidateDto>());

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => candidateIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.AvatarUrl, u.Username })
            .ToListAsync(ct);

        var candidates = users
            .Select(u => new ChannelCandidateDto(
                u.Id,
                u.FullName ?? u.Username ?? string.Empty,
                u.AvatarUrl,
                u.Username,
                u.Id == ctx.OwnerId))
            .OrderByDescending(c => c.IsProjectOwner)
            .ThenBy(c => c.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ChatResult<IReadOnlyList<ChannelCandidateDto>>.Success(candidates);
    }

    /// <inheritdoc />
    public async Task<ChatResult<ChannelMemberDto>> InviteAsync(
        Guid userId, Guid channelId, ChannelInviteDto dto, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null)
            return ChatResult<ChannelMemberDto>.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult<ChannelMemberDto>.Failure(
                "Only channel admins can add members", 403);

        var targetId = dto.UserId;
        var channel = ctx.Channel!;
        var projectId = channel.ProjectId!.Value;

        // Target must be an active project member (or owner) to be invitable.
        var isProjectMember = ctx.OwnerId == targetId ||
            await _context.TeamMembers.AnyAsync(tm =>
                tm.ProjectId == projectId &&
                tm.UserId == targetId &&
                tm.Status == TeamMemberStatus.Active.Value, ct);

        if (!isProjectMember)
            return ChatResult<ChannelMemberDto>.Failure(
                "Invitee is not an active project member", 400);

        var existing = await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == channel.Id && p.UserId == targetId, ct);

        var role = ParseRole(dto.Role) ?? ChannelRole.Member;
        var roleDefinition = await ResolveRoleDefinitionAsync(channel.Id, dto.RoleDefinitionId, ct);
        if (dto.RoleDefinitionId.HasValue && roleDefinition is null)
            return ChatResult<ChannelMemberDto>.Failure("Custom role not found", 400);

        // Project owner is always an admin — no point adding them as plain member.
        if (targetId == ctx.OwnerId)
        {
            role = ChannelRole.Admin;
            roleDefinition = null;
        }
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            existing = new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = channel.Id,
                UserId = targetId,
                JoinedAt = now,
                Role = role,
                ChannelRoleDefinitionId = roleDefinition?.Id,
                State = ParticipantState.Active,
            };
            _context.ConversationParticipants.Add(existing);
        }
        else if (existing.State == ParticipantState.Banned)
        {
            return ChatResult<ChannelMemberDto>.Failure(
                "User is banned from this channel — unban first", 409);
        }
        else
        {
            existing.State = ParticipantState.Active;
            existing.Role = role;
            existing.ChannelRoleDefinitionId = roleDefinition?.Id;
            existing.JoinedAt = now;
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel member invited", "Conversation", channel.Id,
            $"{{\"TargetUserId\":\"{targetId}\",\"Role\":\"{role}\"}}");

        var dtoOut = await BuildSingleMemberAsync(channel, ctx.OwnerId, existing, ct);
        return ChatResult<ChannelMemberDto>.Success(dtoOut);
    }

    /// <inheritdoc />
    public async Task<ChatResult<ChannelMemberDto>> UpdateAsync(
        Guid userId, Guid channelId, Guid targetUserId, ChannelMemberPatchDto dto, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null)
            return ChatResult<ChannelMemberDto>.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult<ChannelMemberDto>.Failure(
                "Only channel admins can change member permissions", 403);

        // Project owner is non-editable — they always have full rights.
        if (targetUserId == ctx.OwnerId)
            return ChatResult<ChannelMemberDto>.Failure(
                "Project owner permissions cannot be altered", 400);

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == channelId && p.UserId == targetUserId, ct);

        if (participant is null || participant.State != ParticipantState.Active)
            return ChatResult<ChannelMemberDto>.Failure("Member not found in this channel", 404);

        if (dto.Role is { } roleRaw)
        {
            var parsed = ParseRole(roleRaw);
            if (parsed is null)
                return ChatResult<ChannelMemberDto>.Failure("Unknown role", 400);
            participant.Role = parsed.Value;
        }

        if (dto.RoleDefinitionId.HasValue)
        {
            var roleDefinition = await ResolveRoleDefinitionAsync(channelId, dto.RoleDefinitionId, ct);
            if (roleDefinition is null)
                return ChatResult<ChannelMemberDto>.Failure("Custom role not found", 400);
            participant.ChannelRoleDefinitionId = roleDefinition.Id;
        }
        else if (dto.Role is not null)
        {
            participant.ChannelRoleDefinitionId = null;
        }

        ApplyOverride(dto.CanPost, dto.ResetCanPost, v => participant.CanPost = v);
        ApplyOverride(dto.CanManageMembers, dto.ResetCanManageMembers, v => participant.CanManageMembers = v);
        ApplyOverride(dto.CanEditChannel, dto.ResetCanEditChannel, v => participant.CanEditChannel = v);
        ApplyOverride(dto.CanDeleteChannel, dto.ResetCanDeleteChannel, v => participant.CanDeleteChannel = v);
        ApplyOverride(dto.CanPinMessages, dto.ResetCanPinMessages, v => participant.CanPinMessages = v);
        ApplyOverride(dto.CanDeleteMessages, dto.ResetCanDeleteMessages, v => participant.CanDeleteMessages = v);

        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel member permissions updated",
            "Conversation", channelId, $"{{\"TargetUserId\":\"{targetUserId}\"}}");

        var channel = ctx.Channel!;
        var dtoOut = await BuildSingleMemberAsync(channel, ctx.OwnerId, participant, ct);
        return ChatResult<ChannelMemberDto>.Success(dtoOut);
    }

    /// <inheritdoc />
    public async Task<ChatResult> KickAsync(
        Guid userId, Guid channelId, Guid targetUserId, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null) return ChatResult.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult.Failure("Only channel admins can remove members", 403);
        if (targetUserId == ctx.OwnerId)
            return ChatResult.Failure("Project owner cannot be removed from channels", 400);
        if (targetUserId == userId)
            return ChatResult.Failure("Use the leave endpoint to remove yourself", 400);

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == channelId && p.UserId == targetUserId, ct);

        if (participant is null || participant.State != ParticipantState.Active)
            return ChatResult.Success();

        // Public channel → keep row with State=Left so auto-enrolment skips
        // them (they can rejoin manually via "join"). Private channel → same
        // semantics: row preserved, visibility switched off.
        participant.State = ParticipantState.Left;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel member kicked",
            "Conversation", channelId, $"{{\"TargetUserId\":\"{targetUserId}\"}}");
        return ChatResult.Success();
    }

    /// <inheritdoc />
    public async Task<ChatResult> BanAsync(
        Guid userId, Guid channelId, Guid targetUserId, ChannelBanDto dto, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null) return ChatResult.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult.Failure("Only channel admins can ban members", 403);
        if (targetUserId == ctx.OwnerId)
            return ChatResult.Failure("Project owner cannot be banned", 400);
        if (targetUserId == userId)
            return ChatResult.Failure("You cannot ban yourself", 400);

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == channelId && p.UserId == targetUserId, ct);

        var now = DateTime.UtcNow;
        if (participant is null)
        {
            // Pre-emptive ban — add a tombstone row so auto-enrolment and invite
            // both know to skip this user.
            participant = new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = channelId,
                UserId = targetUserId,
                JoinedAt = now,
                Role = ChannelRole.Member,
                State = ParticipantState.Banned,
                BannedAt = now,
                BannedByUserId = userId,
                BanReason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason!.Trim(),
            };
            _context.ConversationParticipants.Add(participant);
        }
        else
        {
            participant.State = ParticipantState.Banned;
            participant.BannedAt = now;
            participant.BannedByUserId = userId;
            participant.BanReason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason!.Trim();
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel member banned",
            "Conversation", channelId, $"{{\"TargetUserId\":\"{targetUserId}\"}}");
        return ChatResult.Success();
    }

    /// <inheritdoc />
    public async Task<ChatResult> UnbanAsync(
        Guid userId, Guid channelId, Guid targetUserId, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null) return ChatResult.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult.Failure("Only channel admins can unban members", 403);

        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == channelId && p.UserId == targetUserId, ct);

        if (participant is null || participant.State != ParticipantState.Banned)
            return ChatResult.Success();

        participant.State = ParticipantState.Left;
        participant.BanReason = null;
        participant.BannedAt = null;
        participant.BannedByUserId = null;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel member unbanned",
            "Conversation", channelId, $"{{\"TargetUserId\":\"{targetUserId}\"}}");
        return ChatResult.Success();
    }

    /// <inheritdoc />
    public async Task<ChatResult<IReadOnlyList<ChannelRoleDefinitionDto>>> ListRolesAsync(
        Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null)
            return ChatResult<IReadOnlyList<ChannelRoleDefinitionDto>>.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult<IReadOnlyList<ChannelRoleDefinitionDto>>.Failure("Only channel admins can manage roles", 403);

        var roles = await _context.ChannelRoleDefinitions
            .AsNoTracking()
            .Where(r => r.ConversationId == channelId)
            .OrderBy(r => r.Name)
            .Select(r => MapRoleDto(r))
            .ToListAsync(ct);

        return ChatResult<IReadOnlyList<ChannelRoleDefinitionDto>>.Success(roles);
    }

    /// <inheritdoc />
    public async Task<ChatResult<ChannelRoleDefinitionDto>> CreateRoleAsync(
        Guid userId, Guid channelId, ChannelRoleDefinitionUpsertDto dto, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null)
            return ChatResult<ChannelRoleDefinitionDto>.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult<ChannelRoleDefinitionDto>.Failure("Only channel admins can manage roles", 403);

        var name = NormalizeRoleName(dto.Name);
        if (name is null)
            return ChatResult<ChannelRoleDefinitionDto>.Failure("Role name is required", 400);

        var exists = await _context.ChannelRoleDefinitions
            .AnyAsync(r => r.ConversationId == channelId && r.Name == name, ct);
        if (exists)
            return ChatResult<ChannelRoleDefinitionDto>.Failure("Role already exists", 409);

        var now = DateTime.UtcNow;
        var role = new ChannelRoleDefinition
        {
            Id = Guid.NewGuid(),
            ConversationId = channelId,
            Name = name,
            CanPost = dto.CanPost,
            CanManageMembers = dto.CanManageMembers,
            CanEditChannel = dto.CanEditChannel,
            CanDeleteChannel = dto.CanDeleteChannel,
            CanPinMessages = dto.CanPinMessages,
            CanDeleteMessages = dto.CanDeleteMessages,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _context.ChannelRoleDefinitions.Add(role);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel role created", "Conversation", channelId,
            $"{{\"RoleId\":\"{role.Id}\"}}");

        return ChatResult<ChannelRoleDefinitionDto>.Success(MapRoleDto(role));
    }

    /// <inheritdoc />
    public async Task<ChatResult<ChannelRoleDefinitionDto>> UpdateRoleAsync(
        Guid userId, Guid channelId, Guid roleId, ChannelRoleDefinitionUpsertDto dto, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null)
            return ChatResult<ChannelRoleDefinitionDto>.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult<ChannelRoleDefinitionDto>.Failure("Only channel admins can manage roles", 403);

        var role = await _context.ChannelRoleDefinitions
            .FirstOrDefaultAsync(r => r.Id == roleId && r.ConversationId == channelId, ct);
        if (role is null)
            return ChatResult<ChannelRoleDefinitionDto>.Failure("Role not found", 404);

        var name = NormalizeRoleName(dto.Name);
        if (name is null)
            return ChatResult<ChannelRoleDefinitionDto>.Failure("Role name is required", 400);

        var nameTaken = await _context.ChannelRoleDefinitions
            .AnyAsync(r => r.ConversationId == channelId && r.Id != roleId && r.Name == name, ct);
        if (nameTaken)
            return ChatResult<ChannelRoleDefinitionDto>.Failure("Role already exists", 409);

        role.Name = name;
        role.CanPost = dto.CanPost;
        role.CanManageMembers = dto.CanManageMembers;
        role.CanEditChannel = dto.CanEditChannel;
        role.CanDeleteChannel = dto.CanDeleteChannel;
        role.CanPinMessages = dto.CanPinMessages;
        role.CanDeleteMessages = dto.CanDeleteMessages;
        role.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel role updated", "Conversation", channelId,
            $"{{\"RoleId\":\"{role.Id}\"}}");

        return ChatResult<ChannelRoleDefinitionDto>.Success(MapRoleDto(role));
    }

    /// <inheritdoc />
    public async Task<ChatResult> DeleteRoleAsync(
        Guid userId, Guid channelId, Guid roleId, CancellationToken ct = default)
    {
        var ctx = await LoadChannelContextAsync(userId, channelId, ct);
        if (ctx.Error is not null) return ChatResult.Failure(ctx.Error, ctx.ErrorCode);

        if (!ctx.ViewerCanManageMembers)
            return ChatResult.Failure("Only channel admins can manage roles", 403);

        var role = await _context.ChannelRoleDefinitions
            .FirstOrDefaultAsync(r => r.Id == roleId && r.ConversationId == channelId, ct);
        if (role is null) return ChatResult.Success();

        var participants = await _context.ConversationParticipants
            .Where(p => p.ChannelRoleDefinitionId == roleId)
            .ToListAsync(ct);
        foreach (var participant in participants)
        {
            participant.ChannelRoleDefinitionId = null;
        }

        _context.ChannelRoleDefinitions.Remove(role);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "Channel role deleted", "Conversation", channelId,
            $"{{\"RoleId\":\"{roleId}\"}}");

        return ChatResult.Success();
    }

    // ---------- helpers ----------

    /// <summary>Loaded channel, owner id, and viewer permission flags, or an error payload.</summary>
    private sealed record ChannelContext(
        Conversation? Channel,
        Guid OwnerId,
        bool ViewerIsOwner,
        bool ViewerCanManageMembers,
        string? Error,
        int ErrorCode);

    /// <summary>
    /// Resolves channel access for the viewer, hiding private channels from non-participants and
    /// computing whether they may manage members.
    /// </summary>
    private async Task<ChannelContext> LoadChannelContextAsync(
        Guid userId, Guid channelId, CancellationToken ct)
    {
        var channel = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == channelId &&
                                      c.Type == ConversationType.ProjectChannel, ct);

        if (channel is null || channel.ProjectId is null)
            return new ChannelContext(null, Guid.Empty, false, false, "Channel not found", 404);

        var ownerId = await _context.Projects
            .Where(p => p.Id == channel.ProjectId)
            .Select(p => p.OwnerId)
            .FirstOrDefaultAsync(ct);

        if (ownerId == Guid.Empty)
            return new ChannelContext(null, Guid.Empty, false, false, "Project not found", 404);

        var viewerIsOwner = ownerId == userId;
        var viewerIsProjectMember = viewerIsOwner || await _context.TeamMembers
            .AnyAsync(tm => tm.ProjectId == channel.ProjectId &&
                            tm.UserId == userId &&
                            tm.Status == TeamMemberStatus.Active.Value, ct);

        if (!viewerIsProjectMember)
            return new ChannelContext(null, Guid.Empty, false, false, "Access denied", 403);

        var viewerParticipant = await _context.ConversationParticipants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ConversationId == channel.Id && p.UserId == userId, ct);

        if (channel.IsPrivate && !viewerIsOwner &&
            (viewerParticipant is null || viewerParticipant.State != ParticipantState.Active))
        {
            return new ChannelContext(null, Guid.Empty, false, false, "Channel not found", 404);
        }

        var viewerCanManage = viewerIsOwner
            || channel.CreatedByUserId == userId
            || ResolveEffective(viewerParticipant, ChannelRole.Member, ownerId, userId).CanManageMembers;

        return new ChannelContext(channel, ownerId, viewerIsOwner, viewerCanManage, null, 200);
    }

    /// <summary>Projects filtered participants into DTOs ordered owner-first, then admins, then name.</summary>
    private async Task<List<ChannelMemberDto>> BuildMemberListAsync(
        Conversation channel, Guid ownerId, Func<ConversationParticipant, bool> filter, CancellationToken ct)
    {
        var participants = await _context.ConversationParticipants
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.RoleDefinition)
            .Where(p => p.ConversationId == channel.Id)
            .ToListAsync(ct);

        return participants
            .Where(filter)
            .Select(p => MapToDto(p, channel, ownerId))
            .OrderByDescending(d => d.IsProjectOwner)
            .ThenByDescending(d => d.Role == "admin")
            .ThenBy(d => d.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Hydrates navigation properties needed by <see cref="MapToDto"/> for one participant.</summary>
    private async Task<ChannelMemberDto> BuildSingleMemberAsync(
        Conversation channel, Guid ownerId, ConversationParticipant participant, CancellationToken ct)
    {
        // Make sure User nav is hydrated for the DTO render.
        if (participant.User is null)
        {
            participant.User = (await _context.Users.FindAsync(new object[] { participant.UserId }, ct))!;
        }
        if (participant.ChannelRoleDefinitionId.HasValue && participant.RoleDefinition is null)
        {
            participant.RoleDefinition = await _context.ChannelRoleDefinitions
                .FirstOrDefaultAsync(r => r.Id == participant.ChannelRoleDefinitionId.Value, ct);
        }
        return MapToDto(participant, channel, ownerId);
    }

    /// <summary>Maps a participant row to a client DTO with effective and raw override permissions.</summary>
    private static ChannelMemberDto MapToDto(
        ConversationParticipant p, Conversation channel, Guid ownerId)
    {
        var isOwner = p.UserId == ownerId;
        var effective = ResolveEffective(p, p.Role, ownerId, p.UserId);

        return new ChannelMemberDto(
            UserId: p.UserId,
            FullName: p.User?.FullName ?? p.User?.Username ?? string.Empty,
            AvatarUrl: p.User?.AvatarUrl,
            Username: p.User?.Username,
            Role: isOwner ? "admin" : RoleToString(p.Role),
            RoleDefinitionId: p.ChannelRoleDefinitionId,
            RoleDisplayName: p.RoleDefinition?.Name,
            State: StateToString(p.State),
            CanPost: effective.CanPost,
            CanManageMembers: effective.CanManageMembers,
            CanEditChannel: effective.CanEditChannel,
            CanDeleteChannel: effective.CanDeleteChannel,
            CanPinMessages: effective.CanPinMessages,
            CanDeleteMessages: effective.CanDeleteMessages,
            CanPostOverride: p.CanPost,
            CanManageMembersOverride: p.CanManageMembers,
            CanEditChannelOverride: p.CanEditChannel,
            CanDeleteChannelOverride: p.CanDeleteChannel,
            CanPinMessagesOverride: p.CanPinMessages,
            CanDeleteMessagesOverride: p.CanDeleteMessages,
            JoinedAt: p.JoinedAt,
            IsProjectOwner: isOwner,
            IsChannelCreator: channel.CreatedByUserId == p.UserId,
            BanReason: p.BanReason,
            BannedAt: p.BannedAt);
    }

    /// <summary>
    /// Collapse overrides into effective booleans. Project owner is forced to
    /// all-true so the UI never grays them out.
    /// </summary>
    internal static EffectivePermissions ResolveEffective(
        ConversationParticipant? p, ChannelRole fallbackRole, Guid ownerId, Guid viewerId)
    {
        if (viewerId == ownerId)
            return new EffectivePermissions(true, true, true, true, true, true);

        var role = p?.Role ?? fallbackRole;
        var defaults = p?.RoleDefinition is not null
            ? DefaultsFor(p.RoleDefinition)
            : DefaultsFor(role);

        return new EffectivePermissions(
            p?.CanPost ?? defaults.CanPost,
            p?.CanManageMembers ?? defaults.CanManageMembers,
            p?.CanEditChannel ?? defaults.CanEditChannel,
            p?.CanDeleteChannel ?? defaults.CanDeleteChannel,
            p?.CanPinMessages ?? defaults.CanPinMessages,
            p?.CanDeleteMessages ?? defaults.CanDeleteMessages);
    }

    /// <summary>Built-in permission defaults for built-in <see cref="ChannelRole"/> values.</summary>
    /// <param name="role">Admin grants all gates; member allows post only.</param>
    /// <returns>Effective defaults before participant overrides are applied.</returns>
    internal static EffectivePermissions DefaultsFor(ChannelRole role) => role switch
    {
        ChannelRole.Admin => new EffectivePermissions(true, true, true, true, true, true),
        _ => new EffectivePermissions(true, false, false, false, false, false),
    };

    /// <summary>Permission defaults copied from a <see cref="ChannelRoleDefinition"/> row.</summary>
    private static EffectivePermissions DefaultsFor(ChannelRoleDefinition role) => new(
        role.CanPost,
        role.CanManageMembers,
        role.CanEditChannel,
        role.CanDeleteChannel,
        role.CanPinMessages,
        role.CanDeleteMessages);

    /// <summary>Resolved boolean gates after applying role defaults and nullable participant overrides.</summary>
    internal readonly record struct EffectivePermissions(
        bool CanPost,
        bool CanManageMembers,
        bool CanEditChannel,
        bool CanDeleteChannel,
        bool CanPinMessages,
        bool CanDeleteMessages);

    /// <summary>Loads a custom role scoped to the given channel, or null when id is omitted/unknown.</summary>
    private async Task<ChannelRoleDefinition?> ResolveRoleDefinitionAsync(
        Guid channelId, Guid? roleId, CancellationToken ct)
    {
        if (!roleId.HasValue) return null;
        return await _context.ChannelRoleDefinitions
            .FirstOrDefaultAsync(r => r.Id == roleId.Value && r.ConversationId == channelId, ct);
    }

    /// <summary>Maps an entity row to <see cref="ChannelRoleDefinitionDto"/>.</summary>
    private static ChannelRoleDefinitionDto MapRoleDto(ChannelRoleDefinition role) => new(
        role.Id,
        role.Name,
        role.CanPost,
        role.CanManageMembers,
        role.CanEditChannel,
        role.CanDeleteChannel,
        role.CanPinMessages,
        role.CanDeleteMessages,
        role.CreatedAt,
        role.UpdatedAt);

    /// <summary>Trims role names and returns null for blank input.</summary>
    private static string? NormalizeRoleName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    /// <summary>Parses <c>admin</c> or <c>member</c> strings; unknown values return null.</summary>
    private static ChannelRole? ParseRole(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "admin" => ChannelRole.Admin,
        "member" => ChannelRole.Member,
        _ => null,
    };

    /// <summary>
    /// Converts a built-in channel role to the lowercase API string.
    /// </summary>
    private static string RoleToString(ChannelRole role) => role switch
    {
        ChannelRole.Admin => "admin",
        _ => "member",
    };

    /// <summary>
    /// Converts a participant state to the lowercase API string, defaulting unknown values to active.
    /// </summary>
    private static string StateToString(ParticipantState s) => s switch
    {
        ParticipantState.Active => "active",
        ParticipantState.Left => "left",
        ParticipantState.Banned => "banned",
        _ => "active",
    };

    /// <summary>Applies a patch override or explicit reset-to-inherit via <see cref="ChannelMemberPatchDto"/> reset flags.</summary>
    private static void ApplyOverride(bool? value, bool? reset, Action<bool?> setter)
    {
        if (reset == true) { setter(null); return; }
        if (value.HasValue) setter(value);
    }
}
