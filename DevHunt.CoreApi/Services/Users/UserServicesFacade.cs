namespace DevHunt.CoreApi.Services.Users;

/// <summary>
/// Facade that groups user-related services to reduce constructor over-injection.
/// This simplifies dependency injection by consolidating multiple services into one.
/// </summary>
public interface IUserServices
{
    /// <summary>User stats service.</summary>
    IUserStatsService Stats { get; }
    /// <summary>User follow service.</summary>
    IUserFollowService Follow { get; }
    /// <summary>User activity service.</summary>
    IUserActivityService Activity { get; }
    /// <summary>User profile service.</summary>
    IUserProfileService Profile { get; }
    /// <summary>User search service.</summary>
    IUserSearchService Search { get; }
}

/// <summary>
/// Implementation of IUserServices facade.
/// </summary>
public class UserServicesFacade : IUserServices
{
    /// <inheritdoc />
    public IUserStatsService Stats { get; }
    /// <inheritdoc />
    public IUserFollowService Follow { get; }
    /// <inheritdoc />
    public IUserActivityService Activity { get; }
    /// <inheritdoc />
    public IUserProfileService Profile { get; }
    /// <inheritdoc />
    public IUserSearchService Search { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserServicesFacade"/> class.
    /// </summary>
    /// <param name="stats">User statistics service.</param>
    /// <param name="follow">Follow/follower service.</param>
    /// <param name="activity">Activity feed and suggestion service.</param>
    /// <param name="profile">Profile visibility and DTO service.</param>
    /// <param name="search">User discovery search service.</param>
    public UserServicesFacade(
        IUserStatsService stats,
        IUserFollowService follow,
        IUserActivityService activity,
        IUserProfileService profile,
        IUserSearchService search)
    {
        Stats = stats;
        Follow = follow;
        Activity = activity;
        Profile = profile;
        Search = search;
    }
}
