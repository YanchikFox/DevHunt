using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Service for user follow/follower operations.
/// Extracts common pagination logic from GetFollowers/GetFollowing to eliminate code duplication.
/// </summary>
public interface IUserFollowService
{
    /// <summary>
    /// Get followers for a user with pagination.
    /// </summary>
    /// <param name="userId">Target user identifier.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="limit">Page size.</param>
    /// <param name="ct">Cancels the followers query.</param>
    /// <returns>Paginated follower list.</returns>
    Task<PaginatedFollowResult> GetFollowersAsync(Guid userId, int page, int limit, CancellationToken ct = default);
    /// <summary>
    /// Get accounts the user is following with pagination.
    /// </summary>
    /// <param name="userId">Target user identifier.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="limit">Page size.</param>
    /// <param name="ct">Cancels the following query.</param>
    /// <returns>Paginated following list.</returns>
    Task<PaginatedFollowResult> GetFollowingAsync(Guid userId, int page, int limit, CancellationToken ct = default);
    /// <summary>
    /// Check if a user follows another user.
    /// </summary>
    /// <param name="followerId">Follower user ID.</param>
    /// <param name="followedId">Followed user ID.</param>
    /// <param name="ct">Cancels the follow relationship check.</param>
    /// <returns>True if following.</returns>
    Task<bool> IsFollowingAsync(Guid followerId, Guid followedId, CancellationToken ct = default);
    /// <summary>
    /// Create a follow relationship.
    /// </summary>
    /// <param name="followerId">Follower user ID.</param>
    /// <param name="targetId">Target user ID.</param>
    /// <param name="ct">Cancels follow persistence.</param>
    /// <returns>Follow result with error details if any.</returns>
    Task<FollowResult> FollowAsync(Guid followerId, Guid targetId, CancellationToken ct = default);
    /// <summary>
    /// Remove a follow relationship.
    /// </summary>
    /// <param name="followerId">Follower user ID.</param>
    /// <param name="targetId">Target user ID.</param>
    /// <param name="ct">Cancels unfollow persistence.</param>
    /// <returns>True if unfollowed.</returns>
    Task<bool> UnfollowAsync(Guid followerId, Guid targetId, CancellationToken ct = default);
}

/// <summary>Follow list user projection.</summary>
/// <param name="Id">User identifier.</param>
/// <param name="Role">User role.</param>
/// <param name="FullName">Display name.</param>
/// <param name="AvatarUrl">Avatar URL.</param>
public record FollowUserDto(
    Guid Id,
    string Role,
    string? FullName,
    string? AvatarUrl);

/// <summary>Paginated result for follow lists.</summary>
/// <param name="Users">Users in the page.</param>
/// <param name="Total">Total count.</param>
/// <param name="Page">Current page.</param>
/// <param name="Limit">Page size.</param>
/// <param name="TotalPages">Total pages.</param>
public record PaginatedFollowResult(
    IReadOnlyList<FollowUserDto> Users,
    int Total,
    int Page,
    int Limit,
    int TotalPages);

/// <summary>Result of follow operations.</summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="ErrorMessage">Optional error message.</param>
public record FollowResult(bool Success, string? ErrorMessage = null);

/// <summary>
/// Default implementation of <see cref="IUserFollowService"/>.
/// </summary>
public class UserFollowService : IUserFollowService
{
    private readonly DevHuntDbContext _dbContext;
    private readonly INotificationHelperService _notifications;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserFollowService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for follow relationships.</param>
    /// <param name="notifications">Sends notifications to newly followed users.</param>
    public UserFollowService(DevHuntDbContext dbContext, INotificationHelperService notifications)
    {
        _dbContext = dbContext;
        _notifications = notifications;
    }

    /// <inheritdoc />
    public async Task<PaginatedFollowResult> GetFollowersAsync(Guid userId, int page, int limit, CancellationToken ct = default)
    {
        var (normalizedPage, normalizedLimit) = NormalizePagination(page, limit);

        var query = _dbContext.UserFollows
            .AsNoTracking()
            .Where(f => f.FollowedId == userId && f.Follower.IsEmailVerified);

        var total = await query.CountAsync(ct);
        var totalPages = CalculateTotalPages(total, normalizedLimit);

        var followers = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedLimit)
            .Take(normalizedLimit)
            .Select(f => new FollowUserDto(
                f.Follower.Id,
                f.Follower.Role,
                f.Follower.FullName,
                f.Follower.AvatarUrl))
            .ToListAsync(ct);

        return new PaginatedFollowResult(followers, total, normalizedPage, normalizedLimit, totalPages);
    }

    /// <inheritdoc />
    public async Task<PaginatedFollowResult> GetFollowingAsync(Guid userId, int page, int limit, CancellationToken ct = default)
    {
        var (normalizedPage, normalizedLimit) = NormalizePagination(page, limit);

        var query = _dbContext.UserFollows
            .AsNoTracking()
            .Where(f => f.FollowerId == userId && f.Followed.IsEmailVerified);

        var total = await query.CountAsync(ct);
        var totalPages = CalculateTotalPages(total, normalizedLimit);

        var following = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedLimit)
            .Take(normalizedLimit)
            .Select(f => new FollowUserDto(
                f.Followed.Id,
                f.Followed.Role,
                f.Followed.FullName,
                f.Followed.AvatarUrl))
            .ToListAsync(ct);

        return new PaginatedFollowResult(following, total, normalizedPage, normalizedLimit, totalPages);
    }

    /// <inheritdoc />
    public async Task<bool> IsFollowingAsync(Guid followerId, Guid followedId, CancellationToken ct = default)
    {
        return await _dbContext.UserFollows
            .AnyAsync(f => f.FollowerId == followerId && f.FollowedId == followedId, ct);
    }

    /// <inheritdoc />
    public async Task<FollowResult> FollowAsync(Guid followerId, Guid targetId, CancellationToken ct = default)
    {
        if (followerId == targetId)
        {
            return new FollowResult(false, "Cannot follow yourself");
        }

        var targetExists = await _dbContext.Users.AnyAsync(u => u.Id == targetId && u.IsActive && u.IsEmailVerified, ct);
        if (!targetExists)
        {
            return new FollowResult(false, "User not found");
        }

        var alreadyFollowing = await IsFollowingAsync(followerId, targetId, ct);
        if (alreadyFollowing)
        {
            return new FollowResult(false, "Already following this user");
        }

        var follow = new UserFollow
        {
            FollowerId = followerId,
            FollowedId = targetId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.UserFollows.Add(follow);
        await _dbContext.SaveChangesAsync(ct);

        // Notify the followed user
        var followerName = await _dbContext.Users
            .Where(u => u.Id == followerId)
            .Select(u => u.FullName ?? u.Email)
            .FirstOrDefaultAsync(ct);

        await _notifications.SendNotificationAsync(
            targetId, "follow",
            $"{followerName} started following you",
            relatedEntityType: "User",
            relatedEntityId: followerId,
            priority: "low", ct: ct);

        return new FollowResult(true);
    }

    /// <inheritdoc />
    public async Task<bool> UnfollowAsync(Guid followerId, Guid targetId, CancellationToken ct = default)
    {
        var follow = await _dbContext.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowedId == targetId, ct);

        if (follow == null)
        {
            return false;
        }

        _dbContext.UserFollows.Remove(follow);
        await _dbContext.SaveChangesAsync(ct);

        return true;
    }

    #region Private Helpers

    private static (int Page, int Limit) NormalizePagination(int page, int limit)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedLimit = Math.Clamp(limit, 1, 100);
        return (normalizedPage, normalizedLimit);
    }

    /// <summary>
    /// Calculates how many pages are needed for the total follow count at the given page size.
    /// </summary>
    private static int CalculateTotalPages(int total, int limit)
        => (int)Math.Ceiling(total / (double)limit);

    #endregion
}
