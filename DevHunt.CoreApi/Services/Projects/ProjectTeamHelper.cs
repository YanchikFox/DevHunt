using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.Infrastructure.Constants;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Shared validation and lookup helpers for <see cref="ProjectTeamService"/> join, role, and
/// leadership operations.
/// </summary>
public static class ProjectTeamHelper
{
    /// <summary>Returns true when <paramref name="userId"/> owns the project.</summary>
    public static bool IsOwner(Project project, Guid userId) => project.OwnerId == userId;

    /// <summary>
    /// Ensures optional body project id matches the route id; returns an error message when they differ.
    /// </summary>
    public static string? ValidateRequestIds(Guid routeId, Guid bodyId)
    {
        if (bodyId != Guid.Empty && bodyId != routeId)
            return "Body ProjectId must match route projectId";
        return null;
    }

    /// <summary>Rejects join when the project is missing or archived/completed.</summary>
    public static string? ValidateProjectStatusForJoin(Project? project)
    {
        if (project == null) return "Project not found";
        if (project.Status == ProjectStatus.Archived || project.Status == ProjectStatus.Completed)
            return "Can't join archived/completed project";
        return null;
    }

    /// <summary>Returns an error when active member count has reached <see cref="Project.MaxTeamSize"/>.</summary>
    public static async Task<string?> ValidateTeamCapacityAsync(DevHuntDbContext context, Project project)
    {
        if (project.MaxTeamSize == null) return null;

        var activeMemberCount = await context.TeamMembers.CountAsync(tm => tm.ProjectId == project.Id && tm.Status == TeamMemberStatus.Active);
        if (activeMemberCount >= project.MaxTeamSize)
            return "Max team size reached";

        return null;
    }

    /// <summary>True when the user already has an active membership row on the project.</summary>
    public static async Task<bool> IsAlreadyMemberAsync(DevHuntDbContext context, Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await context.TeamMembers.AnyAsync(tm => tm.UserId == userId && tm.ProjectId == projectId && tm.Status == TeamMemberStatus.Active, ct);
    }
    /// <summary>True for admins, project owners, or active team leaders.</summary>
    public static bool CanUpdatePermissions(Project project, Guid currentUserId, bool isAdmin)
    {
        if (isAdmin) return true;
        if (project.OwnerId == currentUserId) return true;

        var requester = project.TeamMembers.FirstOrDefault(tm => tm.UserId == currentUserId && tm.Status == TeamMemberStatus.Active);
        return requester?.IsLeader == true;
    }

    /// <summary>Applies non-null fields from <see cref="UpdatePermissionsRequest"/> onto the member row.</summary>
    public static void ApplyPermissions(TeamMember member, UpdatePermissionsRequest req)
    {
        if (req.CanPublishNews.HasValue) member.CanPublishNews = req.CanPublishNews.Value;
        if (req.CanManageTasks.HasValue) member.CanManageTasks = req.CanManageTasks.Value;
        if (req.CanManageFiles.HasValue) member.CanManageFiles = req.CanManageFiles.Value;
        if (req.CanManageGallery.HasValue) member.CanManageGallery = req.CanManageGallery.Value;
    }

    /// <summary>Loads a project by primary key.</summary>
    public static async Task<Project?> GetProjectAsync(DevHuntDbContext context, Guid projectId, CancellationToken ct = default)
    {
        return await context.Projects.FirstOrDefaultAsync(x => x.Id == projectId, ct);
    }

    /// <summary>Finds the active team member row, optionally including the linked user entity.</summary>
    public static async Task<TeamMember?> GetActiveMemberAsync(DevHuntDbContext context, Guid projectId, Guid userId, bool includeUser = false, CancellationToken ct = default)
    {
        var query = context.TeamMembers.AsQueryable();
        if (includeUser) query = query.Include(tm => tm.User);

        return await query.FirstOrDefaultAsync(tm => tm.ProjectId == projectId && tm.UserId == userId && tm.Status == TeamMemberStatus.Active, ct);
    }

    /// <summary>Returns the active member flagged as leader, if any.</summary>
    public static async Task<TeamMember?> GetCurrentLeaderAsync(DevHuntDbContext context, Guid projectId, CancellationToken ct = default)
    {
        return await context.TeamMembers.FirstOrDefaultAsync(tm => tm.ProjectId == projectId && tm.IsLeader && tm.Status == TeamMemberStatus.Active, ct);
    }

    /// <summary>Blocks role changes for the current leader until leadership is transferred.</summary>
    public static string? ValidateRoleChange(TeamMember member, string newRole)
    {
        if (member.IsLeader && !string.Equals(member.Role, newRole, StringComparison.OrdinalIgnoreCase))
            return "Can't change leader's role!";
        return null;
    }

    /// <summary>
    /// Adds the user to a legacy group conversation whose id equals the project id, when one exists.
    /// </summary>
    public static async Task JoinProjectChatAsync(DevHuntDbContext context, Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var projectChat = await context.Conversations.FirstOrDefaultAsync(c => c.Id == projectId && c.Type == ConversationType.Group, ct);
        if (projectChat != null)
        {
            var alreadyInChat = await context.ConversationParticipants.AnyAsync(cp => cp.ConversationId == projectId && cp.UserId == userId, ct);
            if (!alreadyInChat)
            {
                context.ConversationParticipants.Add(new ConversationParticipant
                {
                    Id = Guid.NewGuid(),
                    ConversationId = projectId,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>Runs join pre-checks: route/body ids, project status, capacity, and duplicate membership.</summary>
    public static async Task<(string? Error, Project? Project)> ValidateJoinRequestAsync(DevHuntDbContext context, Guid projectId, JoinRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var idError = ValidateRequestIds(projectId, req.ProjectId);
        if (idError != null) return (idError, null);

        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId, ct);

        var statusError = ValidateProjectStatusForJoin(project);
        if (statusError != null) return (statusError, null);

        var capacityError = await ValidateTeamCapacityAsync(context, project!);
        if (capacityError != null) return (capacityError, null);

        if (await IsAlreadyMemberAsync(context, projectId, currentUserId))
            return ("Already member", null);

        return (null, project);
    }

    /// <summary>Owner-only validation before changing another member's role.</summary>
    public static async Task<(TeamResult<object>? Failure, TeamMember? Member)> ValidateChangeRoleRequestAsync(DevHuntDbContext context, Guid projectId, ChangeRoleRequest req, Guid currentUserId)
    {
        var idError = ValidateRequestIds(projectId, req.ProjectId);
        if (idError != null)
            return (TeamResult<object>.Failure(idError, 400), null);

        var project = await GetProjectAsync(context, projectId);
        if (project == null) return (TeamResult<object>.Failure("Project not found", 404), null);

        if (!IsOwner(project, currentUserId))
            return (TeamResult<object>.Failure("Forbidden", 403), null);

        var member = await GetActiveMemberAsync(context, projectId, req.UserId);
        if (member == null) return (TeamResult<object>.Failure("Member not found", 404), null);

        var roleError = ValidateRoleChange(member, req.Role);
        if (roleError != null) return (TeamResult<object>.Failure(roleError, 409), null);

        return (null, member);
    }

    /// <summary>Owner-only validation before swapping the leader flag to another active member.</summary>
    public static async Task<(TeamResult<object>? Failure, TeamMember? CurrentLeader, TeamMember? NewLeader)> ValidateTransferLeadershipAsync(DevHuntDbContext context, Guid projectId, TransferLeadershipRequest req, Guid currentUserId)
    {
        var idError = ValidateRequestIds(projectId, req.ProjectId);
        if (idError != null)
            return (TeamResult<object>.Failure(idError, 400), null, null);

        var project = await GetProjectAsync(context, projectId);
        if (project == null) return (TeamResult<object>.Failure("Project not found", 404), null, null);
        if (!IsOwner(project, currentUserId)) return (TeamResult<object>.Failure("Forbidden", 403), null, null);

        var currentLeader = await GetCurrentLeaderAsync(context, projectId);
        if (currentLeader == null) return (TeamResult<object>.Failure("No current leader found", 400), null, null);

        var newLeader = await GetActiveMemberAsync(context, projectId, req.NewLeaderId, includeUser: true);

        if (newLeader == null) return (TeamResult<object>.Failure("New leader not found in team", 404), null, null);
        if (newLeader.IsLeader) return (TeamResult<object>.Failure("User is already a leader", 400), null, null);

        return (null, currentLeader, newLeader);
    }
}
