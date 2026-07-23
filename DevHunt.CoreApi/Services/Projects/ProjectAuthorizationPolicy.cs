using DevHunt.CoreApi.Models;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Default <see cref="IProjectAuthorizationPolicy"/> using status value objects and single-query membership lookups.
/// </summary>
public sealed class ProjectAuthorizationPolicy : IProjectAuthorizationPolicy
{
    private readonly DevHuntDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectAuthorizationPolicy"/> class.
    /// </summary>
    /// <param name="db">Database context for team membership queries.</param>
    public ProjectAuthorizationPolicy(DevHuntDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<TeamMember?> GetActiveTeamMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default) =>
        _db.TeamMembers.AsNoTracking()
            .FirstOrDefaultAsync(
                tm => tm.ProjectId == projectId
                      && tm.UserId == userId
                      && tm.Status == TeamMemberStatus.Active.Value,
                ct);

    /// <inheritdoc />
    public async Task<bool> IsOwnerOrActiveMemberAsync(Project project, Guid userId, CancellationToken ct = default)
    {
        if (project.OwnerId == userId)
            return true;

        return await GetActiveTeamMemberAsync(project.Id, userId, ct) != null;
    }

    /// <inheritdoc />
    public bool IsPublicNonDraft(Project project) =>
        project.Visibility == ProjectVisibility.Public.Value
        && project.Status != ProjectStatus.Draft.Value;
}
