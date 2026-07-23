using System;
using System.Linq;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Activity feed endpoints for project and user events.
/// </summary>
/// <remarks>
/// Exposes a read-only feed with filtering by project, event type, and visibility.
/// Routes: api/activities/*
/// </remarks>
[ApiController]
[Route("api/activities")]
public class ActivitiesController : BaseProjectController
{
    /// <summary>
    /// Creates the activities controller using the shared project-controller services.
    /// </summary>
    /// <param name="dbContext">Database context used to query activity records and visibility relationships.</param>
    /// <param name="auditService">Audit service passed to <see cref="BaseProjectController"/>.</param>
    /// <param name="notificationService">Notification client passed to <see cref="BaseProjectController"/>.</param>
    /// <param name="eventBus">Event bus passed to <see cref="BaseProjectController"/>.</param>
    /// <param name="cache">Cache service passed to <see cref="BaseProjectController"/>.</param>
    public ActivitiesController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        IEventBusService eventBus,
        ICacheService cache)
        : base(dbContext, auditService, notificationService, eventBus, cache)
    {
    }

    /// <summary>Activity feed item returned by the API.</summary>
    /// <param name="Id">Unique activity identifier.</param>
    /// <param name="ProjectId">Related project ID (null for user-only events).</param>
    /// <param name="ProjectTitle">Project title (if project is loaded).</param>
    /// <param name="ActorId">User who performed the action.</param>
    /// <param name="ActorName">Display name of the actor.</param>
    /// <param name="ActorAvatarUrl">Avatar URL of the actor.</param>
    /// <param name="EventType">Event type key (e.g., "task.created").</param>
    /// <param name="Summary">Human-readable summary text.</param>
    /// <param name="Visibility">Visibility scope: public, subscribers, members.</param>
    /// <param name="PayloadJson">Optional JSON payload for the event.</param>
    /// <param name="CreatedAt">Event timestamp (UTC).</param>
    public record ActivityResponse(
        Guid Id,
        Guid? ProjectId,
        string? ProjectTitle,
        Guid ActorId,
        string? ActorName,
        string? ActorAvatarUrl,
        string EventType,
        string Summary,
        string Visibility,
        string? PayloadJson,
        DateTime CreatedAt);

    /// <summary>
    /// Lists activity records with filters, pagination, orphan protection, and visibility rules based on the caller.
    /// </summary>
    /// <param name="projectId">Optional project ID to scope the feed.</param>
    /// <param name="eventType">Optional event type key filter.</param>
    /// <param name="visibility">Optional visibility filter (public, subscribers, members).</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size (max 100).</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>Rejects invalid visibility filters; otherwise returns only records visible to anonymous, member, subscriber, or privileged viewers.</returns>
    /// <response code="200">Returns a list of activity items.</response>
    /// <response code="400">Invalid visibility filter.</response>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetActivities(
        [FromQuery] Guid? projectId,
        [FromQuery] string? eventType,
        [FromQuery] string? visibility,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var baseQuery = _dbContext.ActivityRecords
            .AsNoTracking()
            .Include(a => a.Project)
            .Include(a => a.Actor)
            .Where(a => !a.ProjectId.HasValue || _dbContext.Projects.Any(p => p.Id == a.ProjectId.Value)) // Exclude orphaned records (project deleted)
            .AsQueryable();

        if (projectId.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.ProjectId == projectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            baseQuery = baseQuery.Where(a => a.EventType == eventType);
        }

        var normalizedVisibilityFilter = string.IsNullOrWhiteSpace(visibility)
            ? null
            : visibility.Trim().ToLowerInvariant();
        var allowedVisibilities = new[] { "public", "subscribers", "members" };
        if (normalizedVisibilityFilter != null && !allowedVisibilities.Contains(normalizedVisibilityFilter))
        {
            return BadRequest($"Visibility filter must be one of: {string.Join(", ", allowedVisibilities)}");
        }

        var requesterId = SecurityHelpers.GetUserId(User);
        var isPrivilegedViewer = SecurityHelpers.IsAdminOrCurator(User);

        if (!isPrivilegedViewer)
        {
            if (!requesterId.HasValue)
            {
                baseQuery = baseQuery.Where(a => a.Visibility == "public");
            }
            else
            {
                var currentUserId = requesterId.Value;
                var memberProjectIdsQuery = _dbContext.Projects
                    .Where(p => p.OwnerId == currentUserId)
                    .Select(p => p.Id)
                    .Union(_dbContext.TeamMembers
                        .Where(tm => tm.UserId == currentUserId && tm.Status == TeamMemberStatus.Active.Value)
                        .Select(tm => tm.ProjectId));

                var memberProjectIds = await memberProjectIdsQuery.ToListAsync(ct);

                var subscriberProjectIds = await _dbContext.ProjectSubscriptions
                    .Where(ps => ps.UserId == currentUserId)
                    .Select(ps => ps.ProjectId)
                    .ToListAsync(ct);

                var subscriberOrMemberProjectIds = subscriberProjectIds
                    .Concat(memberProjectIds)
                    .Distinct()
                    .ToList();

                baseQuery = baseQuery.Where(a =>
                    a.Visibility == "public" ||
                    (a.Visibility == "subscribers" && a.ProjectId.HasValue && subscriberOrMemberProjectIds.Contains(a.ProjectId.Value)) ||
                    (a.Visibility == "members" && a.ProjectId.HasValue && memberProjectIds.Contains(a.ProjectId.Value)));
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedVisibilityFilter))
        {
            baseQuery = baseQuery.Where(a => a.Visibility == normalizedVisibilityFilter);
        }

        var total = await baseQuery.CountAsync(ct);

        var activities = await baseQuery
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityResponse(
                a.Id,
                a.ProjectId,
                a.Project != null ? a.Project.Title : null,
                a.ActorId,
                a.Actor.FullName,
                a.Actor.AvatarUrl,
                a.EventType,
                a.Summary,
                a.Visibility,
                a.PayloadJson,
                a.CreatedAt))
            .ToListAsync(ct);

        return Ok(new
        {
            Data = activities,
            Pagination = new
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                TotalPages = (int)Math.Ceiling((double)total / pageSize),
                HasNext = page * pageSize < total,
                HasPrevious = page > 1
            }
        });
    }
}
