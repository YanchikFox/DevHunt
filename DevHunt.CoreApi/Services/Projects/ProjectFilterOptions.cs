namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Options for filtering projects in catalog queries.
/// Replaces 15 method arguments with a single, cohesive parameter object.
/// </summary>
public sealed record ProjectFilterOptions
{
    // Requester context
    /// <summary>Authenticated user id used for access and "my projects" filters.</summary>
    public Guid? RequesterId { get; init; }
    /// <summary>When true, skips anonymous/public-only access restrictions (admins/curators).</summary>
    public bool IsPrivilegedViewer { get; init; }

    // Status filters
    /// <summary>Exact project status match (e.g. draft, active).</summary>
    public string? Status { get; init; }
    /// <summary>When true, excludes draft projects from results.</summary>
    public bool? ExcludeDrafts { get; init; }
    /// <summary>When true, includes cancelled projects that are normally filtered out.</summary>
    public bool? IncludeCancelled { get; init; }

    // Visibility filters
    /// <summary>Exact visibility match (e.g. public, private).</summary>
    public string? Visibility { get; init; }

    // Search/content filters
    /// <summary>Case-insensitive substring search across title and descriptions.</summary>
    public string? Query { get; init; }
    /// <summary>Comma-separated tech stack tokens passed to <see cref="ITechStackMatcher"/>.</summary>
    public string? Tech { get; init; }
    /// <summary>Exact difficulty level filter.</summary>
    public string? Difficulty { get; init; }

    // Feature flags
    /// <summary>When set, filters by showcase publication flag.</summary>
    public bool? Showcase { get; init; }
    /// <summary>When set, filters by featured flag.</summary>
    public bool? Featured { get; init; }

    // Ownership filters
    /// <summary>When true, limits to projects owned by or joined by <see cref="RequesterId"/>.</summary>
    public bool? MyProjects { get; init; }
    /// <summary>Restricts results to a specific owner user id.</summary>
    public Guid? OwnerId { get; init; }
    /// <summary>Filters projects whose owner profile timezone matches this value.</summary>
    public string? OwnerTimezone { get; init; }

    // Pagination & sorting
    /// <summary>1-based page index (clamped in <see cref="FromQuery"/>).</summary>
    public int Page { get; init; } = 1;
    /// <summary>Page size (clamped to 1–100 in <see cref="FromQuery"/>).</summary>
    public int PageSize { get; init; } = 20;
    /// <summary>Sort field: createdAt, rating, updatedAt, featured, title, or teamSize.</summary>
    public string SortBy { get; init; } = "createdAt";
    /// <summary>Sort direction: asc or desc.</summary>
    public string SortOrder { get; init; } = "desc";

    /// <summary>
    /// Builds validated options from raw query parameters, clamping pagination and truncating long strings.
    /// </summary>
    public static ProjectFilterOptions FromQuery(
        Guid? requesterId,
        bool isPrivilegedViewer,
        string? status = null,
        string? visibility = null,
        string? query = null,
        string? tech = null,
        string? difficulty = null,
        bool? showcase = null,
        bool? featured = null,
        bool? myProjects = null,
        Guid? ownerId = null,
        bool? excludeDrafts = null,
        bool? includeCancelled = null,
        string? ownerTimezone = null,
        int page = 1,
        int pageSize = 20,
        string sortBy = "createdAt",
        string sortOrder = "desc")
    {
        // Validate and clamp pagination
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // Truncate string inputs for security
        if (query?.Length > 200) query = query[..200];
        if (tech?.Length > 500) tech = tech[..500];
        if (ownerTimezone?.Length > 50) ownerTimezone = ownerTimezone[..50];

        return new ProjectFilterOptions
        {
            RequesterId = requesterId,
            IsPrivilegedViewer = isPrivilegedViewer,
            Status = status?.Trim().ToLowerInvariant(),
            Visibility = visibility?.Trim().ToLowerInvariant(),
            Query = query?.Trim(),
            Tech = tech?.Trim(),
            Difficulty = difficulty?.Trim().ToLowerInvariant(),
            Showcase = showcase,
            Featured = featured,
            MyProjects = myProjects,
            OwnerId = ownerId,
            ExcludeDrafts = excludeDrafts,
            IncludeCancelled = includeCancelled,
            OwnerTimezone = ownerTimezone?.Trim(),
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy.Trim().ToLowerInvariant(),
            SortOrder = sortOrder.Trim().ToLowerInvariant()
        };
    }
}
