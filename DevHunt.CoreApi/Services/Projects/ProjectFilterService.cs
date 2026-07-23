using DevHunt.CoreApi.Models;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>Page of project entities plus standard pagination metadata.</summary>
public sealed record ProjectFilterResult<T>
{
    /// <summary>Current page of project rows or DTOs.</summary>
    public required IReadOnlyList<T> Data { get; init; }
    /// <summary>Total count and next/previous flags for the query.</summary>
    public required PaginationInfo Pagination { get; init; }
}

/// <summary>Standard pagination block returned with filtered project lists.</summary>
public sealed record PaginationInfo
{
    /// <summary>1-based current page index.</summary>
    public int Page { get; init; }
    /// <summary>Requested page size after clamping.</summary>
    public int PageSize { get; init; }
    /// <summary>Total rows matching the filter before paging.</summary>
    public int Total { get; init; }
    /// <summary>Derived total page count.</summary>
    public int TotalPages { get; init; }
    /// <summary>True when another page exists after this one.</summary>
    public bool HasNext { get; init; }
    /// <summary>True when the current page is not the first.</summary>
    public bool HasPrevious { get; init; }
}

/// <summary>
/// Service for filtering and querying projects.
/// Extracted from ProjectsController to reduce complexity and improve testability.
/// </summary>
public interface IProjectFilterService
{
    /// <summary>
    /// Applies all filters, sorting, and pagination to a project query.
    /// </summary>
    Task<IQueryable<Project>> ApplyFiltersAsync(IQueryable<Project> query, ProjectFilterOptions options);

    /// <summary>
    /// Applies sorting to a project query.
    /// </summary>
    IQueryable<Project> ApplySorting(IQueryable<Project> query, string sortBy, string sortOrder);
}

/// <summary>
/// EF-backed implementation of <see cref="IProjectFilterService"/>. Applies catalog access rules,
/// text/tech/difficulty filters, and sorting via composable query transforms.
/// </summary>
public sealed class ProjectFilterService : IProjectFilterService
{
    private readonly DevHuntDbContext _dbContext;
    private readonly ITechStackMatcher _techStackMatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectFilterService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context used for owner-timezone filtering.</param>
    /// <param name="techStackMatcher">Builds technology stack predicates from query tokens.</param>
    public ProjectFilterService(DevHuntDbContext dbContext, ITechStackMatcher techStackMatcher)
    {
        _dbContext = dbContext;
        _techStackMatcher = techStackMatcher;
    }

    /// <inheritdoc />
    public async Task<IQueryable<Project>> ApplyFiltersAsync(IQueryable<Project> query, ProjectFilterOptions options)
    {
        // 1. Exclude cancelled by default
        query = ApplyCancelledFilter(query, options);

        // 2. Apply visibility/access rules
        query = ApplyAccessFilter(query, options);

        // 3. Apply ownership filter
        query = ApplyOwnerFilter(query, options);

        // 4. Apply status filter
        query = ApplyStatusFilter(query, options);

        // 5. Apply visibility filter
        query = ApplyVisibilityFilter(query, options);

        // 6. Apply text search
        query = ApplyTextSearch(query, options);

        // 7. Apply tech stack filter (complex)
        query = await ApplyTechFilter(query, options);

        // 8. Apply difficulty filter
        query = ApplyDifficultyFilter(query, options);

        // 9. Apply showcase/featured filters
        query = ApplyFeatureFilters(query, options);

        // 10. Apply "my projects" filter
        query = ApplyMyProjectsFilter(query, options);

        // 11. Apply drafts exclusion
        query = ApplyDraftsFilter(query, options);

        // 12. Apply timezone filter
        query = await ApplyTimezoneFilter(query, options);

        return query;
    }

    /// <inheritdoc />
    public IQueryable<Project> ApplySorting(IQueryable<Project> query, string sortBy, string sortOrder)
    {
        var ascending = sortOrder == "asc";

        return sortBy switch
        {
            "rating" => ascending
                ? query.OrderBy(p => p.Rating ?? 0)
                : query.OrderByDescending(p => p.Rating ?? 0),
            "updatedat" => ascending
                ? query.OrderBy(p => p.UpdatedAt)
                : query.OrderByDescending(p => p.UpdatedAt),
            "featured" => ascending
                ? query.OrderBy(p => p.Featured).ThenBy(p => p.CreatedAt)
                : query.OrderByDescending(p => p.Featured).ThenByDescending(p => p.CreatedAt),
            "title" => ascending
                ? query.OrderBy(p => p.Title)
                : query.OrderByDescending(p => p.Title),
            "teamsize" => ascending
                ? query.OrderBy(p => p.TeamMembers.Count(tm => tm.Status == TeamMemberStatus.Active.Value))
                : query.OrderByDescending(p => p.TeamMembers.Count(tm => tm.Status == TeamMemberStatus.Active.Value)),
            _ => ascending
                ? query.OrderBy(p => p.CreatedAt)
                : query.OrderByDescending(p => p.CreatedAt),
        };
    }

    #region Filter Methods (each ~5-15 lines, single responsibility)

    /// <summary>Excludes cancelled projects unless explicitly requested.</summary>
    private static IQueryable<Project> ApplyCancelledFilter(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (options.IncludeCancelled != true && options.Status != ProjectStatus.Cancelled.Value)
        {
            query = query.Where(p => p.Status != ProjectStatus.Cancelled.Value);
        }
        return query;
    }

    /// <summary>Restricts rows to what anonymous or authenticated viewers may see in the catalog.</summary>
    private static IQueryable<Project> ApplyAccessFilter(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (options.IsPrivilegedViewer)
            return query; // Admins/curators see everything

        if (!options.RequesterId.HasValue)
        {
            // Anonymous: only public + not draft
            return query.Where(p => p.Visibility == ProjectVisibility.Public.Value && p.Status != ProjectStatus.Draft.Value);
        }

        var currentUserId = options.RequesterId.Value;

        // Authenticated: own/team projects (including drafts) + public (excluding drafts)
        return query.Where(p =>
            p.OwnerId == currentUserId ||
            p.TeamMembers.Any(tm => tm.UserId == currentUserId && tm.Status == TeamMemberStatus.Active.Value) ||
            (p.Visibility == "public" && p.Status != ProjectStatus.Draft.Value));
    }

    /// <summary>Filters by a specific owner id when <see cref="ProjectFilterOptions.OwnerId"/> is set.</summary>
    private static IQueryable<Project> ApplyOwnerFilter(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (options.OwnerId.HasValue)
        {
            query = query.Where(p => p.OwnerId == options.OwnerId.Value);
        }
        return query;
    }

    /// <summary>Exact status match when <see cref="ProjectFilterOptions.Status"/> is provided.</summary>
    private static IQueryable<Project> ApplyStatusFilter(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Status))
        {
            query = query.Where(p => p.Status == options.Status);
        }
        return query;
    }

    /// <summary>Exact visibility match when <see cref="ProjectFilterOptions.Visibility"/> is provided.</summary>
    private static IQueryable<Project> ApplyVisibilityFilter(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Visibility))
        {
            query = query.Where(p => p.Visibility == options.Visibility);
        }
        return query;
    }

    /// <summary>Case-insensitive substring search on title and description fields.</summary>
    private static IQueryable<Project> ApplyTextSearch(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Query))
            return query;

        var q = options.Query.Trim();
        if (q.Length > 200) q = q[..200];
        var qLower = q.ToLowerInvariant();

        return query.Where(p =>
            (p.Title != null && p.Title.ToLower().Contains(qLower)) ||
            (p.ShortDescription != null && p.ShortDescription.ToLower().Contains(qLower)) ||
            (p.Description != null && p.Description.ToLower().Contains(qLower)));
    }

    /// <summary>Applies tech stack OR filter from <see cref="ITechStackMatcher"/> when tokens are present.</summary>
    private async Task<IQueryable<Project>> ApplyTechFilter(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Tech))
            return query;

        var techFilter = await _techStackMatcher.BuildTechFilterExpressionAsync(options.Tech);
        if (techFilter != null)
        {
            query = query.Where(techFilter);
        }

        return query;
    }

    /// <summary>Exact difficulty level match.</summary>
    private static IQueryable<Project> ApplyDifficultyFilter(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Difficulty))
        {
            query = query.Where(p => p.DifficultyLevel == options.Difficulty);
        }
        return query;
    }

    /// <summary>Filters showcase and featured boolean flags when requested.</summary>
    private static IQueryable<Project> ApplyFeatureFilters(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (options.Showcase.HasValue)
        {
            query = query.Where(p => p.ShowcasePublished == options.Showcase.Value);
        }

        if (options.Featured.HasValue)
        {
            query = query.Where(p => p.Featured == options.Featured.Value);
        }

        return query;
    }

    /// <summary>Limits to owned or joined projects when <see cref="ProjectFilterOptions.MyProjects"/> is true.</summary>
    private static IQueryable<Project> ApplyMyProjectsFilter(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (options.MyProjects != true || !options.RequesterId.HasValue)
            return query;

        var currentUserId = options.RequesterId.Value;
        return query.Where(p =>
            p.OwnerId == currentUserId ||
            p.TeamMembers.Any(tm => tm.UserId == currentUserId));
    }

    /// <summary>Removes drafts when <see cref="ProjectFilterOptions.ExcludeDrafts"/> is true.</summary>
    private static IQueryable<Project> ApplyDraftsFilter(IQueryable<Project> query, ProjectFilterOptions options)
    {
        if (options.ExcludeDrafts == true)
        {
            query = query.Where(p => p.Status != ProjectStatus.Draft.Value);
        }
        return query;
    }

    /// <summary>Restricts to projects whose owners share the given IANA timezone string.</summary>
    private async Task<IQueryable<Project>> ApplyTimezoneFilter(IQueryable<Project> query, ProjectFilterOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.OwnerTimezone))
            return query;

        var timezone = options.OwnerTimezone.Trim();
        if (timezone.Length > 50) timezone = timezone[..50];

        var userIdsInTimezone = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Timezone == timezone)
            .Select(u => u.Id)
            .ToListAsync(ct);

        return query.Where(p => userIdsInTimezone.Contains(p.OwnerId));
    }

    #endregion
}
