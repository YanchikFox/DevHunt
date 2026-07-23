using System;
using System.Collections.Generic;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>Self-join request body; route project id must match when <see cref="ProjectId"/> is set.</summary>
/// <param name="ProjectId">Optional body project id for route/body consistency checks.</param>
/// <param name="Role">Desired team role; defaults from project requirements when omitted.</param>
public record JoinRequest(Guid ProjectId, string? Role);

/// <summary>Owner-initiated removal of another member from the project.</summary>
/// <param name="ProjectId">Project id echoed for route validation.</param>
/// <param name="UserId">Member user id to remove.</param>
public record KickRequest(Guid ProjectId, Guid UserId);

/// <summary>Owner request to rename a member's team role.</summary>
/// <param name="ProjectId">Project id echoed for route validation.</param>
/// <param name="UserId">Target member user id.</param>
/// <param name="Role">New role label.</param>
public record ChangeRoleRequest(Guid ProjectId, Guid UserId, string Role);

/// <summary>Owner request to move the leader flag to another active member.</summary>
/// <param name="ProjectId">Project id echoed for route validation.</param>
/// <param name="NewLeaderId">User id of the member who will become leader.</param>
public record TransferLeadershipRequest(Guid ProjectId, Guid NewLeaderId);

/// <summary>Open role slot with filled vs needed counts for the join/recruitment UI.</summary>
/// <param name="Role">Role name being recruited.</param>
/// <param name="TotalNeeded">Headcount required for this role.</param>
/// <param name="CurrentFilled">Active members currently holding the role.</param>
/// <param name="HoursPerWeek">Optional expected weekly hours from structured open roles.</param>
/// <param name="EquityOptional">Whether equity is optional for this vacancy.</param>
public record VacancyDto(string Role, int TotalNeeded, int CurrentFilled, int? HoursPerWeek, bool EquityOptional);

/// <summary>Team member row exposed on public project pages.</summary>
/// <param name="Id">Team membership row id.</param>
/// <param name="UserId">Linked user account id.</param>
/// <param name="Name">Display name from the user profile.</param>
/// <param name="Role">Team role label.</param>
/// <param name="IsLeader">Whether this member is the designated project leader.</param>
/// <param name="Avatar">Avatar URL when available.</param>
/// <param name="Status">Membership status string (e.g. active, left).</param>
/// <param name="CanPublishNews">Delegated news permission (zeroed when permissions are hidden).</param>
/// <param name="CanManageTasks">Delegated task permission.</param>
/// <param name="CanManageFiles">Delegated file permission.</param>
/// <param name="CanManageGallery">Delegated gallery permission.</param>
public record PublicTeamMemberDto(
    Guid Id,
    Guid UserId,
    string? Name,
    string Role,
    bool IsLeader,
    string? Avatar,
    string Status,
    bool CanPublishNews,
    bool CanManageTasks,
    bool CanManageFiles,
    bool CanManageGallery);

/// <summary>Partial update of delegated capabilities on a team member row.</summary>
/// <param name="CanPublishNews">Set news permission when provided.</param>
/// <param name="CanManageTasks">Set task permission when provided.</param>
/// <param name="CanManageFiles">Set file permission when provided.</param>
/// <param name="CanManageGallery">Set gallery permission when provided.</param>
public record UpdatePermissionsRequest(bool? CanPublishNews, bool? CanManageTasks, bool? CanManageFiles, bool? CanManageGallery);

/// <summary>Caller identity passed into team service operations.</summary>
/// <param name="UserId">Authenticated user performing the action.</param>
/// <param name="IsAdmin">Platform admin flag for elevated team operations.</param>
public record UserRequestContext(Guid UserId, bool IsAdmin);

/// <summary>
/// Typed service outcome for team operations with HTTP-style status codes.
/// </summary>
public class TeamResult<T>
{
    /// <summary>True when <see cref="Data"/> is populated.</summary>
    public bool IsSuccess { get; set; }
    /// <summary>Payload on success.</summary>
    public T? Data { get; set; }
    /// <summary>Failure message when <see cref="IsSuccess"/> is false.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Suggested HTTP status for API mapping.</summary>
    public int StatusCode { get; set; }

    /// <summary>Constructs a 200 success result.</summary>
    public static TeamResult<T> Success(T data) => new() { IsSuccess = true, Data = data, StatusCode = 200 };
    /// <summary>Constructs a failed result with message and status code.</summary>
    public static TeamResult<T> Failure(string message, int statusCode) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}

/// <summary>Non-generic <see cref="TeamResult{T}"/> for empty success bodies.</summary>
public class TeamResult
{
    /// <summary>True when the operation completed without error.</summary>
    public bool IsSuccess { get; set; }
    /// <summary>Failure message when <see cref="IsSuccess"/> is false.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Suggested HTTP status for API mapping.</summary>
    public int StatusCode { get; set; }

    /// <summary>Constructs a 200 success result.</summary>
    public static TeamResult Success() => new() { IsSuccess = true, StatusCode = 200 };
    /// <summary>Constructs a failed result with message and status code.</summary>
    public static TeamResult Failure(string message, int statusCode) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}
