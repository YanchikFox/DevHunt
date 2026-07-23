using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// CRUD for Discord/Slack-style channels inside a project.
///
/// A channel is just a <c>Conversation</c> of type <c>ProjectChannel</c> —
/// messages continue to flow through <see cref="ChatController"/>
/// (<c>api/chat/conversations/{id}/messages</c>) using the channel's id.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/channels")]
[Authorize]
public sealed class ProjectChannelsController : ControllerBase
{
    private readonly IProjectChannelService _channels;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectChannelsController"/> class.
    /// </summary>
    /// <param name="channels">Service that owns project channel membership and channel CRUD rules.</param>
    public ProjectChannelsController(IProjectChannelService channels)
    {
        _channels = channels;
    }

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User)
            ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Converts a successful chat service payload to <see cref="OkObjectResult"/> or preserves the service error status.
    /// </summary>
    private IActionResult Map<T>(ChatResult<T> result) => result.IsSuccess
        ? Ok(result.Data)
        : StatusCode(result.StatusCode, result.ErrorMessage);

    /// <summary>
    /// Converts a successful no-payload chat service result to <see cref="NoContentResult"/> or preserves the service error status.
    /// </summary>
    private IActionResult Map(ChatResult result) => result.IsSuccess
        ? NoContent()
        : StatusCode(result.StatusCode, result.ErrorMessage);

    /// <summary>
    /// Lists channels visible to the authenticated project participant; service errors are returned with their original status code.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, CancellationToken ct)
    {
        var result = await _channels.ListAsync(GetRequiredUserId(), projectId, ct);
        return Map(result);
    }

    /// <summary>
    /// Creates a project channel after model validation and returns the service-created channel payload.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId, [FromBody] CreateProjectChannelDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await _channels.CreateAsync(GetRequiredUserId(), projectId, dto, ct);
        return Map(result);
    }

    /// <summary>
    /// Updates channel metadata after model validation and returns the service result for authorization or lookup failures.
    /// </summary>
    [HttpPatch("{channelId:guid}")]
    public async Task<IActionResult> Update(
        Guid projectId, Guid channelId, [FromBody] UpdateProjectChannelDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await _channels.UpdateAsync(GetRequiredUserId(), channelId, dto, ct);
        return Map(result);
    }

    /// <summary>
    /// Deletes a channel through the channel service; protected channels such as <c>#general</c> are rejected there.
    /// </summary>
    [HttpDelete("{channelId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid channelId, CancellationToken ct)
    {
        var result = await _channels.DeleteAsync(GetRequiredUserId(), channelId, ct);
        return Map(result);
    }

    /// <summary>
    /// Adds the authenticated user as a channel participant when the service permits joining.
    /// </summary>
    [HttpPost("{channelId:guid}/join")]
    public async Task<IActionResult> Join(Guid projectId, Guid channelId, CancellationToken ct)
    {
        var result = await _channels.JoinAsync(GetRequiredUserId(), channelId, ct);
        return Map(result);
    }

    /// <summary>
    /// Removes the authenticated user from the channel membership when the service permits leaving.
    /// </summary>
    [HttpPost("{channelId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid projectId, Guid channelId, CancellationToken ct)
    {
        var result = await _channels.LeaveAsync(GetRequiredUserId(), channelId, ct);
        return Map(result);
    }
}
