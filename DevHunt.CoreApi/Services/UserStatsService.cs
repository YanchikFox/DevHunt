using DevHunt.CoreApi.Models;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services;

/// <summary>User stats projection used by profile endpoints.</summary>
/// <param name="FollowersCount">Number of followers.</param>
/// <param name="FollowingCount">Number of accounts followed.</param>
/// <param name="ProjectsCount">Number of owned or joined projects.</param>
/// <param name="RecentActivityCount">Activity count in the last 30 days.</param>
/// <param name="ContributionScore">Project-related activity count.</param>
/// <param name="CommunityScore">Social/community activity count.</param>
/// <param name="ConsistencyDaysActiveLast14">Distinct active days in the last 14 days.</param>
public record UserStatsDto(
    int FollowersCount,
    int FollowingCount,
    int ProjectsCount,
    int RecentActivityCount,
    int ContributionScore,
    int CommunityScore,
    int ConsistencyDaysActiveLast14);

/// <summary>
/// Service for computing user statistics.
/// </summary>
public interface IUserStatsService
{
    /// <summary>
    /// Compute aggregated stats for a user.
    /// </summary>
    /// <param name="userId">Target user identifier.</param>
    /// <param name="ct">Cancels user stats queries.</param>
    /// <returns>Computed stats.</returns>
    Task<UserStatsDto> GetStatsAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of <see cref="IUserStatsService"/>.
/// </summary>
public class UserStatsService : IUserStatsService
{
    private readonly DevHuntDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserStatsService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context used for follow, project, team, and activity aggregates.</param>
    public UserStatsService(DevHuntDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<UserStatsDto> GetStatsAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var last30 = now.AddDays(-30);
        var last14 = now.AddDays(-14);

        var followersCount = await _dbContext.UserFollows.CountAsync(f => f.FollowedId == userId);
        var followingCount = await _dbContext.UserFollows.CountAsync(f => f.FollowerId == userId);

        var projectIds = _dbContext.Projects
            .Where(p => p.OwnerId == userId)
            .Select(p => p.Id);

        var teamProjectIds = _dbContext.TeamMembers
            .Where(tm => tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value)
            .Select(tm => tm.ProjectId);

        var projectsCount = await projectIds.Union(teamProjectIds).CountAsync(ct);

        var recentActivityCount = await _dbContext.ActivityRecords
            .Where(ar => (ar.ActorId == userId || ar.TargetUserId == userId) && ar.CreatedAt >= last30)
            .CountAsync(ct);

        var contributionScore = await _dbContext.ActivityRecords
            .Where(ar => ar.ActorId == userId && ar.EventGroup == "project" && ar.CreatedAt >= last30)
            .CountAsync(ct);

        var communityScore = await _dbContext.ActivityRecords
            .Where(ar =>
                ar.CreatedAt >= last30 &&
                (ar.ActorId == userId || ar.TargetUserId == userId) &&
                ar.EventGroup == "social")
            .CountAsync(ct);

        var consistencyDaysActive = await _dbContext.ActivityRecords
            .Where(ar => ar.ActorId == userId && ar.CreatedAt >= last14)
            .GroupBy(ar => ar.CreatedAt.Date)
            .CountAsync(ct);

        return new UserStatsDto(
            FollowersCount: followersCount,
            FollowingCount: followingCount,
            ProjectsCount: projectsCount,
            RecentActivityCount: recentActivityCount,
            ContributionScore: contributionScore,
            CommunityScore: communityScore,
            ConsistencyDaysActiveLast14: consistencyDaysActive);
    }
}
