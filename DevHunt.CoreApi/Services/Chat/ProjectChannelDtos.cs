using System.ComponentModel.DataAnnotations;

namespace DevHunt.CoreApi.Services.Chat;

/// <summary>
/// Project channel summary returned to the client. Comparable to a Discord
/// text channel.
/// </summary>
public sealed record ProjectChannelDto(
    Guid Id,
    Guid ProjectId,
    string Slug,
    string? Title,
    string? Topic,
    bool IsPrivate,
    int Position,
    DateTime CreatedAt,
    DateTime? LastMessageAt,
    int UnreadCount,
    bool IsMember,
    // CanManage = CanEditChannel || CanManageMembers — used for the "edit" button visibility.
    bool CanManage,
    bool CanPost,
    bool CanManageMembers,
    bool CanEditChannel,
    bool CanDeleteChannel,
    bool CanPinMessages,
    bool CanDeleteMessages,
    // ViewerRole: "admin", "member" or null when the viewer has no participant row yet.
    string? ViewerRole
);

/// <summary>
/// Payload for creating a new channel.
/// </summary>
public sealed class CreateProjectChannelDto
{
    /// <summary>
    /// URL-safe channel identifier shown as <c>#slug</c> (1–50 lowercase chars).
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name; defaults to the slug when omitted.
    /// </summary>
    [MaxLength(50)]
    public string? Title { get; set; }

    /// <summary>
    /// Optional channel topic or description (max 280 chars).
    /// </summary>
    [MaxLength(280)]
    public string? Topic { get; set; }

    /// <summary>
    /// When true, only invited participants can see or join the channel.
    /// </summary>
    public bool IsPrivate { get; set; }
}

/// <summary>
/// Payload for editing channel metadata. All fields optional — null means
/// "don't change".
/// </summary>
public sealed class UpdateProjectChannelDto
{
    /// <summary>
    /// New slug; <c>#general</c> cannot be renamed.
    /// </summary>
    [MaxLength(50)]
    public string? Slug { get; set; }

    /// <summary>
    /// New display title, or empty to clear.
    /// </summary>
    [MaxLength(50)]
    public string? Title { get; set; }

    /// <summary>
    /// New topic text, or empty to clear.
    /// </summary>
    [MaxLength(280)]
    public string? Topic { get; set; }

    /// <summary>
    /// Toggle privacy; ignored for <c>#general</c>.
    /// </summary>
    public bool? IsPrivate { get; set; }

    /// <summary>
    /// Sidebar sort order within the project channel list.
    /// </summary>
    public int? Position { get; set; }
}
