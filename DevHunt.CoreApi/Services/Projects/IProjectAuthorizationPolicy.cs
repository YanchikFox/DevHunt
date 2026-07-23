using DevHunt.Infrastructure.Models;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Centralized project membership and catalog visibility rules (DEV-27).
/// </summary>
public interface IProjectAuthorizationPolicy
{
    /// <summary>
    /// Loads the user's active team membership for a project, or null when not an active member.
    /// </summary>
    Task<TeamMember?> GetActiveTeamMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns whether the user owns the project or has an active team membership row.
    /// </summary>
    Task<bool> IsOwnerOrActiveMemberAsync(Project project, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns whether anonymous users may discover the project (public and not draft).
    /// </summary>
    bool IsPublicNonDraft(Project project);
}
