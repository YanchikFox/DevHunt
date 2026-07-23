namespace DevHunt.CoreApi.Services.Profile;

/// <summary>
/// Facade that groups profile-related services to reduce constructor over-injection.
/// This simplifies dependency injection in ProfileController by consolidating 5 services into 1.
/// </summary>
public interface IProfileServices
{
    /// <summary>Object storage for avatars.</summary>
    IObjectStorageService ObjectStorage { get; }
    /// <summary>Cache service for profile caching.</summary>
    ICacheService Cache { get; }
    /// <summary>Event bus for profile events.</summary>
    IEventBusService EventBus { get; }
    /// <summary>Activity logging service.</summary>
    IActivityLogService ActivityLog { get; }
    /// <summary>Host environment information.</summary>
    IWebHostEnvironment Environment { get; }
}

/// <summary>
/// Implementation of IProfileServices facade.
/// Reduces constructor injection from 7 dependencies to 3 (DbContext, IProfileServices, ILogger).
/// </summary>
public class ProfileServicesFacade : IProfileServices
{
    /// <inheritdoc />
    public IObjectStorageService ObjectStorage { get; }
    /// <inheritdoc />
    public ICacheService Cache { get; }
    /// <inheritdoc />
    public IEventBusService EventBus { get; }
    /// <inheritdoc />
    public IActivityLogService ActivityLog { get; }
    /// <inheritdoc />
    public IWebHostEnvironment Environment { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileServicesFacade"/> class.
    /// </summary>
    /// <param name="objectStorage">Object storage used for avatar files.</param>
    /// <param name="cache">Cache used for profile-related invalidation.</param>
    /// <param name="eventBus">Event bus for profile domain events.</param>
    /// <param name="activityLog">Activity log writer for profile actions.</param>
    /// <param name="environment">Host environment used by profile file handling.</param>
    public ProfileServicesFacade(
        IObjectStorageService objectStorage,
        ICacheService cache,
        IEventBusService eventBus,
        IActivityLogService activityLog,
        IWebHostEnvironment environment)
    {
        ObjectStorage = objectStorage;
        Cache = cache;
        EventBus = eventBus;
        ActivityLog = activityLog;
        Environment = environment;
    }
}
