using System.ComponentModel.DataAnnotations;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Query parameters that filter, sort, and paginate the project list endpoint.
/// </summary>
public sealed class ProjectQueryParams
{
    /// <summary>
    /// Filters projects by lifecycle status.
    /// </summary>
    [RegularExpression("^(draft|recruiting|active|completed|archived|cancelled)$")]
    public string? Status { get; set; }

    /// <summary>
    /// Filters projects by visibility level.
    /// </summary>
    [RegularExpression("^(public|private|unlisted)$")]
    public string? Visibility { get; set; }

    /// <summary>
    /// Searches project title or description text.
    /// </summary>
    [MaxLength(200)]
    public string? Query { get; set; }

    /// <summary>
    /// Filters projects by technology stack text.
    /// </summary>
    [MaxLength(200)]
    public string? Tech { get; set; }

    /// <summary>
    /// Filters projects by difficulty level.
    /// </summary>
    [RegularExpression("^(beginner|intermediate|advanced)$")]
    public string? Difficulty { get; set; }

    /// <summary>
    /// Filters projects by showcase publication state when provided.
    /// </summary>
    public bool? Showcase { get; set; }
    /// <summary>
    /// Filters projects by featured state when provided.
    /// </summary>
    public bool? Featured { get; set; }
    /// <summary>
    /// Restricts results to projects associated with the authenticated user.
    /// </summary>
    public bool? MyProjects { get; set; }
    /// <summary>
    /// Filters projects owned by a specific user.
    /// </summary>
    public Guid? OwnerId { get; set; }
    /// <summary>
    /// Excludes draft projects when set.
    /// </summary>
    public bool? ExcludeDrafts { get; set; }
    /// <summary>
    /// Includes cancelled projects when set.
    /// </summary>
    public bool? IncludeCancelled { get; set; }

    /// <summary>
    /// Filters projects by the owner's timezone.
    /// </summary>
    [MaxLength(50)]
    public string? OwnerTimezone { get; set; }

    /// <summary>
    /// One-based page number.
    /// </summary>
    public int Page { get; set; } = 1;
    /// <summary>
    /// Number of projects requested per page.
    /// </summary>
    public int PageSize { get; set; } = 20;
    /// <summary>
    /// Field name used for project ordering.
    /// </summary>
    public string SortBy { get; set; } = "createdAt";
    /// <summary>
    /// Sort direction, usually <c>asc</c> or <c>desc</c>.
    /// </summary>
    public string SortOrder { get; set; } = "desc";
}
