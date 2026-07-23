using System;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Services.Badges;

/// <summary>Operations for listing, awarding, and administering achievement badges.</summary>
public interface IBadgesService
{
    /// <summary>Lists achievement definitions with optional category filter and pagination.</summary>
    /// <param name="category">Optional category filter.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Page size (clamped to 1..200 by the implementation).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged badge definitions or an error result.</returns>
    Task<BadgeResult<object>> GetAllBadgesAsync(string? category, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Loads a single achievement definition by ID.</summary>
    /// <param name="id">Achievement identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The badge definition or a 404 error result.</returns>
    Task<BadgeResult<object>> GetBadgeAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lists badges earned by a user, optionally including progress values.</summary>
    /// <param name="userId">Target user ID.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="includeProgress">When <see langword="true"/>, includes stored progress percentages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged earned badges or an error result.</returns>
    Task<BadgeResult<object>> GetUserBadgesAsync(Guid userId, int page, int pageSize, bool includeProgress, CancellationToken ct = default);

    /// <summary>Grants a badge to a user; relies on a unique index to reject duplicates.</summary>
    /// <param name="request">Award request with user, badge code, and acting user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Created user-achievement metadata, or 404/409 error results.</returns>
    Task<BadgeResult<object>> AwardBadgeAsync(AwardBadgeRequest request, CancellationToken ct = default);

    /// <summary>Updates stored progress for an earned badge (admin-only).</summary>
    /// <param name="request">Progress update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated progress or permission/not-found errors.</returns>
    Task<BadgeResult<object>> UpdateProgressAsync(UpdateBadgeProgressRequest request, CancellationToken ct = default);

    /// <summary>Aggregates earned badge counts and points grouped by category.</summary>
    /// <param name="userId">Target user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Totals and per-category breakdown.</returns>
    Task<BadgeResult<object>> GetUserStatsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Creates a new achievement definition.</summary>
    /// <param name="dto">Badge metadata to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Created badge details or a 409 when the code already exists.</returns>
    Task<BadgeResult<BadgeCreatedResponse>> CreateBadgeAsync(CreateAchievementDto dto, CancellationToken ct = default);

    /// <summary>Updates an existing achievement definition.</summary>
    /// <param name="id">Achievement identifier.</param>
    /// <param name="dto">Partial update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated achievement or not-found/conflict errors.</returns>
    Task<BadgeResult<object>> UpdateBadgeAsync(Guid id, UpdateAchievementDto dto, CancellationToken ct = default);

    /// <summary>Deletes an achievement that has never been awarded.</summary>
    /// <param name="id">Achievement identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or not-found/conflict when users already earned the badge.</returns>
    Task<BadgeResult> DeleteBadgeAsync(Guid id, CancellationToken ct = default);
}
