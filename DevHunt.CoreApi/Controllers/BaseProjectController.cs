using System;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.CoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Shared base controller for project-specific controllers that need project access checks, auditing, events, and cache invalidation.
/// </summary>
public abstract class BaseProjectController : ControllerBase
{
    protected readonly DevHuntDbContext _dbContext;
    protected readonly IAuditService _auditService;
    protected readonly INotificationServiceClient _notificationService;
    protected readonly IEventBusService _eventBus;
    protected readonly ICacheService _cache;

    /// <summary>
    /// Stores shared project controller collaborators for database access, auditing, notifications, events, and cache invalidation.
    /// </summary>
    protected BaseProjectController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        IEventBusService eventBus,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _notificationService = notificationService;
        _eventBus = eventBus;
        _cache = cache;
    }

    /// <summary>
    /// Gets the current user's identifier; kept for derived controllers that still call the legacy owner-oriented name.
    /// </summary>
    protected Guid GetOwnerId()
    {
        return GetRequiredUserId();
    }

    /// <summary>
    /// Checks whether the supplied user owns the project.
    /// </summary>
    protected async Task<bool> IsProjectOwnerAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        return project?.OwnerId == userId;
    }

    /// <summary>
    /// Checks whether the supplied user is an active project team member.
    /// </summary>
    protected async Task<bool> IsProjectMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.TeamMembers
            .AnyAsync(tm => tm.ProjectId == projectId && tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value, ct);
    }

    /// <summary>
    /// Checks whether the supplied user has subscribed to the project.
    /// </summary>
    protected async Task<bool> IsProjectSubscriberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.ProjectSubscriptions
            .AnyAsync(ps => ps.ProjectId == projectId && ps.UserId == userId, ct);
    }

    /// <summary>
    /// Checks whether the supplied user owns the project or is an active member.
    /// </summary>
    protected async Task<bool> HasProjectAccessAsync(Guid projectId, Guid userId)
    {
        var isOwner = await IsProjectOwnerAsync(projectId, userId);
        if (isOwner) return true;

        return await IsProjectMemberAsync(projectId, userId);
    }

    /// <summary>
    /// Removes cached project detail and project list entries after a project mutation.
    /// </summary>
    protected async Task InvalidateProjectCacheAsync(Guid projectId)
    {
        await _cache.RemoveAsync($"project:{projectId}");
        await _cache.RemoveByPatternAsync("projects:*");
    }

    /// <summary>
    /// Gets the authenticated user identifier or throws when the authorization pipeline did not provide the claim.
    /// </summary>
    protected Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }
}
