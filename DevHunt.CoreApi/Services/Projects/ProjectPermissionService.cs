using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Boolean capability flags describing what a user may do on a single project.
/// Produced by <see cref="IProjectPermissionService"/>.
/// </summary>
public sealed record ProjectPermissions
{
    /// <summary>May open project detail when not public/draft or when owner/team/admin.</summary>
    public bool CanView { get; init; }
    /// <summary>May edit project metadata (owner, team leader, or admin).</summary>
    public bool CanEdit { get; init; }
    /// <summary>May delete the project (owner or admin).</summary>
    public bool CanDelete { get; init; }
    /// <summary>May invite/remove members and change roles (owner, leader, or admin).</summary>
    public bool CanManageTeam { get; init; }
    /// <summary>May read project tasks (owner, any active member, or admin).</summary>
    public bool CanViewTasks { get; init; }
    /// <summary>May publish news posts (owner, leader, delegated flag, or admin).</summary>
    public bool CanPublishNews { get; init; }
    /// <summary>May create/edit tasks when delegated on the member row or via leader/owner/admin.</summary>
    public bool CanManageTasks { get; init; }
    /// <summary>May upload or delete project files when delegated or via leader/owner/admin.</summary>
    public bool CanManageFiles { get; init; }
    /// <summary>May manage showcase gallery assets when delegated or via leader/owner/admin.</summary>
    public bool CanManageGallery { get; init; }
    /// <summary>Derived role label: <c>owner</c>, <c>leader</c>, <c>member</c>, or null for outsiders.</summary>
    public string? Role { get; init; }
}

/// <summary>
/// Pre-loaded inputs for <see cref="IProjectPermissionService.BuildPermissions"/> to avoid extra queries.
/// </summary>
public sealed record PermissionContext
{
    /// <summary>User whose permissions are evaluated.</summary>
    public required Guid UserId { get; init; }
    /// <summary>Project entity including owner and visibility/status fields.</summary>
    public required Project Project { get; init; }
    /// <summary>Active team membership row, or null when the user is not on the team.</summary>
    public required TeamMember? TeamMember { get; init; }
    /// <summary>Platform admin/curator flag that grants elevated access.</summary>
    public required bool IsAdmin { get; init; }

    /// <summary>True when <see cref="UserId"/> matches <see cref="Project.OwnerId"/>.</summary>
    public bool IsOwner => Project.OwnerId == UserId;
    /// <summary>True when an active <see cref="TeamMember"/> row exists.</summary>
    public bool IsTeamMember => TeamMember != null;
    /// <summary>True when the member row has <c>IsLeader</c> set.</summary>
    public bool IsTeamLeader => TeamMember?.IsLeader == true;
}

/// <summary>
/// Evaluates project-scoped authorization flags from owner, team role, and delegated member permissions.
/// </summary>
public interface IProjectPermissionService
{
    /// <summary>
    /// Loads project and team membership then builds a <see cref="ProjectPermissions"/> snapshot.
    /// </summary>
    Task<ProjectPermissions?> GetPermissionsAsync(Guid projectId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Computes permissions from a pre-loaded <see cref="PermissionContext"/> without hitting the database.
    /// </summary>
    ProjectPermissions BuildPermissions(PermissionContext context);
}

/// <summary>
/// Default <see cref="IProjectPermissionService"/> using static evaluator methods per capability flag.
/// </summary>
public sealed class ProjectPermissionService : IProjectPermissionService
{
    private readonly DevHuntDbContext _dbContext;
    private readonly IProjectAuthorizationPolicy _projectAuth;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectPermissionService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for loading projects and team membership.</param>
    /// <param name="projectAuth">Shared project membership policy.</param>
    public ProjectPermissionService(DevHuntDbContext dbContext, IProjectAuthorizationPolicy projectAuth)
    {
        _dbContext = dbContext;
        _projectAuth = projectAuth;
    }

    /// <inheritdoc />
    public async Task<ProjectPermissions?> GetPermissionsAsync(Guid projectId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return null;

        var teamMember = await _projectAuth.GetActiveTeamMemberAsync(projectId, userId, ct);

        var context = new PermissionContext
        {
            UserId = userId,
            Project = project,
            TeamMember = teamMember,
            IsAdmin = isAdmin
        };

        return BuildPermissions(context);
    }

    /// <inheritdoc />
    public ProjectPermissions BuildPermissions(PermissionContext ctx)
    {
        return new ProjectPermissions
        {
            CanView = EvaluateCanView(ctx),
            CanEdit = EvaluateCanEdit(ctx),
            CanDelete = EvaluateCanDelete(ctx),
            CanManageTeam = EvaluateCanManageTeam(ctx),
            CanViewTasks = EvaluateCanViewTasks(ctx),
            CanPublishNews = EvaluateCanPublishNews(ctx),
            CanManageTasks = EvaluateCanManageTasks(ctx),
            CanManageFiles = EvaluateCanManageFiles(ctx),
            CanManageGallery = EvaluateCanManageGallery(ctx),
            Role = DetermineRole(ctx)
        };
    }

    #region Permission Evaluators (single responsibility, easy to test)

    /// <summary>True for owner/team, admins, or anonymous viewers of public non-draft projects.</summary>
    private bool EvaluateCanView(PermissionContext ctx) =>
        ctx.IsOwner || ctx.IsTeamMember || ctx.IsAdmin || _projectAuth.IsPublicNonDraft(ctx.Project);

    /// <summary>True for project owner, active team leader, or platform admin.</summary>
    private static bool EvaluateCanEdit(PermissionContext ctx) =>
        ctx.IsOwner || ctx.IsTeamLeader || ctx.IsAdmin;

    /// <summary>True for project owner or platform admin.</summary>
    private static bool EvaluateCanDelete(PermissionContext ctx) =>
        ctx.IsOwner || ctx.IsAdmin;

    /// <summary>True for owner, team leader, or admin.</summary>
    private static bool EvaluateCanManageTeam(PermissionContext ctx) =>
        ctx.IsOwner || ctx.IsTeamLeader || ctx.IsAdmin;

    /// <summary>True for owner, any active member, or admin.</summary>
    private static bool EvaluateCanViewTasks(PermissionContext ctx) =>
        ctx.IsOwner || ctx.IsTeamMember || ctx.IsAdmin;

    /// <summary>True when owner/leader/admin or member has <see cref="TeamMember.CanPublishNews"/>.</summary>
    private static bool EvaluateCanPublishNews(PermissionContext ctx) =>
        ctx.IsOwner || ctx.IsTeamLeader || ctx.TeamMember?.CanPublishNews == true || ctx.IsAdmin;

    /// <summary>True when owner/leader/admin or member has <see cref="TeamMember.CanManageTasks"/>.</summary>
    private static bool EvaluateCanManageTasks(PermissionContext ctx) =>
        ctx.IsOwner || ctx.IsTeamLeader || ctx.TeamMember?.CanManageTasks == true || ctx.IsAdmin;

    /// <summary>True when owner/leader/admin or member has <see cref="TeamMember.CanManageFiles"/>.</summary>
    private static bool EvaluateCanManageFiles(PermissionContext ctx) =>
        ctx.IsOwner || ctx.IsTeamLeader || ctx.TeamMember?.CanManageFiles == true || ctx.IsAdmin;

    /// <summary>True when owner/leader/admin or member has <see cref="TeamMember.CanManageGallery"/>.</summary>
    private static bool EvaluateCanManageGallery(PermissionContext ctx) =>
        ctx.IsOwner || ctx.IsTeamLeader || ctx.TeamMember?.CanManageGallery == true || ctx.IsAdmin;

    /// <summary>Maps context to <c>owner</c>, <c>leader</c>, <c>member</c>, or null.</summary>
    private static string? DetermineRole(PermissionContext ctx)
    {
        if (ctx.IsOwner) return "owner";
        if (ctx.IsTeamLeader) return "leader";
        if (ctx.IsTeamMember) return "member";
        return null;
    }

    #endregion
}