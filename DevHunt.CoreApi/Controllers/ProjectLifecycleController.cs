using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Constants;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Badges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for project lifecycle operations.
/// Handles status transitions: draft → recruiting → active → completed → archived/cancelled
///
/// Extracted from ProjectsController (R12) to reduce complexity and improve cohesion.
/// </summary>
[ApiController]
[Route("api/projects")]
public class ProjectLifecycleController : BaseProjectController
{
    private readonly IActivityLogService _activityLogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectLifecycleController"/> class.
    /// </summary>
    /// <param name="dbContext">Database context used to read and update project lifecycle state.</param>
    /// <param name="auditService">Audit service that records lifecycle transitions.</param>
    /// <param name="notificationService">Notification service supplied to the base project controller.</param>
    /// <param name="eventBus">Event bus used to publish lifecycle events after successful commits.</param>
    /// <param name="activityLogService">Activity log service that records visible project state changes.</param>
    /// <param name="cache">Cache service used to invalidate project data after transitions.</param>
    public ProjectLifecycleController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        IEventBusService eventBus,
        IActivityLogService activityLogService,
        ICacheService cache)
        : base(dbContext, auditService, notificationService, eventBus, cache)
    {
        _activityLogService = activityLogService;
    }

    /// <summary>Request to cancel a project.</summary>
    /// <param name="Reason">Optional cancellation reason.</param>
    public record CancelProjectRequest([MaxLength(500)] string? Reason); // L-13: MaxLength

    /// <summary>
    /// Archives a project from any non-archived status and makes it private.
    /// </summary>
    [HttpPost("{id:guid}/archive")]
    [Authorize]
    public Task<IActionResult> ArchiveProject(Guid id, CancellationToken ct) =>
        ChangeLifecycleStatusAsync(id, new LifecycleTransition
        {
            TargetStatus = ProjectStatus.Archived,
            AllowedFromStatuses = null,
            BlockedStatuses = new[] { ProjectStatus.Archived },
            ConflictMessage = "Project already archived",
            SetVisibilityPrivate = true,
            AuditAction = "ArchiveProject",
            EventFactory = (projectId, userId) => DomainEvents.ProjectArchived(projectId, userId),
            LogActivity = true,  // L-09: was missing
            ActivityType = "project.state_changed"
        }, ct);

    /// <summary>
    /// Restores an archived project to draft status and restores public visibility.
    /// </summary>
    [HttpPost("{id:guid}/unarchive")]
    [Authorize]
    public Task<IActionResult> UnarchiveProject(Guid id, CancellationToken ct) =>
        ChangeLifecycleStatusAsync(id, new LifecycleTransition
        {
            TargetStatus = ProjectStatus.Draft,
            AllowedFromStatuses = new[] { ProjectStatus.Archived },
            BlockedStatuses = null,
            ConflictMessage = "Project is not archived",
            // L-07: Do NOT blindly set to "public" — use RestoreVisibility flag
            RestoreVisibility = true,
            AuditAction = "UnarchiveProject",
            EventFactory = (projectId, userId) => DomainEvents.ProjectUnarchived(projectId, userId),
            SuccessMessage = "Project unarchived",
            LogActivity = true,  // L-09: was missing
            ActivityType = "project.state_changed"
        }, ct);

    /// <summary>
    /// Publishes a draft project into recruiting status.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize]
    public Task<IActionResult> PublishProject(Guid id, CancellationToken ct) =>
        ChangeLifecycleStatusAsync(id, new LifecycleTransition
        {
            TargetStatus = ProjectStatus.Recruiting,
            AllowedFromStatuses = new[] { ProjectStatus.Draft },
            BlockedStatuses = null,
            ConflictMessage = "Can only publish projects in draft status",
            AuditAction = "PublishProject",
            EventFactory = (projectId, userId) => DomainEvents.ProjectPublished(projectId, userId),
            SuccessMessage = "Project published",
            LogActivity = true,
            ActivityType = "project.state_changed"
        }, ct);

    /// <summary>
    /// Moves a recruiting project back to draft status.
    /// </summary>
    [HttpPost("{id:guid}/unpublish")]
    [Authorize]
    public Task<IActionResult> UnpublishProject(Guid id, CancellationToken ct) =>
        ChangeLifecycleStatusAsync(id, new LifecycleTransition
        {
            TargetStatus = ProjectStatus.Draft,
            AllowedFromStatuses = new[] { ProjectStatus.Recruiting },
            BlockedStatuses = null,
            ConflictMessage = "Can only unpublish projects in recruiting status",
            AuditAction = "UnpublishProject",
            EventFactory = (projectId, userId) => DomainEvents.ProjectUnpublished(projectId, userId),
            SuccessMessage = "Project unpublished",
            LogActivity = true,
            ActivityType = "project.state_changed"
        }, ct);

    /// <summary>
    /// Activates a recruiting project when the owner or admin/curator has at least two active members.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize]
    public async Task<IActionResult> ActivateProject(Guid id, CancellationToken ct)
    {
        // L-08: Use CountAsync instead of Include(TeamMembers) + in-memory Count
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return NotFound();

        var userId = GetRequiredUserId();
        if (project.OwnerId != userId && !SecurityHelpers.IsAdminOrCurator(User))
            return Forbid();

        if (project.Status != ProjectStatus.Recruiting)
            return BadRequest("Can only activate projects in recruiting status");

        // L-08: DB-side COUNT instead of loading all team members into memory
        var activeMembers = await _dbContext.TeamMembers
            .CountAsync(tm => tm.ProjectId == id && tm.Status == TeamMemberStatus.Active, ct);
        if (activeMembers < 2)
            return BadRequest("Project must have at least 2 active members to activate");

        return await ExecuteStatusChangeAsync(project, userId, new LifecycleTransition
        {
            TargetStatus = ProjectStatus.Active,
            AuditAction = "ActivateProject",
            EventFactory = (projectId, uid) => DomainEvents.ProjectActivated(projectId, uid),
            SuccessMessage = "Project activated",
            LogActivity = true,
            ActivityType = "project.state_changed"
        }, ct);
    }

    /// <summary>
    /// Completes an active project.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize]
    public Task<IActionResult> CompleteProject(Guid id, CancellationToken ct) =>
        ChangeLifecycleStatusAsync(id, new LifecycleTransition
        {
            TargetStatus = ProjectStatus.Completed,
            AllowedFromStatuses = new[] { ProjectStatus.Active },
            BlockedStatuses = null,
            ConflictMessage = "Can only complete active projects",
            AuditAction = "CompleteProject",
            EventFactory = (projectId, userId) => DomainEvents.ProjectCompleted(projectId, userId),
            SuccessMessage = "Project completed",
            LogActivity = true,
            ActivityType = "project.state_changed"
        }, ct);

    /// <summary>
    /// Cancels a project that is not archived or already cancelled and notifies active members and the owner.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelProject(Guid id, [FromBody] CancelProjectRequest? request, CancellationToken ct)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return NotFound();

        var userId = GetRequiredUserId();
        if (project.OwnerId != userId && !SecurityHelpers.IsAdminOrCurator(User))
            return Forbid();

        if (project.Status is ProjectStatus.Archived or ProjectStatus.Cancelled)
            return BadRequest("Cannot cancel archived or already cancelled project");

        // L-04: Notifications added to context BEFORE SaveChangesAsync — single atomic commit
        project.Status = ProjectStatus.Cancelled;
        project.UpdatedAt = DateTime.UtcNow;

        // Add notifications to the same batch
        var teamUserIds = await _dbContext.TeamMembers
            .Where(tm => tm.ProjectId == project.Id && tm.Status == TeamMemberStatus.Active)
            .Select(tm => tm.UserId)
            .ToListAsync(ct);

        var notifyTo = teamUserIds.Append(project.OwnerId).Distinct();
        foreach (var uid in notifyTo)
        {
            _dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = uid,
                Type = "project",
                Title = "Project cancelled",
                Content = $"Project '{project.Title}' has been cancelled. {request?.Reason ?? ""}",
                RelatedEntityType = "Project",
                RelatedEntityId = project.Id,
                Priority = "medium",
                CreatedAt = DateTime.UtcNow
            });
        }

        // L-03 + L-04: Single SaveChangesAsync with ConcurrencyCheck on Status
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Project status was changed concurrently" });
        }

        await InvalidateProjectCacheAsync(id);

        // Audit, event, and activity log — AFTER successful commit
        await _auditService.LogActionAsync(userId, "ProjectLifecycleController.CancelProject", "Project", id,
            $"Cancelled project {project.Title}: {request?.Reason ?? "No reason"}");

        await _eventBus.PublishAsync(DomainEvents.ProjectCancelled(id, userId, request?.Reason));

        await _activityLogService.LogProjectEventAsync(
            project.Id,
            userId,
            "project.state_changed",
            $"Project '{project.Title}' cancelled",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { Reason = request?.Reason, Status = project.Status });

        return Ok(new { Message = "Project cancelled", Status = project.Status });
    }

    #region Private Helpers (DRY pattern for lifecycle transitions)

    /// <summary>
    /// Encapsulates a lifecycle status transition and its validation rules.
    /// </summary>
    private sealed class LifecycleTransition
    {
        /// <summary>Target status to set.</summary>
        public required string TargetStatus { get; init; }
        /// <summary>Allowed source statuses (null means any).</summary>
        public string[]? AllowedFromStatuses { get; init; }
        /// <summary>Blocked source statuses.</summary>
        public string[]? BlockedStatuses { get; init; }
        /// <summary>Conflict/validation message for invalid transitions.</summary>
        public string? ConflictMessage { get; init; }
        /// <summary>Set visibility to private when transitioning.</summary>
        public bool SetVisibilityPrivate { get; init; }
        /// <summary>Set visibility to public when transitioning.</summary>
        public bool SetVisibilityPublic { get; init; }
        /// <summary>L-07: Restore visibility to "public" (safe default for unarchive).</summary>
        public bool RestoreVisibility { get; init; }
        /// <summary>Audit action name for logging.</summary>
        public required string AuditAction { get; init; }
        /// <summary>Factory for the domain event to publish.</summary>
        public required Func<Guid, Guid, DomainEvent> EventFactory { get; init; }
        /// <summary>Optional success message to return.</summary>
        public string? SuccessMessage { get; init; }
        /// <summary>Whether to log an activity record.</summary>
        public bool LogActivity { get; init; }
        /// <summary>Activity type key.</summary>
        public string? ActivityType { get; init; }
    }

    /// <summary>
    /// Loads a project, verifies owner or admin/curator access, validates source status rules, and executes the transition.
    /// </summary>
    private async Task<IActionResult> ChangeLifecycleStatusAsync(Guid id, LifecycleTransition transition, CancellationToken ct)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return NotFound();

        var userId = GetRequiredUserId();
        if (project.OwnerId != userId && !SecurityHelpers.IsAdminOrCurator(User))
            return Forbid();

        // Validate current status
        if (transition.BlockedStatuses != null && transition.BlockedStatuses.Contains(project.Status))
            return Conflict(transition.ConflictMessage);

        if (transition.AllowedFromStatuses != null && !transition.AllowedFromStatuses.Contains(project.Status))
            return BadRequest(transition.ConflictMessage);

        return await ExecuteStatusChangeAsync(project, userId, transition, ct);
    }

    /// <summary>
    /// Persists a lifecycle transition, invalidates caches, publishes events, triggers achievements, and returns the configured success result.
    /// </summary>
    private async Task<IActionResult> ExecuteStatusChangeAsync(
        Project project, Guid userId, LifecycleTransition transition, CancellationToken ct)
    {
        project.Status = transition.TargetStatus;
        project.UpdatedAt = DateTime.UtcNow;

        if (transition.SetVisibilityPrivate)
            project.Visibility = ProjectVisibility.Private;

        if (transition.SetVisibilityPublic)
            project.Visibility = ProjectVisibility.Public;

        // L-07: Restore visibility to public (safe default for unarchive)
        if (transition.RestoreVisibility)
            project.Visibility = ProjectVisibility.Public;

        // L-03: ConcurrencyCheck on Status prevents double-publish
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Project status was changed concurrently. Refresh and try again." });
        }

        await InvalidateProjectCacheAsync(project.Id);

        await _auditService.LogActionAsync(userId, $"ProjectLifecycleController.{transition.AuditAction}", "Project", project.Id,
            $"{transition.AuditAction} project {project.Title} (ID: {project.Id})");

        await _eventBus.PublishAsync(transition.EventFactory(project.Id, userId));

        // L-10: Achievement triggers use ProjectStatus constants
        if (transition.TargetStatus == ProjectStatus.Completed)
            await HttpContext.RequestServices.TriggerAchievementCheckAsync(userId, AchievementTrigger.ProjectCompleted);
        else if (transition.TargetStatus == ProjectStatus.Recruiting)
            await HttpContext.RequestServices.TriggerAchievementCheckAsync(userId, AchievementTrigger.ProjectPublished);

        if (transition.LogActivity && transition.ActivityType != null)
        {
            await _activityLogService.LogProjectEventAsync(
                project.Id,
                userId,
                transition.ActivityType,
                $"Project '{project.Title}' {transition.TargetStatus}",
                visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
                payload: new { Status = project.Status });
        }

        if (!string.IsNullOrEmpty(transition.SuccessMessage))
        {
            return Ok(new { Message = transition.SuccessMessage, Status = project.Status });
        }

        return NoContent();
    }

    /// <summary>
    /// Adds notifications for active team members and the owner, then persists them.
    /// </summary>
    private async Task NotifyTeamMembersAsync(Project project, string title, string content, CancellationToken ct)
    {
        var teamUserIds = await _dbContext.TeamMembers
            .Where(tm => tm.ProjectId == project.Id && tm.Status == TeamMemberStatus.Active)
            .Select(tm => tm.UserId)
            .ToListAsync(ct);

        var notifyTo = teamUserIds.Append(project.OwnerId).Distinct();

        foreach (var uid in notifyTo)
        {
            _dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = uid,
                Type = "project",
                Title = title,
                Content = content,
                RelatedEntityType = "Project",
                RelatedEntityId = project.Id,
                Priority = "medium",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    #endregion
}
