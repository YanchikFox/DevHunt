using System.ComponentModel.DataAnnotations;

namespace DevHunt.CoreApi.Services.Chat;

/// <summary>
/// Resolved permission bundle for a channel participant — explicit overrides
/// are collapsed against role defaults so the client gets a single boolean per
/// gate. Nullable <c>Can*Override</c> fields expose the raw column values so a
/// moderator UI can render "inherit" vs. "explicit true / false" tri-states.
/// </summary>
public sealed record ChannelMemberDto(
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    string? Username,
    string Role,
    Guid? RoleDefinitionId,
    string? RoleDisplayName,
    string State,
    bool CanPost,
    bool CanManageMembers,
    bool CanEditChannel,
    bool CanDeleteChannel,
    bool CanPinMessages,
    bool CanDeleteMessages,
    bool? CanPostOverride,
    bool? CanManageMembersOverride,
    bool? CanEditChannelOverride,
    bool? CanDeleteChannelOverride,
    bool? CanPinMessagesOverride,
    bool? CanDeleteMessagesOverride,
    DateTime JoinedAt,
    bool IsProjectOwner,
    bool IsChannelCreator,
    string? BanReason,
    DateTime? BannedAt
);

/// <summary>
/// Candidate user surfaced by the "invite to channel" picker — any active
/// project team member plus the owner, minus people already in the channel.
/// </summary>
public sealed record ChannelCandidateDto(
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    string? Username,
    bool IsProjectOwner
);

/// <summary>
/// Invite payload — add a project member to a channel with an optional role.
/// </summary>
public sealed class ChannelInviteDto
{
    /// <summary>Project team member to add to the channel.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>"member" or "admin". Defaults to member.</summary>
    [MaxLength(16)]
    public string? Role { get; set; }

    /// <summary>Optional custom role from <see cref="ChannelRoleDefinitionDto"/> for this channel.</summary>
    public Guid? RoleDefinitionId { get; set; }
}

/// <summary>
/// Patch payload — change role and/or individual permission overrides. Omitted
/// fields are left untouched. Setting a <c>Can*</c> override to null reverts to
/// role-default behaviour.
/// </summary>
public sealed class ChannelMemberPatchDto
{
    /// <summary>"member" or "admin".</summary>
    [MaxLength(16)]
    public string? Role { get; set; }

    /// <summary>Optional custom role; cleared when <see cref="Role"/> is set without an id.</summary>
    public Guid? RoleDefinitionId { get; set; }

    /// <summary>Explicit override for posting; null inherits from role defaults.</summary>
    public bool? CanPost { get; set; }
    /// <summary>Explicit override for inviting/kicking/banning members.</summary>
    public bool? CanManageMembers { get; set; }
    /// <summary>Explicit override for editing channel metadata.</summary>
    public bool? CanEditChannel { get; set; }
    /// <summary>Explicit override for deleting the channel.</summary>
    public bool? CanDeleteChannel { get; set; }
    /// <summary>Explicit override for pinning messages.</summary>
    public bool? CanPinMessages { get; set; }
    /// <summary>Explicit override for deleting others' messages.</summary>
    public bool? CanDeleteMessages { get; set; }

    /// <summary>
    /// If true, the corresponding permission override is explicitly reset to
    /// null (inherit from role). Lets the UI distinguish "don't touch" from
    /// "revert to default" in a single patch.
    /// </summary>
    public bool? ResetCanPost { get; set; }
    /// <summary>When true, clears the <see cref="CanPost"/> override.</summary>
    public bool? ResetCanManageMembers { get; set; }
    /// <summary>When true, clears the <see cref="CanManageMembers"/> override.</summary>
    public bool? ResetCanEditChannel { get; set; }
    /// <summary>When true, clears the <see cref="CanEditChannel"/> override.</summary>
    public bool? ResetCanDeleteChannel { get; set; }
    /// <summary>When true, clears the <see cref="CanDeleteChannel"/> override.</summary>
    public bool? ResetCanPinMessages { get; set; }
    /// <summary>When true, clears the <see cref="CanPinMessages"/> override.</summary>
    public bool? ResetCanDeleteMessages { get; set; }
}

/// <summary>
/// Custom permission template assignable to channel members via
/// <see cref="ChannelMemberPatchDto.RoleDefinitionId"/>.
/// </summary>
/// <param name="Id">Role definition id scoped to one channel.</param>
/// <param name="Name">Display name shown in moderator UI.</param>
/// <param name="CanPost">Default post permission when no participant override is set.</param>
/// <param name="CanManageMembers">Default member-management permission.</param>
/// <param name="CanEditChannel">Default channel-edit permission.</param>
/// <param name="CanDeleteChannel">Default channel-delete permission.</param>
/// <param name="CanPinMessages">Default message-pin permission.</param>
/// <param name="CanDeleteMessages">Default delete-others-messages permission.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="UpdatedAt">UTC last update timestamp.</param>
public sealed record ChannelRoleDefinitionDto(
    Guid Id,
    string Name,
    bool CanPost,
    bool CanManageMembers,
    bool CanEditChannel,
    bool CanDeleteChannel,
    bool CanPinMessages,
    bool CanDeleteMessages,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Create-or-update payload for a channel-scoped custom role and its default permission flags.
/// </summary>
public sealed class ChannelRoleDefinitionUpsertDto
{
    /// <summary>Unique role name within the channel (max 64 chars).</summary>
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether members with this role may post by default.</summary>
    public bool CanPost { get; set; } = true;
    /// <summary>Whether members may invite, kick, and ban by default.</summary>
    public bool CanManageMembers { get; set; }
    /// <summary>Whether members may edit channel metadata by default.</summary>
    public bool CanEditChannel { get; set; }
    /// <summary>Whether members may delete the channel by default.</summary>
    public bool CanDeleteChannel { get; set; }
    /// <summary>Whether members may pin messages by default.</summary>
    public bool CanPinMessages { get; set; }
    /// <summary>Whether members may delete others' messages by default.</summary>
    public bool CanDeleteMessages { get; set; }
}

/// <summary>
/// Ban payload — explicit ban with an optional reason shown in the banned list.
/// </summary>
public sealed class ChannelBanDto
{
    /// <summary>Optional moderator note stored on the participant ban record.</summary>
    [MaxLength(280)]
    public string? Reason { get; set; }
}
