using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Conversation/chat participant.
/// Stores user participation settings in the conversation (read status, mute, join time)
/// and — for Discord/Slack-style project channels — the per-channel role,
/// granular permission overrides and membership state (active / left / banned).
/// Corresponds to ERD: Conversation_Participants (devhunt_erd.puml, lines 160-168).
/// </summary>
[Table("Conversation_Participants")]
public class ConversationParticipant
{
    /// <summary>
    /// Unique identifier of the participation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Conversation identifier.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// User-participant identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Date of joining the conversation.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Date of last read messages in the conversation.
    /// Used to count unread messages.
    /// </summary>
    public DateTime? LastReadAt { get; set; }

    /// <summary>
    /// Muted notifications flag.
    /// </summary>
    public bool IsMuted { get; set; }

    // ---------- channel-specific columns ----------

    /// <summary>
    /// Channel role — defines sensible permission defaults. Actual per-permission
    /// gates are resolved via the <see cref="CanPost"/>/<see cref="CanManageMembers"/>/
    /// <see cref="CanEditChannel"/>/<see cref="CanDeleteChannel"/> overrides
    /// (null = "inherit role default").
    /// Ignored for Direct/Group conversations.
    /// </summary>
    public ChannelRole Role { get; set; } = ChannelRole.Member;

    /// <summary>
    /// Optional custom role definition assigned in this channel. When present,
    /// its permissions are used as defaults before per-member overrides.
    /// </summary>
    public Guid? ChannelRoleDefinitionId { get; set; }

    /// <summary>
    /// Membership state — active members see &amp; post, "left" rows are retained so
    /// voluntary-leavers aren't auto-re-enrolled on public channels, and "banned"
    /// rows block posting and re-entry.
    /// Always <c>Active</c> for Direct/Group conversations.
    /// </summary>
    public ParticipantState State { get; set; } = ParticipantState.Active;

    /// <summary>
    /// Posting override (null = inherit role default: Admin=true, Member=true).
    /// </summary>
    public bool? CanPost { get; set; }

    /// <summary>
    /// Member management override (null = inherit: Admin=true, Member=false).
    /// </summary>
    public bool? CanManageMembers { get; set; }

    /// <summary>
    /// Channel metadata editing override (null = inherit: Admin=true, Member=false).
    /// </summary>
    public bool? CanEditChannel { get; set; }

    /// <summary>
    /// Channel deletion override (null = inherit: Admin=true, Member=false).
    /// Project owner always effectively has this regardless of the flag.
    /// </summary>
    public bool? CanDeleteChannel { get; set; }

    /// <summary>
    /// Message pinning override (null = inherit role default).
    /// </summary>
    public bool? CanPinMessages { get; set; }

    /// <summary>
    /// Message moderation override: delete any message in this channel
    /// (null = inherit role default). Authors can still delete their own messages.
    /// </summary>
    public bool? CanDeleteMessages { get; set; }

    /// <summary>
    /// Optional moderator-supplied reason shown in the banned-users list.
    /// </summary>
    [MaxLength(280)]
    public string? BanReason { get; set; }

    /// <summary>
    /// When the user was banned (null if never). Preserved on unban for history.
    /// </summary>
    public DateTime? BannedAt { get; set; }

    /// <summary>
    /// Who banned the user. Null if State != Banned or for legacy rows.
    /// </summary>
    public Guid? BannedByUserId { get; set; }

    // Navigation properties

    /// <summary>
    /// Conversation in which the user participates.
    /// </summary>
    [ForeignKey(nameof(ConversationId))]
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// User-participant of the conversation.
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>
    /// Moderator who banned this user (nullable — unbanned / legacy / not banned).
    /// </summary>
    [ForeignKey(nameof(BannedByUserId))]
    public User? BannedBy { get; set; }

    /// <summary>
    /// Custom channel role assigned to this participant.
    /// </summary>
    [ForeignKey(nameof(ChannelRoleDefinitionId))]
    public ChannelRoleDefinition? RoleDefinition { get; set; }
}

/// <summary>
/// Custom role definition scoped to one project channel.
/// </summary>
[Table("Channel_Role_Definitions")]
public class ChannelRoleDefinition
{
    /// <summary>Primary key for a custom channel role definition.</summary>
    public Guid Id { get; set; }

    /// <summary>Project channel conversation that owns this custom role.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Display name shown for the custom channel role.</summary>
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Default posting permission granted by this custom role.</summary>
    public bool CanPost { get; set; }
    /// <summary>Default member management permission granted by this custom role.</summary>
    public bool CanManageMembers { get; set; }
    /// <summary>Default channel metadata editing permission granted by this custom role.</summary>
    public bool CanEditChannel { get; set; }
    /// <summary>Default channel deletion permission granted by this custom role.</summary>
    public bool CanDeleteChannel { get; set; }
    /// <summary>Default message pinning permission granted by this custom role.</summary>
    public bool CanPinMessages { get; set; }
    /// <summary>Default message moderation permission granted by this custom role.</summary>
    public bool CanDeleteMessages { get; set; }

    /// <summary>UTC timestamp when the custom role was created.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>UTC timestamp when the custom role was last changed.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Project channel conversation that owns this custom role.</summary>
    [ForeignKey(nameof(ConversationId))]
    public Conversation Conversation { get; set; } = null!;
}

/// <summary>
/// Channel-level role of a conversation participant. Provides default values for
/// granular permission flags and is used by the UI to group members.
/// </summary>
public enum ChannelRole
{
    /// <summary>Regular member — can read and post by default.</summary>
    Member = 0,

    /// <summary>Channel admin — full management rights by default.</summary>
    Admin = 1,
}

/// <summary>
/// Membership state of a conversation participant.
/// </summary>
public enum ParticipantState
{
    /// <summary>Actively participating — sees and can post (if <c>CanPost</c> allows).</summary>
    Active = 0,

    /// <summary>Left voluntarily — retained so auto-enrolment on public channels skips them.</summary>
    Left = 1,

    /// <summary>Banned by a moderator — blocked from posting and re-entry.</summary>
    Banned = 2,
}
