using DevHunt.CoreApi.Models;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Service for user activity and suggestion operations.
/// Extracted from UsersController to reduce Bumpy Road and Complex Methods.
/// </summary>
public interface IUserActivityService
{
    /// <summary>
    /// Retrieve a user's activity feed with visibility filtering and pagination.
    /// </summary>
    /// <param name="userId">Target user identifier.</param>
    /// <param name="requesterId">Viewer user identifier (optional).</param>
    /// <param name="filter">Activity filter options.</param>
    /// <param name="ct">Cancels the activity feed query.</param>
    /// <returns>Paginated activity results or an error.</returns>
    Task<PaginatedActivityResult> GetUserActivityAsync(Guid userId, Guid? requesterId, ActivityFilter filter, CancellationToken ct = default);
    /// <summary>
    /// Get suggested users for discovery.
    /// </summary>
    /// <param name="userId">Viewer user identifier.</param>
    /// <param name="limit">Maximum number of suggestions.</param>
    /// <returns>Suggested users list.</returns>
    Task<IReadOnlyList<SuggestedUserDto>> GetSuggestedUsersAsync(Guid userId, int limit);
}

/// <summary>Filtering and pagination options for activity queries.</summary>
/// <param name="Visibility">Requested visibility scope.</param>
/// <param name="Page">Page number (1-based).</param>
/// <param name="Limit">Page size.</param>
public record ActivityFilter(
    string? Visibility,
    int Page,
    int Limit);

/// <summary>Activity feed item returned by the service.</summary>
/// <param name="Id">Activity record identifier.</param>
/// <param name="EventType">Event type key.</param>
/// <param name="EventGroup">Event group label.</param>
/// <param name="Summary">Human-readable summary.</param>
/// <param name="Visibility">Visibility scope.</param>
/// <param name="PayloadJson">Optional JSON payload.</param>
/// <param name="ActorId">Actor user ID.</param>
/// <param name="ActorName">Actor display name.</param>
/// <param name="ActorAvatarUrl">Actor avatar URL.</param>
/// <param name="TargetUserId">Target user ID (optional).</param>
/// <param name="TargetUserName">Target user display name.</param>
/// <param name="ProjectId">Related project ID.</param>
/// <param name="ProjectTitle">Related project title.</param>
/// <param name="CreatedAt">Event timestamp.</param>
public record ActivityItemDto(
    Guid Id,
    string EventType,
    string EventGroup,
    string Summary,
    string Visibility,
    string? PayloadJson,
    Guid ActorId,
    string ActorName,
    string? ActorAvatarUrl,
    Guid? TargetUserId,
    string? TargetUserName,
    Guid? ProjectId,
    string? ProjectTitle,
    DateTime CreatedAt);

/// <summary>Paginated activity result.</summary>
/// <param name="Items">Activity items.</param>
/// <param name="Total">Total count.</param>
/// <param name="Page">Current page.</param>
/// <param name="Limit">Page size.</param>
/// <param name="TotalPages">Total pages.</param>
/// <param name="Error">Optional error message.</param>
public record PaginatedActivityResult(
    IReadOnlyList<ActivityItemDto> Items,
    int Total,
    int Page,
    int Limit,
    int TotalPages,
    string? Error = null);

/// <summary>Suggested user projection for discovery.</summary>
/// <param name="Id">User identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="AvatarUrl">Avatar URL.</param>
/// <param name="MutualProjectsCount">Mutual projects count.</param>
/// <param name="RecentActivityScore">Recent activity score.</param>
/// <param name="IsVerified">Whether the user is verified.</param>
public record SuggestedUserDto(
    Guid Id,
    string Name,
    string? AvatarUrl,
    int MutualProjectsCount,
    int RecentActivityScore,
    bool IsVerified = false);

/// <summary>
/// User activity and recommendation suggestion service.
/// </summary>
public class UserActivityService : IUserActivityService
{
    private readonly DevHuntDbContext _dbContext;
    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserActivityService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for activity feed and suggestion queries.</param>
    public UserActivityService(DevHuntDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PaginatedActivityResult> GetUserActivityAsync(Guid userId, Guid? requesterId, ActivityFilter filter, CancellationToken ct = default)
    {
        var normalizedFilter = NormalizeFilter(filter);

        // Determine visibility permissions
        var (allowed, error) = await DetermineAllowedVisibilities(userId, requesterId, normalizedFilter.Visibility, ct);
        if (error != null)
        {
            return new PaginatedActivityResult([], 0, normalizedFilter.Page, normalizedFilter.Limit, 0, error);
        }

        var query = BuildActivityQuery(userId, allowed, normalizedFilter.Visibility);

        var total = await query.CountAsync(ct);
        var totalPages = CalculateTotalPages(total, normalizedFilter.Limit);
        var items = await GetPaginatedActivityItems(query, normalizedFilter, ct);

        return new PaginatedActivityResult(items, total, normalizedFilter.Page, normalizedFilter.Limit, totalPages);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SuggestedUserDto>> GetSuggestedUsersAsync(Guid viewerId, int limit)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 50);
        var activeSince = DateTime.UtcNow.AddDays(-30);

        var followedIds = await GetFollowedUserIdsAsync(viewerId);
        var viewerProjectIds = await GetViewerProjectIdsAsync(viewerId);
        var candidates = await GetCandidateUsersAsync(viewerId, followedIds, normalizedLimit);
        var candidateIds = candidates.Select(u => u.Id).ToList();

        var mutualDict = await CalculateMutualProjectsAsync(candidateIds, viewerProjectIds);
        var activityDict = await CalculateActivityScoresAsync(candidateIds, activeSince);

        return RankAndSelectSuggestions(candidates, mutualDict, activityDict, normalizedLimit);
    }

    #region Activity Helpers

    private static (string? Visibility, int Page, int Limit) NormalizeFilter(ActivityFilter filter)
    {
        var page = Math.Max(1, filter.Page);
        var limit = Math.Clamp(filter.Limit, 1, MaxPageSize);
        var visibility = !string.IsNullOrWhiteSpace(filter.Visibility)
            ? filter.Visibility.Trim().ToLowerInvariant()
            : null;
        return (visibility, page, limit);
    }

    private async Task<(List<string> Allowed, string? Error)> DetermineAllowedVisibilities(
        Guid userId,
        Guid? requesterId,
        string? requestedVisibility, CancellationToken ct = default)
    {
        var isOwner = requesterId.HasValue && requesterId.Value == userId;
        var isFollower = requesterId.HasValue && await _dbContext.UserFollows
            .AnyAsync(f => f.FollowerId == requesterId.Value && f.FollowedId == userId, ct);

        var privacy = await _dbContext.UserPrivacySettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var activityVisibility = (privacy?.ActivityVisibility ?? "public").Trim().ToLowerInvariant();

        var allowed = BuildAllowedVisibilities(activityVisibility, isOwner, isFollower);

        // Validate requested visibility
        if (requestedVisibility != null)
        {
            var allowedModes = new[] { "public", "followers", "private" };
            if (!allowedModes.Contains(requestedVisibility))
            {
                return ([], "Visibility must be public, followers, or private");
            }
            if (!allowed.Contains(requestedVisibility))
            {
                return ([], "Forbidden");
            }
        }

        return (allowed, null);
    }

    /// <summary>
    /// Builds the activity visibility scopes allowed by the target user's setting and the requester's relationship.
    /// </summary>
    private static List<string> BuildAllowedVisibilities(string activityVisibility, bool isOwner, bool isFollower)
    {
        var allowed = new List<string> { "public" };

        if (activityVisibility == "followers")
        {
            if (isOwner || isFollower) allowed.Add("followers");
        }
        else if (activityVisibility == "private")
        {
            if (isOwner) allowed.AddRange(["followers", "private"]);
        }
        else
        {
            if (isOwner) allowed.AddRange(["followers", "private"]);
            else if (isFollower) allowed.Add("followers");
        }

        return allowed;
    }

    /// <summary>
    /// Creates the base activity query for a user, including related entities and allowed visibility filtering.
    /// </summary>
    private IQueryable<ActivityRecord> BuildActivityQuery(Guid userId, List<string> allowed, string? visibility)
    {
        var query = _dbContext.ActivityRecords
            .AsNoTracking()
            .Include(ar => ar.Actor)
            .Include(ar => ar.Project)
            .Include(ar => ar.TargetUser)
            .Where(ar => ar.TargetUserId == userId || ar.ActorId == userId)
            .Where(ar => allowed.Contains(ar.Visibility));

        if (visibility != null)
        {
            query = query.Where(ar => ar.Visibility == visibility);
        }

        return query;
    }

    /// <summary>
    /// Calculates how many pages are needed for the total item count at the given page size.
    /// </summary>
    private static int CalculateTotalPages(int total, int limit)
        => (int)Math.Ceiling(total / (double)limit);

    /// <summary>
    /// Orders, pages, and projects activity records into feed item DTOs.
    /// </summary>
    private static async Task<IReadOnlyList<ActivityItemDto>> GetPaginatedActivityItems(
        IQueryable<ActivityRecord> query,
        (string? Visibility, int Page, int Limit) filter, CancellationToken ct = default)
    {
        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((filter.Page - 1) * filter.Limit)
            .Take(filter.Limit)
            .Select(ar => new ActivityItemDto(
                ar.Id,
                ar.EventType,
                ar.EventGroup,
                ar.Summary,
                ar.Visibility,
                ar.PayloadJson,
                ar.ActorId,
                ar.Actor.FullName ?? ar.Actor.Email,
                ar.Actor.AvatarUrl,
                ar.TargetUserId,
                ar.TargetUser != null ? ar.TargetUser.FullName ?? ar.TargetUser.Email : null,
                ar.ProjectId,
                ar.Project != null ? ar.Project.Title : null,
                ar.CreatedAt))
            .ToListAsync(ct);
    }

    #endregion

    #region Suggestion Helpers

    /// <summary>
    /// Loads the set of user IDs followed by the specified user.
    /// </summary>
    private async Task<HashSet<Guid>> GetFollowedUserIdsAsync(Guid userId, CancellationToken ct = default)
    {
        var followedIds = await _dbContext.UserFollows
            .AsNoTracking()
            .Where(f => f.FollowerId == userId)
            .Select(f => f.FollowedId)
            .ToListAsync(ct);

        return followedIds.ToHashSet();
    }

    /// <summary>
    /// Loads project IDs the viewer owns or actively belongs to.
    /// </summary>
    private async Task<HashSet<Guid>> GetViewerProjectIdsAsync(Guid viewerId, CancellationToken ct = default)
    {
        var projectIds = await _dbContext.Projects
            .AsNoTracking()
            .Where(p => p.OwnerId == viewerId)
            .Select(p => p.Id)
            .Union(_dbContext.TeamMembers
                .AsNoTracking()
                .Where(tm => tm.UserId == viewerId && tm.Status == TeamMemberStatus.Active.Value)
                .Select(tm => tm.ProjectId))
            .Distinct()
            .ToListAsync(ct);

        return projectIds.ToHashSet();
    }

    /// <summary>
    /// Loads active, verified, unfollowed users and keeps public-profile candidates for suggestions.
    /// </summary>
    private async Task<List<CandidateUser>> GetCandidateUsersAsync(
        Guid viewerId,
        HashSet<Guid> followedIds,
        int limit, CancellationToken ct = default)
    {
        var candidates = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.IsEmailVerified && u.Id != viewerId && !followedIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                Name = u.FullName ?? u.Email ?? "DevHunt member",
                u.AvatarUrl,
                u.IsVerified,
                Visibility = _dbContext.UserPrivacySettings
                    .AsNoTracking()
                    .Where(ps => ps.UserId == u.Id)
                    .Select(ps => ps.ProfileVisibility)
                    .FirstOrDefault()
            })
            .Take(limit * 3)
            .ToListAsync(ct);

        // Perform the visibility filter client-side to avoid EF translation issues
        return candidates
            .Select(u => new CandidateUser(
                u.Id,
                u.Name,
                u.AvatarUrl,
                u.Visibility ?? "public",
                u.IsVerified))
            .Where(u => u.Visibility == "public")
            .ToList();
    }

    private async Task<Dictionary<Guid, int>> CalculateMutualProjectsAsync(
        List<Guid> candidateIds,
        HashSet<Guid> viewerProjectIds, CancellationToken ct = default)
    {
        var candidateOwned = await _dbContext.Projects.AsNoTracking()
            .Where(p => candidateIds.Contains(p.OwnerId))
            .Select(p => new { p.OwnerId, p.Id })
            .ToListAsync(ct);

        var candidateTeam = await _dbContext.TeamMembers.AsNoTracking()
            .Where(tm => tm.Status == TeamMemberStatus.Active.Value && candidateIds.Contains(tm.UserId))
            .Select(tm => new { tm.UserId, tm.ProjectId })
            .ToListAsync(ct);

        var mutualDict = new Dictionary<Guid, int>();
        foreach (var proj in candidateOwned)
        {
            if (viewerProjectIds.Contains(proj.Id))
            {
                mutualDict[proj.OwnerId] = mutualDict.GetValueOrDefault(proj.OwnerId) + 1;
            }
        }
        foreach (var tm in candidateTeam)
        {
            if (viewerProjectIds.Contains(tm.ProjectId))
            {
                mutualDict[tm.UserId] = mutualDict.GetValueOrDefault(tm.UserId) + 1;
            }
        }

        return mutualDict;
    }

    private async Task<Dictionary<Guid, int>> CalculateActivityScoresAsync(
        List<Guid> candidateIds,
        DateTime activeSince, CancellationToken ct = default)
    {
        var activityScores = await _dbContext.ActivityRecords.AsNoTracking()
            .Where(ar => candidateIds.Contains(ar.ActorId) && ar.CreatedAt >= activeSince)
            .GroupBy(ar => ar.ActorId)
            .Select(g => new { UserId = g.Key, Score = g.Count() })
            .ToListAsync(ct);

        return activityScores.ToDictionary(x => x.UserId, x => x.Score);
    }

    /// <summary>
    /// Projects candidates into suggestions, ranks them by mutual projects and recent activity, then limits the result.
    /// </summary>
    private static IReadOnlyList<SuggestedUserDto> RankAndSelectSuggestions(
        List<CandidateUser> candidates,
        Dictionary<Guid, int> mutualDict,
        Dictionary<Guid, int> activityDict,
        int limit)
    {
        return candidates
            .Select(u => new SuggestedUserDto(
                u.Id,
                u.Name,
                u.AvatarUrl,
                mutualDict.GetValueOrDefault(u.Id),
                activityDict.GetValueOrDefault(u.Id),
                u.IsVerified))
            .OrderByDescending(u => u.MutualProjectsCount)
            .ThenByDescending(u => u.RecentActivityScore)
            .Take(limit)
            .ToList();
    }

    /// <summary>Internal candidate user representation for ranking.</summary>
    private record CandidateUser(Guid Id, string Name, string? AvatarUrl, string Visibility, bool IsVerified);

    #endregion
}
