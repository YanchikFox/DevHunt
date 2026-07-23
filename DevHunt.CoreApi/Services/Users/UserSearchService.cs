using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Services.Users;

/// <summary>Searches active, verified users with text, role, skill, and experience filters.</summary>
public class UserSearchService : IUserSearchService
{
    private readonly DevHuntDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSearchService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for user and skill queries.</param>
    public UserSearchService(DevHuntDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Builds a filtered, sorted, paginated user search result.</summary>
    public async Task<UserSearchResult> SearchUsersAsync(UserSearchRequest request, CancellationToken ct = default)
    {
        var (page, pageSize) = ValidatePagination(request.Page, request.PageSize);
        var usersQuery = BuildBaseQuery(request);

        usersQuery = await ApplySkillsFilterAsync(usersQuery, request.Skills);
        usersQuery = ApplySorting(usersQuery, request.SortBy, request.SortOrder);

        var total = await usersQuery.CountAsync(ct);
        var users = await FetchPaginatedUsersAsync(usersQuery, page, pageSize, request.IncludeSkills);
        var userDtos = MapToDtos(users, request.IncludeSkills);

        return new UserSearchResult(
            userDtos,
            new PaginationMetadata(
                page,
                pageSize,
                total,
                (int)Math.Ceiling((double)total / pageSize),
                page * pageSize < total,
                page > 1
            )
        );
    }

    /// <summary>Clamps page to at least 1 and page size to 1..100.</summary>
    private static (int Page, int PageSize) ValidatePagination(int page, int pageSize)
    {
        var validatedPage = page < 1 ? 1 : page;
        var validatedSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);
        return (validatedPage, validatedSize);
    }

    /// <summary>Applies active/verified filters and optional scalar filters.</summary>
    private IQueryable<User> BuildBaseQuery(UserSearchRequest request)
    {
        var query = _dbContext.Users.AsNoTracking().Where(u => u.IsActive && u.IsEmailVerified).AsQueryable();

        query = ApplyTextFilter(query, request.Query);
        query = ApplyRoleFilter(query, request.Role);
        query = ApplyTimezoneFilter(query, request.Timezone);
        query = ApplyExperienceFilter(query, request.MinExperience);

        return query;
    }

    /// <summary>Matches full name or bio with a length-capped search term.</summary>
    private static IQueryable<User> ApplyTextFilter(IQueryable<User> query, string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery)) return query;

        var sanitized = searchQuery.Trim();
        if (sanitized.Length > 100) sanitized = sanitized[..100];

        return query.Where(u =>
            (u.FullName != null && u.FullName.Contains(sanitized)) ||
            (u.Bio != null && u.Bio.Contains(sanitized)));
    }

    /// <summary>Filters by exact role when provided.</summary>
    private static IQueryable<User> ApplyRoleFilter(IQueryable<User> query, string? role)
    {
        return string.IsNullOrWhiteSpace(role) ? query : query.Where(u => u.Role == role);
    }

    /// <summary>Filters by exact timezone when provided.</summary>
    private static IQueryable<User> ApplyTimezoneFilter(IQueryable<User> query, string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone)) return query;

        var sanitized = timezone.Length > 50 ? timezone[..50] : timezone;
        return query.Where(u => u.Timezone == sanitized);
    }

    /// <summary>Keeps users with at least the requested years of experience.</summary>
    private static IQueryable<User> ApplyExperienceFilter(IQueryable<User> query, int? minExperience)
    {
        if (!minExperience.HasValue || minExperience.Value <= 0) return query;

        var minExp = minExperience.Value;
        return query.Where(u => u.Experience.HasValue && u.Experience.Value >= minExp);
    }

    /// <summary>Resolves skill aliases and matches linked or raw skill entries.</summary>
    private async Task<IQueryable<User>> ApplySkillsFilterAsync(IQueryable<User> query, string? skills)
    {
        if (string.IsNullOrWhiteSpace(skills)) return query;

        var tokens = ParseSkillTokens(skills);
        if (tokens.Length == 0) return query;

        var normalizedTokens = tokens.Select(SkillNormalization.NormalizeSkillToken).Distinct().ToList();
        var rawLowerTokens = tokens.Select(t => t.ToLowerInvariant()).Distinct().ToList();

        var skillIds = await ResolveSkillIdsAsync(normalizedTokens, rawLowerTokens);

        return ApplySkillConditions(query, normalizedTokens, rawLowerTokens, skillIds);
    }

    /// <summary>Splits a comma-separated skill list into at most ten distinct tokens.</summary>
    private string[] ParseSkillTokens(string skills)
    {
        return skills
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    /// <summary>Maps normalized tokens and raw names to skill IDs.</summary>
    private async Task<List<Guid>> ResolveSkillIdsAsync(List<string> normalizedTokens, List<string> rawLowerTokens, CancellationToken ct = default)
    {
        var aliasSkillIds = normalizedTokens.Count == 0
            ? new List<Guid>()
            : await _dbContext.SkillAliases
                .AsNoTracking()
                .Where(a => normalizedTokens.Contains(a.AliasNormalized))
                .Select(a => a.SkillId)
                .Distinct()
                .ToListAsync(ct);

        var nameMatchedSkillIds = rawLowerTokens.Count == 0
            ? new List<Guid>()
            : await _dbContext.Skills
                .AsNoTracking()
                .Where(s => rawLowerTokens.Contains(s.Name.ToLower()))
                .Select(s => s.Id)
                .Distinct()
                .ToListAsync(ct);

        return aliasSkillIds.Concat(nameMatchedSkillIds).Distinct().ToList();
    }

    /// <summary>Restricts the query to users matching any resolved skill criteria.</summary>
    private IQueryable<User> ApplySkillConditions(
        IQueryable<User> query,
        List<string> normalizedTokens,
        List<string> rawLowerTokens,
        List<Guid> skillIds)
    {
        if (NoSkillsToFilter(normalizedTokens, rawLowerTokens, skillIds))
            return query;

        // Combine IDs from soft matches (aliases/raw names) and hard matches (linked skill IDs)
        // Using Union() avoids complex OR logic in LINQ, which improves readability and often performance
        var matchingUserIds = GetSoftMatchedUserIds(normalizedTokens, skillIds)
            .Union(GetHardMatchedUserIds(rawLowerTokens, skillIds));

        return query.Where(u => matchingUserIds.Contains(u.Id));
    }

    /// <summary>Returns true when no skill filters were resolved.</summary>
    private static bool NoSkillsToFilter(List<string> normalizedTokens, List<string> rawLowerTokens, List<Guid> skillIds)
    {
        return normalizedTokens.Count == 0 && rawLowerTokens.Count == 0 && skillIds.Count == 0;
    }

    /// <summary>Finds users via normalized raw skill entries or alias skill IDs.</summary>
    private IQueryable<Guid> GetSoftMatchedUserIds(List<string> normalizedTokens, List<Guid> skillIds)
    {
        var skillIdNeedles = skillIds.Select(id => (Guid?)id).ToList();

        var idsByTokens = _dbContext.UserSkillEntries
            .Where(e => normalizedTokens.Contains(e.RawNormalized))
            .Select(e => e.UserId);

        var idsBySkillIds = _dbContext.UserSkillEntries
            .Where(e => skillIdNeedles.Contains(e.SkillId))
            .Select(e => e.UserId);

        return idsByTokens.Union(idsBySkillIds);
    }

    /// <summary>Finds users via linked <c>UserSkills</c> records.</summary>
    private IQueryable<Guid> GetHardMatchedUserIds(List<string> rawLowerTokens, List<Guid> skillIds)
    {
        var idsBySkillIds = _dbContext.UserSkills
            .Where(us => skillIds.Contains(us.SkillId))
            .Select(us => us.UserId);

        var idsBySkillNames = _dbContext.UserSkills
            .Where(us => us.Skill != null && rawLowerTokens.Contains(us.Skill.Name.ToLower()))
            .Select(us => us.UserId);

        return idsBySkillIds.Union(idsBySkillNames);
    }

    private static readonly Dictionary<string, Func<IQueryable<User>, bool, IOrderedQueryable<User>>> SortStrategies = new()
    {
        ["rating"] = (q, desc) => desc ? q.OrderByDescending(u => u.Rating ?? 0) : q.OrderBy(u => u.Rating ?? 0),
        ["createdat"] = (q, desc) => desc ? q.OrderByDescending(u => u.CreatedAt) : q.OrderBy(u => u.CreatedAt),
        ["created"] = (q, desc) => desc ? q.OrderByDescending(u => u.CreatedAt) : q.OrderBy(u => u.CreatedAt),
        ["name"] = (q, desc) => desc ? q.OrderByDescending(u => u.FullName ?? u.Email) : q.OrderBy(u => u.FullName ?? u.Email),
    };

    /// <summary>Orders results by rating, created date, or name.</summary>
    private static IQueryable<User> ApplySorting(IQueryable<User> query, string? sortBy, string? sortOrder)
    {
        var isDesc = "desc".Equals(sortOrder, StringComparison.OrdinalIgnoreCase);
        var sortKey = (sortBy ?? "name").ToLowerInvariant();

        var strategy = SortStrategies.GetValueOrDefault(sortKey, SortStrategies["name"]);
        return strategy(query, isDesc);
    }

    /// <summary>Loads one page of users, optionally including skill relations.</summary>
    private static async Task<List<User>> FetchPaginatedUsersAsync(IQueryable<User> query, int page, int pageSize, bool includeSkills, CancellationToken ct = default)
    {
        var paginatedQuery = query.Skip((page - 1) * pageSize).Take(pageSize);

        if (includeSkills)
        {
            paginatedQuery = paginatedQuery.Include(u => u.UserSkills).ThenInclude(us => us.Skill);
        }

        return await paginatedQuery.ToListAsync(ct);
    }

    /// <summary>Projects users into summary DTOs for API responses.</summary>
    private static List<UserSummaryDto> MapToDtos(List<User> users, bool includeSkills)
    {
        return users.Select(user =>
        {
            IReadOnlyCollection<UserSkillDto>? skillsList = null;
            if (includeSkills && user.UserSkills.Count != 0)
            {
                skillsList = user.UserSkills
                    .Where(us => us.Skill != null)
                    .Select(us => new UserSkillDto(
                        us.Skill!.Name,
                        us.Skill.Category,
                        us.ProficiencyLevel,
                        us.YearsOfExperience,
                        us.Verified))
                    .ToList();
            }

            return new UserSummaryDto(
                user.Id,
                user.Role,
                user.FullName,
                user.Bio,
                user.Timezone,
                user.IsVerified,
                skillsList);
        }).ToList();
    }
}
