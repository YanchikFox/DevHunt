namespace DevHunt.CoreApi.Services.Chat;

/// <summary>
/// Manages Discord/Slack-style channels scoped to a project. Channels are
/// Conversations with <c>Type = ProjectChannel</c> — messages go through the
/// existing <see cref="IChatService"/> pipeline using the channel's
/// conversation id.
/// </summary>
public interface IProjectChannelService
{
    /// <summary>List channels visible to the user in the given project.</summary>
    Task<ChatResult<IReadOnlyList<ProjectChannelDto>>> ListAsync(
        Guid userId, Guid projectId, CancellationToken ct = default);

    /// <summary>Create a new channel in the project.</summary>
    Task<ChatResult<ProjectChannelDto>> CreateAsync(
        Guid userId, Guid projectId, CreateProjectChannelDto dto, CancellationToken ct = default);

    /// <summary>Update channel metadata (slug/title/topic/visibility/position).</summary>
    Task<ChatResult<ProjectChannelDto>> UpdateAsync(
        Guid userId, Guid channelId, UpdateProjectChannelDto dto, CancellationToken ct = default);

    /// <summary>Archive/delete a channel. <c>#general</c> cannot be removed.</summary>
    Task<ChatResult> DeleteAsync(Guid userId, Guid channelId, CancellationToken ct = default);

    /// <summary>Join a channel (adds the user as participant).</summary>
    Task<ChatResult<ProjectChannelDto>> JoinAsync(Guid userId, Guid channelId, CancellationToken ct = default);

    /// <summary>Leave a channel. <c>#general</c> cannot be left while user is a project member.</summary>
    Task<ChatResult> LeaveAsync(Guid userId, Guid channelId, CancellationToken ct = default);

    /// <summary>
    /// Resolve the project's <c>#general</c> channel id, creating one if none
    /// exists yet. Used by the legacy "project chat" endpoint to stay working.
    /// </summary>
    Task<ChatResult<ProjectChannelDto>> EnsureGeneralAsync(
        Guid userId, Guid projectId, CancellationToken ct = default);
}
