using System;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing machine learning-based project recommendations.
/// </summary>
/// <remarks>
/// This controller implements the matching_module from the C4 architecture.
/// It integrates with an external ML Service to generate personalized project recommendations based on user skills, interests, and project requirements.
///
/// Core functionality:
/// - Fetches ML-generated recommendations and caches them in the database for performance
/// - Tracks user engagement (viewed/actioned) for analytics and ML feedback loops
/// - Supports both user-to-project and project-to-user matching
/// - Admin tools for bulk recommendation refresh
///
/// Routes: api/recommendations/*
/// </remarks>
[ApiController]
[Route("api/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly DevHuntDbContext _context;
    private readonly IMLServiceClient _mlServiceClient;
    private readonly ILogger<RecommendationsController> _logger;
    private readonly IMemoryCache _cache;

    // REC-03: Per-user refresh rate limit constants
    private static readonly TimeSpan RefreshCooldown = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Creates the recommendations controller with persistence, ML, logging, and rate-limit cache services.
    /// </summary>
    /// <param name="context">Database context used to read and write recommendation records.</param>
    /// <param name="mlServiceClient">ML service client used to fetch and refresh recommendation data.</param>
    /// <param name="logger">Logger for refresh and engagement diagnostics.</param>
    /// <param name="cache">Memory cache used to rate-limit user-triggered refreshes.</param>
    public RecommendationsController(
        DevHuntDbContext context,
        IMLServiceClient mlServiceClient,
        ILogger<RecommendationsController> logger,
        IMemoryCache cache)
    {
        _context = context;
        _mlServiceClient = mlServiceClient;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Builds a de-duplicated skill payload from normalized user skills and legacy raw skill entries.
    /// </summary>
    private static IReadOnlyCollection<object> BuildUserSkillsForRecommendation(User user)
    {
        var results = new List<object>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (user.UserSkills != null)
        {
            foreach (var us in user.UserSkills)
            {
                if (us.Skill == null)
                {
                    continue;
                }

                if (!seen.Add(us.Skill.Name))
                {
                    continue;
                }

                results.Add(new
                {
                    us.Skill.Name,
                    us.Skill.Category,
                    us.ProficiencyLevel,
                    us.YearsOfExperience
                });
            }
        }

        if (user.UserSkillEntries != null)
        {
            foreach (var entry in user.UserSkillEntries)
            {
                var name = entry.Skill?.Name ?? entry.Raw;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!seen.Add(name))
                {
                    continue;
                }

                results.Add(new
                {
                    Name = name,
                    Category = entry.Skill?.Category ?? "other",
                    ProficiencyLevel = "unspecified",
                    YearsOfExperience = (int?)null
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Retrieves personalized project recommendations for the authenticated user, optionally refreshing them through the ML service.
    /// </summary>
    /// <remarks>
    /// This endpoint uses a two-tier strategy:
    /// 1. If refresh=true, queries the ML Service to generate fresh recommendations based on the user's current profile and saves them to the database.
    /// 2. Returns cached recommendations from the database, ordered by match score.
    ///
    /// When refreshing, existing recommendations are updated (preserving their IDs), and new ones are created.
    /// The 'Viewed' flag is reset to false when recommendations are refreshed.
    ///
    /// Includes project details, tech stack, and team members for each recommendation.
    /// </remarks>
    /// <param name="limit">Maximum number of recommendations to return (default: 10, max: 50).</param>
    /// <param name="refresh">If true, fetches fresh recommendations from the ML Service before returning results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of personalized project recommendations with match scores and reasoning.</returns>
    /// <response code="200">Returns the list of recommendations.</response>
    /// <response code="429">If refresh was called more than once in the last 5 minutes.</response>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyRecommendations(
        [FromQuery] int limit = 10,
        [FromQuery] bool refresh = false,
        CancellationToken ct = default)
    {
        var userIdValue = GetRequiredUserId();

        // REC-05: Clamp limit to prevent resource abuse
        limit = Math.Clamp(limit, 1, 50);

        // If refresh requested, call ML Service
        if (refresh)
        {
            // REC-03: Per-user rate limiting — 1 refresh per 5 minutes
            var rateLimitKey = $"rec:refresh:{userIdValue}";
            if (_cache.TryGetValue(rateLimitKey, out _))
                return StatusCode(429, new { Message = "Recommendations can only be refreshed once every 5 minutes." });

            _cache.Set(rateLimitKey, true, RefreshCooldown);

            var mlRecommendations = await _mlServiceClient.GetRecommendationsForUserAsync(userIdValue, limit);

            // REC-01: Batch load — one IN-query instead of O(N) individual SELECTs
            var projectIds = mlRecommendations.Select(r => r.ProjectId).ToList();
            var existingMap = await _context.Recommendations
                .Where(r => r.UserId == userIdValue && projectIds.Contains(r.ProjectId))
                .ToDictionaryAsync(r => r.ProjectId, ct);

            foreach (var mlRec in mlRecommendations)
            {
                if (existingMap.TryGetValue(mlRec.ProjectId, out var existing))
                {
                    existing.MatchScore = mlRec.MatchScore;
                    existing.Reasoning = mlRec.Reasoning;
                    existing.GeneratedAt = DateTime.UtcNow;
                    existing.Viewed = false;
                }
                else
                {
                    _context.Recommendations.Add(new Recommendation
                    {
                        Id = Guid.NewGuid(),
                        UserId = userIdValue,
                        ProjectId = mlRec.ProjectId,
                        MatchScore = mlRec.MatchScore,
                        Reasoning = mlRec.Reasoning,
                        GeneratedAt = DateTime.UtcNow,
                        Viewed = false,
                        Actioned = false
                    });
                }
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Refreshed {Count} recommendations for user {UserId}",
                mlRecommendations.Count, userIdValue);
        }

        // Get recommendations from DB
        var recommendations = await _context.Recommendations
            .Include(r => r.Project)
                // REC-06: B-09 — Value Object instead of string literal "active"
                .ThenInclude(p => p.TeamMembers.Where(tm => tm.Status == TeamMemberStatus.Active.Value))
            .Include(r => r.Project)
                .ThenInclude(p => p.TechStacks)
                    .ThenInclude(pts => pts.Skill)
            .Where(r => r.UserId == userIdValue)
            .OrderByDescending(r => r.MatchScore)
            .ThenByDescending(r => r.GeneratedAt)
            .Take(limit)
            .Select(r => new
            {
                r.Id,
                Project = new
                {
                    r.ProjectId,
                    r.Project.Title,
                    r.Project.ShortDescription,
                    r.Project.Description,
                    r.Project.DifficultyLevel,
                    r.Project.Status,
                    TechStack = r.Project.TechStacks.Select(pts => new
                    {
                        pts.Skill.Name,
                        pts.Skill.Category
                    }).ToList()
                },
                r.MatchScore,
                Reasoning = r.Reasoning,
                r.GeneratedAt,
                r.Viewed,
                r.Actioned
            })
            .ToListAsync(ct);

        return Ok(recommendations);
    }

    /// <summary>
    /// Retrieves user recommendations for a specific project for admins and curators.
    /// </summary>
    /// <remarks>
    /// This endpoint performs reverse matching: instead of recommending projects to users,
    /// it recommends users to a project based on skill alignment, experience, and other factors.
    ///
    /// Useful for project owners and curators to discover potential team members.
    /// Returns user profiles with their skills, proficiency levels, and match scores.
    ///
    /// Requires admin or curator role.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="limit">Maximum number of user recommendations to return (default: 20, max: 50).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of users ranked by how well they match the project's requirements.</returns>
    /// <response code="200">Returns the list of user recommendations.</response>
    /// <response code="403">If the user is not an admin or curator.</response>
    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetProjectRecommendations(
        Guid projectId,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        // REC-15: Replace hardcoded [Authorize(Roles = "admin,curator")] with SecurityHelpers
        // Also fixes missing "superadmin" from the role list
        if (!SecurityHelpers.IsAdminOrCurator(User))
            return Forbid();

        // REC-05: Clamp limit
        limit = Math.Clamp(limit, 1, 50);

        var recommendations = await _context.Recommendations
            .AsNoTracking()
            .Include(r => r.User)
                .ThenInclude(u => u.UserSkills)
                    .ThenInclude(us => us.Skill)
            .Include(r => r.User)
                .ThenInclude(u => u.UserSkillEntries)
                    .ThenInclude(e => e.Skill)
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.MatchScore)
            .Take(limit)
            .ToListAsync(ct);

        var payload = recommendations.Select(r => new
        {
            r.Id,
            User = new
            {
                r.UserId,
                r.User.FullName,
                // REC-11: Email removed — PII leak to all curators
                r.User.AvatarUrl,
                Skills = BuildUserSkillsForRecommendation(r.User)
            },
            r.MatchScore,
            Reasoning = r.Reasoning,
            r.GeneratedAt,
            r.Viewed,
            r.Actioned
        });

        return Ok(payload);
    }

    /// <summary>
    /// Marks one of the current user's recommendations as viewed.
    /// </summary>
    /// <remarks>
    /// This endpoint tracks user engagement with recommendations for analytics and ML feedback.
    /// The 'Viewed' flag indicates that the user has seen the recommendation, even if they didn't take action.
    ///
    /// This data helps the ML Service improve future recommendations by understanding which suggestions users engage with.
    ///
    /// Users can only mark their own recommendations as viewed.
    /// </remarks>
    /// <param name="recommendationId">The unique identifier of the recommendation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <response code="200">If the recommendation was successfully marked as viewed.</response>
    /// <response code="404">If the recommendation is not found or doesn't belong to the current user.</response>
    [HttpPut("{recommendationId}/view")]
    public async Task<IActionResult> MarkAsViewed(Guid recommendationId, CancellationToken ct = default)
    {
        var recommendation = await _context.Recommendations
            .FirstOrDefaultAsync(r => r.Id == recommendationId && r.UserId == GetRequiredUserId(), ct);

        if (recommendation == null)
        {
            return NotFound("Recommendation not found");
        }

        recommendation.Viewed = true;
        await _context.SaveChangesAsync(ct);

        return Ok();
    }

    /// <summary>
    /// Marks one of the current user's recommendations as actioned and viewed.
    /// </summary>
    /// <remarks>
    /// This endpoint tracks when users act on recommendations, such as:
    /// - Joining the recommended project
    /// - Applying to become a team member
    /// - Contacting the project owner
    ///
    /// Marking a recommendation as actioned automatically marks it as viewed as well.
    /// This is a strong positive signal for the ML Service to refine future recommendations.
    ///
    /// The action is logged for analytics purposes.
    /// </remarks>
    /// <param name="recommendationId">The unique identifier of the recommendation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <response code="200">If the recommendation was successfully marked as actioned.</response>
    /// <response code="404">If the recommendation is not found or doesn't belong to the current user.</response>
    [HttpPut("{recommendationId}/action")]
    public async Task<IActionResult> MarkAsActioned(Guid recommendationId, CancellationToken ct = default)
    {
        var recommendation = await _context.Recommendations
            .FirstOrDefaultAsync(r => r.Id == recommendationId && r.UserId == GetRequiredUserId(), ct);

        if (recommendation == null)
        {
            return NotFound("Recommendation not found");
        }

        recommendation.Actioned = true;
        recommendation.Viewed = true; // Automatically mark as viewed
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} actioned recommendation {RecommendationId} for project {ProjectId}",
            recommendation.UserId, recommendationId, recommendation.ProjectId);

        return Ok();
    }

    /// <summary>
    /// Deletes one of the current user's recommendations as negative feedback.
    /// </summary>
    /// <remarks>
    /// This endpoint allows users to remove recommendations they're not interested in.
    /// This serves as negative feedback for the ML Service, helping it learn user preferences.
    ///
    /// Deleted recommendations are permanently removed from the database.
    /// Users can only delete their own recommendations.
    /// </remarks>
    /// <param name="recommendationId">The unique identifier of the recommendation to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">If the recommendation was successfully deleted.</response>
    /// <response code="404">If the recommendation is not found or doesn't belong to the current user.</response>
    [HttpDelete("{recommendationId}")]
    public async Task<IActionResult> DeleteRecommendation(Guid recommendationId, CancellationToken ct = default)
    {
        var recommendation = await _context.Recommendations
            .FirstOrDefaultAsync(r => r.Id == recommendationId && r.UserId == GetRequiredUserId(), ct);

        if (recommendation == null)
        {
            return NotFound("Recommendation not found");
        }

        _context.Recommendations.Remove(recommendation);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Triggers an asynchronous ML-service refresh for all users when the caller is an admin.
    /// </summary>
    /// <remarks>
    /// This is an administrative endpoint that initiates a background job in the ML Service
    /// to regenerate recommendations for all users in the system.
    ///
    /// This operation is asynchronous and may take significant time to complete.
    /// Typically used when:
    /// - The recommendation algorithm has been updated
    /// - A major data migration has occurred
    /// - Periodic maintenance (e.g., weekly refresh)
    ///
    /// Returns immediately with a 202 Accepted status while the ML Service processes the request in the background.
    ///
    /// Requires admin role.
    /// </remarks>
    /// <returns>Accepted status indicating the refresh has been queued.</returns>
    /// <response code="202">If the refresh was successfully triggered.</response>
    /// <response code="403">If the user is not an admin.</response>
    [HttpPost("refresh-all")]
    public async Task<IActionResult> RefreshAllRecommendations()
    {
        // REC-15: Replace hardcoded [Authorize(Roles = "admin")] with SecurityHelpers
        if (!SecurityHelpers.IsAdmin(User))
            return Forbid();

        _logger.LogInformation("Triggering recommendations refresh for all users");

        // Trigger refresh in ML Service (it will process all users in background)
        await _mlServiceClient.TriggerRecommendationsRefreshAsync();

        return Accepted(new { Message = "Recommendations refresh triggered. This may take some time." });
    }

    /// <summary>
    /// Regenerates recommendations for a specific user by replacing existing rows in a transaction.
    /// </summary>
    /// <remarks>
    /// This endpoint performs a synchronous refresh of recommendations for a single user:
    /// 1. Fetches up to 50 fresh recommendations from the ML Service
    /// 2. Deletes all existing recommendations for the user within a transaction
    /// 3. Creates new recommendation records in the database
    ///
    /// Unlike the general refresh endpoint, this operation:
    /// - Executes synchronously (waits for ML Service response)
    /// - Completely replaces old recommendations rather than updating them
    /// - Resets all tracking flags (viewed/actioned)
    ///
    /// Requires admin or curator role.
    /// </remarks>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of new recommendations generated.</returns>
    /// <response code="200">Returns the count of new recommendations.</response>
    /// <response code="403">If the user is not an admin or curator.</response>
    [HttpPost("refresh/{userId}")]
    public async Task<IActionResult> RefreshUserRecommendations(Guid userId, CancellationToken ct = default)
    {
        // REC-15: Replace hardcoded [Authorize(Roles = "admin,curator")] with SecurityHelpers
        if (!SecurityHelpers.IsAdminOrCurator(User))
            return Forbid();

        var mlRecommendations = await _mlServiceClient.GetRecommendationsForUserAsync(userId, limit: 50);

        var oldRecommendations = await _context.Recommendations
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);

        // REC-04: Wrap in transaction — prevents data loss on partial failure (delete succeeds, insert fails)
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            _context.Recommendations.RemoveRange(oldRecommendations);

            var newRecommendations = mlRecommendations.Select(mlRec => new Recommendation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProjectId = mlRec.ProjectId,
                MatchScore = mlRec.MatchScore,
                Reasoning = mlRec.Reasoning,
                GeneratedAt = DateTime.UtcNow,
                Viewed = false,
                Actioned = false
            }).ToList();

            _context.Recommendations.AddRange(newRecommendations);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Refreshed {Count} recommendations for user {UserId}",
                newRecommendations.Count, userId);

            return Ok(new { Count = newRecommendations.Count });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
