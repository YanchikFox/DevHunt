namespace DevHunt.CoreApi.Services.Chat;

/// <summary>
/// Manages channel-level membership — roles, granular permission overrides,
/// voluntary leave, kick, ban/unban and invite of project team members.
/// Permission gates (<c>CanManageMembers</c>, <c>CanEditChannel</c>,
/// <c>CanDeleteChannel</c>) are enforced here; project owner always implicitly
/// has all of them.
/// </summary>
public interface IChannelMemberService
{
    /// <summary>Active participants of the channel (excludes banned / left).</summary>
    Task<ChatResult<IReadOnlyList<ChannelMemberDto>>> ListActiveAsync(
        Guid userId, Guid channelId, CancellationToken ct = default);

    /// <summary>Banned users for the channel.</summary>
    Task<ChatResult<IReadOnlyList<ChannelMemberDto>>> ListBannedAsync(
        Guid userId, Guid channelId, CancellationToken ct = default);

    /// <summary>Project team members that can be added to the channel (not already in, not banned).</summary>
    Task<ChatResult<IReadOnlyList<ChannelCandidateDto>>> ListCandidatesAsync(
        Guid userId, Guid channelId, CancellationToken ct = default);

    Task<ChatResult<IReadOnlyList<ChannelRoleDefinitionDto>>> ListRolesAsync(
        Guid userId, Guid channelId, CancellationToken ct = default);

    /// <summary>Creates a custom role with default permission flags for the channel.</summary>
    Task<ChatResult<ChannelRoleDefinitionDto>> CreateRoleAsync(
        Guid userId, Guid channelId, ChannelRoleDefinitionUpsertDto dto, CancellationToken ct = default);

    /// <summary>Renames a custom role and replaces its permission defaults.</summary>
    Task<ChatResult<ChannelRoleDefinitionDto>> UpdateRoleAsync(
        Guid userId, Guid channelId, Guid roleId, ChannelRoleDefinitionUpsertDto dto, CancellationToken ct = default);

    /// <summary>Deletes a custom role and clears it from any assigned participants.</summary>
    Task<ChatResult> DeleteRoleAsync(
        Guid userId, Guid channelId, Guid roleId, CancellationToken ct = default);

    /// <summary>Invite / re-add a user as an Active participant.</summary>
    Task<ChatResult<ChannelMemberDto>> InviteAsync(
        Guid userId, Guid channelId, ChannelInviteDto dto, CancellationToken ct = default);

    /// <summary>Patch role / permission overrides for a member.</summary>
    Task<ChatResult<ChannelMemberDto>> UpdateAsync(
        Guid userId, Guid channelId, Guid targetUserId, ChannelMemberPatchDto dto, CancellationToken ct = default);

    /// <summary>Soft kick — participant stays with <c>State = Left</c>, can rejoin manually.</summary>
    Task<ChatResult> KickAsync(
        Guid userId, Guid channelId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>Hard ban — blocks re-entry, optional public reason.</summary>
    Task<ChatResult> BanAsync(
        Guid userId, Guid channelId, Guid targetUserId, ChannelBanDto dto, CancellationToken ct = default);

    /// <summary>Lift a ban — the user returns to the "Left" state, can rejoin via Join.</summary>
    Task<ChatResult> UnbanAsync(
        Guid userId, Guid channelId, Guid targetUserId, CancellationToken ct = default);
}
