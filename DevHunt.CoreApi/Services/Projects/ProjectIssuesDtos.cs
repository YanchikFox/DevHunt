using System;
using System.ComponentModel.DataAnnotations;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>Request to create a new project issue.</summary>
public record CreateIssueRequest(
    [Required, MaxLength(50)] string Type, // technical, legal, conflict, abuse, violation, security, other
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(5000)] string Description,
    Guid? RelatedUserId); // User related to the issue

/// <summary>Request to cancel an issue.</summary>
/// <param name="Reason">Optional explanation stored in the audit log.</param>
public record CancelIssueRequest(string? Reason);

/// <summary>Request to update issue fields.</summary>
public record UpdateIssueRequest(
    [MaxLength(200)] string? Title,
    [MaxLength(5000)] string? Description,
    string? Status,
    string? Priority,
    Guid? RelatedUserId);

/// <summary>Inputs bundled for <see cref="IProjectIssuesService.UpdateIssueAsync"/>.</summary>
/// <param name="ProjectId">Project owning the issue.</param>
/// <param name="IssueId">Issue row to update.</param>
/// <param name="Request">Partial field updates.</param>
/// <param name="UserId">Authenticated user performing the update.</param>
/// <param name="IsAdmin">Whether admin-only status transitions are allowed.</param>
public record UpdateIssueContext(
    Guid ProjectId,
    Guid IssueId,
    UpdateIssueRequest Request,
    Guid UserId,
    bool IsAdmin);

/// <summary>Typed service outcome for issue operations.</summary>
public class IssueResult<T>
{
    /// <summary>True when <see cref="Data"/> is populated.</summary>
    public bool IsSuccess { get; set; }
    /// <summary>Payload on success.</summary>
    public T? Data { get; set; }
    /// <summary>Failure message when <see cref="IsSuccess"/> is false.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Suggested HTTP status for API mapping.</summary>
    public int StatusCode { get; set; }

    /// <summary>Constructs a 200 success result.</summary>
    public static IssueResult<T> Success(T data) => new() { IsSuccess = true, Data = data, StatusCode = 200 };
    /// <summary>Constructs a 201 created result.</summary>
    public static IssueResult<T> Created(T data) => new() { IsSuccess = true, Data = data, StatusCode = 201 };
    /// <summary>Constructs a failed result with message and status code.</summary>
    public static IssueResult<T> Failure(string message, int statusCode) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}

/// <summary>Non-generic <see cref="IssueResult{T}"/>; success uses 204 for empty bodies.</summary>
public class IssueResult
{
    /// <summary>True when the operation completed without error.</summary>
    public bool IsSuccess { get; set; }
    /// <summary>Failure message when <see cref="IsSuccess"/> is false.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Suggested HTTP status for API mapping.</summary>
    public int StatusCode { get; set; }

    /// <summary>Constructs a 204 no-content success result.</summary>
    public static IssueResult Success() => new() { IsSuccess = true, StatusCode = 204 }; // 204 for NoContent
    /// <summary>Constructs a failed result with message and status code.</summary>
    public static IssueResult Failure(string message, int statusCode) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}
