using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing project subscriptions.
/// Separated from ProjectsController to improve cohesion.
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectSubscriptionsController : ControllerBase
{
    private readonly DevHuntDbContext _dbContext;
    private readonly IAuditService _auditService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectSubscriptionsController"/> class.
    /// </summary>
    /// <param name="dbContext">Database context used to read projects and persist subscription rows.</param>
    /// <param name="auditService">Audit service that records subscribe and unsubscribe actions.</param>
    public ProjectSubscriptionsController(DevHuntDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    /// <summary>
    /// Subscribes the authenticated user to a project, requiring membership for private projects and treating duplicate rows as success.
    /// </summary>
    /// <param name="projectId">Project to subscribe to.</param>
    /// <param name="ct">Cancellation token for database operations.</param>
    /// <returns>No content on success, not found for missing projects, or forbidden for inaccessible private projects.</returns>
    [HttpPost("{projectId:guid}/subscriptions")]
    public async Task<IActionResult> SubscribeToProject(Guid projectId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        // B09-01: ProjectVisibility.Private.Value instead of "private"
        if (project.Visibility == ProjectVisibility.Private.Value && !await HasProjectAccessAsync(project, projectId, userId))
            return Forbid();

        // RACE-02: Remove FindAsync-before-insert (TOCTOU B-08).
        // Composite PK (ProjectId, UserId) enforces uniqueness at DB level — catch duplicate instead.
        _dbContext.ProjectSubscriptions.Add(new ProjectSubscription
        {
            ProjectId = projectId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Already subscribed — idempotent result, not an error
            return NoContent();
        }

        await _auditService.LogActionAsync(userId, "ProjectSubscriptions.Subscribe", "ProjectSubscription", projectId,
            $"User subscribed to project: {project.Title}");

        return NoContent();
    }

    /// <summary>
    /// Removes the authenticated user's project subscription.
    /// </summary>
    /// <param name="projectId">Project to unsubscribe from.</param>
    /// <param name="ct">Cancellation token for database operations.</param>
    /// <returns>No content on success, or not found when the subscription does not exist.</returns>
    [HttpDelete("{projectId:guid}/subscriptions")]
    public async Task<IActionResult> UnsubscribeFromProject(Guid projectId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var subscription = await _dbContext.ProjectSubscriptions.FindAsync(new object[] { projectId, userId }, ct);
        if (subscription == null) return NotFound("Subscription not found");

        _dbContext.ProjectSubscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(userId, "ProjectSubscriptions.Unsubscribe", "ProjectSubscription", projectId,
            "User unsubscribed from project");

        return NoContent();
    }

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId() =>
        SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");

    // DOUBLE-FETCH: Accept already-loaded project — avoids redundant FindAsync (B-08 pattern)
    /// <summary>
    /// Checks whether the user owns the already-loaded project or is an active team member.
    /// </summary>
    private async Task<bool> HasProjectAccessAsync(Project project, Guid projectId, Guid userId, CancellationToken ct = default)
    {
        if (project.OwnerId == userId) return true;

        // B09-01: TeamMemberStatus.Active.Value instead of "active"
        return await _dbContext.TeamMembers
            .AnyAsync(tm => tm.ProjectId == projectId
                         && tm.UserId == userId
                         && tm.Status == TeamMemberStatus.Active.Value, ct);
    }

    /// <summary>
    /// Detects a PostgreSQL duplicate-key error raised by the project subscription unique constraint.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";
}
