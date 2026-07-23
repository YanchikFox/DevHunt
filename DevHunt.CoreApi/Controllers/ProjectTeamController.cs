using System;
using System.Threading.Tasks;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Canonical project team management controller.
/// Routes: /api/projects/{projectId}/team/* (see API_CLEANUP_REPORT.md § Teams/Members/Roles).
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/team")]
[Authorize]
public class ProjectTeamController : ControllerBase
{
    private readonly IProjectTeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectTeamController"/> class.
    /// </summary>
    /// <param name="teamService">Service that applies team membership, role, and permission rules.</param>
    public ProjectTeamController(IProjectTeamService teamService)
    {
        _teamService = teamService;
    }

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Converts a successful team service payload to <see cref="OkObjectResult"/> or preserves the service error status.
    /// </summary>
    private IActionResult MapResult<T>(TeamResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Converts a successful no-payload team service result to <see cref="OkResult"/> or preserves the service error status.
    /// </summary>
    private IActionResult MapResult(TeamResult result)
    {
        if (result.IsSuccess)
        {
            return Ok();
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Retrieves project team members and optionally includes permissions for authorized callers.
    /// </summary>
    [HttpGet]
    [HttpGet("members")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMembers(Guid projectId, [FromQuery] bool includePermissions = false)
    {
        var callerId = SecurityHelpers.GetUserId(User);
        var callerIsAdmin = SecurityHelpers.IsAdminOrCurator(User);
        var result = await _teamService.GetMembersAsync(projectId, includePermissions, callerId, callerIsAdmin);
        return MapResult(result);
    }

    /// <summary>
    /// Updates a team member's permissions using the authenticated caller context.
    /// </summary>
    [HttpPatch("{memberId:guid}/permissions")]
    public async Task<IActionResult> UpdateMemberPermissions(Guid projectId, Guid memberId, [FromBody] UpdatePermissionsRequest req)
    {
        var result = await _teamService.UpdateMemberPermissionsAsync(
            projectId,
            memberId,
            req,
            new UserRequestContext(GetRequiredUserId(), SecurityHelpers.IsAdminOrCurator(User)));
        return MapResult(result);
    }

    /// <summary>
    /// Retrieves open team roles for the project.
    /// </summary>
    [HttpGet("roles")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRoles(Guid projectId)
    {
        var result = await _teamService.GetRolesAsync(projectId);
        return MapResult(result);
    }

    /// <summary>
    /// Allows the authenticated user to join the project team, returning bad request when the body is missing.
    /// </summary>
    [HttpPost("members")]
    public async Task<IActionResult> Join(Guid projectId, [FromBody] JoinRequest req)
    {
        if (req == null) return BadRequest("Request body is required"); // Controller validation
        var result = await _teamService.JoinAsync(projectId, req, GetRequiredUserId());
        return MapResult(result);
    }

    /// <summary>
    /// Allows the authenticated team member to leave the project.
    /// </summary>
    [HttpPost("members/leave")]
    public async Task<IActionResult> Leave(Guid projectId)
    {
        var result = await _teamService.LeaveAsync(projectId, GetRequiredUserId());
        return MapResult(result);
    }

    /// <summary>
    /// Removes a team member from the project through the team service's authorization rules.
    /// </summary>
    [HttpDelete("members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid projectId, Guid userId)
    {
        var result = await _teamService.RemoveMemberAsync(projectId, userId, GetRequiredUserId());
        return MapResult(result);
    }

    /// <summary>
    /// Changes a team member's role, returning bad request when the body is missing.
    /// </summary>
    [HttpPost("roles")]
    public async Task<IActionResult> ChangeRole(Guid projectId, [FromBody] ChangeRoleRequest req)
    {
         if (req == null) return BadRequest("Request body is required");
        var result = await _teamService.ChangeRoleAsync(projectId, req, GetRequiredUserId());
        return MapResult(result);
    }

    /// <summary>
    /// Transfers project leadership to another team member, returning bad request when the body is missing.
    /// </summary>
    [HttpPost("roles/transfer-leadership")]
    public async Task<IActionResult> TransferLeadership(Guid projectId, [FromBody] TransferLeadershipRequest req)
    {
        if (req == null) return BadRequest("Request body is required");
        var result = await _teamService.TransferLeadershipAsync(projectId, req, GetRequiredUserId());
        return MapResult(result);
    }
}
