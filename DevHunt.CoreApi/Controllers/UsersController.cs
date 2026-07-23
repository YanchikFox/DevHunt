using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Badges;
using DevHunt.CoreApi.Services.Users; // Shared DTOs and Logic
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// User directory, profiles, and follow relationships.
/// </summary>
/// <remarks>
/// Handles public user search, profile visibility, follow lists, and privacy settings.
/// Routes: api/users/*
/// </remarks>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly DevHuntDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IActivityLogService _activityLogService;
    private readonly IUserServices _userServices;
    private readonly IPresenceService _presenceService;
    private readonly ICacheService _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    /// <param name="dbContext">Database context used for user, follow, privacy, and activity lookups.</param>
    /// <param name="auditService">Audit service used to record follow and unfollow events.</param>
    /// <param name="activityLogService">Activity logger used to write social activity events.</param>
    /// <param name="userServices">Facade for user search, profile visibility, follow lists, activity, and stats services.</param>
    /// <param name="presenceService">Presence service used to batch resolve online status.</param>
    /// <param name="cache">Cache service used to invalidate profile summary entries after follow changes.</param>
    public UsersController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        IActivityLogService activityLogService,
        IUserServices userServices,
        IPresenceService presenceService,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _activityLogService = activityLogService;
        _userServices = userServices;
        _presenceService = presenceService;
        _cache = cache;
    }

    /// <summary>
    /// Reads the authenticated user's identifier from claims and fails fast when authentication middleware did not provide it.
    /// </summary>
    /// <returns>The current user's ID.</returns>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Returns online status for up to 50 comma-separated user IDs without requiring authentication.
    /// </summary>
    /// <param name="ids">Comma-separated user IDs; invalid GUID tokens are ignored.</param>
    /// <returns>An ID-to-online-status map, an empty map for missing input, or 400 when more than 50 IDs are requested.</returns>
    [HttpGet("online-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOnlineStatus([FromQuery] string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return Ok(new Dictionary<string, bool>());

        var rawIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (rawIds.Length > 50)
            return BadRequest("Maximum 50 user IDs per request.");

        var guids = new List<Guid>(rawIds.Length);
        foreach (string id in rawIds)
        {
            if (Guid.TryParse(id, out Guid guid))
                guids.Add(guid);
        }

        Dictionary<Guid, bool> statuses = await _presenceService.GetOnlineStatusAsync(guids);
        var result = statuses.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        return Ok(result);
    }

    /// <summary>Query parameters for user search with filtering and pagination.</summary>
    public class UserSearchQuery
    {
        /// <summary>Search by name or bio.</summary>
        [MaxLength(100)]
        public string? Query { get; set; }

        /// <summary>Filter by role (participant, curator, admin).</summary>
        [RegularExpression("^(participant|curator|admin)$", ErrorMessage = "Role must be one of: participant, curator, admin")]
        public string? Role { get; set; }

        /// <summary>Filter by timezone.</summary>
        [MaxLength(50)]
        public string? Timezone { get; set; }

        /// <summary>Filter by skills (comma-separated list).</summary>
        public string? Skills { get; set; }

        /// <summary>Minimum experience in years.</summary>
        public int? MinExperience { get; set; }

        /// <summary>Page number (default: 1).</summary>
        public int Page { get; set; } = 1;

        /// <summary>Page size (default: 20, max: 100).</summary>
        public int PageSize { get; set; } = 20;

        /// <summary>Sort by name, rating, or createdAt.</summary>
        public string SortBy { get; set; } = "name";

        /// <summary>Sort order: asc or desc.</summary>
        public string SortOrder { get; set; } = "asc";

        /// <summary>Whether to include skills in response.</summary>
        public bool IncludeSkills { get; set; }

        /// <summary>
        /// Converts controller query parameters to the user search service request shape.
        /// </summary>
        /// <returns>The service-layer search request.</returns>
        public UserSearchRequest ToSearchRequest() =>
            new(Query, Role, Timezone, Skills, MinExperience, Page, PageSize, SortBy, SortOrder, IncludeSkills);
    }

    /// <summary>
    /// Searches public users with filtering and pagination; <c>/search</c> remains available as a compatibility route.
    /// </summary>
    /// <param name="q">Search/filter/pagination parameters.</param>
    /// <returns>Search results wrapped with pagination metadata.</returns>
    [HttpGet]
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult> GetUsers([FromQuery] UserSearchQuery q)
    {
        var result = await _userServices.Search.SearchUsersAsync(q.ToSearchRequest());
        return Ok(new { Data = result.Data, Pagination = result.Pagination });
    }

    /// <summary>
    /// GET /api/users/{id} - Get a user profile with visibility checks.
    /// </summary>
    /// <param name="id">Target user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>User profile projection based on visibility rules.</returns>
    /// <response code="200">Returns the user profile.</response>
    /// <response code="404">User not found or inactive.</response>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<UserProfileDto>> GetUser(Guid id, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserSkills)
                .ThenInclude(us => us.Skill)
            .Include(u => u.UserSkillEntries)
                .ThenInclude(e => e.Skill)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null || !user.IsActive)
        {
            return NotFound();
        }

        var viewerId = SecurityHelpers.GetUserId(User);

        var visibilityState = await _userServices.Profile.EvaluateProfileVisibilityAsync(id, viewerId);
        var followRelation = await _userServices.Profile.GetFollowRelationshipAsync(id, viewerId);

        var profileDto = await _userServices.Profile.BuildUserProfileDtoAsync(
            user,
            visibilityState,
            followRelation);

        return Ok(profileDto);
    }

    #region Follow API

    /// <summary>
    /// Follows an active user, records social activity, and invalidates both profile stat caches.
    /// </summary>
    /// <param name="userId">User to follow.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>204 when the follow exists or is created, 400 when following self, or 404 when the target user is missing or inactive.</returns>
    [HttpPost("{userId:guid}/follow")]
    [Authorize]
    public async Task<IActionResult> FollowUser(Guid userId, CancellationToken ct = default)
    {
        var followerId = GetRequiredUserId();
        if (followerId == userId)
            return BadRequest("Cannot follow yourself");

        var target = await _dbContext.Users.FindAsync(new object[] { userId }, ct);
        if (target == null || !target.IsActive)
            return NotFound("User not found");

        var existing = await _dbContext.UserFollows.FindAsync(new object[] { followerId, userId }, ct);
        if (existing != null)
            return NoContent();

        var follow = new UserFollow
        {
            FollowerId = followerId,
            FollowedId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.UserFollows.Add(follow);
        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(followerId, "UsersController.FollowUser", "UserFollow", userId,
            $"User {followerId} followed {userId}");

        await _activityLogService.LogUserEventAsync(
            followerId,
            "user.follow",
            $"Followed {target.FullName ?? target.Email}",
            visibility: ActivityVisibilityLevel.Followers,
            targetUserId: userId);

        // Trigger for followed user (first_follower, popular, etc.) and follower (social_butterfly)
        await HttpContext.RequestServices.TriggerAchievementCheckAsync(userId, AchievementTrigger.UserFollowed);
        await HttpContext.RequestServices.TriggerAchievementCheckAsync(followerId, AchievementTrigger.UserFollowed);

        // Invalidate cached profile stats for both users
        await _cache.RemoveAsync($"profile:{followerId}");
        await _cache.RemoveAsync($"profile:{userId}");

        return NoContent();
    }

    /// <summary>
    /// Removes the current user's follow relationship to another user and invalidates both profile stat caches.
    /// </summary>
    /// <param name="userId">User to unfollow.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>204 after removal, or 404 when the follow relationship does not exist.</returns>
    [HttpDelete("{userId:guid}/follow")]
    [Authorize]
    public async Task<IActionResult> UnfollowUser(Guid userId, CancellationToken ct = default)
    {
        var followerId = GetRequiredUserId();
        var existing = await _dbContext.UserFollows
            .Include(f => f.Followed)
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowedId == userId, ct);
        if (existing == null)
            return NotFound("Follow relationship not found");

        _dbContext.UserFollows.Remove(existing);
        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(followerId, "UsersController.UnfollowUser", "UserFollow", userId,
            $"User {followerId} unfollowed {userId}");

        await _activityLogService.LogUserEventAsync(
            followerId,
            "user.unfollow",
            $"Unfollowed {existing.Followed.FullName ?? existing.Followed.Email}",
            visibility: ActivityVisibilityLevel.Followers,
            targetUserId: userId);

        // Invalidate cached profile stats for both users
        await _cache.RemoveAsync($"profile:{followerId}");
        await _cache.RemoveAsync($"profile:{userId}");

        return NoContent();
    }

    /// <summary>
    /// Returns suggested users for the current user to follow.
    /// </summary>
    /// <param name="limit">Maximum number of suggestions to request from the activity service.</param>
    /// <returns>Suggested users from the social activity service.</returns>
    [HttpGet("suggested")]
    [Authorize]
    public async Task<IActionResult> GetSuggestedUsers([FromQuery] int limit = 20)
    {
        var viewerId = GetRequiredUserId();
        var result = await _userServices.Activity.GetSuggestedUsersAsync(viewerId, limit);
        return Ok(result);
    }

    /// <summary>
    /// Lists paginated followers for a user.
    /// </summary>
    /// <param name="userId">User whose followers should be listed.</param>
    /// <param name="page">Page number to return.</param>
    /// <param name="pageSize">Number of users per page.</param>
    /// <returns>Follower summaries with pagination metadata.</returns>
    [HttpGet("{userId:guid}/followers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFollowers(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _userServices.Follow.GetFollowersAsync(userId, page, pageSize);
        return Ok(BuildPaginatedFollowResponse(result));
    }

    /// <summary>
    /// Lists paginated accounts followed by a user.
    /// </summary>
    /// <param name="userId">User whose following list should be returned.</param>
    /// <param name="page">Page number to return.</param>
    /// <param name="pageSize">Number of users per page.</param>
    /// <returns>Following summaries with pagination metadata.</returns>
    [HttpGet("{userId:guid}/following")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFollowing(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _userServices.Follow.GetFollowingAsync(userId, page, pageSize);
        return Ok(BuildPaginatedFollowResponse(result));
    }

    /// <summary>
    /// Projects follow-service results into the anonymous response shape used by follower and following endpoints.
    /// </summary>
    /// <param name="result">Paginated follow service result.</param>
    /// <returns>Follow summaries plus pagination flags.</returns>
    private object BuildPaginatedFollowResponse(PaginatedFollowResult result) => new
    {
        Data = result.Users.Select(u => new FollowSummaryDto(u.Id, u.Role, u.FullName, u.AvatarUrl)),
        Pagination = new
        {
            Page = result.Page,
            PageSize = result.Limit,
            Total = result.Total,
            TotalPages = result.TotalPages,
            HasNext = result.Page * result.Limit < result.Total,
            HasPrevious = result.Page > 1
        }
    };

    /// <summary>Request to update privacy, profile display, and notification settings for the current user.</summary>
    /// <param name="ProfileVisibility">Visibility level for the user's profile.</param>
    /// <param name="ShowEmail">Whether the profile may display the user's email.</param>
    /// <param name="ShowSkills">Whether the profile may display skills.</param>
    /// <param name="ShowExperience">Whether the profile may display experience information.</param>
    /// <param name="ShowRating">Whether the profile may display rating information.</param>
    /// <param name="ShowProjects">Whether the profile may display project participation.</param>
    /// <param name="ShowSocialLinks">Whether the profile may display social links.</param>
    /// <param name="ShowAchievements">Whether the profile may display achievements.</param>
    /// <param name="AllowEmailSearch">Whether the account can be found by email search.</param>
    /// <param name="NotifyOnMessages">Whether message notifications are enabled.</param>
    /// <param name="NotifyOnInvitations">Whether invitation notifications are enabled.</param>
    /// <param name="ActivityVisibility">Default visibility for user activity events.</param>
    public record UpdatePrivacySettingsRequest(
        string? ProfileVisibility,
        bool? ShowEmail,
        bool? ShowSkills,
        bool? ShowExperience,
        bool? ShowRating,
        bool? ShowProjects,
        bool? ShowSocialLinks,
        bool? ShowAchievements,
        bool? AllowEmailSearch,
        bool? NotifyOnMessages,
        bool? NotifyOnInvitations,
        string? ActivityVisibility
    );

    /// <summary>
    /// Returns the current user's privacy settings, using unsaved defaults when no settings row exists.
    /// </summary>
    /// <param name="ct">Cancellation token for the settings lookup.</param>
    /// <returns>The current privacy settings, default settings, or 401 when the user claim is absent.</returns>
    [HttpGet("me/settings")]
    [Authorize]
    public async Task<IActionResult> GetMySettings(CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        var settings = await _dbContext.UserPrivacySettings
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (settings == null)
        {
            return Ok(new UserPrivacySettings { UserId = userId.Value });
        }

        return Ok(settings);
    }

    /// <summary>
    /// Creates or updates the current user's privacy settings from the supplied partial request.
    /// </summary>
    /// <param name="request">Privacy, profile display, and notification values to apply when present.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>The saved settings, or 401 when the user claim is absent.</returns>
    [HttpPut("me/settings")]
    [Authorize]
    public async Task<IActionResult> UpdateMySettings([FromBody] UpdatePrivacySettingsRequest request, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        var settings = await _dbContext.UserPrivacySettings
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (settings == null)
        {
            settings = new UserPrivacySettings { UserId = userId.Value };
            _dbContext.UserPrivacySettings.Add(settings);
        }

        ApplyPrivacySettings(settings, request);

        settings.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(settings);
    }

    /// <summary>
    /// Applies all privacy settings groups from the request to a settings entity.
    /// </summary>
    /// <param name="settings">Settings entity to mutate.</param>
    /// <param name="request">Optional setting values supplied by the client.</param>
    private static void ApplyPrivacySettings(UserPrivacySettings settings, UpdatePrivacySettingsRequest request)
    {
        ApplyVisibilitySettings(settings, request);
        ApplyDisplaySettings(settings, request);
        ApplyNotificationSettings(settings, request);
    }

    /// <summary>
    /// Applies profile, activity, and email-search visibility values when present.
    /// </summary>
    /// <param name="settings">Settings entity to mutate.</param>
    /// <param name="request">Optional visibility values supplied by the client.</param>
    private static void ApplyVisibilitySettings(UserPrivacySettings settings, UpdatePrivacySettingsRequest request)
    {
        if (request.ProfileVisibility != null) settings.ProfileVisibility = request.ProfileVisibility;
        if (request.ActivityVisibility != null) settings.ActivityVisibility = request.ActivityVisibility;
        if (request.AllowEmailSearch.HasValue) settings.AllowEmailSearch = request.AllowEmailSearch.Value;
    }

    /// <summary>
    /// Applies profile display toggles when they are present in the request.
    /// </summary>
    /// <param name="settings">Settings entity to mutate.</param>
    /// <param name="request">Optional display values supplied by the client.</param>
    private static void ApplyDisplaySettings(UserPrivacySettings settings, UpdatePrivacySettingsRequest request)
    {
        if (request.ShowEmail.HasValue) settings.ShowEmail = request.ShowEmail.Value;
        if (request.ShowSkills.HasValue) settings.ShowSkills = request.ShowSkills.Value;
        if (request.ShowExperience.HasValue) settings.ShowExperience = request.ShowExperience.Value;
        if (request.ShowRating.HasValue) settings.ShowRating = request.ShowRating.Value;
        if (request.ShowProjects.HasValue) settings.ShowProjects = request.ShowProjects.Value;
        if (request.ShowSocialLinks.HasValue) settings.ShowSocialLinks = request.ShowSocialLinks.Value;
        if (request.ShowAchievements.HasValue) settings.ShowAchievements = request.ShowAchievements.Value;
    }

    /// <summary>
    /// Applies notification preferences when they are present in the request.
    /// </summary>
    /// <param name="settings">Settings entity to mutate.</param>
    /// <param name="request">Optional notification values supplied by the client.</param>
    private static void ApplyNotificationSettings(UserPrivacySettings settings, UpdatePrivacySettingsRequest request)
    {
        if (request.NotifyOnMessages.HasValue) settings.NotifyOnMessages = request.NotifyOnMessages.Value;
        if (request.NotifyOnInvitations.HasValue) settings.NotifyOnInvitations = request.NotifyOnInvitations.Value;
    }

    #endregion

    #region User Activity Feed

    /// <summary>
    /// Returns a user's activity feed after applying visibility rules for the requester.
    /// </summary>
    /// <param name="userId">User whose activity feed should be returned.</param>
    /// <param name="visibility">Optional visibility filter.</param>
    /// <param name="page">Page number to return.</param>
    /// <param name="pageSize">Number of activities per page.</param>
    /// <returns>Activity items with pagination metadata, 403 for hidden feeds, or 400 for service validation errors.</returns>
    [HttpGet("{userId:guid}/activities")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserActivity(
        Guid userId,
        [FromQuery] string? visibility,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var requesterId = SecurityHelpers.GetUserId(User);
        var filter = new ActivityFilter(visibility, page, pageSize);
        var result = await _userServices.Activity.GetUserActivityAsync(userId, requesterId, filter);

        if (result.Error == "Forbidden")
            return Forbid();
        if (result.Error != null)
            return BadRequest(result.Error);

        return Ok(new
        {
            Data = result.Items.Select(a => new UserActivityDto(
                a.Id, a.EventType, a.EventGroup, a.Summary, a.Visibility,
                a.PayloadJson, a.ActorId, a.ActorName, a.ActorAvatarUrl,
                a.TargetUserId, a.TargetUserName, a.ProjectId, a.ProjectTitle, a.CreatedAt)),
            Pagination = new
            {
                Page = result.Page,
                PageSize = result.Limit,
                Total = result.Total,
                TotalPages = result.TotalPages,
                HasNext = result.Page * result.Limit < result.Total,
                HasPrevious = result.Page > 1
            }
        });
    }

    /// <summary>
    /// Returns aggregated user stats when profile visibility permits the requester to see them.
    /// </summary>
    /// <param name="userId">User whose stats should be returned.</param>
    /// <param name="ct">Cancellation token for the user lookup.</param>
    /// <returns>User stats, 403 when visibility blocks access, or 404 when the user is missing or inactive.</returns>
    [HttpGet("{userId:guid}/stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserStats(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null || !user.IsActive)
            return NotFound();

        var viewerId = SecurityHelpers.GetUserId(User);
        var state = await _userServices.Profile.EvaluateProfileVisibilityAsync(userId, viewerId);
        var followRel = await _userServices.Profile.GetFollowRelationshipAsync(userId, viewerId);

        if (!IsStatsAccessAllowed(state, followRel))
            return Forbid();

        var stats = await _userServices.Stats.GetStatsAsync(userId);
        return Ok(stats);
    }

    /// <summary>
    /// Evaluates whether the viewer can access user stats based on ownership, public visibility, or follower relationship.
    /// </summary>
    /// <param name="state">Computed profile visibility state for the viewer.</param>
    /// <param name="followRel">Follow relationship between the viewer and profile owner.</param>
    /// <returns><see langword="true"/> when stats should be returned to the viewer.</returns>
    private static bool IsStatsAccessAllowed(ProfileVisibilityState state, FollowRelationship followRel)
    {
        if (state.IsOwner) return true;
        if (state.NormalizedVisibility == ActivityVisibilityLevel.Public) return true;
        if (state.NormalizedVisibility == ActivityVisibilityLevel.Followers) return followRel.IsFollower;
        return false;
    }

    #endregion
}
