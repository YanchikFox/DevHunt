using System;
using System.ComponentModel.DataAnnotations;

namespace DevHunt.CoreApi.Services.Badges;

/// <summary>Request to create a new achievement/badge.</summary>
public class CreateAchievementDto
{
    /// <summary>Unique badge code.</summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable badge title.</summary>
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional badge description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional icon URL.</summary>
    public string? IconUrl { get; set; }

    /// <summary>Optional category label.</summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>Point value used for stats.</summary>
    public int Points { get; set; } = 0;
}

/// <summary>Request to update an existing badge.</summary>
public class UpdateAchievementDto
{
    /// <summary>New badge code (optional).</summary>
    [MaxLength(50)]
    public string? Code { get; set; }

    /// <summary>New badge title (optional).</summary>
    [MaxLength(255)]
    public string? Title { get; set; }

    /// <summary>New description (optional).</summary>
    public string? Description { get; set; }

    /// <summary>New icon URL (optional).</summary>
    public string? IconUrl { get; set; }

    /// <summary>New category (optional).</summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>New points value (optional).</summary>
    public int? Points { get; set; }
}

/// <summary>Request to update badge progress.</summary>
public class UpdateProgressDto
{
    /// <summary>Progress percentage (0-100).</summary>
    [Required]
    [Range(0, 100)]
    public int Progress { get; set; }
}

/// <summary>Unified request for updating badge progress in the service layer.</summary>
public class UpdateBadgeProgressRequest
{
    /// <summary>User whose badge progress is being updated.</summary>
    public Guid UserId { get; set; }
    /// <summary>Achievement code to update.</summary>
    public string AchievementCode { get; set; } = string.Empty;
    /// <summary>New progress percentage (0-100).</summary>
    public int Progress { get; set; }
    /// <summary>Authenticated user performing the update.</summary>
    public Guid CurrentUserId { get; set; }
    /// <summary>Whether the caller presented an admin claim.</summary>
    public bool IsAdmin { get; set; }
}

/// <summary>Unified request for awarding a badge in the service layer.</summary>
public class AwardBadgeRequest
{
    /// <summary>User receiving the badge.</summary>
    public Guid UserId { get; set; }
    /// <summary>Achievement code to award.</summary>
    public string AchievementCode { get; set; } = string.Empty;
    /// <summary>Optional initial progress percentage.</summary>
    public int? Progress { get; set; }
    /// <summary>User or system actor performing the award.</summary>
    public Guid CurrentUserId { get; set; }
}

/// <summary>Typed response for the CreateBadge endpoint — eliminates (dynamic) cast in the controller.</summary>
public record BadgeCreatedResponse(Guid Id, string Code, string Title, string? Description, string? IconUrl, string? Category, int Points);

/// <summary>Typed service-layer result with HTTP status code mapping for controllers.</summary>
public class BadgeResult<T>
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; set; }
    /// <summary>Payload returned on success.</summary>
    public T? Data { get; set; }
    /// <summary>Human-readable error when <see cref="IsSuccess"/> is false.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Suggested HTTP status code for API responses.</summary>
    public int StatusCode { get; set; }

    /// <summary>Creates a successful 200 result.</summary>
    /// <param name="data">Result payload.</param>
    /// <returns>Success wrapper.</returns>
    public static BadgeResult<T> Success(T data) => new() { IsSuccess = true, Data = data, StatusCode = 200 };
    /// <summary>Creates a failed result with the given HTTP status.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="statusCode">HTTP status code.</param>
    /// <returns>Failure wrapper.</returns>
    public static BadgeResult<T> Failure(string message, int statusCode) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}

/// <summary>Non-generic service-layer result for operations without a payload.</summary>
public class BadgeResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; set; }
    /// <summary>Human-readable error when <see cref="IsSuccess"/> is false.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Suggested HTTP status code for API responses.</summary>
    public int StatusCode { get; set; }

    /// <summary>Creates a successful 204-style result.</summary>
    /// <returns>Success wrapper.</returns>
    public static BadgeResult Success() => new() { IsSuccess = true, StatusCode = 204 }; // NoContent usually
    /// <summary>Creates a failed result with the given HTTP status.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="statusCode">HTTP status code.</param>
    /// <returns>Failure wrapper.</returns>
    public static BadgeResult Failure(string message, int statusCode) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}
