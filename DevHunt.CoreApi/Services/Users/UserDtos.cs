using System;
using System.Collections.Generic;
using DevHunt.CoreApi.Services;

namespace DevHunt.CoreApi.Services.Users;

/// <summary>
/// User skill detail item.
/// </summary>
public record UserSkillDto(string Name, string Category, string ProficiencyLevel, int? YearsOfExperience, bool Verified);

/// <summary>
/// Summary user projection for list/search results.
/// </summary>
public record UserSummaryDto(
    Guid Id,
    string Role,
    string? FullName,
    string? Bio,
    string? Timezone,
    bool IsVerified = false,
    IReadOnlyCollection<UserSkillDto>? Skills = null);

/// <summary>
/// Detailed user profile with visibility flags and stats.
/// </summary>
public record UserProfileDto(
    Guid Id,
    string? Email,
    string Role,
    string? FullName,
    string? Bio,
    string? Timezone,
    int? Experience,
    decimal? Rating,
    string? AvatarUrl,
    string? Github,
    string? Linkedin,
    string? Website,
    DateTime CreatedAt,
    IReadOnlyCollection<UserSkillDto>? Skills,
    string ProfileVisibility,
    string RelationVisibility,
    bool CanViewFullProfile,
    bool CanRequestAccess,
    string? VisibilityMessage,
    bool IsFollower,
    bool IsFollowing,
    UserStatsDto? Stats,
    bool IsVerified,
    bool IsActive,
    DateTime? UpdatedAt,
    DateTime? LastLogin,
    string? Language);

/// <summary>
/// Minimal user projection for follow lists.
/// </summary>
public record FollowSummaryDto(Guid Id, string Role, string? FullName, string? AvatarUrl);

/// <summary>
/// User activity feed item.
/// </summary>
public record UserActivityDto(
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

/// <summary>
/// Suggested user card for discovery.
/// </summary>
public record SuggestedUserDto(
    Guid Id,
    string Name,
    string? AvatarUrl,
    int MutualProjectsCount,
    int RecentActivityScore,
    bool IsVerified = false);
