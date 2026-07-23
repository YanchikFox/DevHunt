using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Team membership lifecycle for projects: roster reads, join/leave, role changes, leadership
/// transfer, and granular member permissions. Implemented by <see cref="ProjectTeamService"/>.
/// </summary>
public interface IProjectTeamService
{
    /// <summary>
    /// Returns active and left team members. Restricted projects require the caller to be a member
    /// unless they are a platform admin.
    /// </summary>
    /// <param name="projectId">Project whose roster is listed.</param>
    /// <param name="includePermissions">When true, exposes per-member capability flags in the DTO.</param>
    /// <param name="callerId">Authenticated user, or null for anonymous callers.</param>
    /// <param name="callerIsAdmin">Bypasses restricted-project roster gates when true.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered member DTOs, or 401/403/404 when access rules fail.</returns>
    Task<TeamResult<List<PublicTeamMemberDto>>> GetMembersAsync(Guid projectId, bool includePermissions, Guid? callerId, bool callerIsAdmin, CancellationToken ct = default);

    /// <summary>
    /// Patches publish/tasks/files/gallery flags on an active member. Requires owner, leader, or admin.
    /// </summary>
    Task<TeamResult<object>> UpdateMemberPermissionsAsync(Guid projectId, Guid memberId, UpdatePermissionsRequest req, UserRequestContext userContext, CancellationToken ct = default);

    /// <summary>
    /// Lists open role vacancies by comparing <c>OpenRoles</c> or legacy <c>RequiredRoles</c> against filled counts.
    /// </summary>
    Task<TeamResult<List<VacancyDto>>> GetRolesAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Adds the caller as an active team member and joins legacy project group chat when present.</summary>
    Task<TeamResult<object>> JoinAsync(Guid projectId, JoinRequest req, Guid currentUserId, CancellationToken ct = default);

    /// <summary>Marks the caller as left; promotes another member to leader when the sole leader leaves.</summary>
    Task<TeamResult> LeaveAsync(Guid projectId, Guid currentUserId, CancellationToken ct = default);

    /// <summary>Owner-only removal of another member with status set to removed.</summary>
    Task<TeamResult> RemoveMemberAsync(Guid projectId, Guid userId, Guid currentUserId, CancellationToken ct = default);

    /// <summary>Owner-only role rename; cannot change a leader's role without transferring leadership first.</summary>
    Task<TeamResult<object>> ChangeRoleAsync(Guid projectId, ChangeRoleRequest req, Guid currentUserId, CancellationToken ct = default);

    /// <summary>Owner-only swap of the single active leader flag to another team member.</summary>
    Task<TeamResult<object>> TransferLeadershipAsync(Guid projectId, TransferLeadershipRequest req, Guid currentUserId, CancellationToken ct = default);
}
