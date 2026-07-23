using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Dialog, group chat, or project channel.
/// Unifies direct messages, ad-hoc group conversations and Discord-style
/// per-project channels under a single entity — channels are represented as
/// <see cref="ConversationType.ProjectChannel"/> with a non-null <see cref="ProjectId"/>.
/// Corresponds to ERD: Conversations (devhunt_erd.puml, lines 150-158).
/// </summary>
[Table("Conversations")]
public class Conversation
{
    /// <summary>
    /// Unique identifier of the conversation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Conversation type: direct, group, or project channel.
    /// </summary>
    public ConversationType Type { get; set; }

    /// <summary>
    /// Human-readable title (for group chats and channels — for channels this
    /// may differ from <see cref="Slug"/>, e.g. title = "Backend discussion",
    /// slug = "backend").
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Project this conversation belongs to. Non-null only for
    /// <see cref="ConversationType.ProjectChannel"/>.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// URL-friendly channel identifier, unique within a project
    /// (e.g. "general", "frontend", "bugs"). Max 50 chars. Non-null only for
    /// <see cref="ConversationType.ProjectChannel"/>.
    /// </summary>
    [MaxLength(50)]
    public string? Slug { get; set; }

    /// <summary>
    /// Optional short topic / description shown under the channel title.
    /// </summary>
    [MaxLength(280)]
    public string? Topic { get; set; }

    /// <summary>
    /// When true, the channel is invite-only — non-participant project
    /// members cannot see or join it. Always false for Direct/Group.
    /// </summary>
    public bool IsPrivate { get; set; }

    /// <summary>
    /// Sort order of the channel inside the project sidebar. Lower first.
    /// Ignored for Direct/Group conversations.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// User who created the conversation (channel). Null for legacy rows
    /// that predate the channels migration.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// Conversation creation date.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last update date (participant changes, title changes).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Date of the last message in the conversation.
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Project this channel belongs to (nullable — set for ProjectChannel only).
    /// </summary>
    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// User who created the channel (nullable for legacy rows).
    /// </summary>
    [ForeignKey(nameof(CreatedByUserId))]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Conversation participants.
    /// </summary>
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

    /// <summary>
    /// Messages in the conversation.
    /// </summary>
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

/// <summary>
/// Conversation type: direct, group, or project channel.
/// </summary>
public enum ConversationType
{
    /// <summary>
    /// Direct conversation between two users.
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Ad-hoc group chat with multiple participants, not tied to a project.
    /// </summary>
    Group = 1,

    /// <summary>
    /// Discord/Slack-style channel inside a project. Scoped by
    /// <see cref="Conversation.ProjectId"/> + unique <see cref="Conversation.Slug"/>.
    /// </summary>
    ProjectChannel = 2
}

