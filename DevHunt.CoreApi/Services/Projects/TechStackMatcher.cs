using System.Linq.Expressions;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Handles complex tech stack matching logic including:
/// - Skill normalization and alias resolution
/// - Expression tree building for EF Core queries
/// - Both denormalized (TechStack string[]) and relational (ProjectTechStack) matching
/// </summary>
public interface ITechStackMatcher
{
    /// <summary>
    /// Builds a filter expression for matching projects by technology stack.
    /// </summary>
    /// <param name="techQuery">Comma-separated list of technologies to search for</param>
    /// <returns>Filter expression or null if no valid tech tokens provided</returns>
    Task<Expression<Func<Project, bool>>?> BuildTechFilterExpressionAsync(string techQuery);
}

/// <summary>
/// EF-backed implementation of <see cref="ITechStackMatcher"/>. Normalizes comma-separated tech
/// tokens, resolves skill ids via aliases and names, and builds OR expressions for both
/// denormalized <see cref="Project.TechStack"/> and relational <see cref="Project.TechStacks"/>.
/// </summary>
public sealed class TechStackMatcher : ITechStackMatcher
{
    private readonly DevHuntDbContext _dbContext;
    private const int MaxTechTokens = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="TechStackMatcher"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for resolving skill aliases and canonical names.</param>
    public TechStackMatcher(DevHuntDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Expression<Func<Project, bool>>?> BuildTechFilterExpressionAsync(string techQuery)
    {
        if (string.IsNullOrWhiteSpace(techQuery))
            return null;

        var rawTokens = ParseTechTokens(techQuery);
        if (rawTokens.Length == 0)
            return null;

        var rawLower = rawTokens
            .Select(t => t.ToLowerInvariant())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToArray();

        var normalizedTokens = rawTokens
            .Select(SkillNormalization.NormalizeSkillToken)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToArray();

        // Resolve skill IDs from aliases
        var aliasSkillIds = await ResolveSkillIdsFromAliasesAsync(normalizedTokens);

        // Resolve skill IDs from direct name matches
        var nameMatchedSkillIds = await ResolveSkillIdsFromNamesAsync(rawLower);

        var skillIds = aliasSkillIds
            .Concat(nameMatchedSkillIds)
            .Distinct()
            .ToList();

        // Get canonical names for matched skills
        var canonicalNames = await GetCanonicalNamesAsync(skillIds);

        // Build combined search needles
        var techNeedles = canonicalNames
            .Concat(rawTokens)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (techNeedles.Length == 0 && skillIds.Count == 0)
            return null;

        return BuildFilterExpression(techNeedles, skillIds);
    }

    /// <summary>Splits a comma-separated query into at most <see cref="MaxTechTokens"/> distinct tokens.</summary>
    private static string[] ParseTechTokens(string techQuery)
    {
        return techQuery
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTechTokens)
            .ToArray();
    }

    /// <summary>Looks up skill ids whose normalized alias matches any token.</summary>
    private async Task<List<Guid>> ResolveSkillIdsFromAliasesAsync(string[] normalizedTokens, CancellationToken ct = default)
    {
        if (normalizedTokens.Length == 0)
            return new List<Guid>();

        return await _dbContext.SkillAliases
            .AsNoTracking()
            .Where(a => normalizedTokens.Any(t => t == a.AliasNormalized))
            .Select(a => a.SkillId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>Looks up skill ids whose canonical name matches any lowercased token.</summary>
    private async Task<List<Guid>> ResolveSkillIdsFromNamesAsync(string[] rawLower, CancellationToken ct = default)
    {
        if (rawLower.Length == 0)
            return new List<Guid>();

        return await _dbContext.Skills
            .AsNoTracking()
            .Where(s => rawLower.Any(t => t == s.Name.ToLower()))
            .Select(s => s.Id)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>Loads display names for resolved skill ids to include in string-array matching.</summary>
    private async Task<List<string>> GetCanonicalNamesAsync(List<Guid> skillIds, CancellationToken ct = default)
    {
        if (skillIds.Count == 0)
            return new List<string>();

        return await _dbContext.Skills
            .AsNoTracking()
            .Where(s => skillIds.Any(id => id == s.Id))
            .Select(s => s.Name)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Combines OR predicates over <see cref="Project.TechStack"/> string entries and
    /// <see cref="Project.TechStacks"/> skill id links.
    /// </summary>
    private static Expression<Func<Project, bool>>? BuildFilterExpression(string[] techNeedles, List<Guid> skillIds)
    {
        var projectParam = Expression.Parameter(typeof(Project), "p");
        Expression? predicateBody = null;

        // Build expression for denormalized TechStack (string[])
        if (techNeedles.Length > 0)
        {
            var techStackExpr = Expression.Property(projectParam, nameof(Project.TechStack));
            Expression? techPredicateBody = null;

            foreach (var techValue in techNeedles)
            {
                var tParam = Expression.Parameter(typeof(string), "t");
                var equals = Expression.Equal(tParam, Expression.Constant(techValue));
                var anyLambda = Expression.Lambda(equals, tParam);
                var anyCall = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Any),
                    new[] { typeof(string) },
                    techStackExpr,
                    anyLambda);

                techPredicateBody = techPredicateBody == null
                    ? anyCall
                    : Expression.OrElse(techPredicateBody, anyCall);
            }

            predicateBody = techPredicateBody;
        }

        // Build expression for relational TechStacks (ProjectTechStack)
        if (skillIds.Count > 0)
        {
            var techStacksExpr = Expression.Property(projectParam, nameof(Project.TechStacks));
            Expression? skillPredicateBody = null;

            foreach (var id in skillIds)
            {
                var tsParam = Expression.Parameter(typeof(ProjectTechStack), "ts");
                var skillIdProp = Expression.Property(tsParam, nameof(ProjectTechStack.SkillId));
                var equals = Expression.Equal(skillIdProp, Expression.Constant(id));
                var anyLambda = Expression.Lambda(equals, tsParam);
                var anyCall = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Any),
                    new[] { typeof(ProjectTechStack) },
                    techStacksExpr,
                    anyLambda);

                skillPredicateBody = skillPredicateBody == null
                    ? anyCall
                    : Expression.OrElse(skillPredicateBody, anyCall);
            }

            predicateBody = predicateBody == null
                ? skillPredicateBody
                : Expression.OrElse(predicateBody, skillPredicateBody!);
        }

        if (predicateBody == null)
            return null;

        return Expression.Lambda<Func<Project, bool>>(predicateBody, projectParam);
    }
}
