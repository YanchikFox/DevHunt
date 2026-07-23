using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Services.Users;

/// <summary>User discovery search used by profile and team-building flows.</summary>
public interface IUserSearchService
{
    /// <summary>Runs a filtered, paginated search over active verified users.</summary>
    /// <param name="request">Search filters, sort options, and pagination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching users and pagination metadata.</returns>
    Task<UserSearchResult> SearchUsersAsync(UserSearchRequest request, CancellationToken ct = default);
}

/// <summary>Filter, sort, and pagination options for user search.</summary>
/// <param name="Query">Optional text matched against name and bio.</param>
/// <param name="Role">Optional exact role filter.</param>
/// <param name="Timezone">Optional exact timezone filter.</param>
/// <param name="Skills">Optional comma-separated skill tokens.</param>
/// <param name="MinExperience">Optional minimum years of experience.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Requested page size.</param>
/// <param name="SortBy">Sort field: rating, createdAt/created, or name.</param>
/// <param name="SortOrder">Sort direction, typically asc or desc.</param>
/// <param name="IncludeSkills">When true, includes skill details in results.</param>
public record UserSearchRequest(
    string? Query,
    string? Role,
    string? Timezone,
    string? Skills,
    int? MinExperience,
    int Page,
    int PageSize,
    string SortBy,
    string SortOrder,
    bool IncludeSkills
);

/// <summary>User search response payload.</summary>
/// <param name="Data">Matching user summaries.</param>
/// <param name="Pagination">Paging metadata for the query.</param>
public record UserSearchResult(
    IEnumerable<UserSummaryDto> Data,
    PaginationMetadata Pagination
);

/// <summary>Pagination metadata returned with list endpoints.</summary>
/// <param name="Page">Current 1-based page.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="Total">Total matching items.</param>
/// <param name="TotalPages">Total number of pages.</param>
/// <param name="HasNext">Whether another page exists after this one.</param>
/// <param name="HasPrevious">Whether a page exists before this one.</param>
public record PaginationMetadata(
    int Page,
    int PageSize,
    int Total,
    int TotalPages,
    bool HasNext,
    bool HasPrevious
);
