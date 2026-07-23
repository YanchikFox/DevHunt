using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Integrations;

/// <summary>
/// Result of integration access check.
/// </summary>
public record IntegrationAccessResult(bool Allowed, string? Reason = null)
{
    /// <summary>Successful access check result.</summary>
    public static IntegrationAccessResult Allow() => new(true);
    /// <summary>Denied access check result with a reason message.</summary>
    public static IntegrationAccessResult Deny(string reason) => new(false, reason);
    /// <summary>Denied result used when the project does not exist.</summary>
    public static IntegrationAccessResult ProjectNotFound() => Deny("Project not found");
    /// <summary>Denied result used when the integration does not exist.</summary>
    public static IntegrationAccessResult IntegrationNotFound() => Deny("Integration not found");
}

/// <summary>
/// Service for integration authorization and access control.
/// Extracts duplicated authorization logic from IntegrationsController.
/// </summary>
public interface IIntegrationAuthorizationService
{
    /// <summary>Check if user can view integrations in a project (owner or team member).</summary>
    Task<IntegrationAccessResult> CanViewIntegrationsAsync(Guid projectId, Guid userId, CancellationToken ct = default);

    /// <summary>Check if user can manage integrations (owner only).</summary>
    Task<IntegrationAccessResult> CanManageIntegrationsAsync(Guid projectId, Guid userId, CancellationToken ct = default);

    /// <summary>Check if user can sync an integration (owner or team member).</summary>
    Task<IntegrationAccessResult> CanSyncIntegrationAsync(Integration integration, Guid userId, CancellationToken ct = default);

    /// <summary>Load integration with project for authorization checks.</summary>
    Task<Integration?> LoadIntegrationWithProjectAsync(Guid integrationId, CancellationToken ct = default);
}

/// <summary>
/// Implementation of integration authorization service.
/// Consolidates access control logic to reduce cyclomatic complexity in controller.
/// </summary>
public class IntegrationAuthorizationService : IIntegrationAuthorizationService
{
    private readonly DevHuntDbContext _db;
    private readonly IProjectAuthorizationPolicy _projectAuth;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationAuthorizationService"/> class.
    /// </summary>
    /// <param name="db">Database context for project and team membership lookups.</param>
    /// <param name="projectAuth">Shared project membership policy.</param>
    public IntegrationAuthorizationService(DevHuntDbContext db, IProjectAuthorizationPolicy projectAuth)
    {
        _db = db;
        _projectAuth = projectAuth;
    }

    /// <inheritdoc />
    public async Task<IntegrationAccessResult> CanViewIntegrationsAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return IntegrationAccessResult.ProjectNotFound();
        if (await _projectAuth.IsOwnerOrActiveMemberAsync(project, userId, ct)) return IntegrationAccessResult.Allow();
        return IntegrationAccessResult.Deny("Only project owner or team members can view integrations");
    }

    /// <inheritdoc />
    public async Task<IntegrationAccessResult> CanManageIntegrationsAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return IntegrationAccessResult.ProjectNotFound();
        if (project.OwnerId == userId) return IntegrationAccessResult.Allow();
        return IntegrationAccessResult.Deny("Only project owner can manage integrations");
    }

    /// <inheritdoc />
    public async Task<IntegrationAccessResult> CanSyncIntegrationAsync(Integration integration, Guid userId, CancellationToken ct = default)
    {
        Project? project = integration.Project;
        if (project == null)
        {
            project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == integration.ProjectId, ct);
            if (project == null) return IntegrationAccessResult.ProjectNotFound();
        }

        if (await _projectAuth.IsOwnerOrActiveMemberAsync(project, userId, ct)) return IntegrationAccessResult.Allow();
        return IntegrationAccessResult.Deny("Only project owner or team members can sync integration");
    }

    /// <inheritdoc />
    public Task<Integration?> LoadIntegrationWithProjectAsync(Guid integrationId, CancellationToken ct = default) =>
        _db.Integrations
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == integrationId, ct);
}
