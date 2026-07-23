using DevHunt.CoreApi.Models;
using DevHunt.Infrastructure;
using DomainTaskStatus = DevHunt.CoreApi.Models.TaskStatus; // Alias avoids conflict with System.Threading.Tasks.TaskStatus.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevHunt.CoreApi.Services.Badges;

/// <summary>
/// Evaluates achievement conditions and awards badges in response to domain events.
/// Called by controllers and services after state-changing operations via
/// <see cref="AchievementTriggerExtensions.TriggerAchievementCheckAsync"/>.
/// Uses a per-invocation count cache to avoid repeated identical DB queries
/// when multiple badge thresholds share the same underlying count.
/// </summary>
public class AchievementTriggerService : IAchievementTriggerService
{
    private readonly DevHuntDbContext _db;
    private readonly IBadgesService _badges;
    private readonly ILogger<AchievementTriggerService> _logger;

    // System user id for automated badge awards
    private static readonly Guid SystemUserId = Guid.Empty;

    // Per-invocation cache to avoid repeated identical DB queries within one CheckAndAwardAsync call
    private readonly Dictionary<string, int> _countCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AchievementTriggerService"/> class.
    /// </summary>
    /// <param name="db">Database context used to evaluate achievement conditions.</param>
    /// <param name="badges">Badge service used to grant newly earned achievements.</param>
    /// <param name="logger">Logger for failed condition checks and auto-awards.</param>
    public AchievementTriggerService(
        DevHuntDbContext db,
        IBadgesService badges,
        ILogger<AchievementTriggerService> logger)
    {
        _db = db;
        _badges = badges;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates all badge conditions relevant to <paramref name="trigger"/> for the given user
    /// and awards any that are newly met. Already-earned badges are skipped via a single
    /// pre-flight query. Individual check failures are swallowed so one broken condition
    /// does not block the rest.
    /// </summary>
    /// <param name="userId">The user to evaluate.</param>
    /// <param name="trigger">The domain event that caused this check.</param>
    public async Task CheckAndAwardAsync(Guid userId, AchievementTrigger trigger)
    {
        try
        {
            _countCache.Clear();
            var codesToCheck = GetRelevantCodes(trigger);
            if (codesToCheck.Length == 0) return;

            // Load codes the user already has — single query
            var earnedCodes = await _db.UserAchievements
                .Where(ua => ua.UserId == userId)
                .Include(ua => ua.Achievement)
                .Select(ua => ua.Achievement.Code)
                .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

            var unchecked_ = codesToCheck.Where(c => !earnedCodes.Contains(c)).ToArray();
            if (unchecked_.Length == 0) return;

            foreach (var code in unchecked_)
            {
                try
                {
                    if (await IsConditionMet(userId, code))
                    {
                        await AwardAsync(userId, code);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check/award achievement {Code} for user {UserId}", code, userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Achievement trigger check failed for user {UserId}, trigger {Trigger}", userId, trigger);
        }
    }

    /// <summary>
    /// Awards badge <paramref name="code"/> to <paramref name="userId"/> via <see cref="IBadgesService"/>.
    /// HTTP 409 (already has badge) is silently ignored.
    /// </summary>
    private async Task AwardAsync(Guid userId, string code)
    {
        var result = await _badges.AwardBadgeAsync(new AwardBadgeRequest
        {
            UserId = userId,
            AchievementCode = code,
            CurrentUserId = SystemUserId,
        });

        if (result.IsSuccess)
        {
            _logger.LogInformation("Auto-awarded achievement {Code} to user {UserId}", code, userId);
        }
        // 409 = already has badge — safe to ignore
    }

    // ────────────────────────────────────────────
    // Trigger → codes mapping
    // ────────────────────────────────────────────

    /// <summary>
    /// Returns the badge codes that can potentially be unlocked by <paramref name="trigger"/>.
    /// Limits DB work to only the badges the trigger could affect.
    /// </summary>
    private static string[] GetRelevantCodes(AchievementTrigger trigger) => trigger switch
    {
        AchievementTrigger.ProjectCreated => ["first_project", "three_projects", "five_projects", "ten_projects", "project_owner", "project_published", "polyglot"],
        AchievementTrigger.ProjectCompleted => ["project_completed", "three_completed", "five_completed"],
        AchievementTrigger.ProjectPublished => ["project_published"],
        AchievementTrigger.ProjectFeatured => ["featured_project"],
        AchievementTrigger.ShowcasePublished => ["showcase_published"],

        AchievementTrigger.TeamJoined => ["first_team", "three_teams", "five_teams", "ten_teams", "first_contribution", "multi_role", "big_team"],
        AchievementTrigger.TeamLeaderAssigned => ["team_leader"],

        AchievementTrigger.UserFollowed => ["first_follower", "ten_followers", "popular", "influencer", "social_butterfly"],

        AchievementTrigger.ReviewCreated => ["first_review", "helpful", "code_reviewer", "top_rated"],
        AchievementTrigger.ShowcaseCommentCreated => ["first_comment"],

        AchievementTrigger.ProfileUpdated => ["profile_complete", "skilled", "social_presence"],
        AchievementTrigger.AvatarUploaded => ["avatar_set"],
        AchievementTrigger.UserVerified => ["verified"],
        AchievementTrigger.GithubConnected => ["github_connected"],

        AchievementTrigger.TaskCompleted => ["first_task", "ten_tasks", "task_master", "hundred_tasks"],
        AchievementTrigger.MessageSent => ["first_message", "chatterbox"],

        AchievementTrigger.ProjectIssueReported => ["bug_hunter"],
        AchievementTrigger.RoleChanged => ["mentor"],

        // Moderation
        AchievementTrigger.ReportProcessed => ["first_moderation", "mod_veteran"],
        AchievementTrigger.UserBlocked => ["guardian"],
        AchievementTrigger.AdminVerifiedUser => ["verifier"],
        AchievementTrigger.IssueResolved => ["issue_resolver"],
        AchievementTrigger.TicketResolved => ["support_hero"],
        AchievementTrigger.ProjectActioned => ["curator_star"],
        AchievementTrigger.IssueEscalated => ["watchdog"],

        _ => [],
    };

    // ────────────────────────────────────────────
    // Condition checks — one method per badge code
    // ────────────────────────────────────────────

    /// <summary>
    /// Dispatches to the specific condition-check method for <paramref name="code"/>.
    /// Returns <see langword="false"/> for unrecognised codes.
    /// </summary>
    private Task<bool> IsConditionMet(Guid userId, string code) => code switch
    {
        // Projects (cached: same count reused for all threshold levels)
        "first_project" => CachedCountCheck("projects_owned", () => _db.Projects.CountAsync(p => p.OwnerId == userId), 1),
        "three_projects" => CachedCountCheck("projects_owned", () => _db.Projects.CountAsync(p => p.OwnerId == userId), 3),
        "five_projects" => CachedCountCheck("projects_owned", () => _db.Projects.CountAsync(p => p.OwnerId == userId), 5),
        "ten_projects" => CachedCountCheck("projects_owned", () => _db.Projects.CountAsync(p => p.OwnerId == userId), 10),
        "project_completed" => CachedCountCheck("projects_completed", () => _db.Projects.CountAsync(p => p.OwnerId == userId && p.Status == ProjectStatus.Completed.Value), 1),
        "three_completed" => CachedCountCheck("projects_completed", () => _db.Projects.CountAsync(p => p.OwnerId == userId && p.Status == ProjectStatus.Completed.Value), 3),
        "five_completed" => CachedCountCheck("projects_completed", () => _db.Projects.CountAsync(p => p.OwnerId == userId && p.Status == ProjectStatus.Completed.Value), 5),
        "featured_project" => _db.Projects.AnyAsync(p => p.OwnerId == userId && p.Featured),
        "showcase_published" => _db.Projects.AnyAsync(p => p.OwnerId == userId && p.ShowcasePublished),
        "project_published" => _db.Projects.AnyAsync(p => p.OwnerId == userId && p.Status != ProjectStatus.Draft.Value),

        // Teamwork
        "first_team" => CachedCountCheck("teams_distinct", () => DistinctTeamCount(userId), 1),
        "three_teams" => CachedCountCheck("teams_distinct", () => DistinctTeamCount(userId), 3),
        "five_teams" => CachedCountCheck("teams_distinct", () => DistinctTeamCount(userId), 5),
        "ten_teams" => CachedCountCheck("teams_distinct", () => DistinctTeamCount(userId), 10),
        "team_leader" => _db.TeamMembers.AnyAsync(tm => tm.UserId == userId && tm.IsLeader),
        "project_owner" => CachedCountCheck("projects_owned", () => _db.Projects.CountAsync(p => p.OwnerId == userId), 3),
        // FIX B-09: "active" → TeamMemberStatus.Active.Value
        "first_contribution" => _db.TeamMembers.AnyAsync(tm => tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value && !tm.IsLeader),
        "multi_role" => CountCheck(() => DistinctRoleCount(userId), 3),
        "big_team" => CheckBigTeam(userId),

        // Social
        "first_follower" => CachedCountCheck("followers", () => _db.UserFollows.CountAsync(f => f.FollowedId == userId), 1),
        "ten_followers" => CachedCountCheck("followers", () => _db.UserFollows.CountAsync(f => f.FollowedId == userId), 10),
        "popular" => CachedCountCheck("followers", () => _db.UserFollows.CountAsync(f => f.FollowedId == userId), 50),
        "influencer" => CachedCountCheck("followers", () => _db.UserFollows.CountAsync(f => f.FollowedId == userId), 100),
        "social_butterfly" => CachedCountCheck("following", () => _db.UserFollows.CountAsync(f => f.FollowerId == userId), 20),
        "first_review" => CachedCountCheck("reviews_given", () => _db.Reviews.CountAsync(r => r.ReviewerId == userId), 1),
        "helpful" => CachedCountCheck("reviews_given", () => _db.Reviews.CountAsync(r => r.ReviewerId == userId), 10),
        "code_reviewer" => CountCheck(() => DistinctReviewedUserCount(userId), 10),
        "first_comment" => CountCheck(() => _db.ShowcaseComments.CountAsync(c => c.AuthorId == userId), 1),
        "top_rated" => CheckTopRated(userId),

        // Profile
        "profile_complete" => CheckProfileComplete(userId),
        "avatar_set" => CheckAvatarSet(userId),
        "verified" => CheckVerified(userId),
        "skilled" => CountCheck(() => _db.UserSkillEntries.CountAsync(s => s.UserId == userId), 5),
        "github_connected" => CheckGithubConnected(userId),
        "social_presence" => CheckSocialPresence(userId),

        // Activity
        "first_task" => CachedCountCheck("tasks_done", () => CompletedTaskCount(userId), 1),
        "ten_tasks" => CachedCountCheck("tasks_done", () => CompletedTaskCount(userId), 10),
        "task_master" => CachedCountCheck("tasks_done", () => CompletedTaskCount(userId), 50),
        "hundred_tasks" => CachedCountCheck("tasks_done", () => CompletedTaskCount(userId), 100),
        "first_message" => CachedCountCheck("messages_sent", () => _db.Messages.CountAsync(m => m.SenderId == userId), 1),
        "chatterbox" => CachedCountCheck("messages_sent", () => _db.Messages.CountAsync(m => m.SenderId == userId), 100),

        // Special
        "bug_hunter" => CountCheck(() => _db.ProjectIssues.CountAsync(i => i.ReporterId == userId), 5),
        "mentor" => CheckMentorRole(userId),
        "polyglot" => CheckPolyglot(userId),

        // Count only moderation reports processed by this user so badges are awarded to the actual moderator.
        // Accept the legacy "processed" status alongside the current resolved constant.
        "first_moderation" => CountCheck(() => _db.ModerationReports.CountAsync(r => r.ProcessedByUserId == userId && (r.Status == Infrastructure.Constants.ModerationReportStatus.Resolved || r.Status == "processed")), 1),
        "mod_veteran" => CountCheck(() => _db.ModerationReports.CountAsync(r => r.ProcessedByUserId == userId && (r.Status == Infrastructure.Constants.ModerationReportStatus.Resolved || r.Status == "processed")), 50),
        "guardian" => CountCheck(() => _db.AuditLogs.CountAsync(a => a.UserId == userId && a.Action == "AdminController.BlockUser"), 5),
        "verifier" => CountCheck(() => _db.AuditLogs.CountAsync(a => a.UserId == userId && a.Action == "AdminController.VerifyUser"), 10),
        // FIX B-09: "done" → TaskStatus.Done.Value
        "issue_resolver" => CountCheck(() => _db.ProjectIssues.CountAsync(i => i.AssignedToAdminId == userId && i.Status == "resolved"), 10),
        "support_hero" => CountCheck(() => _db.SupportTickets.CountAsync(t => t.AssignedToUserId == userId && t.Status == "resolved"), 25),
        "curator_star" => CountCheck(() => _db.AuditLogs.CountAsync(a => a.UserId == userId && a.Action == "AdminController.ProjectAction"), 10),
        "watchdog" => CountCheck(() => _db.AuditLogs.CountAsync(a => a.UserId == userId && a.Action == "AdminController.EscalateIssue"), 5),

        _ => Task.FromResult(false),
    };

    // ────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────

    /// <summary>
    /// Cached count check: same (cacheKey) returns cached result within one CheckAndAwardAsync call.
    /// Eliminates repeated identical DB queries (e.g., project count queried 5x for different thresholds).
    /// </summary>
    private async Task<bool> CachedCountCheck(string cacheKey, Func<Task<int>> countFn, int threshold)
    {
        if (!_countCache.TryGetValue(cacheKey, out var count))
        {
            count = await countFn();
            _countCache[cacheKey] = count;
        }
        return count >= threshold;
    }

    /// <summary>
    /// Runs <paramref name="countFn"/> and returns whether the result meets <paramref name="threshold"/>.
    /// Use <see cref="CachedCountCheck"/> instead when the same count is needed for multiple thresholds.
    /// </summary>
    private static async Task<bool> CountCheck(Func<Task<int>> countFn, int threshold)
        => await countFn() >= threshold;

    // FIX B-09: "active" → TeamMemberStatus.Active.Value
    /// <summary>
    /// Counts the distinct active project teams the user belongs to.
    /// </summary>
    private Task<int> DistinctTeamCount(Guid userId)
        => _db.TeamMembers
            .Where(tm => tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value)
            .Select(tm => tm.ProjectId)
            .Distinct()
            .CountAsync();

    /// <summary>
    /// Counts the distinct team member role values recorded for the user.
    /// </summary>
    private Task<int> DistinctRoleCount(Guid userId)
        => _db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Select(tm => tm.Role)
            .Distinct()
            .CountAsync();

    /// <summary>
    /// Counts distinct users that the user has reviewed.
    /// </summary>
    private Task<int> DistinctReviewedUserCount(Guid userId)
        => _db.Reviews
            .Where(r => r.ReviewerId == userId && r.ReviewedUserId != null)
            .Select(r => r.ReviewedUserId)
            .Distinct()
            .CountAsync();

    // Use the domain task status alias to avoid conflict with System.Threading.Tasks.TaskStatus.
    /// <summary>
    /// Counts tasks assigned to the user that are marked done.
    /// </summary>
    private Task<int> CompletedTaskCount(Guid userId)
        => _db.Tasks.CountAsync(t => t.AssignedToUserId == userId && t.Status == DomainTaskStatus.Done.Value);

    // FIX B-09: "active" → TeamMemberStatus.Active.Value
    /// <summary>
    /// Returns <see langword="true"/> when any project owned by the user has 5 or more active team members.
    /// </summary>
    private async Task<bool> CheckBigTeam(Guid userId)
        => await _db.Projects
            .Where(p => p.OwnerId == userId)
            .AnyAsync(p => _db.TeamMembers.Count(tm => tm.ProjectId == p.Id && tm.Status == TeamMemberStatus.Active.Value) >= 5);

    /// <summary>
    /// Returns <see langword="true"/> when the user has a display name, bio, avatar URL,
    /// at least one social link (GitHub / LinkedIn / website), and at least one skill entry.
    /// </summary>
    private async Task<bool> CheckProfileComplete(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FullName, u.Bio, u.AvatarUrl, u.Github, u.Linkedin, u.Website })
            .FirstOrDefaultAsync(ct);
        if (user is null) return false;

        bool hasBasics = !string.IsNullOrWhiteSpace(user.FullName) && !string.IsNullOrWhiteSpace(user.Bio) && !string.IsNullOrWhiteSpace(user.AvatarUrl);
        bool hasLink = !string.IsNullOrWhiteSpace(user.Github) || !string.IsNullOrWhiteSpace(user.Linkedin) || !string.IsNullOrWhiteSpace(user.Website);
        bool hasSkills = await _db.UserSkillEntries.AnyAsync(s => s.UserId == userId, ct);
        return hasBasics && hasLink && hasSkills;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the user record has any avatar URL set.
    /// </summary>
    private async Task<bool> CheckAvatarSet(Guid userId)
        => await _db.Users.AnyAsync(u => u.Id == userId && u.AvatarUrl != null);

    /// <summary>
    /// Returns <see langword="true"/> when the user's email is verified.
    /// </summary>
    private async Task<bool> CheckVerified(Guid userId)
        => await _db.Users.AnyAsync(u => u.Id == userId && u.IsEmailVerified);

    /// <summary>
    /// Returns <see langword="true"/> when the user has a GitHub account identifier.
    /// </summary>
    private async Task<bool> CheckGithubConnected(Guid userId)
        => await _db.Users.AnyAsync(u => u.Id == userId && u.GithubId != null);

    /// <summary>
    /// Returns <see langword="true"/> when GitHub, LinkedIn, and website profile links are all set.
    /// </summary>
    private async Task<bool> CheckSocialPresence(Guid userId)
        => await _db.Users.AnyAsync(u =>
            u.Id == userId &&
            u.Github != null && u.Github != "" &&
            u.Linkedin != null && u.Linkedin != "" &&
            u.Website != null && u.Website != "");

    /// <summary>
    /// Returns <see langword="true"/> when the user's rating is ≥ 4.5 and they have received
    /// at least 3 reviews — guards against awarding the badge on a single high rating.
    /// </summary>
    private async Task<bool> CheckTopRated(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Rating })
            .FirstOrDefaultAsync(ct);
        if (user?.Rating is null or < 4.5f) return false;

        int reviewCount = await _db.Reviews.CountAsync(r => r.ReviewedUserId == userId);
        return reviewCount >= 3;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the user holds a Curator, Admin, or SuperAdmin role.
    /// </summary>
    private async Task<bool> CheckMentorRole(Guid userId, CancellationToken ct = default)
    {
        string? role = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);
        return role is UserRoles.Curator or UserRoles.Admin or UserRoles.SuperAdmin;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the user's owned projects collectively reference
    /// 5 or more distinct technologies in their tech stacks.
    /// </summary>
    private async Task<bool> CheckPolyglot(Guid userId, CancellationToken ct = default)
    {
        int techCount = await _db.Projects
            .Where(p => p.OwnerId == userId)
            .SelectMany(p => p.TechStack)
            .Distinct()
            .CountAsync(ct);
        return techCount >= 5;
    }
}

// ────────────────────────────────────────────
// Awaitable achievement trigger extension
// ────────────────────────────────────────────

/// <summary>Extension methods that run achievement checks in an isolated DI scope.</summary>
public static class AchievementTriggerExtensions
{
    /// <summary>
    /// Runs achievement checks in a separate dependency-injection scope and returns the asynchronous operation.
    /// The caller must await this method before returning from the action method.
    /// </summary>
    public static async Task TriggerAchievementCheckAsync(
        this IServiceProvider services,
        Guid userId,
        AchievementTrigger trigger,
        CancellationToken ct = default)
    {
        try
        {
            using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IAchievementTriggerService>();
            await svc.CheckAndAwardAsync(userId, trigger);
        }
        catch (Exception ex)
        {
            var logger = services.GetService<ILoggerFactory>()?.CreateLogger("AchievementTrigger");
            logger?.LogError(ex, "Achievement check failed for user {UserId}, trigger {Trigger}", userId, trigger);
        }
    }
}
