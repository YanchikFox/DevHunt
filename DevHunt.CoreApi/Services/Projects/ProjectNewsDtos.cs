using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>Attachment descriptor stored in news posts.</summary>
public record ProjectNewsAttachmentDto(string Type, Guid? FileId, string? Url);

/// <summary>Resolved attachment details returned to clients.</summary>
public record ProjectNewsAttachmentResponse(
    string Type,
    Guid? FileId,
    string? Url,
    string? FileName,
    string? ContentType,
    string? DownloadUrl);

/// <summary>News post response with attachments.</summary>
public record ProjectNewsResponse(
    Guid Id,
    Guid ProjectId,
    Guid AuthorId,
    string AuthorName,
    string AuthorAvatarUrl,
    string Title,
    string Content,
    string Visibility,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<ProjectNewsAttachmentResponse> Attachments);

/// <summary>Request to create a news post.</summary>
public record CreateNewsPostRequest(
    string Title,
    string Content,
    [RegularExpression("^(public|subscribers|members)$", ErrorMessage = "Visibility must be one of: public, subscribers, members")]
    string Visibility,
    bool IsPinned = false,
    List<Guid>? AttachmentIds = null,
    List<string>? AttachmentUrls = null);

/// <summary>Request to update a news post.</summary>
public record UpdateNewsPostRequest(
    string? Title,
    string? Content,
    [RegularExpression("^(public|subscribers|members)$", ErrorMessage = "Visibility must be one of: public, subscribers, members")]
    string? Visibility,
    bool? IsPinned = null,
    List<Guid>? AttachmentIds = null,
    List<string>? AttachmentUrls = null);

/// <summary>Query for fetching project news posts.</summary>
public record GetProjectNewsQuery(
    Guid ProjectId,
    string? Visibility,
    int Page,
    int PageSize,
    Guid? RequesterId,
    bool IsPrivileged);

/// <summary>Command for creating a news post.</summary>
public record CreateProjectNewsCommand(
    Guid ProjectId,
    CreateNewsPostRequest Request,
    Guid UserId,
    bool IsPrivileged);

/// <summary>Command for updating a news post.</summary>
public record UpdateProjectNewsCommand(
    Guid ProjectId,
    Guid NewsId,
    UpdateNewsPostRequest Request,
    Guid UserId,
    bool IsPrivileged);

/// <summary>Command for deleting a news post.</summary>
public record DeleteProjectNewsCommand(
    Guid ProjectId,
    Guid NewsId,
    Guid UserId,
    bool IsPrivileged);

/// <summary>Typed service outcome for news operations.</summary>
public class NewsResult<T>
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
    public static NewsResult<T> Success(T data) => new() { IsSuccess = true, Data = data, StatusCode = 200 };
    /// <summary>Constructs a 201 created result.</summary>
    public static NewsResult<T> Created(T data) => new() { IsSuccess = true, Data = data, StatusCode = 201 };
    /// <summary>Constructs a failed result with message and status code.</summary>
    public static NewsResult<T> Failure(string message, int statusCode) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}

/// <summary>Non-generic <see cref="NewsResult{T}"/> for empty success bodies.</summary>
public class NewsResult
{
    /// <summary>True when the operation completed without error.</summary>
    public bool IsSuccess { get; set; }
    /// <summary>Failure message when <see cref="IsSuccess"/> is false.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Suggested HTTP status for API mapping.</summary>
    public int StatusCode { get; set; }

    /// <summary>Constructs a 200 success result.</summary>
    public static NewsResult Success() => new() { IsSuccess = true, StatusCode = 200 };
    /// <summary>Constructs a failed result with message and status code.</summary>
    public static NewsResult Failure(string message, int statusCode) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}
