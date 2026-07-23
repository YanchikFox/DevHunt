using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Tasks;

/// <summary>
/// Service for task authorization and access control.
/// Extracts complex authorization logic from TasksController (R12 refactoring).
/// </summary>
public interface ITaskAuthorizationService
{
    /// <summary>Check if user can view tasks in a project.</summary>
    Task<TaskAccessResult> CanViewTasksAsync(Guid projectId, Guid userId, CancellationToken ct = default);

    /// <summary>Check if user can create tasks in a project.</summary>
    Task<TaskAccessResult> CanCreateTaskAsync(Guid projectId, Guid userId, CancellationToken ct = default);

    /// <summary>Check if user can edit a specific task.</summary>
    Task<TaskAccessResult> CanEditTaskAsync(TaskItem task, Guid userId, CancellationToken ct = default);

    /// <summary>Check if user can delete a specific task.</summary>
    Task<TaskAccessResult> CanDeleteTaskAsync(TaskItem task, Guid userId, CancellationToken ct = default);

    /// <summary>Check if user can restore a deleted task.</summary>
    Task<TaskAccessResult> CanRestoreTaskAsync(TaskItem task, Guid userId, CancellationToken ct = default);

    /// <summary>Validate that assignee is project owner or active team member.</summary>
    Task<bool> IsValidAssigneeAsync(Guid projectId, Guid assigneeId, CancellationToken ct = default);
}

/// <summary>Result of task access check.</summary>
public record TaskAccessResult(bool Allowed, string? Reason = null)
{
    /// <inheritdoc />
    /// <summary>Successful task access check.</summary>
    public static TaskAccessResult Allow() => new(true);
    /// <inheritdoc />
    /// <summary>Denied task access check with a reason.</summary>
    public static TaskAccessResult Deny(string reason) => new(false, reason);
    /// <inheritdoc />
    /// <summary>Denied result used when the project does not exist.</summary>
    public static TaskAccessResult ProjectNotFound() => Deny("Project not found");
}

/// <summary>
/// Implementation of task authorization service.
/// Consolidates access control logic to reduce cyclomatic complexity in controller.
/// </summary>
public class TaskAuthorizationService : ITaskAuthorizationService
{
    private readonly DevHuntDbContext _db;
    private readonly IProjectAuthorizationPolicy _projectAuth;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskAuthorizationService"/> class.
    /// </summary>
    /// <param name="db">Database context for project lookups.</param>
    /// <param name="projectAuth">Shared project membership policy.</param>
    public TaskAuthorizationService(DevHuntDbContext db, IProjectAuthorizationPolicy projectAuth)
    {
        _db = db;
        _projectAuth = projectAuth;
    }

    /// <summary>Allows project owners and active team members to read tasks.</summary>
    public Task<TaskAccessResult> CanViewTasksAsync(Guid projectId, Guid userId, CancellationToken ct = default) =>
        CheckProjectMemberAccessAsync(projectId, userId, "Only project owner and team members can view tasks", ct);

    /// <summary>Allows project owners and active team members to create tasks.</summary>
    public Task<TaskAccessResult> CanCreateTaskAsync(Guid projectId, Guid userId, CancellationToken ct = default) =>
        CheckProjectMemberAccessAsync(projectId, userId, "Only project owner or team members can create tasks", ct);

    /// <summary>Allows owners, creators/assignees, or leaders with task-management permission to edit.</summary>
    public async Task<TaskAccessResult> CanEditTaskAsync(TaskItem task, Guid userId, CancellationToken ct = default)
    {
        var context = await LoadTaskContextAsync(task.ProjectId, userId, ct);
        if (context.Project == null) return TaskAccessResult.ProjectNotFound();

        if (IsOwner(context, userId)) return TaskAccessResult.Allow();
        if (IsTaskCreatorOrAssignee(task, userId)) return TaskAccessResult.Allow();
        if (HasTaskManagementPermission(context.Member)) return TaskAccessResult.Allow();
        if (context.Member != null) return TaskAccessResult.Deny("Only task creator, assignee, or team leader can edit this task");

        return TaskAccessResult.Deny("Only project owner or team members can update tasks");
    }

    /// <summary>Allows owners, task creators, or leaders with task-management permission to delete.</summary>
    public async Task<TaskAccessResult> CanDeleteTaskAsync(TaskItem task, Guid userId, CancellationToken ct = default)
    {
        var context = await LoadTaskContextAsync(task.ProjectId, userId, ct);
        if (context.Project == null) return TaskAccessResult.ProjectNotFound();

        if (IsOwner(context, userId)) return TaskAccessResult.Allow();
        if (task.CreatedByUserId == userId) return TaskAccessResult.Allow();
        if (HasTaskManagementPermission(context.Member)) return TaskAccessResult.Allow();

        return TaskAccessResult.Deny("Only project owner, task creator, or team leader can delete tasks");
    }

    /// <summary>Uses the same rules as delete for restoring soft-deleted tasks.</summary>
    public async Task<TaskAccessResult> CanRestoreTaskAsync(TaskItem task, Guid userId, CancellationToken ct = default)
    {
        var context = await LoadTaskContextAsync(task.ProjectId, userId, ct);
        if (context.Project == null) return TaskAccessResult.ProjectNotFound();

        if (IsOwner(context, userId)) return TaskAccessResult.Allow();
        if (task.CreatedByUserId == userId) return TaskAccessResult.Allow();
        if (HasTaskManagementPermission(context.Member)) return TaskAccessResult.Allow();

        return TaskAccessResult.Deny("Only project owner, task creator, or team leader can restore tasks");
    }

    /// <summary>Returns whether the assignee is the owner or an active team member.</summary>
    public async Task<bool> IsValidAssigneeAsync(Guid projectId, Guid assigneeId, CancellationToken ct = default)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return false;
        return await _projectAuth.IsOwnerOrActiveMemberAsync(project, assigneeId, ct);
    }

    #region Private Helpers

    private record TaskContext(Project? Project, TeamMember? Member);

    private async Task<TaskContext> LoadTaskContextAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null)
            return new TaskContext(null, null);

        if (project.OwnerId == userId)
            return new TaskContext(project, null);

        var member = await _projectAuth.GetActiveTeamMemberAsync(projectId, userId, ct);
        return new TaskContext(project, member);
    }

    private async Task<TaskAccessResult> CheckProjectMemberAccessAsync(
        Guid projectId,
        Guid userId,
        string denyMessage,
        CancellationToken ct = default)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return TaskAccessResult.ProjectNotFound();
        if (await _projectAuth.IsOwnerOrActiveMemberAsync(project, userId, ct)) return TaskAccessResult.Allow();
        return TaskAccessResult.Deny(denyMessage);
    }

    private static bool IsOwner(TaskContext context, Guid userId) => context.Project?.OwnerId == userId;

    private static bool IsTaskCreatorOrAssignee(TaskItem task, Guid userId) =>
        task.CreatedByUserId == userId || task.AssignedToUserId == userId;

    private static bool HasTaskManagementPermission(TeamMember? member) =>
        member != null && (member.IsLeader || member.CanManageTasks);

    #endregion
}
