using System.Text.RegularExpressions;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Badges;
using DevHunt.CoreApi.Services.Profile;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevHunt.CoreApi.Models;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for user profile management.
/// </summary>
/// <remarks>
/// Provides endpoints for:
/// - Viewing own profile (authenticated)
/// - Viewing public profiles (anonymous)
/// - Updating profile information
/// - Avatar upload/management
/// - Activity history
///
/// Profile data is cached for performance:
/// - Own profile: 10 minutes
/// - Public profiles: 15 minutes
///
/// Routes: api/profile/*
/// </remarks>
[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly DevHuntDbContext _context;
    private readonly IProfileServices _services;
    private readonly ILogger<ProfileController> _logger;
    private readonly IUserProfileService _userProfileService;
    private readonly IUserStatsService _userStatsService;

    // Convenience accessors for facade services
    /// <summary>Object storage service used for avatar file operations.</summary>
    private IObjectStorageService ObjectStorage => _services.ObjectStorage;
    /// <summary>Cache service used to store and invalidate profile responses.</summary>
    private ICacheService Cache => _services.Cache;
    /// <summary>Event bus used to publish profile and avatar domain events.</summary>
    private IEventBusService EventBus => _services.EventBus;
    /// <summary>Activity log service used to record profile updates.</summary>
    private IActivityLogService ActivityLog => _services.ActivityLog;
    /// <summary>Hosting environment used to decide whether avatar errors include details.</summary>
    private IWebHostEnvironment Environment => _services.Environment;

    /// <summary>
    /// Creates the profile controller with profile storage, facade services, diagnostics, privacy, and stats services.
    /// </summary>
    /// <param name="context">Database context used to query and mutate users, skills, avatars, and privacy settings.</param>
    /// <param name="services">Facade exposing object storage, cache, event bus, activity log, and environment services.</param>
    /// <param name="logger">Logger for profile and avatar diagnostics.</param>
    /// <param name="userProfileService">Service that evaluates public-profile visibility.</param>
    /// <param name="userStatsService">Service that computes follower, following, and project counts.</param>
    public ProfileController(
        DevHuntDbContext context,
        IProfileServices services,
        ILogger<ProfileController> logger,
        IUserProfileService userProfileService,
        IUserStatsService userStatsService)
    {
        _context = context;
        _services = services;
        _logger = logger;
        _userProfileService = userProfileService;
        _userStatsService = userStatsService;
    }

    // ========================================================================
    // DTOs
    // ========================================================================

    /// <summary>Full profile response for authenticated user viewing their own profile.</summary>
    /// <param name="Id">User's unique identifier.</param>
    /// <param name="Email">User's email address.</param>
    /// <param name="Role">User's role (participant, company, curator, admin).</param>
    /// <param name="FullName">Display name.</param>
    /// <param name="Bio">User's biography/description.</param>
    /// <param name="Timezone">User's timezone (e.g., "Europe/Moscow").</param>
    /// <param name="Skills">List of user's skills.</param>
    /// <param name="Experience">Years of experience.</param>
    /// <param name="Rating">Average rating from project reviews.</param>
    /// <param name="AvatarUrl">URL to user's avatar image.</param>
    /// <param name="IsVerified">Whether user is verified (curator-approved).</param>
    /// <param name="IsActive">Whether account is active.</param>
    /// <param name="CreatedAt">Account creation date.</param>
    /// <param name="UpdatedAt">Last profile update.</param>
    /// <param name="LastLogin">Last login timestamp.</param>
    /// <param name="Language">Preferred UI language.</param>
    /// <param name="Github">GitHub profile URL.</param>
    /// <param name="GithubUsername">GitHub username.</param>
    /// <param name="Linkedin">LinkedIn profile URL.</param>
    /// <param name="Website">Personal website URL.</param>
    /// <param name="Username">Public username handle.</param>
    /// <param name="FollowersCount">Number of followers.</param>
    /// <param name="FollowingCount">Number of accounts followed.</param>
    /// <param name="ProjectsCount">Number of owned or joined projects.</param>
    public record ProfileResponse(
        Guid Id, string Email, string Role, string? FullName, string? Bio, string? Timezone,
        List<string> Skills, int? Experience, float? Rating, string? AvatarUrl, bool IsVerified, bool IsActive,
        DateTime CreatedAt, DateTime? UpdatedAt, DateTime? LastLogin, string? Language, string? Github, string? GithubUsername, string? Linkedin, string? Website,
        string? Username,
        int FollowersCount = 0, int FollowingCount = 0, int ProjectsCount = 0);

    /// <summary>Public profile response (excludes sensitive fields like email).</summary>
    public record ProfilePublicResponse(
        Guid Id, string Role, string? FullName, string? Bio, string? Timezone,
        List<string> Skills, int? Experience, float? Rating, string? AvatarUrl, bool IsActive,
        DateTime CreatedAt, string? Language, string? Github, string? GithubUsername, string? Linkedin, string? Website,
        string? Username);

    /// <summary>Request to update user profile.</summary>
    /// <param name="FullName">New display name (2-100 chars).</param>
    /// <param name="Bio">New biography (max 2000 chars).</param>
    /// <param name="Timezone">Timezone identifier.</param>
    /// <param name="Skills">Updated list of skills.</param>
    /// <param name="Experience">Years of experience.</param>
    /// <param name="Github">GitHub profile URL.</param>
    /// <param name="GithubUsername">GitHub username.</param>
    /// <param name="Linkedin">LinkedIn profile URL.</param>
    /// <param name="Website">Personal website URL.</param>
    /// <param name="AvatarUrl">New avatar URL (or null to keep current).</param>
    /// <param name="Language">Preferred language code (ru, en).</param>
    /// <param name="Username">Unique handle (null = no change, empty string = clear, non-empty = update).</param>
    public record UpdateProfileRequest(
        string? FullName, string? Bio, string? Timezone, List<string>? Skills, int? Experience,
        string? Github, string? GithubUsername, string? Linkedin, string? Website,
        string? AvatarUrl, string? Language, string? Username = null);

    /// <summary>
    /// Returns the current user's full profile, serving cached data when available and refreshing follower/project stats.
    /// </summary>
    /// <returns>Full profile including email and private fields.</returns>
    /// <response code="200">Profile retrieved successfully.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        // Try to get from cache first
        var cacheKey = $"profile:{userId}";
        var cached = await Cache.GetAsync<ProfileResponse>(cacheKey);
        if (cached != null)
        {
            return Ok(cached);
        }

        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserSkillEntries)
                .ThenInclude(e => e.Skill)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return NotFound();
        }

        var stats = await _userStatsService.GetStatsAsync(userId, ct);
        var response = MapToResponse(user) with
        {
            FollowersCount = stats.FollowersCount,
            FollowingCount = stats.FollowingCount,
            ProjectsCount = stats.ProjectsCount,
        };

        // Cache for 10 minutes (profile doesn't change frequently)
        await Cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(10));

        return Ok(response);
    }

    /// <summary>
    /// Returns a user's public profile, applying privacy settings and hiding private fields when the viewer lacks access.
    /// </summary>
    /// <param name="id">User's unique identifier.</param>
    /// <param name="ct">Cancels the public profile query.</param>
    /// <returns>Public profile (excludes email and sensitive fields).</returns>
    /// <response code="200">Profile retrieved successfully.</response>
    /// <response code="404">User not found or inactive.</response>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProfilePublicResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile(Guid id, CancellationToken ct)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserSkillEntries)
                .ThenInclude(e => e.Skill)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null || !user.IsActive)
            return NotFound();

        // A-02: Enforce privacy settings before returning profile data
        var viewerId = SecurityHelpers.GetUserId(User); // null for anonymous
        var state = await _userProfileService.EvaluateProfileVisibilityAsync(id, viewerId);

        if (!state.CanViewFullProfile && !state.IsOwner)
        {
            // Return minimal profile — hide Bio, Timezone, Skills, social links, etc.
            return Ok(new ProfilePublicResponse(
                user.Id, user.Role, FullName: user.FullName,
                Bio: null, Timezone: null, Skills: new List<string>(),
                Experience: null, Rating: null, AvatarUrl: user.AvatarUrl, user.IsActive,
                user.CreatedAt, Language: null,
                Github: null, GithubUsername: null, Linkedin: null, Website: null,
                Username: user.Username));
        }

        var res = new ProfilePublicResponse(
            user.Id, user.Role, user.FullName, user.Bio, user.Timezone,
            GetDisplaySkills(user), user.Experience, user.Rating ?? 0, user.AvatarUrl, user.IsActive,
            user.CreatedAt, user.Language, user.Github, user.GithubUsername, user.Linkedin, user.Website,
            user.Username
        );

        return Ok(res);
    }

    /// <summary>
    /// Updates the current user's profile fields, skills, and username while enforcing avatar and username rules.
    /// </summary>
    /// <param name="request">Profile fields to update (only provided fields are changed).</param>
    /// <param name="ct">Cancels the profile update persistence.</param>
    /// <returns>Updated profile.</returns>
    /// <response code="200">Profile updated successfully.</response>
    /// <response code="400">Validation error (invalid URLs, etc.).</response>
    /// <response code="404">User not found.</response>
    /// <remarks>
    /// Content is filtered for profanity.
    /// Cache is invalidated after successful update.
    /// </remarks>
    [HttpPut("me")]
    [ServiceFilter(typeof(ProfanityFilter))]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var user = await _context.Users
            .Include(u => u.UserSkillEntries)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return NotFound();

        ApplyProfileFieldUpdates(user, request);
        await ApplySkillUpdatesAsync(userId, user, request.Skills);
        if (request.Experience != null) user.Experience = request.Experience;

        // Handle username separately: null = no change, "" = clear, non-empty = update
        if (request.Username != null)
        {
            var trimmed = request.Username.Trim();
            if (trimmed.Length == 0)
            {
                user.Username = null;
            }
            else
            {
                if (!Regex.IsMatch(trimmed, @"^[a-zA-Z0-9_\-\.]{3,50}$"))
                    return BadRequest("Invalid username format.");
                user.Username = trimmed;
            }
        }

        // A-01: Rely on DB unique constraint instead of TOCTOU AnyAsync check
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict("Username is already taken.");
        }
        await OnProfileUpdatedAsync(userId);
        await HttpContext.RequestServices.TriggerAchievementCheckAsync(userId, AchievementTrigger.ProfileUpdated);

        var updatedUser = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserSkillEntries)
                .ThenInclude(e => e.Skill)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        return Ok(MapToResponse(updatedUser ?? user));
    }

    /// <summary>
    /// Builds a de-duplicated display skill list from normalized skill entries and the legacy skills collection.
    /// </summary>
    private static List<string> GetDisplaySkills(User user)
    {
        var fromEntries = user.UserSkillEntries
            .Select(e => e.Skill != null ? e.Skill.Name : e.Raw)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => x.Length > 0);

        // Include legacy list too (existing data before migration).
        var legacy = user.Skills
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => x.Length > 0);

        return fromEntries
            .Concat(legacy)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Replaces a user's normalized skill entries by resolving aliases and exact skill-name matches.
    /// </summary>
    private async Task UpsertUserSkillEntriesAsync(Guid userId, User user, List<string> requested, CancellationToken ct = default)
    {
        var requestedNormalized = requested
            .Select(x => new { Raw = x, Normalized = SkillNormalization.NormalizeSkillToken(x) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Normalized))
            .ToList();

        var normalizedSet = requestedNormalized.Select(x => x.Normalized).Distinct().ToList();
        var rawLowerSet = requested.Select(x => x.Trim().ToLowerInvariant()).Distinct().ToList();

        var aliasMatches = await _context.SkillAliases
            .AsNoTracking()
            .Where(a => normalizedSet.Contains(a.AliasNormalized))
            .Include(a => a.Skill)
            .ToListAsync(ct);

        var nameMatches = await _context.Skills
            .AsNoTracking()
            .Where(s => rawLowerSet.Contains(s.Name.ToLower()))
            .ToListAsync(ct);

        var aliasByNormalized = aliasMatches
            .GroupBy(x => x.AliasNormalized)
            .ToDictionary(g => g.Key, g => g.First().SkillId);

        var skillIdByNameLower = nameMatches
            .GroupBy(x => x.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);

        var desired = requestedNormalized
            .Select(x =>
            {
                Guid? skillId = null;
                if (aliasByNormalized.TryGetValue(x.Normalized, out var byAlias))
                {
                    skillId = byAlias;
                }
                else
                {
                    var key = x.Raw.Trim().ToLowerInvariant();
                    if (skillIdByNameLower.TryGetValue(key, out var byName))
                    {
                        skillId = byName;
                    }
                }

                return new UserSkillEntry
                {
                    UserId = userId,
                    SkillId = skillId,
                    Raw = x.Raw,
                    RawNormalized = x.Normalized,
                    CreatedAt = DateTime.UtcNow
                };
            })
            .GroupBy(x => x.RawNormalized)
            .Select(g => g.First())
            .ToList();

        // Replace existing entries (these are just tags; no additional state to preserve yet).
        if (user.UserSkillEntries.Count > 0)
        {
            _context.UserSkillEntries.RemoveRange(user.UserSkillEntries);
        }

        user.UserSkillEntries = desired;
    }

    /// <summary>
    /// Deactivates the current user's account unless it is already inactive.
    /// </summary>
    [HttpPost("deactivate")]
    public async Task<IActionResult> DeactivateAccount(
        [FromBody] DeactivateAccountRequest? request = null,
        CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return NotFound();

        if (!user.IsActive)
        {
            return BadRequest("Account is already deactivated");
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { Message = "Account deactivated", IsActive = user.IsActive });
    }

    /// <summary>Request to deactivate the current account.</summary>
    /// <param name="Reason">Optional deactivation reason.</param>
    public record DeactivateAccountRequest(string? Reason);

    /// <summary>
    /// Reactivates the current user's account unless it is already active.
    /// </summary>
    [HttpPost("activate")]
    public async Task<IActionResult> ActivateAccount(CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return NotFound();

        if (user.IsActive)
        {
            return BadRequest("Account is already active");
        }

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { Message = "Account activated", IsActive = user.IsActive });
    }

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Maps a user entity to the full profile response shape.
    /// </summary>
    private static ProfileResponse MapToResponse(User user) =>
        new(
            user.Id, user.Email, user.Role, user.FullName, user.Bio, user.Timezone,
            GetDisplaySkills(user), user.Experience, user.Rating ?? 0, user.AvatarUrl, user.IsVerified, user.IsActive,
            user.CreatedAt, user.UpdatedAt, user.LastLogin, user.Language, user.Github, user.GithubUsername, user.Linkedin, user.Website,
            user.Username
        );

    // ========== Avatar Upload ==========

    /// <summary>
    /// Uploads a validated avatar, deletes the previous stored avatar when applicable, updates the profile, and triggers achievements.
    /// </summary>
    [HttpPost("avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB max
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var validationError = ValidateAvatarFile(file);
        if (validationError != null) return validationError;

        var user = await _context.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) return NotFound();

        try
        {
            await DeleteOldAvatarIfExistsAsync(user.AvatarUrl);
            var fileUrl = await UploadAvatarFileAsync(userId, file);
            await UpdateUserAvatarAsync(user, fileUrl, userId);
            await HttpContext.RequestServices.TriggerAchievementCheckAsync(userId, AchievementTrigger.AvatarUploaded);

            return Ok(new { AvatarUrl = fileUrl, Message = "Avatar uploaded successfully" });
        }
        catch (Exception ex)
        {
            return HandleAvatarError(ex, userId, "upload");
        }
    }

    /// <summary>
    /// Deletes the current user's stored avatar and clears the profile avatar URL.
    /// </summary>
    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar(CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var user = await _context.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) return NotFound();
        if (string.IsNullOrEmpty(user.AvatarUrl)) return BadRequest("No avatar to delete");

        try
        {
            await DeleteAvatarFromStorageAsync(user.AvatarUrl);
            await ClearUserAvatarAsync(user, userId);
            return Ok(new { Message = "Avatar deleted successfully" });
        }
        catch (Exception ex)
        {
            return HandleAvatarError(ex, userId, "delete");
        }
    }

    // ========== Privacy Settings ==========

    /// <summary>Privacy settings response for the current user.</summary>
    /// <param name="ProfileVisibility">Profile visibility scope.</param>
    /// <param name="ShowEmail">Whether email is visible.</param>
    /// <param name="ShowSkills">Whether skills are visible.</param>
    /// <param name="ShowExperience">Whether experience is visible.</param>
    /// <param name="ShowRating">Whether rating is visible.</param>
    /// <param name="ShowProjects">Whether projects are visible.</param>
    /// <param name="ShowSocialLinks">Whether social links are visible.</param>
    /// <param name="ShowAchievements">Whether achievements are visible.</param>
    /// <param name="AllowEmailSearch">Whether email search is allowed.</param>
    /// <param name="NotifyOnMessages">Whether to notify on messages.</param>
    /// <param name="NotifyOnInvitations">Whether to notify on invitations.</param>
    /// <param name="ActivityVisibility">Activity visibility scope.</param>
    public record PrivacySettingsResponse(
        string ProfileVisibility, bool ShowEmail, bool ShowSkills, bool ShowExperience,
        bool ShowRating, bool ShowProjects, bool ShowSocialLinks, bool ShowAchievements,
        bool AllowEmailSearch, bool NotifyOnMessages, bool NotifyOnInvitations, string ActivityVisibility);

    /// <summary>Request to update privacy settings.</summary>
    /// <param name="ProfileVisibility">Profile visibility scope.</param>
    /// <param name="ShowEmail">Whether email is visible.</param>
    /// <param name="ShowSkills">Whether skills are visible.</param>
    /// <param name="ShowExperience">Whether experience is visible.</param>
    /// <param name="ShowRating">Whether rating is visible.</param>
    /// <param name="ShowProjects">Whether projects are visible.</param>
    /// <param name="ShowSocialLinks">Whether social links are visible.</param>
    /// <param name="ShowAchievements">Whether achievements are visible.</param>
    /// <param name="AllowEmailSearch">Whether email search is allowed.</param>
    /// <param name="NotifyOnMessages">Whether to notify on messages.</param>
    /// <param name="NotifyOnInvitations">Whether to notify on invitations.</param>
    /// <param name="ActivityVisibility">Activity visibility scope.</param>
    public record UpdatePrivacySettingsRequest(
        string? ProfileVisibility, bool? ShowEmail, bool? ShowSkills, bool? ShowExperience,
        bool? ShowRating, bool? ShowProjects, bool? ShowSocialLinks, bool? ShowAchievements,
        bool? AllowEmailSearch, bool? NotifyOnMessages, bool? NotifyOnInvitations, string? ActivityVisibility);

    /// <summary>
    /// Returns current profile privacy settings, creating defaults when none exist.
    /// </summary>
    [HttpGet("privacy")]
    public async Task<IActionResult> GetPrivacySettings(CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        // A-04: Race-safe upsert via DB unique constraint
        var settings = await GetOrCreatePrivacySettingsAsync(userId, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(MapToPrivacyResponse(settings));
    }

    /// <summary>
    /// Updates profile privacy settings using patch semantics and validates visibility values.
    /// </summary>
    [HttpPut("privacy")]
    public async Task<IActionResult> UpdatePrivacySettings(
        [FromBody] UpdatePrivacySettingsRequest request,
        CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var settings = await GetOrCreatePrivacySettingsAsync(userId, ct);

        var validationError = ValidateAndApplyVisibilitySettings(settings, request);
        if (validationError != null) return validationError;

        ApplyBooleanPrivacySettings(settings, request);
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(MapToPrivacyResponse(settings));
    }

    #region Helper Methods (R12 refactoring - reduce cyclomatic complexity)

    /// <summary>Applies profile field updates using patch semantics and sanitizes the biography.</summary>
    private static void ApplyProfileFieldUpdates(User user, UpdateProfileRequest request)
    {
        ApplyIfNotNull(request.FullName, v => user.FullName = v);
        if (request.Bio is not null)
            user.Bio = SecurityHelpers.SanitizeHtml(request.Bio);
        ApplyIfNotNull(request.Timezone, v => user.Timezone = v);
        ApplyIfNotNull(request.Language, v => user.Language = v);
        // B-10: URL fields must pass IsValidUrl before assignment
        if (request.Github is not null)
            user.Github = SecurityHelpers.IsValidUrl(request.Github) ? request.Github.Trim() : null;
        ApplyIfNotNull(request.GithubUsername, v => user.GithubUsername = v);
        if (request.Linkedin is not null)
            user.Linkedin = SecurityHelpers.IsValidUrl(request.Linkedin) ? request.Linkedin.Trim() : null;
        if (request.Website is not null)
            user.Website = SecurityHelpers.IsValidUrl(request.Website) ? request.Website.Trim() : null;
        // A-03: Ignore AvatarUrl from DTO — avatars changed only via POST /api/profile/avatar
        user.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies a normalized string to a setter when the incoming value is non-null.
    /// </summary>
    private static void ApplyIfNotNull(string? value, Action<string?> setter)
    {
        if (value != null) setter(Normalize(value));
    }

    /// <summary>Applies normalized skill updates when the request includes a skills collection.</summary>
    private async Task ApplySkillUpdatesAsync(Guid userId, User user, List<string>? skills)
    {
        if (skills == null) return;

        var requested = skills
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Take(20)
            .ToList();

        await UpsertUserSkillEntriesAsync(userId, user, requested);
        user.Skills = GetDisplaySkills(user);
    }

    /// <summary>Invalidates profile caches, publishes a profile-updated event, and logs profile activity.</summary>
    private async Task OnProfileUpdatedAsync(Guid userId)
    {
        // Invalidate cache
        await Cache.RemoveAsync($"profile:{userId}");
        await Cache.RemoveAsync($"profile:public:{userId}");

        // Publish event
        await EventBus.PublishAsync(DomainEvents.ProfileUpdated(userId));

        // Log activity
        var visibility = await GetProfileVisibilityAsync(userId);
        await ActivityLog.LogUserEventAsync(userId, "user.updated_profile", "Updated profile information", visibility: visibility);
    }

    /// <summary>Returns a normalized profile visibility value used for profile update activity records.</summary>
    private async Task<string> GetProfileVisibilityAsync(Guid userId, CancellationToken ct = default)
    {
        var privacy = await _context.UserPrivacySettings.FirstOrDefaultAsync(ps => ps.UserId == userId, ct);
        var visibility = (privacy?.ProfileVisibility ?? "public").Trim().ToLowerInvariant();
        return new[] { "public", "followers", "private" }.Contains(visibility) ? visibility : "public";
    }

    // A-04: Race-safe upsert — if two concurrent requests create, second will find existing row
    /// <summary>
    /// Gets existing privacy settings or creates defaults, handling concurrent create attempts through the unique constraint.
    /// </summary>
    private async Task<UserPrivacySettings> GetOrCreatePrivacySettingsAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await _context.UserPrivacySettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (settings != null) return settings;

        settings = new UserPrivacySettings { Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow };
        _context.UserPrivacySettings.Add(settings);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Another request created the row first — reload it
            _context.Entry(settings).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            settings = await _context.UserPrivacySettings.FirstAsync(s => s.UserId == userId, ct);
        }
        return settings;
    }

    /// <summary>
    /// Validates and applies profile and activity visibility updates.
    /// </summary>
    private BadRequestObjectResult? ValidateAndApplyVisibilitySettings(UserPrivacySettings settings, UpdatePrivacySettingsRequest request)
    {
        var error = ValidateAndApplyProfileVisibility(settings, request.ProfileVisibility);
        if (error != null) return error;

        return ValidateAndApplyActivityVisibility(settings, request.ActivityVisibility);
    }

    /// <summary>
    /// Validates a profile visibility value and applies it to privacy settings.
    /// </summary>
    private static BadRequestObjectResult? ValidateAndApplyProfileVisibility(UserPrivacySettings settings, string? visibility)
    {
        if (string.IsNullOrWhiteSpace(visibility)) return null;

        var level = ProfileVisibilityLevel.FromString(visibility);
        if (level == null)
            return new BadRequestObjectResult("Invalid ProfileVisibility. Allowed: public, private, friends_only");

        settings.ProfileVisibility = level;
        return null;
    }

    /// <summary>
    /// Validates an activity visibility value and applies it to privacy settings.
    /// </summary>
    private static BadRequestObjectResult? ValidateAndApplyActivityVisibility(UserPrivacySettings settings, string? visibility)
    {
        if (string.IsNullOrWhiteSpace(visibility)) return null;

        var level = ActivityVisibilityLevel.FromString(visibility);
        if (level == null)
            return new BadRequestObjectResult("Invalid ActivityVisibility. Allowed: public, followers, private");

        settings.ActivityVisibility = level;
        return null;
    }

    /// <summary>
    /// Applies nullable boolean privacy toggles when values are supplied.
    /// </summary>
    private static void ApplyBooleanPrivacySettings(UserPrivacySettings settings, UpdatePrivacySettingsRequest request)
    {
        ApplyBoolIfHasValue(request.ShowEmail, v => settings.ShowEmail = v);
        ApplyBoolIfHasValue(request.ShowSkills, v => settings.ShowSkills = v);
        ApplyBoolIfHasValue(request.ShowExperience, v => settings.ShowExperience = v);
        ApplyBoolIfHasValue(request.ShowRating, v => settings.ShowRating = v);
        ApplyBoolIfHasValue(request.ShowProjects, v => settings.ShowProjects = v);
        ApplyBoolIfHasValue(request.ShowSocialLinks, v => settings.ShowSocialLinks = v);
        ApplyBoolIfHasValue(request.ShowAchievements, v => settings.ShowAchievements = v);
        ApplyBoolIfHasValue(request.AllowEmailSearch, v => settings.AllowEmailSearch = v);
        ApplyBoolIfHasValue(request.NotifyOnMessages, v => settings.NotifyOnMessages = v);
        ApplyBoolIfHasValue(request.NotifyOnInvitations, v => settings.NotifyOnInvitations = v);
    }

    /// <summary>
    /// Applies a boolean value to a setter when it is present.
    /// </summary>
    private static void ApplyBoolIfHasValue(bool? value, Action<bool> setter)
    {
        if (value.HasValue) setter(value.Value);
    }

    /// <summary>
    /// Maps privacy settings to the API response shape.
    /// </summary>
    private static PrivacySettingsResponse MapToPrivacyResponse(UserPrivacySettings settings) =>
        new(settings.ProfileVisibility, settings.ShowEmail, settings.ShowSkills,
            settings.ShowExperience, settings.ShowRating, settings.ShowProjects,
            settings.ShowSocialLinks, settings.ShowAchievements, settings.AllowEmailSearch,
            settings.NotifyOnMessages, settings.NotifyOnInvitations, settings.ActivityVisibility);

    /// <summary>
    /// Validates avatar upload presence, size, and content type.
    /// </summary>
    private static IActionResult? ValidateAvatarFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return new BadRequestObjectResult("No file provided");

        if (file.Length > 5 * 1024 * 1024)
            return new BadRequestObjectResult("File size exceeds 5 MB limit");

        var allowedImageTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        var normalizedContentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        if (!allowedImageTypes.Contains(normalizedContentType))
            return new BadRequestObjectResult($"Invalid file type. Allowed: {string.Join(", ", allowedImageTypes)}");

        return null;
    }

    /// <summary>
    /// Deletes an existing avatar object when the stored URL points to the avatars bucket key namespace.
    /// </summary>
    private async Task DeleteOldAvatarIfExistsAsync(string? avatarUrl)
    {
        if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith("avatars/"))
            await ObjectStorage.DeleteAsync(ObjectStorageBuckets.Avatars, avatarUrl);
    }

    /// <summary>
    /// Uploads an avatar file under the current user's avatar key prefix and returns its storage URL.
    /// </summary>
    private async Task<string> UploadAvatarFileAsync(Guid userId, IFormFile file)
    {
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{userId}/{Guid.NewGuid()}{fileExtension}";

        using var stream = file.OpenReadStream();
        return await ObjectStorage.UploadAsync(
            ObjectStorageBuckets.Avatars,
            fileName,
            stream,
            file.ContentType ?? "application/octet-stream");
    }

    /// <summary>
    /// Stores the new avatar URL, clears profile caches, and publishes the avatar-uploaded event.
    /// </summary>
    private async Task UpdateUserAvatarAsync(User user, string fileUrl, Guid userId, CancellationToken ct = default)
    {
        user.AvatarUrl = fileUrl;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await Cache.RemoveAsync($"profile:{userId}");
        await Cache.RemoveAsync($"profile:public:{userId}");
        await EventBus.PublishAsync(DomainEvents.AvatarUploaded(userId));
    }

    /// <summary>
    /// Logs avatar operation failures and returns an environment-appropriate error response.
    /// </summary>
    private IActionResult HandleAvatarError(Exception ex, Guid userId, string operation)
    {
        _logger.LogError(ex, "Failed to {Operation} avatar for user {UserId}", operation, userId);
        if (Environment.IsDevelopment())
            return StatusCode(500, new { Error = $"Failed to {operation} avatar", Details = ex.Message });
        return StatusCode(500, new { Error = $"Failed to {operation} avatar. Please try again later." });
    }

    // A-08: Use same key extraction logic as DeleteOldAvatarIfExistsAsync — pass full relative key
    /// <summary>
    /// Deletes an avatar object when the stored URL points to the avatars bucket key namespace.
    /// </summary>
    private async Task DeleteAvatarFromStorageAsync(string avatarUrl)
    {
        if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith("avatars/"))
            await ObjectStorage.DeleteAsync(ObjectStorageBuckets.Avatars, avatarUrl);
    }

    /// <summary>
    /// Detects database unique-constraint failures across supported provider message formats.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Clears the user's avatar URL, invalidates profile caches, and publishes the avatar-deleted event.
    /// </summary>
    private async Task ClearUserAvatarAsync(User user, Guid userId, CancellationToken ct = default)
    {
        user.AvatarUrl = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await Cache.RemoveAsync($"profile:{userId}");
        await Cache.RemoveAsync($"profile:public:{userId}");
        await EventBus.PublishAsync(DomainEvents.AvatarDeleted(userId));
    }

    /// <summary>
    /// Trims a string and maps blank values to <see langword="null"/>.
    /// </summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    #endregion
}
