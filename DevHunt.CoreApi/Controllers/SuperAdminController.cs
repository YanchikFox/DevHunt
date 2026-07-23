using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Middleware;
using DevHunt.CoreApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Superadmin-exclusive endpoints for maximum-privilege operations.
/// All actions are audit-logged with critical severity.
/// Protected by: JWT auth + DB role validation + IP whitelist + password confirmation.
/// </summary>
[ApiController]
[Route("api/superadmin")]
[Authorize]
[IpWhitelist]
public class SuperAdminController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly IAuditService _auditService;
    private readonly ILogger<SuperAdminController> _logger;
    private readonly IMemoryCache _cache;
    private readonly IFeatureFlagService _featureFlags;

    /// <summary>
    /// Creates the superadmin controller with database, audit, cache, feature flag, and logging services.
    /// </summary>
    /// <param name="db">Database context used for privileged platform mutations.</param>
    /// <param name="auditService">Audit service that records critical superadmin actions.</param>
    /// <param name="logger">Logger for high-risk operational events.</param>
    /// <param name="cache">Memory cache used by maintenance-mode checks.</param>
    /// <param name="featureFlags">Feature flag service used to invalidate changed flags.</param>
    public SuperAdminController(DevHuntDbContext db, IAuditService auditService, ILogger<SuperAdminController> logger, IMemoryCache cache, IFeatureFlagService featureFlags)
    {
        _db = db;
        _auditService = auditService;
        _logger = logger;
        _cache = cache;
        _featureFlags = featureFlags;
    }

    // ── Request DTOs ───────────────────────────────────────

    /// <summary>
    /// Carries a password confirmation for endpoints that pre-check dangerous operations.
    /// </summary>
    /// <param name="ConfirmPassword">Plain-text password supplied by the current superadmin for verification.</param>
    public record ConfirmableRequest(string ConfirmPassword);
    /// <summary>
    /// Identifies a user to permanently delete after superadmin password verification.
    /// </summary>
    /// <param name="UserId">User account to remove along with dependent records.</param>
    /// <param name="ConfirmPassword">Password confirmation for the acting superadmin.</param>
    /// <param name="Reason">Audit reason for the irreversible deletion.</param>
    public record HardDeleteUserRequest(Guid UserId, string ConfirmPassword, string Reason);
    /// <summary>
    /// Identifies a project to permanently delete after superadmin password verification.
    /// </summary>
    /// <param name="ProjectId">Project to remove along with related collaboration and moderation data.</param>
    /// <param name="ConfirmPassword">Password confirmation for the acting superadmin.</param>
    /// <param name="Reason">Audit reason for the irreversible deletion.</param>
    public record HardDeleteProjectRequest(Guid ProjectId, string ConfirmPassword, string Reason);
    /// <summary>
    /// Identifies a user to promote to admin after superadmin password verification.
    /// </summary>
    /// <param name="UserId">User account that should receive the admin role.</param>
    /// <param name="ConfirmPassword">Password confirmation for the acting superadmin.</param>
    public record PromoteAdminRequest(Guid UserId, string ConfirmPassword);
    /// <summary>
    /// Identifies an admin to demote and the non-superadmin role to assign.
    /// </summary>
    /// <param name="UserId">Admin account whose role should be changed.</param>
    /// <param name="ConfirmPassword">Password confirmation for the acting superadmin.</param>
    /// <param name="NewRole">Requested target role; unsupported values fall back to participant.</param>
    public record DemoteAdminRequest(Guid UserId, string ConfirmPassword, string NewRole = "participant");
    /// <summary>
    /// Contains platform setting key/value updates guarded by password confirmation.
    /// </summary>
    /// <param name="Settings">Settings to upsert, normalized to lowercase keys before storage.</param>
    /// <param name="ConfirmPassword">Password confirmation for the acting superadmin.</param>
    public record UpdateSettingsRequest(Dictionary<string, string> Settings, string ConfirmPassword);
    /// <summary>
    /// Supplies password confirmation for toggling a feature flag.
    /// </summary>
    /// <param name="ConfirmPassword">Password confirmation for the acting superadmin.</param>
    public record ToggleFeatureFlagRequest(string ConfirmPassword);
    /// <summary>
    /// Sets maintenance mode after superadmin password verification.
    /// </summary>
    /// <param name="Enabled">Whether maintenance mode should be enabled.</param>
    /// <param name="ConfirmPassword">Password confirmation for the acting superadmin.</param>
    public record ToggleMaintenanceRequest(bool Enabled, string ConfirmPassword);
    /// <summary>
    /// Adds an IP address to the superadmin whitelist after password verification.
    /// </summary>
    /// <param name="Ip">IP address or host token to persist in platform settings.</param>
    /// <param name="ConfirmPassword">Password confirmation for the acting superadmin.</param>
    public record AddIpWhitelistRequest(string Ip, string ConfirmPassword);
    /// <summary>
    /// Describes a system notification to broadcast to every active user.
    /// </summary>
    /// <param name="Title">Notification title; blank titles are rejected.</param>
    /// <param name="Content">Notification body; null content is saved as an empty string.</param>
    /// <param name="Priority">Requested priority; unsupported values are treated as normal.</param>
    /// <param name="ConfirmPassword">Password confirmation for the acting superadmin.</param>
    public record BroadcastNotificationRequest(string Title, string Content, string Priority, string ConfirmPassword);

    // ── Helpers ────────────────────────────────────────────

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Loads the active superadmin user from the database instead of trusting JWT role claims.
    /// </summary>
    private async Task<User?> GetVerifiedSuperAdminAsync(CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return null;

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.IsActive && u.Role == "superadmin", ct);

        return user;
    }

    /// <summary>
    /// Verifies a supplied password against the superadmin's BCrypt hash, returning false for missing or invalid hashes.
    /// </summary>
    private bool VerifyPassword(User superAdmin, string password)
    {
        if (string.IsNullOrEmpty(superAdmin.PasswordHash)) return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, superAdmin.PasswordHash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads a tracked active superadmin and verifies the confirmation password.
    /// </summary>
    /// <returns>The tracked superadmin when role and password checks pass; otherwise, <see langword="null"/>.</returns>
    private async Task<User?> AuthorizeDangerousActionAsync(string confirmPassword, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return null;

        // Need tracked entity to verify password
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.IsActive && u.Role == "superadmin", ct);

        if (user == null) return null;
        if (!VerifyPassword(user, confirmPassword)) return null;

        return user;
    }

    // ── Audit Log Viewer ───────────────────────────────────

    /// <summary>
    /// Returns paged audit log entries after verifying the caller is still an active superadmin in the database.
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string? action = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? ip = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] bool adminOnly = true,
        CancellationToken ct = default)
    {
        if (await GetVerifiedSuperAdminAsync() == null) return Forbid();

        var logs  = await _auditService.GetAuditLogsAsync(page, pageSize, action, userId, severity, ip, entityType, dateFrom, dateTo, adminOnly);
        var total = await _auditService.GetAuditLogCountAsync(action, userId, severity, ip, entityType, dateFrom, dateTo, adminOnly);

        return Ok(new { Data = logs, Total = total, Page = page, PageSize = pageSize });
    }

    // ── Hard Delete User ───────────────────────────────────

    /// <summary>
    /// Permanently deletes a non-superadmin user and dependent records after password confirmation.
    /// </summary>
    /// <returns>Forbids failed superadmin/password checks, returns not found for a missing target, rejects deleting self or another superadmin, and returns a success message after the transaction commits.</returns>
    [HttpPost("hard-delete/user")]
    public async Task<IActionResult> HardDeleteUser([FromBody] HardDeleteUserRequest req, CancellationToken ct = default)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Forbid();

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct);
        if (target == null) return NotFound();

        // SECURITY: Cannot delete yourself or another superadmin
        if (target.Id == admin.Id)
            return BadRequest("Cannot delete your own account");
        if (target.Role == "superadmin")
            return BadRequest("Cannot delete another superadmin account");

        var targetEmail = target.Email;
        var targetRole = target.Role;

        // AP-14: Wrap all 7-table cascade delete in an explicit transaction
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Remove related data in order (respecting FK constraints).
        // DEV-126: bulk ExecuteDeleteAsync instead of loading entire dependent tables into the
        // change tracker — bounded memory and a shorter transaction.
        await _db.UserAchievements.Where(ua => ua.UserId == req.UserId).ExecuteDeleteAsync(ct);
        await _db.TeamMembers.Where(tm => tm.UserId == req.UserId).ExecuteDeleteAsync(ct);
        await _db.Notifications.Where(n => n.UserId == req.UserId).ExecuteDeleteAsync(ct);
        await _db.UserSkillEntries.Where(us => us.UserId == req.UserId).ExecuteDeleteAsync(ct);
        await _db.UserFollows.Where(f => f.FollowerId == req.UserId || f.FollowedId == req.UserId).ExecuteDeleteAsync(ct);
        await _db.ActivityRecords.Where(a => a.ActorId == req.UserId).ExecuteDeleteAsync(ct);
        await _db.RefreshTokens.Where(rt => rt.UserId == req.UserId).ExecuteDeleteAsync(ct);

        await _db.Users.Where(u => u.Id == req.UserId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        await _auditService.LogActionAsync(admin.Id, "superadmin.hard_delete_user", "User", req.UserId,
            $"Hard deleted user {targetEmail} (role: {targetRole}). Reason: {req.Reason}", null, "critical");

        _logger.LogWarning("SUPERADMIN: Hard deleted user {Email} ({UserId}) by {AdminId}. Reason: {Reason}",
            targetEmail, req.UserId, admin.Id, req.Reason);

        return Ok(new { Message = $"User {targetEmail} permanently deleted" });
    }

    // ── Hard Delete Project ────────────────────────────────

    /// <summary>
    /// Permanently deletes a project and related collaboration, file, subscription, and moderation records after password confirmation.
    /// </summary>
    /// <returns>Forbids failed superadmin/password checks, returns not found for a missing project, and returns a success message after deletion is audited.</returns>
    [HttpPost("hard-delete/project")]
    public async Task<IActionResult> HardDeleteProject([FromBody] HardDeleteProjectRequest req, CancellationToken ct = default)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Forbid();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == req.ProjectId, ct);
        if (project == null) return NotFound();

        var projectTitle = project.Title;

        // Remove related data
        _db.TeamMembers.RemoveRange(await _db.TeamMembers.Where(tm => tm.ProjectId == req.ProjectId).ToListAsync(ct));
        _db.Tasks.RemoveRange(await _db.Tasks.Where(t => t.ProjectId == req.ProjectId).ToListAsync(ct));
        _db.Invitations.RemoveRange(await _db.Invitations.Where(i => i.ProjectId == req.ProjectId).ToListAsync(ct));
        _db.ProjectTechStacks.RemoveRange(await _db.ProjectTechStacks.Where(pt => pt.ProjectId == req.ProjectId).ToListAsync(ct));
        _db.ProjectRoles.RemoveRange(await _db.ProjectRoles.Where(pr => pr.ProjectId == req.ProjectId).ToListAsync(ct));
        _db.ProjectFiles.RemoveRange(await _db.ProjectFiles.Where(pf => pf.ProjectId == req.ProjectId).ToListAsync(ct));
        _db.ProjectDocuments.RemoveRange(await _db.ProjectDocuments.Where(pd => pd.ProjectId == req.ProjectId).ToListAsync(ct));
        _db.ProjectSubscriptions.RemoveRange(await _db.ProjectSubscriptions.Where(ps => ps.ProjectId == req.ProjectId).ToListAsync(ct));
        _db.ModerationReports.RemoveRange(await _db.ModerationReports.Where(mr => mr.TargetId == req.ProjectId).ToListAsync(ct));

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(admin.Id, "superadmin.hard_delete_project", "Project", req.ProjectId,
            $"Hard deleted project '{projectTitle}'. Reason: {req.Reason}", null, "critical");

        return Ok(new { Message = $"Project '{projectTitle}' permanently deleted" });
    }

    // ── Admin Management ───────────────────────────────────

    /// <summary>
    /// Promotes a non-superadmin user to admin, creates a notification, and writes a critical audit entry.
    /// </summary>
    /// <returns>Forbids failed superadmin/password checks, returns not found for a missing user, rejects existing admins or superadmins, and returns a success message.</returns>
    [HttpPost("promote-admin")]
    public async Task<IActionResult> PromoteToAdmin([FromBody] PromoteAdminRequest req, CancellationToken ct)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Forbid();

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct);
        if (target == null) return NotFound();

        if (target.Role == "admin") return BadRequest("User is already an admin");
        if (target.Role == "superadmin") return BadRequest("Cannot modify superadmin role");

        var oldRole = target.Role;
        target.Role = "admin";

        // AP-08: Single SaveChangesAsync for role change + notification
        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = target.Id,
            Type = "system",
            Title = "Role Updated",
            Content = "You have been promoted to Admin.",
            Priority = "high",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        // Audit after successful commit
        await _auditService.LogActionAsync(admin.Id, "superadmin.promote_admin", "User", req.UserId,
            $"Promoted user {target.Email} from {oldRole} to admin", null, "critical");

        return Ok(new { Message = $"User {target.Email} promoted to admin" });
    }

    /// <summary>
    /// Demotes an admin to participant, curator, or company after password confirmation.
    /// </summary>
    /// <returns>Forbids failed superadmin/password checks, returns not found for a missing user, rejects self-demotion and superadmin demotion, and returns the assigned role.</returns>
    [HttpPost("demote-admin")]
    public async Task<IActionResult> DemoteAdmin([FromBody] DemoteAdminRequest req, CancellationToken ct = default)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Forbid();

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct);
        if (target == null) return NotFound();

        if (target.Role == "superadmin") return BadRequest("Cannot demote a superadmin");
        if (target.Id == admin.Id) return BadRequest("Cannot demote yourself");

        var oldRole = target.Role;
        var newRole = req.NewRole is "participant" or "curator" or "company" ? req.NewRole : "participant";
        target.Role = newRole;
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(admin.Id, "superadmin.demote_admin", "User", req.UserId,
            $"Demoted user {target.Email} from {oldRole} to {newRole}", null, "critical");

        return Ok(new { Message = $"User {target.Email} demoted to {newRole}" });
    }

    // ── System Overview ────────────────────────────────────

    /// <summary>
    /// Returns platform-wide counts and the latest critical audit actions for a verified superadmin.
    /// </summary>
    [HttpGet("system-info")]
    public async Task<IActionResult> GetSystemInfo(CancellationToken ct = default)
    {
        if (await GetVerifiedSuperAdminAsync() == null) return Forbid();

        var stats = new
        {
            TotalUsers = await _db.Users.CountAsync(ct),
            ActiveUsers = await _db.Users.CountAsync(u => u.IsActive),
            BlockedUsers = await _db.Users.CountAsync(u => !u.IsActive),
            AdminCount = await _db.Users.CountAsync(u => u.Role == "admin"),
            CuratorCount = await _db.Users.CountAsync(u => u.Role == "curator"),
            SuperAdminCount = await _db.Users.CountAsync(u => u.Role == "superadmin"),
            TotalProjects = await _db.Projects.CountAsync(ct),
            TotalAuditLogs = await _db.AuditLogs.CountAsync(ct),
            RecentCriticalActions = await _db.AuditLogs
                .Where(a => a.Severity == "critical")
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .Select(a => new { a.Action, a.Details, a.CreatedAt, a.IpAddress })
                .ToListAsync(ct),
        };

        return Ok(stats);
    }

    // ── List Admins ────────────────────────────────────────

    /// <summary>
    /// Lists active metadata for users with admin, curator, or superadmin roles after database-backed superadmin verification.
    /// </summary>
    [HttpGet("admins")]
    public async Task<IActionResult> ListAdmins(CancellationToken ct = default)
    {
        if (await GetVerifiedSuperAdminAsync() == null) return Forbid();

        var admins = await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == "admin" || u.Role == "curator" || u.Role == "superadmin")
            .OrderBy(u => u.Role)
            .ThenBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.IsActive,
                u.IsVerified,
                u.CreatedAt,
                u.LastLogin
            })
            .ToListAsync(ct);

        return Ok(admins);
    }

    // ── Password Verification Endpoint ─────────────────────

    /// <summary>
    /// Verifies the current superadmin's password as a pre-check for dangerous actions.
    /// </summary>
    [HttpPost("verify-password")]
    public async Task<IActionResult> VerifyPasswordEndpoint([FromBody] ConfirmableRequest req)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Unauthorized(new { Message = "Invalid password or insufficient permissions" });

        return Ok(new { Message = "Password verified" });
    }

    // ── Platform Settings ───────────────────────────────────

    /// <summary>
    /// Lists platform settings ordered by key after database-backed superadmin verification.
    /// </summary>
    /// <param name="ct">Cancellation token for the database query.</param>
    /// <returns>Forbids non-superadmins; otherwise returns setting metadata including updater and timestamp.</returns>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        if (await GetVerifiedSuperAdminAsync() == null) return Forbid();

        var settings = await _db.PlatformSettings.AsNoTracking().OrderBy(s => s.Key).ToListAsync(ct);
        return Ok(settings.Select(s => new
        {
            s.Key, s.Value, s.Description, s.UpdatedAt, s.UpdatedById
        }));
    }

    /// <summary>
    /// Upserts platform settings, invalidates maintenance-mode cache when needed, and records a critical audit entry.
    /// </summary>
    /// <param name="req">Settings payload and password confirmation.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids failed superadmin/password checks and returns the number of submitted setting changes.</returns>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest req, CancellationToken ct)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Forbid();

        var changes = new List<string>();
        foreach (var (key, value) in req.Settings)
        {
            // Normalize to lowercase — the frontend's PascalCase request interceptor
            // capitalises the first letter (maintenance_mode → Maintenance_mode).
            // Always store and look up with the canonical lowercase key.
            var normalizedKey = key.ToLowerInvariant();

            var setting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == normalizedKey, ct);
            if (setting == null)
            {
                setting = new PlatformSetting { Key = normalizedKey, Value = value, UpdatedAt = DateTime.UtcNow, UpdatedById = admin.Id };
                _db.PlatformSettings.Add(setting);
            }
            else if (setting.Value != value)
            {
                setting.Value = value;
                setting.UpdatedAt = DateTime.UtcNow;
                setting.UpdatedById = admin.Id;
            }
            changes.Add($"{normalizedKey}={value}");
        }

        if (changes.Count > 0)
        {
            await _db.SaveChangesAsync(ct);

            // Invalidate maintenance mode cache if that setting was part of the update
            if (req.Settings.Keys.Any(k => k.ToLowerInvariant() == "maintenance_mode"))
                _cache.Remove(MaintenanceModeMiddleware.CacheKey);

            await _auditService.LogActionAsync(admin.Id, "superadmin.update_settings", "PlatformSettings", null,
                $"Updated settings: {string.Join(", ", changes)}", null, "critical");
        }

        return Ok(new { Message = $"{changes.Count} settings updated" });
    }

    // ── Feature Flags ───────────────────────────────────────

    private static readonly (string Key, string Description)[] DefaultFeatureFlags =
    [
        ("ai_features",          "AI-powered features (ML service)"),
        ("ai_chat",              "AI chat assistant"),
        ("showcase",             "Project showcase / public gallery"),
        ("notifications",        "Push notifications"),
        ("github_integration",   "GitHub repository integration"),
        ("registration",         "New user registration"),
    ];

    /// <summary>
    /// Ensures default feature flags exist and returns all flags ordered by key for a verified superadmin.
    /// </summary>
    /// <param name="ct">Cancellation token for database queries and inserts.</param>
    /// <returns>Forbids non-superadmins; otherwise returns feature flag state and update metadata.</returns>
    [HttpGet("feature-flags")]
    public async Task<IActionResult> GetFeatureFlags(CancellationToken ct)
    {
        if (await GetVerifiedSuperAdminAsync() == null) return Forbid();

        // Upsert: ensure every default flag exists (safe to run on every GET)
        var existing = await _db.FeatureFlags.Select(f => f.Key).ToHashSetAsync(ct);
        var missing = DefaultFeatureFlags.Where(d => !existing.Contains(d.Key)).ToList();
        if (missing.Count > 0)
        {
            _db.FeatureFlags.AddRange(missing.Select(f => new Infrastructure.Models.FeatureFlag
            {
                Key = f.Key, Enabled = true, Description = f.Description, UpdatedAt = DateTime.UtcNow
            }));
            await _db.SaveChangesAsync(ct);
        }

        var flags = await _db.FeatureFlags.AsNoTracking().OrderBy(f => f.Key).ToListAsync(ct);
        return Ok(flags.Select(f => new
        {
            f.Key, f.Enabled, f.Description, f.UpdatedAt, f.UpdatedById
        }));
    }

    /// <summary>
    /// Toggles a feature flag, invalidates its cached state, and writes a critical audit entry.
    /// </summary>
    /// <param name="key">Feature flag key to toggle.</param>
    /// <param name="req">Password confirmation payload.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids failed superadmin/password checks, returns not found for unknown flags, and returns the new enabled state.</returns>
    [HttpPut("feature-flags/{key}")]
    public async Task<IActionResult> ToggleFeatureFlag(string key, [FromBody] ToggleFeatureFlagRequest req, CancellationToken ct)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Forbid();

        var flag = await _db.FeatureFlags.FirstOrDefaultAsync(f => f.Key == key, ct);
        if (flag == null) return NotFound();

        flag.Enabled = !flag.Enabled;
        flag.UpdatedAt = DateTime.UtcNow;
        flag.UpdatedById = admin.Id;
        await _db.SaveChangesAsync(ct);

        _featureFlags.Invalidate(key);

        await _auditService.LogActionAsync(admin.Id, "superadmin.toggle_feature_flag", "FeatureFlag", null,
            $"Toggled {key} → {flag.Enabled}", null, "critical");

        return Ok(new { flag.Key, flag.Enabled });
    }

    // ── Maintenance Mode ────────────────────────────────────

    /// <summary>
    /// Enables or disables maintenance mode, updates the middleware cache, and writes a critical audit entry.
    /// </summary>
    /// <param name="req">Requested maintenance state and password confirmation.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids failed superadmin/password checks and returns the applied maintenance state.</returns>
    [HttpPost("maintenance")]
    public async Task<IActionResult> ToggleMaintenance([FromBody] ToggleMaintenanceRequest req, CancellationToken ct)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Forbid();

        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == "maintenance_mode", ct);
        if (setting == null)
        {
            setting = new PlatformSetting { Key = "maintenance_mode", Value = req.Enabled.ToString().ToLower(), UpdatedAt = DateTime.UtcNow, UpdatedById = admin.Id };
            _db.PlatformSettings.Add(setting);
        }
        else
        {
            setting.Value = req.Enabled.ToString().ToLower();
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedById = admin.Id;
        }

        await _db.SaveChangesAsync(ct);

        // Invalidate middleware cache immediately so the change takes effect within this request cycle
        _cache.Remove(MaintenanceModeMiddleware.CacheKey);

        await _auditService.LogActionAsync(admin.Id, "superadmin.toggle_maintenance", "PlatformSettings", null,
            $"Maintenance mode → {req.Enabled}", null, "critical");

        _logger.LogWarning("SUPERADMIN: Maintenance mode {State} by {AdminId}", req.Enabled ? "ENABLED" : "DISABLED", admin.Id);

        return Ok(new { Enabled = req.Enabled });
    }

    // ── IP Whitelist ────────────────────────────────────────

    /// <summary>
    /// Lists IP whitelist entries stored in platform settings after database-backed superadmin verification.
    /// </summary>
    /// <param name="ct">Cancellation token for the database query.</param>
    /// <returns>Forbids non-superadmins; otherwise returns whitelisted IP values and update timestamps.</returns>
    [HttpGet("ip-whitelist")]
    public async Task<IActionResult> GetIpWhitelist(CancellationToken ct)
    {
        if (await GetVerifiedSuperAdminAsync() == null) return Forbid();

        var entries = await _db.PlatformSettings
            .AsNoTracking()
            .Where(s => s.Key.StartsWith("ip_whitelist_"))
            .OrderBy(s => s.Key)
            .ToListAsync(ct);

        return Ok(entries.Select(e => new { Ip = e.Value, e.UpdatedAt }));
    }

    /// <summary>
    /// Adds a new IP whitelist entry in platform settings and audits the change.
    /// </summary>
    /// <param name="req">IP address and password confirmation.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids failed superadmin/password checks, rejects duplicate whitelist keys, and returns a success message.</returns>
    [HttpPost("ip-whitelist")]
    public async Task<IActionResult> AddIpToWhitelist([FromBody] AddIpWhitelistRequest req, CancellationToken ct)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Forbid();

        var key = $"ip_whitelist_{req.Ip.Replace('.', '_').Replace(':', '_')}";
        var existing = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing != null) return BadRequest("IP already whitelisted");

        _db.PlatformSettings.Add(new PlatformSetting
        {
            Key = key,
            Value = req.Ip,
            Description = $"Whitelisted IP: {req.Ip}",
            UpdatedAt = DateTime.UtcNow,
            UpdatedById = admin.Id
        });
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(admin.Id, "superadmin.add_ip_whitelist", "PlatformSettings", null,
            $"Added IP to whitelist: {req.Ip}", null, "critical");

        return Ok(new { Message = $"IP {req.Ip} added to whitelist" });
    }

    // ── Broadcast Notification ─────────────────────────────────

    /// <summary>
    /// Sends a system notification to all active users after password confirmation.
    /// </summary>
    /// <returns>Forbids failed superadmin/password checks, rejects blank titles, normalizes unsupported priorities, and returns the sent count.</returns>
    [HttpPost("broadcast-notification")]
    public async Task<IActionResult> BroadcastNotification([FromBody] BroadcastNotificationRequest req, CancellationToken ct)
    {
        var admin = await AuthorizeDangerousActionAsync(req.ConfirmPassword);
        if (admin == null) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("Title is required");

        var priority = req.Priority is "low" or "normal" or "high" or "critical" ? req.Priority : "normal";

        var activeUserIds = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var notifications = activeUserIds.Select(userId => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = "system",
            Title = req.Title,
            Content = req.Content ?? "",
            Priority = priority,
            CreatedAt = now,
        }).ToList();

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(admin.Id, "superadmin.broadcast_notification", "Notification", null,
            $"Broadcast to {notifications.Count} users: '{req.Title}'", null, "critical");

        _logger.LogInformation("SUPERADMIN: Broadcast notification '{Title}' to {Count} users by {AdminId}",
            req.Title, notifications.Count, admin.Id);

        return Ok(new { Message = $"Notification sent to {notifications.Count} users", Count = notifications.Count });
    }

    /// <summary>
    /// Removes an IP whitelist entry from platform settings and audits the change.
    /// </summary>
    /// <param name="ip">IP address whose normalized whitelist key should be removed.</param>
    /// <param name="confirmPassword">Password confirmation for the acting superadmin.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids failed superadmin/password checks, returns not found for unknown entries, and returns no content after removal.</returns>
    [HttpDelete("ip-whitelist/{ip}")]
    public async Task<IActionResult> RemoveIpFromWhitelist(string ip, [FromQuery] string confirmPassword, CancellationToken ct)
    {
        var admin = await AuthorizeDangerousActionAsync(confirmPassword);
        if (admin == null) return Forbid();

        var key = $"ip_whitelist_{ip.Replace('.', '_').Replace(':', '_')}";
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting == null) return NotFound();

        _db.PlatformSettings.Remove(setting);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(admin.Id, "superadmin.remove_ip_whitelist", "PlatformSettings", null,
            $"Removed IP from whitelist: {ip}", null, "critical");

        return NoContent();
    }
}
