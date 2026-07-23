using DevHunt.CoreApi.Models;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Badges;
using System.ComponentModel.DataAnnotations;
using DevHunt.CoreApi.Services;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Admin API surface at /api/admin/* for user moderation, ticket triage, and project governance.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly IAuditService _auditService;
    private readonly INotificationServiceClient _notificationService;
    private readonly ILogger<AdminController> _logger;

    /// <summary>
    /// Creates the admin controller with persistence, audit logging, notification, and diagnostic services.
    /// </summary>
    /// <param name="db">Database context used for admin mutations and projections.</param>
    /// <param name="auditService">Audit service that records privileged account and project changes.</param>
    /// <param name="notificationService">Notification client used for user-facing admin messages.</param>
    /// <param name="logger">Logger for issue-handling diagnostics.</param>
    public AdminController(DevHuntDbContext db, IAuditService auditService, INotificationServiceClient notificationService, ILogger<AdminController> logger)
    {
        _db = db;
        _auditService = auditService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>Request to block a user account.</summary>
    /// <param name="UserId">User identifier to block.</param>
    /// <param name="Reason">Reason for the block.</param>
    /// <param name="IsPermanent">Whether the block is permanent.</param>
    public record BlockUserRequest(Guid UserId, string Reason, bool IsPermanent);

    /// <summary>Request to change a user's role.</summary>
    /// <param name="UserId">User identifier to update.</param>
    /// <param name="NewRole">New role value.</param>
    public record ChangeUserRoleRequest(Guid UserId, string NewRole);

    /// <summary>Request to apply an admin action to a project.</summary>
    /// <param name="ProjectId">Target project identifier.</param>
    /// <param name="Action">Action name (hide, archive, feature, unfeature).</param>
    /// <param name="Reason">Required reason for audit/notification.</param>
    public record AdminProjectActionRequest(Guid ProjectId, string Action, string Reason); // Action: hide, archive, feature, unfeature

    /// <summary>Request to change a user's username and record why the change was made.</summary>
    /// <param name="NewUsername">New username to validate and persist.</param>
    /// <param name="Reason">Reason included in notifications and audit logs.</param>
    public record ChangeUsernameRequest(string NewUsername, string Reason);

    // -------------------------------------------------------------------------
    // Localisation helper — returns user-facing message in the user's language.
    // Supports "pl" (Polish) with English fallback for everything else.
    // -------------------------------------------------------------------------
    private static readonly Dictionary<string, (string En, string Pl)> _notifTemplates = new()
    {
        ["project.hide"]       = ("Your project \"{0}\" has been hidden from public view.", "Twój projekt \"{0}\" został ukryty przed publicznym widokiem."),
        ["project.archive"]    = ("Your project \"{0}\" has been archived.", "Twój projekt \"{0}\" został zarchiwizowany."),
        ["project.feature"]    = ("Your project \"{0}\" has been featured on the platform!", "Twój projekt \"{0}\" został wyróżniony na platformie!"),
        ["project.unfeature"]  = ("Your project \"{0}\" has been removed from featured.", "Twój projekt \"{0}\" został usunięty z wyróżnionych."),
        ["account.blocked"]    = ("Your account has been blocked.", "Twoje konto zostało zablokowane."),
        ["account.username"]   = ("Your username has been changed to \"{0}\".", "Twoja nazwa użytkownika została zmieniona na \"{0}\"."),
    };

    /// <summary>
    /// Formats a notification title in Polish when requested, otherwise using the English template.
    /// </summary>
    private static string GetLocalizedTitle(string? lang, string key, params string[] args)
    {
        if (!_notifTemplates.TryGetValue(key, out var pair)) return key;
        var template = lang == "pl" ? pair.Pl : pair.En;
        return args.Length > 0 ? string.Format(template, args) : template;
    }

    /// <summary>
    /// Builds localized notification body text by combining the localized title with a reason line.
    /// </summary>
    private static string GetLocalizedContent(string? lang, string key, string reason, params string[] args)
    {
        var title = GetLocalizedTitle(lang, key, args);
        var prefix = lang == "pl" ? "Powód" : "Reason";
        return $"{title}\n{prefix}: {reason}";
    }

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Attempts to read the authenticated user identifier from the current claims principal.
    /// </summary>
    private Guid? TryGetUserId()
    {
        return SecurityHelpers.GetUserId(User);
    }

    /// <summary>
    /// Fetches the active current user's role from the database instead of trusting JWT claims.
    /// </summary>
    private async Task<string?> GetUserRoleFromDbAsync(CancellationToken ct = default)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue) return null;

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId.Value && u.IsActive)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Checks whether the current active database user may perform admin or curator operations.
    /// </summary>
    private async Task<bool> IsAdminOrCuratorAsync(CancellationToken ct = default)
    {
        var role = await GetUserRoleFromDbAsync(ct);
        return role is UserRoles.Admin or UserRoles.Curator or UserRoles.SuperAdmin;
    }

    /// <summary>
    /// Checks whether the current active database user may perform admin-only operations.
    /// </summary>
    private async Task<bool> IsAdminAsync(CancellationToken ct = default)
    {
        var role = await GetUserRoleFromDbAsync(ct);
        return role is UserRoles.Admin or UserRoles.SuperAdmin;
    }

    /// <summary>
    /// Checks whether the current active database user is a superadmin.
    /// </summary>
    private async Task<bool> IsSuperAdminAsync(CancellationToken ct = default)
    {
        var role = await GetUserRoleFromDbAsync(ct);
        return role == UserRoles.SuperAdmin;
    }

    /// <summary>
    /// Blocks a user account, queues a localized notification, audits the action, and triggers the blocking achievement.
    /// </summary>
    /// <param name="req">Block details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Forbids non-admin/curator callers, rejects missing reasons, returns not found for missing users, and returns OK after persistence.</returns>
    [HttpPost("users/block")]
    public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest req, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Reason)) return BadRequest("Reason is required");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct);
        if (user == null) return NotFound();

        var adminUserId = GetRequiredUserId();

        // AP-05: Single transaction for block + notification
        user.IsActive = false;
        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), UserId = req.UserId, Type = "admin",
            Title = GetLocalizedTitle(user.Language, "account.blocked"),
            Content = GetLocalizedContent(user.Language, "account.blocked", req.Reason),
            Priority = "high", CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        // Audit + achievements after successful commit
        await _auditService.LogActionAsync(adminUserId, "AdminController.BlockUser", "User", req.UserId,
            $"Blocked user {req.UserId}. Reason: {req.Reason}. Permanent: {req.IsPermanent}");
        await HttpContext.RequestServices.TriggerAchievementCheckAsync(adminUserId, AchievementTrigger.UserBlocked);

        return Ok();
    }

    /// <summary>
    /// Reactivates a blocked user account.
    /// </summary>
    /// <param name="userId">User identifier to unblock.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Forbids non-admin/curator callers, returns not found for missing users, and returns OK after reactivation.</returns>
    [HttpPost("users/unblock")]
    public async Task<IActionResult> UnblockUser([FromBody] Guid userId, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound();
        user.IsActive = true;
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// <summary>
    /// Marks a user as verified, audits the action, and triggers the verification achievement.
    /// </summary>
    /// <param name="userId">User identifier to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Forbids non-admin/curator callers, returns not found for missing users, and returns OK after persistence.</returns>
    [HttpPost("users/verify")]
    public async Task<IActionResult> VerifyUser([FromBody] Guid userId, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound();

        var adminUserId = GetRequiredUserId();

        user.IsVerified = true;
        await _db.SaveChangesAsync(ct);

        // SECURITY: Audit logging (SEC-020)
        await _auditService.LogActionAsync(adminUserId, "AdminController.VerifyUser", "User", userId,
            $"Verified user {userId}");

        await HttpContext.RequestServices.TriggerAchievementCheckAsync(adminUserId, AchievementTrigger.AdminVerifiedUser);

        return Ok();
    }

    /// <summary>
    /// Changes a user's role while protecting admin and superadmin role transitions.
    /// </summary>
    /// <param name="req">Role change details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Forbids non-admin callers and unauthorized protected-role changes, rejects assigning superadmin, returns not found for missing users, and returns OK after auditing.</returns>
    [HttpPost("users/change-role")]
    public async Task<IActionResult> ChangeUserRole([FromBody] ChangeUserRoleRequest req, CancellationToken ct = default)
    {
        // SECURITY: Only admin can change roles, validated in DB
        if (!await IsAdminAsync()) return Forbid();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct);
        if (user == null) return NotFound();

        var adminUserId = GetRequiredUserId();
        var oldRole = user.Role;
        var isSuperAdmin = await IsSuperAdminAsync();

        // SECURITY: Only superadmin can promote to/demote from admin or superadmin
        var protectedRoles = new[] { UserRoles.Admin, UserRoles.SuperAdmin };
        if (!isSuperAdmin)
        {
            if (protectedRoles.Contains(user.Role))
                return Forbid(); // Cannot demote existing admin/superadmin
            if (protectedRoles.Contains(req.NewRole))
                return Forbid(); // Cannot promote to admin/superadmin
        }

        // SECURITY: superadmin role can never be assigned via this endpoint
        if (req.NewRole == UserRoles.SuperAdmin)
            return BadRequest("Cannot assign superadmin role through this endpoint");

        user.Role = req.NewRole;
        await _db.SaveChangesAsync(ct);

        // SECURITY: Audit logging for role changes (SEC-020)
        await _auditService.LogActionAsync(adminUserId, "AdminController.ChangeUserRole", "User", req.UserId,
            $"Changed role from {oldRole} to {req.NewRole}", null, "critical");
        await HttpContext.RequestServices.TriggerAchievementCheckAsync(req.UserId, AchievementTrigger.RoleChanged);

        return Ok();
    }

    /// <summary>
    /// Applies a hide, archive, feature, or unfeature moderation action to a project and notifies the owner.
    /// </summary>
    /// <param name="req">Project action details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Forbids non-admin/curator callers, rejects missing reasons or unknown actions, returns not found for missing projects, and returns OK after auditing.</returns>
    [HttpPost("projects/action")]
    public async Task<IActionResult> ProjectAction([FromBody] AdminProjectActionRequest req, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Reason)) return BadRequest("Reason is required");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == req.ProjectId, ct);
        if (project == null) return NotFound();

        var action = req.Action.ToLowerInvariant();
        if (!ApplyProjectModerationAction(project, action))
            return BadRequest("Invalid action");

        project.UpdatedAt = DateTime.UtcNow;

        await QueueProjectNotificationAsync(project, action, req.Reason, ct);
        await _db.SaveChangesAsync(ct);

        var adminUserId = GetRequiredUserId();
        await _auditService.LogActionAsync(adminUserId, "AdminController.ProjectAction", "Project", project.Id,
            $"Action: {action}. Reason: {req.Reason}");

        if (action == ProjectModerationAction.Feature)
            await HttpContext.RequestServices.TriggerAchievementCheckAsync(adminUserId, AchievementTrigger.ProjectActioned);

        return Ok();
    }

    private static readonly Dictionary<string, Action<Project>> _projectActions = new()
    {
        [ProjectModerationAction.Hide]      = p => p.Visibility = ProjectVisibility.Private.Value,
        [ProjectModerationAction.Archive]    = p => { p.Status = ProjectStatus.Archived.Value; p.Visibility = ProjectVisibility.Private.Value; },
        [ProjectModerationAction.Feature]    = p => p.Featured = true,
        [ProjectModerationAction.Unfeature]  = p => p.Featured = false,
    };

    /// <summary>Applies the requested <see cref="ProjectModerationAction"/> mutation to a project.</summary>
    /// <returns><see langword="false"/> when the action key is unknown; otherwise <see langword="true"/>.</returns>
    private static bool ApplyProjectModerationAction(Project project, string action)
    {
        if (!_projectActions.TryGetValue(action, out var apply)) return false;
        apply(project);
        return true;
    }

    /// <summary>Queues a localized in-app notification for the project owner after an admin project action.</summary>
    private async Task QueueProjectNotificationAsync(Project project, string action, string reason, CancellationToken ct)
    {
        var ownerLang = await _db.Users.AsNoTracking()
            .Where(u => u.Id == project.OwnerId).Select(u => u.Language)
            .FirstOrDefaultAsync(ct);

        var notifKey = $"project.{action}";
        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), UserId = project.OwnerId, Type = "admin",
            Title = GetLocalizedTitle(ownerLang, notifKey, project.Title),
            Content = GetLocalizedContent(ownerLang, notifKey, reason, project.Title),
            RelatedEntityType = "Project", RelatedEntityId = project.Id,
            Priority = action == ProjectModerationAction.Feature ? "low" : "medium",
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Aggregated counts used by the admin dashboard and extended statistics endpoint.</summary>
    private record BaseStats(int TotalUsers, int ActiveUsers, int TotalProjects, int ActiveProjects, int CompletedProjects, int ShowcaseProjects, int TotalTeams, int PendingModeration);

    /// <summary>
    /// Computes baseline user, project, team, and moderation counts for admin dashboards.
    /// </summary>
    private async Task<BaseStats> GetBaseStatsAsync(CancellationToken ct)
    {
        var userStats = await _db.Users
            .GroupBy(_ => 0)
            .Select(g => new { Total = g.Count(), Active = g.Count(u => u.IsActive) })
            .FirstOrDefaultAsync(ct);

        var projectStats = await _db.Projects
            .GroupBy(_ => 0)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(p => p.Status == ProjectStatus.Active.Value),
                Completed = g.Count(p => p.Status == ProjectStatus.Completed.Value),
                Showcase = g.Count(p => p.ShowcasePublished)
            })
            .FirstOrDefaultAsync(ct);

        var teamCount = await _db.TeamMembers.CountAsync(tm => tm.Status == TeamMemberStatus.Active.Value, ct);
        var pendingMod = await _db.ModerationReports.CountAsync(r => r.Status == Infrastructure.Constants.ModerationReportStatus.Pending, ct);

        return new BaseStats(
            userStats?.Total ?? 0, userStats?.Active ?? 0,
            projectStats?.Total ?? 0, projectStats?.Active ?? 0,
            projectStats?.Completed ?? 0, projectStats?.Showcase ?? 0,
            teamCount, pendingMod);
    }

    /// <summary>
    /// Returns basic user, project, team, and moderation statistics for admins and curators.
    /// </summary>
    /// <returns>Forbids non-admin/curator callers; otherwise returns aggregated dashboard counts.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();
        var s = await GetBaseStatsAsync(ct);
        return Ok(s);
    }

    /// <summary>Query parameters for filtered, paged admin user listing.</summary>
    /// <param name="Page">Page number to return.</param>
    /// <param name="PageSize">Number of users per page.</param>
    /// <param name="Role">Optional exact role filter.</param>
    /// <param name="Active">Optional active-state filter.</param>
    /// <param name="Search">Optional name or email search text.</param>
    /// <param name="Suspended">When true, limits results to currently suspended users.</param>
    /// <param name="DateFrom">Optional earliest creation date.</param>
    /// <param name="DateTo">Optional latest creation date.</param>
    public record AdminUsersFilter(
        [FromQuery] int Page = 1,
        [FromQuery] int PageSize = 20,
        [FromQuery] string? Role = null,
        [FromQuery] bool? Active = null,
        [FromQuery] string? Search = null,
        [FromQuery] bool? Suspended = null,
        [FromQuery] DateTime? DateFrom = null,
        [FromQuery] DateTime? DateTo = null);

    /// <summary>
    /// Applies role, active-state, search, suspension, and creation-date filters to a user query.
    /// </summary>
    private static IQueryable<User> ApplyUserFilters(IQueryable<User> query, AdminUsersFilter f)
    {
        if (!string.IsNullOrWhiteSpace(f.Role))    query = query.Where(u => u.Role == f.Role);
        if (f.Active != null)                       query = query.Where(u => u.IsActive == f.Active);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.ToLower();
            query = query.Where(u => (u.FullName != null && u.FullName.ToLower().Contains(s)) || u.Email.ToLower().Contains(s));
        }
        if (f.Suspended == true) query = query.Where(u => u.SuspendedUntil != null && u.SuspendedUntil > DateTime.UtcNow);
        if (f.DateFrom != null)  query = query.Where(u => u.CreatedAt >= f.DateFrom);
        if (f.DateTo != null)    query = query.Where(u => u.CreatedAt <= f.DateTo);
        return query;
    }

    /// <summary>
    /// Lists users with filters and pagination for admins and curators.
    /// </summary>
    /// <param name="filter">Filter and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Forbids non-admin/curator callers; otherwise returns total count, page metadata, and user projections.</returns>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] AdminUsersFilter filter, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var q = ApplyUserFilters(_db.Users.AsQueryable(), filter);
        var total = await q.CountAsync(ct);
        var list = await q.OrderByDescending(u => u.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(u => new
            {
                u.Id, u.Email, u.FullName, u.Username, u.Role, u.IsActive, u.IsVerified,
                u.CreatedAt, u.Rating, u.LastLogin, u.AvatarUrl,
                u.SuspendedUntil, u.SuspensionReason
            })
            .ToListAsync(ct);
        return Ok(new { Total = total, Page = filter.Page, PageSize = filter.PageSize, Data = list });
    }

    // ========== User Details & Suspension ==========

    /// <summary>
    /// Describes a user suspension and the reason shown in audit history.
    /// </summary>
    /// <param name="UserId">User account to suspend.</param>
    /// <param name="SuspendedUntil">Timestamp until which the account remains suspended.</param>
    /// <param name="Reason">Reason recorded in the user record and audit log.</param>
    public record SuspendUserRequest(Guid UserId, DateTime SuspendedUntil, string Reason);
    /// <summary>
    /// Describes a block, unblock, or verify action to apply to multiple users.
    /// </summary>
    /// <param name="UserIds">User accounts to update; requests over 100 are rejected.</param>
    /// <param name="Action">Bulk action key: block, unblock, or verify.</param>
    public record BulkUserActionRequest(List<Guid> UserIds, string Action); // block, unblock, verify

    /// <summary>Returns profile, account, and aggregate activity details for one user.</summary>
    [HttpGet("users/{id:guid}/details")]
    public async Task<IActionResult> GetUserDetails(Guid id, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return NotFound();

        var projectsOwned = await _db.Projects.CountAsync(p => p.OwnerId == id, ct);
        var teamsCount = await _db.TeamMembers.CountAsync(tm => tm.UserId == id && tm.Status == TeamMemberStatus.Active.Value, ct);
        var messagesCount = await _db.Messages.CountAsync(m => m.SenderId == id, ct);
        var notesCount = await _db.AdminNotes.CountAsync(n => n.UserId == id, ct);

        return Ok(new
        {
            user.Id, user.Email, user.FullName, user.Username, user.Role, user.IsActive, user.IsVerified,
            user.Bio, user.AvatarUrl, user.Github, user.Linkedin, user.Website,
            user.Skills, user.Experience, user.Rating, user.Language, user.Timezone,
            user.CreatedAt, user.UpdatedAt, user.LastLogin,
            user.SuspendedUntil, user.SuspensionReason,
            user.IsEmailVerified, user.GithubUsername, user.GoogleId,
            Stats = new { projectsOwned, teamsCount, messagesCount, notesCount }
        });
    }

    /// <summary>Returns a paged audit-log activity history for one user.</summary>
    [HttpGet("users/{id:guid}/activity")]
    public async Task<IActionResult> GetUserActivity(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync(ct);
        var logs = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new { a.Id, a.Action, a.EntityType, a.EntityId, a.Details, a.IpAddress, a.Severity, a.CreatedAt })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, data = logs });
    }

    /// <summary>Suspends a user until a specified timestamp and records the reason in the audit log.</summary>
    [HttpPost("users/suspend")]
    public async Task<IActionResult> SuspendUser([FromBody] SuspendUserRequest req, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct);
        if (user == null) return NotFound();

        user.IsActive = false;
        user.SuspendedUntil = req.SuspendedUntil;
        user.SuspensionReason = req.Reason;
        await _db.SaveChangesAsync(ct);

        var adminId = GetRequiredUserId();
        await _auditService.LogActionAsync(adminId, "AdminController.SuspendUser", "User", req.UserId,
            $"Suspended until {req.SuspendedUntil:yyyy-MM-dd}. Reason: {req.Reason}");

        return Ok();
    }

    /// <summary>Clears a user's suspension fields, reactivates the account, and records an audit entry.</summary>
    [HttpPost("users/unsuspend")]
    public async Task<IActionResult> UnsuspendUser([FromBody] Guid userId, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound();

        user.IsActive = true;
        user.SuspendedUntil = null;
        user.SuspensionReason = null;
        await _db.SaveChangesAsync(ct);

        var adminId = GetRequiredUserId();
        await _auditService.LogActionAsync(adminId, "AdminController.UnsuspendUser", "User", userId,
            $"Removed suspension for user {userId}");

        return Ok();
    }

    // ========== Admin Notes ==========

    /// <summary>Lists admin notes for a user, newest first, including author display names.</summary>
    [HttpGet("users/{id:guid}/notes")]
    public async Task<IActionResult> GetAdminNotes(Guid id, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var notes = await _db.AdminNotes
            .AsNoTracking()
            .Where(n => n.UserId == id)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id, n.Content, n.CreatedAt,
                AuthorId = n.AuthorId,
                AuthorName = n.Author != null ? n.Author.FullName ?? n.Author.Email : "Unknown"
            })
            .ToListAsync(ct);

        return Ok(notes);
    }

    /// <summary>Adds a trimmed admin note to a user after validating non-empty content.</summary>
    [HttpPost("users/{id:guid}/notes")]
    public async Task<IActionResult> AddAdminNote(Guid id, [FromBody] string content, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(content)) return BadRequest("Content is required");

        var adminId = GetRequiredUserId();
        var note = new AdminNote
        {
            Id = Guid.NewGuid(),
            UserId = id,
            AuthorId = adminId,
            Content = content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.AdminNotes.Add(note);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(adminId, "AdminController.AddAdminNote", "AdminNote", note.Id,
            $"Added note to user {id}");

        return Ok(new { note.Id });
    }

    /// <summary>Deletes an admin note when the caller is its author or a superadmin.</summary>
    [HttpDelete("users/notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteAdminNote(Guid noteId, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var note = await _db.AdminNotes.FirstOrDefaultAsync(n => n.Id == noteId, ct);
        if (note == null) return NotFound();

        var adminId = GetRequiredUserId();
        var isSuperAdmin = await IsSuperAdminAsync();

        // Only author or superadmin can delete
        if (note.AuthorId != adminId && !isSuperAdmin) return Forbid();

        _db.AdminNotes.Remove(note);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(adminId, "AdminController.DeleteAdminNote", "AdminNote", noteId,
            $"Deleted note from user {note.UserId}");

        return NoContent();
    }

    // ========== Bulk Actions ==========

    /// <summary>Applies a valid block, unblock, or verify action to up to 100 users and audits the affected count.</summary>
    [HttpPost("users/bulk-action")]
    public async Task<IActionResult> BulkUserAction([FromBody] BulkUserActionRequest req, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();
        if (req.UserIds.Count == 0) return BadRequest("No users specified");
        if (req.UserIds.Count > 100) return BadRequest("Maximum 100 users per batch");

        var action = req.Action.ToLowerInvariant();
        if (!BulkAction.IsValid(action))
            return BadRequest("Invalid action. Allowed: block, unblock, verify");

        var users = await _db.Users.Where(u => req.UserIds.Contains(u.Id)).ToListAsync(ct);
        var adminId = GetRequiredUserId();
        var affected = 0;

        foreach (var user in users)
        {
            if (ApplyBulkAction(user, action)) affected++;
        }

        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(adminId, "AdminController.BulkUserAction", "User", Guid.Empty,
            $"Bulk {action}: {affected}/{req.UserIds.Count} users affected");

        return Ok(new { affected, total = req.UserIds.Count });
    }

    /// <summary>
    /// Applies a bulk user action only when it changes the current account state.
    /// </summary>
    /// <returns><see langword="true"/> when the user was changed; otherwise <see langword="false"/>.</returns>
    private static bool ApplyBulkAction(User user, string action)
    {
        if (action == BulkAction.Block && user.IsActive) { user.IsActive = false; return true; }
        if (action == BulkAction.Unblock && !user.IsActive) { user.IsActive = true; user.SuspendedUntil = null; user.SuspensionReason = null; return true; }
        if (action == BulkAction.Verify && !user.IsVerified) { user.IsVerified = true; return true; }
        return false;
    }

    // ========== Project Issues Management ==========

    /// <summary>Request to resolve a project issue report.</summary>
    /// <param name="IssueId">Issue identifier.</param>
    /// <param name="Resolution">Resolution details.</param>
    /// <param name="ActionTaken">Action taken (warn, suspend, remove, dismiss).</param>
    public record ResolveIssueRequest(Guid IssueId, string Resolution, string ActionTaken); // Action: warn, suspend, remove, dismiss

    /// <summary>Projection for project issue listing.</summary>
    /// <param name="Id">Issue identifier.</param>
    /// <param name="Type">Issue type (e.g., abuse, spam).</param>
    /// <param name="Title">Issue title.</param>
    /// <param name="Status">Current issue status.</param>
    /// <param name="Priority">Issue priority.</param>
    /// <param name="ProjectId">Related project identifier.</param>
    /// <param name="ProjectTitle">Related project title.</param>
    /// <param name="ReporterId">Reporter user identifier.</param>
    /// <param name="ReporterEmail">Reporter email address.</param>
    /// <param name="AssignedToAdminId">Assigned admin identifier.</param>
    /// <param name="RelatedUserId">User referenced by the issue.</param>
    /// <param name="CreatedAt">Issue creation timestamp.</param>
    /// <param name="ResolvedAt">Resolution timestamp.</param>
    /// <param name="AdminResolution">Resolution summary.</param>
    public record IssueResponse(
        Guid Id, string Type, string Title, string Status, string Priority,
        Guid ProjectId, string ProjectTitle, Guid ReporterId, string ReporterEmail,
        Guid? AssignedToAdminId, Guid? RelatedUserId,
        DateTime CreatedAt, DateTime? ResolvedAt, string? AdminResolution);

    /// <summary>
    /// Lists project issues with optional status and type filters for admins and curators.
    /// </summary>
    [HttpGet("issues")]
    public async Task<IActionResult> GetAllIssues(
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var query = _db.ProjectIssues
            .Include(i => i.Project)
            .Include(i => i.Reporter)
            .Include(i => i.AssignedToAdmin)
            .Include(i => i.RelatedUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i => i.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(i => i.Type == type);
        }

        var total = await query.CountAsync(ct);
        var issues = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new IssueResponse(
                i.Id, i.Type, i.Title, i.Status, i.Priority,
                i.ProjectId, i.Project != null ? i.Project.Title : "Unknown",
                i.ReporterId, i.Reporter != null ? i.Reporter.Email : "Unknown",
                i.AssignedToAdminId, i.RelatedUserId,
                i.CreatedAt, i.ResolvedAt, i.AdminResolution))
            .ToListAsync(ct);

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Data = issues });
    }

    /// <summary>
    /// Returns a single project issue with project, reporter, assignee, related user, and resolution details.
    /// </summary>
    [HttpGet("issues/{issueId:guid}")]
    public async Task<IActionResult> GetIssue(Guid issueId, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync(ct)) return Forbid();

        var issue = await _db.ProjectIssues
            .Include(i => i.Project)
            .Include(i => i.Reporter)
            .Include(i => i.AssignedToAdmin)
            .Include(i => i.RelatedUser)
            .Include(i => i.ResolvedByAdmin)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct);

        if (issue == null) return NotFound();

        var response = new
        {
            issue.Id,
            issue.Type,
            issue.Title,
            issue.Description,
            issue.Status,
            issue.Priority,
            Project = new { issue.ProjectId, issue.Project?.Title },
            Reporter = new { issue.ReporterId, issue.Reporter?.Email, issue.Reporter?.FullName },
            RelatedUser = issue.RelatedUserId != null ? new { issue.RelatedUserId, issue.RelatedUser?.Email, issue.RelatedUser?.FullName } : null,
            AssignedToAdmin = issue.AssignedToAdminId != null ? new { issue.AssignedToAdminId, issue.AssignedToAdmin?.Email, issue.AssignedToAdmin?.FullName } : null,
            issue.AdminResolution,
            ResolvedByAdmin = issue.ResolvedByAdminId != null ? new { issue.ResolvedByAdminId, issue.ResolvedByAdmin?.Email, issue.ResolvedByAdmin?.FullName } : null,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.ResolvedAt,
            issue.EscalatedAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Assigns a project issue to an admin or curator and moves it into investigation.
    /// </summary>
    [HttpPost("issues/{issueId:guid}/assign")]
    public async Task<IActionResult> AssignIssue(Guid issueId, [FromBody] Guid adminUserId, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync(ct)) return Forbid();

        var issue = await _db.ProjectIssues.FindAsync(new object[] { issueId }, ct);
        if (issue == null) return NotFound();

        var admin = await _db.Users.FindAsync(new object[] { adminUserId }, ct);
        if (admin is not { Role: UserRoles.Admin or UserRoles.Curator })
            return BadRequest("User is not an admin or curator");

        issue.AssignedToAdminId = adminUserId;
        issue.Status = IssueStatus.Investigating.Value;
        issue.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var currentAdminUserId = GetRequiredUserId();
        await _auditService.LogActionAsync(currentAdminUserId, "AdminController.AssignIssue", "ProjectIssue", issueId,
            $"Assigned issue to {admin.Email}");

        return Ok(new { Message = "Issue assigned", AssignedToAdminId = adminUserId });
    }

    // --- Issue Resolution helpers (decomposed from ResolveIssue cc=20) ---

    /// <summary>Notification payload buffered until the issue resolution transaction has been saved.</summary>
    private record DeferredNotification(Guid UserId, string Type, string Title, string Content, string EntityType, Guid EntityId, string Priority);

    /// <summary>Mutable resolution context shared by issue action helpers while they update the issue and queue notifications.</summary>
    private record IssueActionContext(ProjectIssue Issue, string Action, string Resolution, List<DeferredNotification> Deferred);

    /// <summary>Queues an in-database notification and tracks a matching deferred email notification for later delivery.</summary>
    private void QueueIssueNotification(
        Guid userId, string title, string content,
        string entityType, Guid entityId,
        List<DeferredNotification> deferred)
    {
        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), UserId = userId, Type = "admin",
            Title = title, Content = content, Priority = "high",
            RelatedEntityType = entityType, RelatedEntityId = entityId,
            CreatedAt = DateTime.UtcNow
        });
        deferred.Add(new DeferredNotification(userId, "admin", title, content, entityType, entityId, "high"));
    }

    /// <summary>Applies the follow-up action selected while resolving a project issue.</summary>
    private async Task ApplyIssueActionAsync(IssueActionContext ctx, CancellationToken ct)
    {
        if (ctx.Action == IssueActionType.Warn)
        {
            if (ctx.Issue.RelatedUserId.HasValue)
            {
                QueueIssueNotification(
                    ctx.Issue.RelatedUserId.Value,
                    "Admin warning",
                    $"Project '{ctx.Issue.Project?.Title}': {ctx.Resolution}",
                    "ProjectIssue", ctx.Issue.Id, ctx.Deferred);
            }
        }
        else if (ctx.Action == IssueActionType.Suspend || ctx.Action == IssueActionType.Remove)
        {
            await ApplyTeamMemberActionAsync(ctx, ct);
        }
        else if (ctx.Action != IssueActionType.Dismiss)
        {
            _logger.LogWarning("Unknown action taken: {Action}", ctx.Action);
        }
    }

    /// <summary>Suspends or removes the related team member from the issue project and queues notifications.</summary>
    private async Task ApplyTeamMemberActionAsync(IssueActionContext ctx, CancellationToken ct)
    {
        if (!ctx.Issue.RelatedUserId.HasValue || ctx.Issue.ProjectId == Guid.Empty) return;

        var teamMember = await _db.TeamMembers
            .FirstOrDefaultAsync(tm => tm.ProjectId == ctx.Issue.ProjectId && tm.UserId == ctx.Issue.RelatedUserId.Value, ct);
        if (teamMember == null) return;

        var (newStatus, title, content) = ctx.Action == IssueActionType.Suspend
            ? (TeamMemberStatus.Suspended.Value, "Project participation suspended",
               $"Your participation in project '{ctx.Issue.Project?.Title}' has been suspended. {ctx.Resolution}")
            : (TeamMemberStatus.Removed.Value, "Removed from project",
               $"You have been removed from project '{ctx.Issue.Project?.Title}'. {ctx.Resolution}");

        teamMember.Status = newStatus;
        QueueIssueNotification(ctx.Issue.RelatedUserId.Value, title, content, "Project", ctx.Issue.ProjectId, ctx.Deferred);
    }

    /// <summary>
    /// Resolves a project issue, applies any follow-up action, sends deferred notifications, and audits the resolution.
    /// </summary>
    [HttpPost("issues/{issueId:guid}/resolve")]
    public async Task<IActionResult> ResolveIssue(Guid issueId, [FromBody] ResolveIssueRequest request, CancellationToken ct = default)
    {
        if (!await IsAdminAsync()) return Forbid();

        var issue = await _db.ProjectIssues
            .Include(i => i.Project)
            .Include(i => i.RelatedUser)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct);

        if (issue == null) return NotFound();

        var adminUserId = GetRequiredUserId();

        issue.Status = IssueStatus.Resolved.Value;
        issue.AdminResolution = request.Resolution;
        issue.ResolvedByAdminId = adminUserId;
        issue.ResolvedAt = DateTime.UtcNow;
        issue.UpdatedAt = DateTime.UtcNow;

        // AP-07: Collect notifications to send AFTER SaveChangesAsync
        var deferred = new List<DeferredNotification>();

        if (!string.IsNullOrWhiteSpace(request.ActionTaken))
            await ApplyIssueActionAsync(new IssueActionContext(issue, request.ActionTaken.ToLowerInvariant(), request.Resolution, deferred), ct);

        await _db.SaveChangesAsync(ct);

        // AP-07: Send deferred email notifications AFTER successful SaveChanges
        foreach (var n in deferred)
        {
            await _notificationService.SendNotificationAsync(
                n.UserId, n.Type, n.Title, n.Content, n.EntityType, n.EntityId, n.Priority);
        }

        if (issue.ReporterId != adminUserId)
        {
            await _notificationService.SendNotificationAsync(
                issue.ReporterId, "admin",
                $"Issue in project '{issue.Project?.Title}' resolved",
                $"Administrator resolved issue '{issue.Title}': {request.Resolution}",
                "ProjectIssue", issueId, "medium");
        }

        await _auditService.LogActionAsync(adminUserId, "AdminController.ResolveIssue", "ProjectIssue", issueId,
            $"Resolved issue. Action: {request.ActionTaken}. Resolution: {request.Resolution}");

        await HttpContext.RequestServices.TriggerAchievementCheckAsync(adminUserId, AchievementTrigger.IssueResolved);

        return Ok(new { Message = "Issue resolved", Status = issue.Status, ActionTaken = request.ActionTaken });
    }

    /// <summary>
    /// Escalates a project issue to urgent priority and records the admin action.
    /// </summary>
    [HttpPut("issues/{issueId:guid}/escalate")]
    public async Task<IActionResult> EscalateIssue(Guid issueId, CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync(ct)) return Forbid();

        var issue = await _db.ProjectIssues.FindAsync(new object[] { issueId }, ct);
        if (issue == null) return NotFound();

        issue.Priority = IssuePriority.Urgent.Value;
        issue.Status = IssueStatus.Escalated.Value;
        issue.EscalatedAt = DateTime.UtcNow;
        issue.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var adminUserId = GetRequiredUserId();
        await _auditService.LogActionAsync(adminUserId, "AdminController.EscalateIssue", "ProjectIssue", issueId,
            "Issue escalated to urgent priority");

        await HttpContext.RequestServices.TriggerAchievementCheckAsync(adminUserId, AchievementTrigger.IssueEscalated);

        return Ok(new { Message = "Issue escalated", Priority = issue.Priority });
    }

    // ========== Username Change ==========

    /// <summary>Validates username length and allowed characters.</summary>
    /// <returns>An error message when invalid; otherwise <see langword="null"/>.</returns>
    private static string? ValidateUsernameFormat(string username)
    {
        if (username.Length > 50) return "Username must be 50 characters or fewer";
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_\-\.]+$"))
            return "Username may only contain letters, digits, underscores, hyphens and dots";
        return null;
    }

    /// <summary>Saves pending changes and maps username unique-index violations to a conflict response.</summary>
    /// <returns>A conflict response when the username is taken; otherwise <see langword="null"/>.</returns>
    private async Task<IActionResult?> SaveWithUsernameConflictAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException?.Message.Contains("username", StringComparison.OrdinalIgnoreCase) == true
               || ex.InnerException?.Message.Contains("23505") == true)
        {
            return Conflict("Username is already taken");
        }
    }

    /// <summary>Queues a localized notification for a username change.</summary>
    private void QueueUsernameNotification(Guid userId, string? language, string newUsername, string reason)
    {
        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), UserId = userId, Type = "admin",
            Title = GetLocalizedTitle(language, "account.username", newUsername),
            Content = GetLocalizedContent(language, "account.username", reason, newUsername),
            Priority = "normal", CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// PUT /api/admin/users/{userId}/change-name — Change a user's username (nickname).
    /// Requires admin or curator role. Sends a localised notification to the affected user.
    /// </summary>
    [HttpPut("users/{userId:guid}/change-name")]
    public async Task<IActionResult> ChangeUserName(Guid userId, [FromBody] ChangeUsernameRequest req, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(req.NewUsername)) return BadRequest("New username is required");
        if (string.IsNullOrWhiteSpace(req.Reason))      return BadRequest("Reason is required");

        var formatError = ValidateUsernameFormat(req.NewUsername);
        if (formatError != null) return BadRequest(formatError);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound();

        // AP-04: Removed TOCTOU AnyAsync — rely on UNIQUE INDEX + catch in SaveWithUsernameConflictAsync
        var oldUsername = user.Username;
        user.Username = req.NewUsername;
        user.UpdatedAt = DateTime.UtcNow;

        QueueUsernameNotification(userId, user.Language, req.NewUsername, req.Reason);

        var conflict = await SaveWithUsernameConflictAsync(ct);
        if (conflict != null) return conflict;

        var adminId = GetRequiredUserId();
        await _auditService.LogActionAsync(adminId, "admin.change_username", "User", userId,
            $"\"{oldUsername ?? "(none)"}\" → \"{req.NewUsername}\". Reason: {req.Reason}");

        return Ok(new { username = req.NewUsername });
    }

    // ========== Edit User Profile ==========

    /// <summary>
    /// Describes profile fields an admin or curator may patch for a user.
    /// </summary>
    /// <param name="FullName">Optional replacement display name.</param>
    /// <param name="Username">Optional replacement username; an empty string clears it.</param>
    /// <param name="Bio">Optional replacement biography, sanitized before storage.</param>
    /// <param name="Reason">Reason recorded in audit history and username notifications.</param>
    public record AdminEditProfileRequest(string? FullName, string? Username, string? Bio, string Reason);

    /// <summary>
    /// Validates and applies a display-name patch, recording the change description.
    /// </summary>
    /// <returns>An error message for invalid lengths; otherwise <see langword="null"/>.</returns>
    private static string? ValidateAndApplyFullName(User user, string? fullName, List<string> changes)
    {
        if (fullName == null) return null;
        var trimmed = fullName.Trim();
        if (trimmed.Length < 2) return "Full name must be at least 2 characters";
        if (trimmed.Length > 100) return "Full name must be 100 characters or fewer";
        changes.Add($"FullName: \"{user.FullName}\" → \"{trimmed}\"");
        user.FullName = trimmed;
        return null;
    }

    /// <summary>
    /// Validates and applies a username patch, including clearing usernames with empty input.
    /// </summary>
    /// <returns>The validation error, if any, and whether the username changed.</returns>
    private static (string? Error, bool Changed) ValidateAndApplyUsername(User user, string? username, List<string> changes)
    {
        if (username == null) return (null, false);
        var trimmed = username.Trim();
        if (trimmed.Length == 0)
        {
            changes.Add($"Username: \"{user.Username ?? "(none)"}\" → (cleared)");
            user.Username = null;
            return (null, true);
        }
        var formatError = ValidateUsernameFormat(trimmed);
        if (formatError != null) return (formatError, false);
        // AP-04: Removed TOCTOU AnyAsync — rely on UNIQUE INDEX + catch below
        changes.Add($"Username: \"{user.Username ?? "(none)"}\" → \"{trimmed}\"");
        user.Username = trimmed;
        return (null, true);
    }

    /// <summary>
    /// Applies a sanitized biography patch and records that the bio changed.
    /// </summary>
    private static void ApplyBio(User user, string? bio, List<string> changes)
    {
        if (bio == null) return;
        var trimmed = bio.Trim();
        // AP-03: Sanitize Bio to prevent XSS
        user.Bio = trimmed.Length == 0 ? null : SecurityHelpers.SanitizeHtml(trimmed);
        changes.Add("Bio updated");
    }

    /// <summary>Collected profile change descriptions plus whether a username notification is needed.</summary>
    private record ProfileChanges(List<string> Changes, bool UsernameChanged);

    /// <summary>
    /// Validates admin profile edits and applies them to the tracked user.
    /// </summary>
    /// <returns>An error for invalid or empty updates; otherwise the collected changes.</returns>
    private static (string? Error, ProfileChanges? Result) CollectProfileChanges(User user, AdminEditProfileRequest req)
    {
        var changes = new List<string>();

        var fullNameError = ValidateAndApplyFullName(user, req.FullName, changes);
        if (fullNameError != null) return (fullNameError, null);

        var (usernameError, usernameChanged) = ValidateAndApplyUsername(user, req.Username, changes);
        if (usernameError != null) return (usernameError, null);

        ApplyBio(user, req.Bio, changes);

        if (changes.Count == 0) return ("No changes provided", null);

        return (null, new ProfileChanges(changes, usernameChanged));
    }

    /// <summary>Edits a user's display name, username, and bio, then audits the applied changes.</summary>
    [HttpPut("users/{userId:guid}/edit-profile")]
    public async Task<IActionResult> EditUserProfile(Guid userId, [FromBody] AdminEditProfileRequest req, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Reason)) return BadRequest("Reason is required");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound();

        var (error, result) = CollectProfileChanges(user, req);
        if (error != null) return BadRequest(error);

        user.UpdatedAt = DateTime.UtcNow;

        if (result!.UsernameChanged && user.Username != null)
            QueueUsernameNotification(userId, user.Language, user.Username, req.Reason);

        var conflict = await SaveWithUsernameConflictAsync(ct);
        if (conflict != null) return conflict;

        var adminId = GetRequiredUserId();
        await _auditService.LogActionAsync(adminId, "admin.edit_profile", "User", userId,
            string.Join("; ", result.Changes) + $". Reason: {req.Reason}");

        return Ok();
    }

    /// <summary>
    /// Returns dashboard statistics that combine base, support, feedback, and issue counts.
    /// </summary>
    [HttpGet("stats/extended")]
    public async Task<IActionResult> GetExtendedStats(CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var baseStats = await GetBaseStatsAsync(ct);
        var supportStats = await GetSupportStatsAsync(ct);
        var feedbackStats = await GetFeedbackStatsAsync(ct);
        var issueStats = await GetIssueStatsAsync(ct);

        return Ok(new
        {
            baseStats.TotalUsers,
            baseStats.ActiveUsers,
            baseStats.TotalProjects,
            baseStats.ActiveProjects,
            baseStats.CompletedProjects,
            baseStats.ShowcaseProjects,
            baseStats.TotalTeams,
            baseStats.PendingModeration,
            supportStats.OpenSupportTickets,
            supportStats.InProgressTickets,
            supportStats.ResolvedTickets,
            feedbackStats.OpenFeedback,
            feedbackStats.PlannedFeedback,
            feedbackStats.CompletedFeedback,
            issueStats.OpenIssues,
            issueStats.InvestigatingIssues,
            issueStats.ResolvedIssues
        });
    }

    /// <summary>Support ticket counts by current workflow status.</summary>
    private record SupportStats(int OpenSupportTickets, int InProgressTickets, int ResolvedTickets);
    /// <summary>Feedback counts by current product-planning status.</summary>
    private record FeedbackStats(int OpenFeedback, int PlannedFeedback, int CompletedFeedback);
    /// <summary>Project issue counts by current moderation workflow status.</summary>
    private record IssueStats(int OpenIssues, int InvestigatingIssues, int ResolvedIssues);

    /// <summary>
    /// Computes support ticket counts used by the extended stats endpoint.
    /// </summary>
    private async Task<SupportStats> GetSupportStatsAsync(CancellationToken ct)
    {
        var s = await _db.SupportTickets
            .GroupBy(_ => 0)
            .Select(g => new { Open = g.Count(t => t.Status == SupportTicketStatus.Open.Value), InProgress = g.Count(t => t.Status == SupportTicketStatus.InProgress.Value), Resolved = g.Count(t => t.Status == SupportTicketStatus.Resolved.Value) })
            .FirstOrDefaultAsync(ct);
        return new SupportStats(s?.Open ?? 0, s?.InProgress ?? 0, s?.Resolved ?? 0);
    }

    /// <summary>
    /// Computes community feedback counts used by the extended stats endpoint.
    /// </summary>
    private async Task<FeedbackStats> GetFeedbackStatsAsync(CancellationToken ct)
    {
        var f = await _db.FeedbackItems
            .GroupBy(_ => 0)
            .Select(g => new { Open = g.Count(x => x.Status == FeedbackItemStatus.Open.Value), Planned = g.Count(x => x.Status == FeedbackItemStatus.Planned.Value), Completed = g.Count(x => x.Status == FeedbackItemStatus.Completed.Value) })
            .FirstOrDefaultAsync(ct);
        return new FeedbackStats(f?.Open ?? 0, f?.Planned ?? 0, f?.Completed ?? 0);
    }

    /// <summary>
    /// Computes project issue counts used by the extended stats endpoint.
    /// </summary>
    private async Task<IssueStats> GetIssueStatsAsync(CancellationToken ct)
    {
        var i = await _db.ProjectIssues
            .GroupBy(_ => 0)
            .Select(g => new { Open = g.Count(x => x.Status == IssueStatus.Open.Value), Investigating = g.Count(x => x.Status == IssueStatus.Investigating.Value), Resolved = g.Count(x => x.Status == IssueStatus.Resolved.Value) })
            .FirstOrDefaultAsync(ct);
        return new IssueStats(i?.Open ?? 0, i?.Investigating ?? 0, i?.Resolved ?? 0);
    }
}

