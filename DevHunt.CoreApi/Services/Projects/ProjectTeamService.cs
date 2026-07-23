using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Services.Badges;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// EF-backed implementation of <see cref="IProjectTeamService"/>. Coordinates team roster access,
/// membership mutations, notifications, domain events, and achievement triggers via
/// <see cref="ProjectTeamHelper"/>.
/// </summary>
public class ProjectTeamService : IProjectTeamService
{
    private readonly DevHuntDbContext _context;
    private readonly INotificationHelperService _notificationService;
    private readonly IEventBusService _eventBus;
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<ProjectTeamService> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectTeamService"/> class.
    /// </summary>
    /// <param name="context">Database context for projects, team members, users, and legacy project chat rows.</param>
    /// <param name="notificationService">Sends team membership and leadership notifications.</param>
    /// <param name="eventBus">Publishes team membership domain events.</param>
    /// <param name="activityLogService">Writes join and leave events to project activity.</param>
    /// <param name="logger">Logger for team service diagnostics.</param>
    /// <param name="serviceProvider">Resolves achievement checks triggered by team actions.</param>
    public ProjectTeamService(
        DevHuntDbContext context,
        INotificationHelperService notificationService,
        IEventBusService eventBus,
        IActivityLogService activityLogService,
        ILogger<ProjectTeamService> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _notificationService = notificationService;
        _eventBus = eventBus;
        _activityLogService = activityLogService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }







    /// <inheritdoc />
    public async Task<TeamResult<List<PublicTeamMemberDto>>> GetMembersAsync(Guid projectId, bool includePermissions, Guid? callerId, bool callerIsAdmin, CancellationToken ct = default)
    {
        // PE-06: Gate restricted projects so anonymous callers cannot enumerate team rosters
        var projectVisibility = await _context.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.Visibility)
            .FirstOrDefaultAsync(ct);

        if (projectVisibility == null)
            return TeamResult<List<PublicTeamMemberDto>>.Failure("Project not found", 404);

        var isRestricted = ProjectVisibility.FromString(projectVisibility)?.IsRestricted ?? false;
        if (isRestricted && !callerIsAdmin)
        {
            if (callerId == null)
                return TeamResult<List<PublicTeamMemberDto>>.Failure("Unauthorized", 401);

            var isMember = await _context.TeamMembers
                .AnyAsync(tm => tm.ProjectId == projectId && tm.UserId == callerId.Value && tm.Status == TeamMemberStatus.Active.Value, ct);

            if (!isMember)
                return TeamResult<List<PublicTeamMemberDto>>.Failure("Forbidden", 403);
        }

        var members = await _context.TeamMembers
            .AsNoTracking()
            .Where(tm => tm.ProjectId == projectId && (tm.Status == TeamMemberStatus.Active.Value || tm.Status == TeamMemberStatus.Left.Value))
            .OrderBy(tm => tm.JoinedAt)
            .Select(GetMemberProjection(includePermissions))
            .ToListAsync(ct);

        return TeamResult<List<PublicTeamMemberDto>>.Success(members);
    }

    /// <summary>EF projection that optionally masks permission flags unless the caller may view them.</summary>
    private static System.Linq.Expressions.Expression<Func<TeamMember, PublicTeamMemberDto>> GetMemberProjection(bool includePermissions)
    {
        return tm => new PublicTeamMemberDto(
            tm.Id,
            tm.UserId,
            tm.User != null ? tm.User.FullName : null,
            tm.Role,
            tm.IsLeader,
            tm.User != null ? tm.User.AvatarUrl : null,
            tm.Status,
            includePermissions && tm.CanPublishNews,
            includePermissions && tm.CanManageTasks,
            includePermissions && tm.CanManageFiles,
            includePermissions && tm.CanManageGallery);
    }

    /// <inheritdoc />
    public async Task<TeamResult<object>> UpdateMemberPermissionsAsync(Guid projectId, Guid memberId, UpdatePermissionsRequest req, UserRequestContext userContext, CancellationToken ct = default)
    {
        var project = await _context.Projects.Include(p => p.TeamMembers).FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return TeamResult<object>.Failure("Project not found", 404);

        if (!ProjectTeamHelper.CanUpdatePermissions(project, userContext.UserId, userContext.IsAdmin))
            return TeamResult<object>.Failure("Forbidden", 403);

        var member = project.TeamMembers.FirstOrDefault(tm => tm.Id == memberId && tm.Status == TeamMemberStatus.Active.Value);
        if (member == null) return TeamResult<object>.Failure("Member not found", 404);

        ProjectTeamHelper.ApplyPermissions(member, req);

        await _context.SaveChangesAsync(ct);

        return TeamResult<object>.Success(new
        {
            member.Id,
            member.UserId,
            member.CanPublishNews,
            member.CanManageTasks,
            member.CanManageFiles,
            member.CanManageGallery
        });
    }

    /// <inheritdoc />
    public async Task<TeamResult<List<VacancyDto>>> GetRolesAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId, ct);
        if (project == null) return TeamResult<List<VacancyDto>>.Failure("Project not found", 404);

        var current = await _context.TeamMembers
            .Where(tm => tm.ProjectId == projectId && tm.Status == TeamMemberStatus.Active.Value)
            .GroupBy(tm => tm.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        List<VacancyDto> list;

        if (project.OpenRoles is { Count: > 0 })
        {
            // Use structured OpenRoles (new format)
            list = project.OpenRoles
                .Select(r => new VacancyDto(
                    r.Role,
                    r.TotalNeeded,
                    current.FirstOrDefault(c => string.Equals(c.Role, r.Role, StringComparison.OrdinalIgnoreCase))?.Count ?? 0,
                    r.HoursPerWeek,
                    r.EquityOptional))
                .Where(x => x.CurrentFilled < x.TotalNeeded)
                .ToList();
        }
        else
        {
            // Fallback: use legacy RequiredRoles string array
            var roles = project.RequiredRoles ?? new List<string>();

            // PE-13: Pre-compute role counts with a dictionary to avoid O(n²)
            var roleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var role in roles)
            {
                roleCounts.TryGetValue(role, out var count);
                roleCounts[role] = count + 1;
            }

            list = roleCounts
                .Select(kv => new VacancyDto(
                    kv.Key,
                    kv.Value,
                    current.FirstOrDefault(c => string.Equals(c.Role, kv.Key, StringComparison.OrdinalIgnoreCase))?.Count ?? 0,
                    null,
                    false))
                .Where(x => x.CurrentFilled < x.TotalNeeded)
                .ToList();
        }

        return TeamResult<List<VacancyDto>>.Success(list);
    }

    /// <inheritdoc />
    public async Task<TeamResult<object>> JoinAsync(Guid projectId, JoinRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var (error, project) = await ProjectTeamHelper.ValidateJoinRequestAsync(_context, projectId, req, currentUserId);
        if (error != null) return TeamResult<object>.Failure(error, error == "Project not found" ? 404 : 400);

        // Project is validated and returned, no need to fetch again.
        // The project variable is not null here since error is null.

        var member = new TeamMember
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            ProjectId = projectId,
            Role = req.Role ?? (project.RequiredRoles?.FirstOrDefault() ?? "member"),
            Status = TeamMemberStatus.Active.Value,
            IsLeader = false,
            Contribution = null,
            JoinedAt = DateTime.UtcNow
        };

        // PE-09: Wrap member creation + chat join in a single transaction
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        _context.TeamMembers.Add(member);
        await _context.SaveChangesAsync(ct);
        await ProjectTeamHelper.JoinProjectChatAsync(_context, projectId, currentUserId);
        await tx.CommitAsync(ct);

        await _eventBus.PublishAsync(DomainEvents.TeamMemberJoined(projectId, currentUserId, member.Role));

        await _notificationService.SendNotificationAsync(
             project.OwnerId, "team", "New team member", "A new member has joined the project.", "Project", project.Id, "low");

        await _activityLogService.LogProjectEventAsync(
            projectId, currentUserId, "user.joined_project", $"Joined project {project.Title}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { member.Role });

        await _serviceProvider.TriggerAchievementCheckAsync(currentUserId, AchievementTrigger.TeamJoined);

        return TeamResult<object>.Success(new { MemberId = member.Id });
    }

    /// <inheritdoc />
    public async Task<TeamResult> LeaveAsync(Guid projectId, Guid currentUserId, CancellationToken ct = default)
    {
        // PE-04: RepeatableRead prevents two leaders from leaving simultaneously
        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, ct);

        var member = await _context.TeamMembers.FirstOrDefaultAsync(
            tm => tm.UserId == currentUserId && tm.ProjectId == projectId && tm.Status == TeamMemberStatus.Active.Value, ct);
        if (member == null) return TeamResult.Failure("Member not found", 404);

        var project = await _context.Projects.FirstAsync(x => x.Id == projectId, ct);

        if (member.IsLeader)
        {
            var otherLeaders = await _context.TeamMembers
                .Where(tm => tm.ProjectId == projectId && tm.UserId != currentUserId
                    && tm.Status == TeamMemberStatus.Active.Value && tm.IsLeader)
                .ToListAsync(ct);

            if (otherLeaders.Count == 0)
            {
                // No other leaders — promote the first active member
                var firstActive = await _context.TeamMembers
                    .Where(tm => tm.ProjectId == projectId && tm.UserId != currentUserId
                        && tm.Status == TeamMemberStatus.Active.Value)
                    .OrderBy(tm => tm.JoinedAt)
                    .FirstOrDefaultAsync(ct);

                if (firstActive == null) return TeamResult.Failure("Can't leave as sole member", 409);
                firstActive.IsLeader = true;
            }
        }

        member.Status = TeamMemberStatus.Left.Value;
        member.LeftAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _eventBus.PublishAsync(DomainEvents.TeamMemberLeft(projectId, currentUserId));

        await _notificationService.SendNotificationAsync(
             project.OwnerId, "team", "Team member left", "A team member has left the project.", "Project", project.Id, "low");

        await _activityLogService.LogProjectEventAsync(
            projectId, currentUserId, "user.left_project", $"Left project {project.Title}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility));

        return TeamResult.Success();
    }

    /// <inheritdoc />
    public async Task<TeamResult> RemoveMemberAsync(Guid projectId, Guid userId, Guid currentUserId, CancellationToken ct = default)
    {
        var project = await _context.Projects.Include(p => p.TeamMembers).FirstOrDefaultAsync(x => x.Id == projectId, ct);
        if (project == null) return TeamResult.Failure("Project not found", 404);
        if (!ProjectTeamHelper.IsOwner(project, currentUserId)) return TeamResult.Failure("Forbidden", 403);

        var member = await _context.TeamMembers.FirstOrDefaultAsync(
            tm => tm.UserId == userId && tm.ProjectId == projectId && tm.Status == TeamMemberStatus.Active.Value, ct);
        if (member == null) return TeamResult.Failure("Member not found", 404);

        // PE-10: Owner can remove a leader (the old blanket block caused deadlocks)
        // Owner cannot remove themselves this way — they should use TransferOwnership
        if (member.UserId == currentUserId) return TeamResult.Failure("Use leave endpoint to leave the project", 400);

        member.Status = TeamMemberStatus.Removed.Value;
        member.LeftAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(DomainEvents.TeamMemberRemoved(projectId, userId, member.Role));

        await _notificationService.SendNotificationAsync(
             userId, "team", "Removed from project", "The project owner has removed you from the team.", "Project", project.Id, "medium");

        return TeamResult.Success();
    }

    /// <inheritdoc />
    public async Task<TeamResult<object>> ChangeRoleAsync(Guid projectId, ChangeRoleRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var (validationFailure, member) = await ProjectTeamHelper.ValidateChangeRoleRequestAsync(_context, projectId, req, currentUserId);
        if (validationFailure != null) return validationFailure;

        // Member is guaranteed to be not null if validationFailure is null
        var targetMember = member!;

        var oldRole = targetMember.Role;
        targetMember.Role = req.Role;
        await _context.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(DomainEvents.TeamMemberRoleUpdated(projectId, req.UserId, oldRole, req.Role));

        await _notificationService.SendNotificationAsync(req.UserId, "team", "Role changed", $"Your role has been changed to: {req.Role}", "Project", projectId, "low");

        return TeamResult<object>.Success(new { targetMember.UserId, targetMember.Role });
    }



    /// <inheritdoc />
    public async Task<TeamResult<object>> TransferLeadershipAsync(Guid projectId, TransferLeadershipRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var (failure, currentLeader, newLeader) = await ProjectTeamHelper.ValidateTransferLeadershipAsync(_context, projectId, req, currentUserId);
        if (failure != null) return failure;

        var cLeader = currentLeader!;
        var nLeader = newLeader!;

        cLeader.IsLeader = false;
        nLeader.IsLeader = true;

        await _context.SaveChangesAsync(ct);

        await _notificationService.SendNotificationAsync(cLeader.UserId, "team", "Leadership transferred", $"You have transferred leadership to {nLeader.User?.FullName ?? nLeader.User?.Email}.", "Project", projectId, "medium");
        await _notificationService.SendNotificationAsync(req.NewLeaderId, "team", "You are now a project leader", "Leadership has been transferred to you.", "Project", projectId, "high");

        return TeamResult<object>.Success(new { Message = "Leadership transferred", req.NewLeaderId });
    }
}
