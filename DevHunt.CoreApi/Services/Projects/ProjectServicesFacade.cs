namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Facade that aggregates project-related services to reduce constructor over-injection.
/// Instead of injecting multiple services separately, controllers inject this single facade.
/// </summary>
public interface IProjectServices
{
    /// <summary>Project filtering service.</summary>
    IProjectFilterService Filter { get; }
    /// <summary>Project permission service.</summary>
    IProjectPermissionService Permissions { get; }
    /// <summary>Activity log service.</summary>
    IActivityLogService ActivityLog { get; }
}

/// <summary>
/// Default implementation of <see cref="IProjectServices"/>.
/// </summary>
public sealed class ProjectServicesFacade : IProjectServices
{
    /// <inheritdoc />
    public IProjectFilterService Filter { get; }
    /// <inheritdoc />
    public IProjectPermissionService Permissions { get; }
    /// <inheritdoc />
    public IActivityLogService ActivityLog { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectServicesFacade"/> class.
    /// </summary>
    /// <param name="filterService">Service used for catalog filtering and sorting.</param>
    /// <param name="permissionService">Service used to evaluate project permissions.</param>
    /// <param name="activityLogService">Service used to write project activity events.</param>
    public ProjectServicesFacade(
        IProjectFilterService filterService,
        IProjectPermissionService permissionService,
        IActivityLogService activityLogService)
    {
        Filter = filterService;
        Permissions = permissionService;
        ActivityLog = activityLogService;
    }
}
