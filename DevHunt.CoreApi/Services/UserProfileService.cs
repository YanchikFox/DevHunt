using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using DevHunt.CoreApi.Services.Users;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Service for handling user profile operations including visibility evaluation and DTO building.
/// Extracted from UsersController to reduce complexity and improve testability.
/// </summary>
public interface IUserProfileService
{
    /// <summary>
    /// Evaluate profile visibility rules for a viewer.
    /// </summary>
    /// <param name="userId">Target user identifier.</param>
    /// <param name="viewerId">Viewer user identifier (optional).</param>
    /// <param name="ct">Cancels profile visibility queries.</param>
    /// <returns>Visibility state for the profile.</returns>
    Task<ProfileVisibilityState> EvaluateProfileVisibilityAsync(Guid userId, Guid? viewerId, CancellationToken ct = default);
    /// <summary>
    /// Get follow relationship flags between viewer and target user.
    /// </summary>
    /// <param name="targetUserId">Target user identifier.</param>
    /// <param name="viewerId">Viewer user identifier (optional).</param>
    /// <param name="ct">Cancels follow relationship queries.</param>
    /// <returns>Follow relationship flags.</returns>
    Task<FollowRelationship> GetFollowRelationshipAsync(Guid targetUserId, Guid? viewerId, CancellationToken ct = default);
    /// <summary>
    /// Build a visible skill list based on privacy rules.
    /// </summary>
    /// <param name="user">User entity.</param>
    /// <param name="canViewFullProfile">Whether viewer can see full profile.</param>
    /// <param name="isOwner">Whether viewer is the profile owner.</param>
    /// <param name="showSkills">Whether skills are visible per privacy settings.</param>
    /// <returns>Skill list or null.</returns>
    IReadOnlyCollection<UserSkillDto>? BuildSkillsList(User user, bool canViewFullProfile, bool isOwner, bool showSkills);
    /// <summary>
    /// Build a profile DTO based on visibility state.
    /// </summary>
    /// <param name="user">User entity.</param>
    /// <param name="state">Visibility state.</param>
    /// <param name="followRelation">Follow relationship flags.</param>
    /// <returns>Profile DTO.</returns>
    Task<UserProfileDto> BuildUserProfileDtoAsync(User user, ProfileVisibilityState state, FollowRelationship followRelation);
    /// <summary>
    /// Get a visibility message for restricted profiles.
    /// </summary>
    /// <param name="normalizedVisibility">Normalized visibility value.</param>
    /// <param name="canViewFullProfile">Whether viewer can see full profile.</param>
    /// <returns>Visibility message or null.</returns>
    string? GetVisibilityMessage(string normalizedVisibility, bool canViewFullProfile);
}

/// <summary>Computed visibility state for a profile.</summary>
public record ProfileVisibilityState(
    UserPrivacySettings Settings,
    bool IsOwner,
    bool CanViewFullProfile,
    bool CanRequestAccess,
    string NormalizedVisibility);

/// <summary>Relationship between viewer and target user.</summary>
public record FollowRelationship(bool IsFollower, bool IsFollowing);

/// <summary>
/// Default implementation of <see cref="IUserProfileService"/>.
/// </summary>
public class UserProfileService : IUserProfileService
{
    private readonly DevHuntDbContext _dbContext;
    private readonly IUserStatsService _userStatsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserProfileService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for privacy settings, follows, and profile data.</param>
    /// <param name="userStatsService">Loads visible profile statistics.</param>
    public UserProfileService(DevHuntDbContext dbContext, IUserStatsService userStatsService)
    {
        _dbContext = dbContext;
        _userStatsService = userStatsService;
    }

    /// <inheritdoc />
    public async Task<ProfileVisibilityState> EvaluateProfileVisibilityAsync(Guid userId, Guid? viewerId, CancellationToken ct = default)
    {
        var settings = await _dbContext.UserPrivacySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct)
            ?? new UserPrivacySettings { UserId = userId };

        var normalizedVisibility = NormalizeVisibility(settings.ProfileVisibility);
        var isOwner = viewerId.HasValue && viewerId.Value == userId;

        // A-05: Check follow relationship so "followers" visibility level actually works
        var isFollower = false;
        if (!isOwner && viewerId.HasValue && normalizedVisibility == "followers")
        {
            isFollower = await _dbContext.UserFollows
                .AnyAsync(f => f.FollowerId == viewerId.Value && f.FollowedId == userId, ct);
        }

        var canViewFullProfile = CanViewProfile(normalizedVisibility, isOwner, isFollower);
        var canRequestAccess = !canViewFullProfile && viewerId.HasValue;

        return new ProfileVisibilityState(settings, isOwner, canViewFullProfile, canRequestAccess, normalizedVisibility);
    }

    /// <inheritdoc />
    public async Task<FollowRelationship> GetFollowRelationshipAsync(Guid targetUserId, Guid? viewerId, CancellationToken ct = default)
    {
        if (!viewerId.HasValue)
        {
            return new FollowRelationship(false, false);
        }

        var viewer = viewerId.Value;
        var isFollower = await _dbContext.UserFollows
            .AnyAsync(f => f.FollowerId == viewer && f.FollowedId == targetUserId, ct);
        var isFollowing = await _dbContext.UserFollows
            .AnyAsync(f => f.FollowerId == targetUserId && f.FollowedId == viewer, ct);

        return new FollowRelationship(isFollower, isFollowing);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<UserSkillDto>? BuildSkillsList(
        User user,
        bool canViewFullProfile,
        bool isOwner,
        bool showSkills)
    {
        if (!ShouldShowSkills(canViewFullProfile, isOwner, showSkills))
        {
            return null;
        }

        var results = new List<UserSkillDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddUserSkills(user.UserSkills, results, seen);
        AddUserSkillEntries(user.UserSkillEntries, results, seen);

        return results.Count == 0 ? null : results;
    }

    /// <inheritdoc />
    public async Task<UserProfileDto> BuildUserProfileDtoAsync(
        User user,
        ProfileVisibilityState state,
        FollowRelationship followRelation)
    {
        var privacySettings = state.Settings;
        var canView = state.CanViewFullProfile;
        var isOwner = state.IsOwner;

        var visibilityMessage = GetVisibilityMessage(state.NormalizedVisibility, canView);
        var skillsList = BuildSkillsList(user, canView, isOwner, privacySettings.ShowSkills);
        var stats = await LoadStatsAsync(user.Id, state, followRelation.IsFollower);
        var rating = GetRatingIfVisible(user, canView, isOwner, privacySettings.ShowRating);

        return new UserProfileDto(
            Id: user.Id,
            Email: GetFieldIfVisible(user.Email, canView, isOwner, privacySettings.ShowEmail),
            Role: user.Role,
            FullName: user.FullName,
            Bio: canView ? user.Bio : null,
            Timezone: canView ? user.Timezone : null,
            Experience: GetFieldIfVisible(user.Experience, canView, isOwner, privacySettings.ShowExperience),
            Rating: rating,
            AvatarUrl: user.AvatarUrl,
            Github: GetFieldIfVisible(user.Github, canView, isOwner, privacySettings.ShowSocialLinks),
            Linkedin: GetFieldIfVisible(user.Linkedin, canView, isOwner, privacySettings.ShowSocialLinks),
            Website: GetFieldIfVisible(user.Website, canView, isOwner, privacySettings.ShowSocialLinks),
            CreatedAt: user.CreatedAt,
            Skills: skillsList,
            ProfileVisibility: state.NormalizedVisibility,
            RelationVisibility: state.NormalizedVisibility,
            CanViewFullProfile: canView,
            CanRequestAccess: state.CanRequestAccess,
            VisibilityMessage: visibilityMessage,
            IsFollower: followRelation.IsFollower,
            IsFollowing: followRelation.IsFollowing,
            Stats: stats,
            IsVerified: user.IsVerified,
            IsActive: user.IsActive,
            UpdatedAt: canView ? user.UpdatedAt : null,
            LastLogin: canView ? user.LastLogin : null,
            Language: canView ? user.Language : null);
    }

    /// <inheritdoc />
    public string? GetVisibilityMessage(string normalizedVisibility, bool canViewFullProfile)
    {
        if (canViewFullProfile)
        {
            return null;
        }

        return normalizedVisibility switch
        {
            "friends_only" => "This profile is shared only with approved contacts.",
            "private" => "This profile is private.",
            _ => null
        };
    }

    #region Private Helpers

    /// <summary>
    /// Converts a missing profile visibility value to public and lowercases the resulting value.
    /// </summary>
    private static string NormalizeVisibility(string? visibility)
        => (visibility ?? "public").ToLowerInvariant();

    // A-05: Added isFollower parameter so "followers" visibility level works correctly
    /// <summary>
    /// Determines whether a viewer can see a full profile from visibility, ownership, and follower status.
    /// </summary>
    private static bool CanViewProfile(string normalizedVisibility, bool isOwner, bool isFollower)
        => normalizedVisibility == "public"
           || isOwner
           || (normalizedVisibility == "followers" && isFollower);

    /// <summary>
    /// Determines whether skills should be returned for a profile view.
    /// </summary>
    private static bool ShouldShowSkills(bool canViewFullProfile, bool isOwner, bool showSkills)
        => canViewFullProfile && (isOwner || showSkills);

    /// <summary>
    /// Adds linked user skills to the result list while skipping missing or duplicate skill names.
    /// </summary>
    private static void AddUserSkills(
        ICollection<UserSkill>? userSkills,
        List<UserSkillDto> results,
        HashSet<string> seen)
    {
        if (userSkills == null) return;

        foreach (var us in userSkills)
        {
            if (us.Skill == null || !seen.Add(us.Skill.Name))
            {
                continue;
            }

            results.Add(new UserSkillDto(
                us.Skill.Name,
                us.Skill.Category,
                us.ProficiencyLevel,
                us.YearsOfExperience,
                us.Verified));
        }
    }

    /// <summary>
    /// Adds skill entries to the result list using linked skill data or raw names while skipping blanks and duplicates.
    /// </summary>
    private static void AddUserSkillEntries(
        ICollection<UserSkillEntry>? entries,
        List<UserSkillDto> results,
        HashSet<string> seen)
    {
        if (entries == null) return;

        foreach (var entry in entries)
        {
            var name = entry.Skill?.Name ?? entry.Raw;
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
            {
                continue;
            }

            results.Add(new UserSkillDto(
                name,
                entry.Skill?.Category ?? "other",
                "unspecified",
                null,
                Verified: false));
        }
    }

    /// <summary>
    /// Loads user stats only when the profile visibility state allows stats to be shown.
    /// </summary>
    private async Task<UserStatsDto?> LoadStatsAsync(Guid userId, ProfileVisibilityState state, bool isFollower)
    {
        var statsVisible = state.NormalizedVisibility switch
        {
            "public" => true,
            "followers" => isFollower || state.IsOwner,
            "private" => state.IsOwner,
            _ => false
        };

        return statsVisible ? await _userStatsService.GetStatsAsync(userId) : null;
    }

    /// <summary>
    /// Returns the user's rating only when profile access and rating visibility allow it.
    /// </summary>
    private static decimal? GetRatingIfVisible(User user, bool canView, bool isOwner, bool showRating)
    {
        if (!canView || (!isOwner && !showRating))
        {
            return null;
        }
        return user.Rating.HasValue ? (decimal?)user.Rating.Value : null;
    }

    /// <summary>
    /// Returns a reference-type profile field only when profile access and field visibility allow it.
    /// </summary>
    private static T? GetFieldIfVisible<T>(T? value, bool canView, bool isOwner, bool showField) where T : class
        => canView && (isOwner || showField) ? value : null;

    /// <summary>
    /// Returns a nullable integer profile field only when profile access and field visibility allow it.
    /// </summary>
    private static int? GetFieldIfVisible(int? value, bool canView, bool isOwner, bool showField)
        => canView && (isOwner || showField) ? value : null;

    #endregion
}
