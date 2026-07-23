using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Per-channel member management — list active members &amp; banned users,
/// invite from the project team, patch role / granular permission overrides,
/// kick, ban and unban.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/channels/{channelId:guid}/members")]
[Authorize]
public sealed class ChannelMembersController : ControllerBase
{
    private readonly IChannelMemberService _members;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelMembersController"/> class.
    /// </summary>
    /// <param name="members">Service that enforces channel membership, ban, and custom-role rules.</param>
    public ChannelMembersController(IChannelMemberService members)
    {
        _members = members;
    }

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId() =>
        SecurityHelpers.GetUserId(User)
            ?? throw new InvalidOperationException("User identifier claim is missing");

    /// <summary>
    /// Converts a successful chat service payload to <see cref="OkObjectResult"/> or preserves the service error status.
    /// </summary>
    private IActionResult Map<T>(ChatResult<T> r) =>
        r.IsSuccess ? Ok(r.Data) : StatusCode(r.StatusCode, r.ErrorMessage);

    /// <summary>
    /// Converts a successful no-payload chat service result to <see cref="NoContentResult"/> or preserves the service error status.
    /// </summary>
    private IActionResult Map(ChatResult r) =>
        r.IsSuccess ? NoContent() : StatusCode(r.StatusCode, r.ErrorMessage);

    /// <summary>
    /// Lists active channel members visible to the caller, returning the membership service's authorization failures unchanged.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, Guid channelId, CancellationToken ct)
        => Map(await _members.ListActiveAsync(GetRequiredUserId(), channelId, ct));

    /// <summary>
    /// Lists banned channel users when the membership service recognizes the caller as a channel administrator.
    /// </summary>
    [HttpGet("banned")]
    public async Task<IActionResult> ListBanned(Guid projectId, Guid channelId, CancellationToken ct)
        => Map(await _members.ListBannedAsync(GetRequiredUserId(), channelId, ct));

    /// <summary>
    /// Lists project members eligible to be invited into the channel when the caller has channel administration rights.
    /// </summary>
    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates(Guid projectId, Guid channelId, CancellationToken ct)
        => Map(await _members.ListCandidatesAsync(GetRequiredUserId(), channelId, ct));

    /// <summary>
    /// Lists custom roles defined for the channel, preserving service-level access and lookup failures.
    /// </summary>
    [HttpGet("roles")]
    public async Task<IActionResult> Roles(Guid projectId, Guid channelId, CancellationToken ct)
        => Map(await _members.ListRolesAsync(GetRequiredUserId(), channelId, ct));

    /// <summary>
    /// Creates a custom channel role after model validation and returns the created role payload.
    /// </summary>
    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(
        Guid projectId, Guid channelId, [FromBody] ChannelRoleDefinitionUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Map(await _members.CreateRoleAsync(GetRequiredUserId(), channelId, dto, ct));
    }

    /// <summary>
    /// Updates a custom channel role after model validation and maps missing or unauthorized roles from the service.
    /// </summary>
    [HttpPut("roles/{roleId:guid}")]
    public async Task<IActionResult> UpdateRole(
        Guid projectId, Guid channelId, Guid roleId,
        [FromBody] ChannelRoleDefinitionUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Map(await _members.UpdateRoleAsync(GetRequiredUserId(), channelId, roleId, dto, ct));
    }

    /// <summary>
    /// Deletes a custom channel role and returns no content when the membership service accepts the request.
    /// </summary>
    [HttpDelete("roles/{roleId:guid}")]
    public async Task<IActionResult> DeleteRole(Guid projectId, Guid channelId, Guid roleId, CancellationToken ct)
        => Map(await _members.DeleteRoleAsync(GetRequiredUserId(), channelId, roleId, ct));

    /// <summary>
    /// Invites a project member to the channel or reactivates a previous participant after request validation.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Invite(
        Guid projectId, Guid channelId, [FromBody] ChannelInviteDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Map(await _members.InviteAsync(GetRequiredUserId(), channelId, dto, ct));
    }

    /// <summary>
    /// Updates a channel member's role and permission overrides after model validation.
    /// </summary>
    [HttpPatch("{targetUserId:guid}")]
    public async Task<IActionResult> Update(
        Guid projectId, Guid channelId, Guid targetUserId,
        [FromBody] ChannelMemberPatchDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Map(await _members.UpdateAsync(GetRequiredUserId(), channelId, targetUserId, dto, ct));
    }

    /// <summary>
    /// Soft-kicks a member from the channel; the service allows future re-entry through the join endpoint when appropriate.
    /// </summary>
    [HttpDelete("{targetUserId:guid}")]
    public async Task<IActionResult> Kick(
        Guid projectId, Guid channelId, Guid targetUserId, CancellationToken ct)
        => Map(await _members.KickAsync(GetRequiredUserId(), channelId, targetUserId, ct));

    /// <summary>
    /// Bans a channel member after model validation, blocking re-entry until the ban is lifted.
    /// </summary>
    [HttpPost("{targetUserId:guid}/ban")]
    public async Task<IActionResult> Ban(
        Guid projectId, Guid channelId, Guid targetUserId,
        [FromBody] ChannelBanDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Map(await _members.BanAsync(GetRequiredUserId(), channelId, targetUserId, dto, ct));
    }

    /// <summary>
    /// Removes a user's channel ban and returns no content when the membership service accepts the request.
    /// </summary>
    [HttpPost("{targetUserId:guid}/unban")]
    public async Task<IActionResult> Unban(
        Guid projectId, Guid channelId, Guid targetUserId, CancellationToken ct)
        => Map(await _members.UnbanAsync(GetRequiredUserId(), channelId, targetUserId, ct));
}
