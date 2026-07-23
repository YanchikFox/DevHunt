using System;
using System.ComponentModel.DataAnnotations;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Badges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Manages the public project showcase surface, including demos, screenshots, metrics, likes, featuring, and comments.
/// Routes: api/showcase/* and api/projects/{projectId}/showcase/*
/// </summary>
[ApiController]
[Route("api")]
public class ShowcaseController : BaseProjectController
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShowcaseController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="auditService">The audit service.</param>
    /// <param name="notificationService">The notification service client.</param>
    /// <param name="eventBus">The event bus service.</param>
    /// <param name="cache">The cache service.</param>
    public ShowcaseController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        IEventBusService eventBus,
        ICacheService cache)
        : base(dbContext, auditService, notificationService, eventBus, cache)
    {
    }

    /// <summary>
    /// Retrieves showcase details for a project and increments its view counter.
    /// </summary>
    /// <remarks>
    /// This endpoint returns the full showcase information including summary, demo URLs, screenshots, and team members.
    /// It also increments the view counter for the showcase.
    /// Accessible to anonymous users if the showcase is published.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="ct">Cancels the showcase lookup and view update.</param>
    /// <returns>The showcase details DTO.</returns>
    /// <response code="200">Returns the showcase details.</response>
    /// <response code="404">If the showcase is not found for the specified project.</response>
    [HttpGet("projects/{projectId:guid}/showcase")]
    [AllowAnonymous]
    public async Task<IActionResult> GetShowcase(Guid projectId, CancellationToken ct = default)
    {
        var showcase = await _dbContext.ShowcaseProjects
            .Include(sp => sp.Project)
                .ThenInclude(p => p.TeamMembers)
                    .ThenInclude(tm => tm.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(sp => sp.ProjectId == projectId, ct);

        if (showcase == null)
        {
            return NotFound("Showcase not found for this project");
        }

        // Increment view counter (DEV-117: atomic — this anonymous endpoint is the
        // highest-contention write in the app; read-modify-write loses views).
        await _dbContext.ShowcaseProjects
            .Where(sp => sp.ProjectId == projectId)
            .ExecuteUpdateAsync(s => s.SetProperty(sp => sp.ViewsCount, sp => sp.ViewsCount + 1), ct);
        showcase.ViewsCount++; // reflect the increment in the response DTO (entity is AsNoTracking)

        var showcaseDto = new
        {
            showcase.Id,
            Project = new
            {
                showcase.ProjectId,
                showcase.Project.Title,
                showcase.Project.Description
            },
            showcase.Summary,
            showcase.DemoUrl,
            showcase.DemoVideoUrl,
            Screenshots = showcase.Screenshots,
            showcase.RepositoryUrl,
            Metrics = showcase.Metrics,
            showcase.PublishedAt,
            showcase.Featured,
            showcase.ViewsCount,
            showcase.LikesCount,
            showcase.UpdatedAt,
            Team = showcase.Project.TeamMembers
                .Where(tm => tm.Status == TeamMemberStatus.Active.Value)
                .Select(tm => new { tm.User?.FullName, tm.User?.AvatarUrl, tm.Role })
        };

        return Ok(showcaseDto);
    }

    /// <summary>
    /// Retrieves public showcase summaries, optionally limited to featured entries.
    /// </summary>
    /// <remarks>
    /// This endpoint supports pagination and filtering by 'featured' status.
    /// It returns a lightweight list of showcases suitable for catalog views.
    /// </remarks>
    /// <param name="featuredOnly">If true, returns only showcases marked as featured.</param>
    /// <param name="skip">Number of records to skip for pagination (default: 0).</param>
    /// <param name="take">Number of records to take for pagination (default: 20).</param>
    /// <param name="ct">Cancels the showcase list query.</param>
    /// <returns>A list of showcase summaries.</returns>
    /// <response code="200">Returns the list of showcases.</response>
    [HttpGet("showcase")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllShowcases(
        [FromQuery] bool featuredOnly = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20, CancellationToken ct = default)
    {
        var query = _dbContext.ShowcaseProjects
            .Include(sp => sp.Project)
            .AsNoTracking();

        if (featuredOnly)
        {
            query = query.Where(sp => sp.Featured);
        }

        var showcases = await query
            .OrderByDescending(sp => sp.PublishedAt)
            .Skip(skip)
            .Take(take)
            .Select(sp => new
            {
                sp.Id,
                ProjectId = sp.ProjectId,
                ProjectTitle = sp.Project.Title,
                sp.Summary,
                sp.DemoUrl,
                sp.ScreenshotsJson,
                sp.RepositoryUrl,
                sp.PublishedAt,
                sp.Featured,
                sp.ViewsCount,
                sp.LikesCount
            })
            .ToListAsync(ct);

        return Ok(showcases);
    }

    /// <summary>
    /// Creates the project's showcase when the caller owns the project and no showcase already exists.
    /// </summary>
    /// <remarks>
    /// Only the project owner can create a showcase.
    /// A project can have only one showcase.
    /// Triggers a `ShowcaseSubmitted` domain event.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="dto">The data transfer object containing showcase details (summary, URLs, screenshots).</param>
    /// <param name="ct">Cancels showcase creation persistence.</param>
    /// <returns>The created showcase object.</returns>
    /// <response code="201">Returns the created showcase.</response>
    /// <response code="404">If the project is not found.</response>
    /// <response code="403">If the user is not the project owner.</response>
    /// <response code="409">If a showcase already exists for this project.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost("projects/{projectId:guid}/showcase")]
    [Authorize]
    public async Task<IActionResult> CreateShowcase(Guid projectId, [FromBody] CreateShowcaseDto dto, CancellationToken ct = default)
    {
        var userId = GetOwnerId();
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);

        if (project == null)
        {
            return NotFound("Project not found");
        }

        if (project.OwnerId != userId)
        {
            return Forbid("Only project owner can create showcase");
        }

        // Check that showcase doesn't exist yet for this project
        var exists = await _dbContext.ShowcaseProjects.AnyAsync(sp => sp.ProjectId == projectId, ct);
        if (exists)
        {
            return Conflict("Showcase already exists for this project");
        }

        // B-10: validate user-supplied URLs before persisting
        var urlError = ValidateShowcaseUrls(dto.DemoUrl, dto.DemoVideoUrl, dto.RepositoryUrl);
        if (urlError != null) return urlError;

        var showcase = new ShowcaseProject
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Summary = SecurityHelpers.SanitizeHtml(dto.Summary),
            DemoUrl = dto.DemoUrl,
            DemoVideoUrl = dto.DemoVideoUrl,
            Screenshots = dto.Screenshots,
            RepositoryUrl = dto.RepositoryUrl,
            Metrics = dto.Metrics,
            PublishedAt = DateTime.UtcNow,
            Featured = false,
            ViewsCount = 0,
            LikesCount = 0,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.ShowcaseProjects.Add(showcase);
        await _dbContext.SaveChangesAsync(ct);

        // Publish event
        await _eventBus.PublishAsync(DomainEvents.ShowcaseSubmitted(projectId, userId));
        await HttpContext.RequestServices.TriggerAchievementCheckAsync(userId, AchievementTrigger.ShowcasePublished);

        return CreatedAtAction(nameof(GetShowcase), new { projectId }, showcase);
    }

    /// <summary>
    /// Updates an existing showcase when the caller is the project owner or an administrator.
    /// </summary>
    /// <remarks>
    /// Only the project owner or an administrator can update the showcase.
    /// Triggers a `ShowcaseSubmitted` domain event (re-submission).
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="dto">The data transfer object containing updated showcase details.</param>
    /// <param name="ct">Cancels showcase update persistence.</param>
    /// <returns>The updated showcase object.</returns>
    /// <response code="200">Returns the updated showcase.</response>
    /// <response code="404">If the project or showcase is not found.</response>
    /// <response code="403">If the user is not authorized to update the showcase.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPut("projects/{projectId:guid}/showcase")]
    [Authorize]
    public async Task<IActionResult> UpdateShowcase(Guid projectId, [FromBody] UpdateShowcaseDto dto, CancellationToken ct = default)
    {
        var userId = GetOwnerId();
        var isAdmin = SecurityHelpers.IsAdmin(User);

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null)
        {
            return NotFound("Project not found");
        }

        // Check permissions (owner or admin)
        if (project.OwnerId != userId && !isAdmin)
        {
            return Forbid("Only project owner or admin can update showcase");
        }

        var showcase = await _dbContext.ShowcaseProjects.FirstOrDefaultAsync(sp => sp.ProjectId == projectId, ct);
        if (showcase == null)
        {
            return NotFound("Showcase not found");
        }

        // B-10: validate user-supplied URLs before persisting
        var urlError = ValidateShowcaseUrls(dto.DemoUrl, dto.DemoVideoUrl, dto.RepositoryUrl);
        if (urlError != null) return urlError;

        showcase.Summary = SecurityHelpers.SanitizeHtml(dto.Summary);
        showcase.DemoUrl = dto.DemoUrl;
        showcase.DemoVideoUrl = dto.DemoVideoUrl;
        showcase.Screenshots = dto.Screenshots;
        showcase.RepositoryUrl = dto.RepositoryUrl;
        showcase.Metrics = dto.Metrics;
        showcase.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        // Publish event
        await _eventBus.PublishAsync(DomainEvents.ShowcaseSubmitted(projectId, userId));

        return Ok(showcase);
    }

    /// <summary>
    /// Adds a like to a showcase, updates the counter, and publishes a liked event.
    /// </summary>
    /// <remarks>
    /// Increments the like counter for the showcase.
    /// Triggers a `ShowcaseLiked` domain event.
    /// Requires authentication.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="ct">Cancels the showcase like update.</param>
    /// <returns>The updated likes count.</returns>
    /// <response code="200">Returns the new likes count.</response>
    /// <response code="404">If the showcase is not found.</response>
    [HttpPost("projects/{projectId:guid}/showcase/like")]
    [Authorize]
    public async Task<IActionResult> LikeShowcase(Guid projectId, CancellationToken ct = default)
    {
        var userId = GetOwnerId();
        if (userId == Guid.Empty) return Unauthorized();

        var showcase = await _dbContext.ShowcaseProjects.FirstOrDefaultAsync(sp => sp.ProjectId == projectId, ct);
        if (showcase == null)
        {
            return NotFound("Showcase not found");
        }

        // DEV-114: one like per user. Insert the per-user row and rely on the (ShowcaseId, UserId)
        // primary key to make repeated likes idempotent (catch the duplicate-key race per B-08).
        if (await _dbContext.ShowcaseLikes.AnyAsync(l => l.ShowcaseId == showcase.Id && l.UserId == userId, ct))
        {
            return Ok(new { LikesCount = showcase.LikesCount });
        }

        _dbContext.ShowcaseLikes.Add(new ShowcaseLike
        {
            ShowcaseId = showcase.Id,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
        });

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent like by the same user won the race — already counted.
            return Ok(new { LikesCount = showcase.LikesCount });
        }

        // Atomic increment avoids lost updates on the denormalized counter.
        await _dbContext.ShowcaseProjects
            .Where(sp => sp.Id == showcase.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(sp => sp.LikesCount, sp => sp.LikesCount + 1)
                .SetProperty(sp => sp.UpdatedAt, _ => DateTime.UtcNow), ct);

        await _eventBus.PublishAsync(DomainEvents.ShowcaseLiked(projectId, userId));

        return Ok(new { LikesCount = showcase.LikesCount + 1 });
    }

    /// <summary>
    /// Removes one like from a showcase without allowing the counter to drop below zero.
    /// </summary>
    /// <remarks>
    /// Decrements the like counter for the showcase (minimum 0).
    /// Triggers a `ShowcaseUnliked` domain event.
    /// Requires authentication.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="ct">Cancels the showcase unlike update.</param>
    /// <returns>The updated likes count.</returns>
    /// <response code="200">Returns the new likes count.</response>
    /// <response code="404">If the showcase is not found.</response>
    [HttpPost("projects/{projectId:guid}/showcase/unlike")]
    [Authorize]
    public async Task<IActionResult> UnlikeShowcase(Guid projectId, CancellationToken ct = default)
    {
        var userId = GetOwnerId();
        if (userId == Guid.Empty) return Unauthorized();

        var showcase = await _dbContext.ShowcaseProjects.FirstOrDefaultAsync(sp => sp.ProjectId == projectId, ct);
        if (showcase == null)
        {
            return NotFound("Showcase not found");
        }

        // DEV-114: only decrement if THIS user had actually liked it — prevents zeroing others' likes.
        var like = await _dbContext.ShowcaseLikes
            .FirstOrDefaultAsync(l => l.ShowcaseId == showcase.Id && l.UserId == userId, ct);
        if (like == null)
        {
            return Ok(new { LikesCount = showcase.LikesCount });
        }

        _dbContext.ShowcaseLikes.Remove(like);
        await _dbContext.SaveChangesAsync(ct);

        // Atomic, floored decrement of the denormalized counter.
        await _dbContext.ShowcaseProjects
            .Where(sp => sp.Id == showcase.Id && sp.LikesCount > 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(sp => sp.LikesCount, sp => sp.LikesCount - 1)
                .SetProperty(sp => sp.UpdatedAt, _ => DateTime.UtcNow), ct);

        await _eventBus.PublishAsync(DomainEvents.ShowcaseUnliked(projectId, userId));

        return Ok(new { LikesCount = Math.Max(0, showcase.LikesCount - 1) });
    }

    /// <summary>
    /// Marks a showcase as featured for administrators or curators and triggers achievement checks.
    /// </summary>
    /// <remarks>
    /// Only administrators or curators can perform this action.
    /// Triggers a `ShowcaseFeatured` domain event.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="ct">Cancels the showcase feature update.</param>
    /// <returns>No content.</returns>
    /// <response code="200">If the operation was successful.</response>
    /// <response code="404">If the showcase is not found.</response>
    /// <response code="403">If the user is not an admin or curator.</response>
    [HttpPost("projects/{projectId:guid}/showcase/feature")]
    [Authorize(Roles = "admin,curator")]
    public async Task<IActionResult> FeatureShowcase(Guid projectId, CancellationToken ct = default)
    {
        var showcase = await _dbContext.ShowcaseProjects.FirstOrDefaultAsync(sp => sp.ProjectId == projectId, ct);
        if (showcase == null)
        {
            return NotFound("Showcase not found");
        }

        showcase.Featured = true;
        showcase.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        // Publish event (use curator/admin userId)
        var curatorId = GetRequiredUserId();
        await _eventBus.PublishAsync(DomainEvents.ShowcaseFeatured(projectId, curatorId));

        // Award featured badge to the project owner
        var project = await _dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project != null)
        {
            await HttpContext.RequestServices.TriggerAchievementCheckAsync(project.OwnerId, AchievementTrigger.ProjectFeatured);
        }

        await HttpContext.RequestServices.TriggerAchievementCheckAsync(curatorId, AchievementTrigger.ProjectActioned);

        return Ok(showcase);
    }

    /// <summary>
    /// Removes the featured status from a showcase for administrators or curators.
    /// </summary>
    /// <remarks>
    /// Reverses the featured designation set by the FeatureShowcase endpoint.
    /// Only administrators or curators can perform this action.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="ct">Cancels the showcase unfeature update.</param>
    /// <returns>No content.</returns>
    /// <response code="200">If the operation was successful.</response>
    /// <response code="404">If the showcase is not found.</response>
    /// <response code="403">If the user is not an admin or curator.</response>
    [HttpPost("projects/{projectId:guid}/showcase/unfeature")]
    [Authorize(Roles = "admin,curator")]
    public async Task<IActionResult> UnfeatureShowcase(Guid projectId, CancellationToken ct = default)
    {
        var showcase = await _dbContext.ShowcaseProjects.FirstOrDefaultAsync(sp => sp.ProjectId == projectId, ct);
        if (showcase == null)
        {
            return NotFound("Showcase not found");
        }

        showcase.Featured = false;
        showcase.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        // Publish event (use curator/admin userId)
        var curatorId = GetRequiredUserId();
        await _eventBus.PublishAsync(DomainEvents.ShowcaseUnfeatured(projectId, curatorId));

        return Ok(showcase);
    }

    /// <summary>
    /// Deletes a showcase when the caller is the project owner or an administrator.
    /// </summary>
    /// <remarks>
    /// Permanently removes the showcase from the project.
    /// Only the project owner or administrators can perform this action.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="ct">Cancels showcase deletion persistence.</param>
    /// <returns>No content.</returns>
    /// <response code="204">If the showcase was successfully deleted.</response>
    /// <response code="404">If the showcase is not found.</response>
    /// <response code="403">If the user is not the owner or admin.</response>
    [HttpDelete("projects/{projectId:guid}/showcase")]
    [Authorize]
    public async Task<IActionResult> DeleteShowcase(Guid projectId, CancellationToken ct = default)
    {
        var userId = GetOwnerId();
        var isAdmin = SecurityHelpers.IsAdmin(User);

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null)
        {
            return NotFound("Project not found");
        }

        // Check permissions (owner or admin)
        if (project.OwnerId != userId && !isAdmin)
        {
            return Forbid("Only project owner or admin can delete showcase");
        }

        var showcase = await _dbContext.ShowcaseProjects.FirstOrDefaultAsync(sp => sp.ProjectId == projectId, ct);
        if (showcase == null)
        {
            return NotFound("Showcase not found");
        }

        _dbContext.ShowcaseProjects.Remove(showcase);
        await _dbContext.SaveChangesAsync(ct);

        return NoContent();
    }

    // ========== Showcase Comments ==========

    /// <summary>
    /// Retrieves top-level showcase comments with non-deleted replies.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="skip">Number of top-level comments to skip (default: 0).</param>
    /// <param name="take">Number of top-level comments to take (default: 20).</param>
    /// <param name="ct">Cancels the showcase comments query.</param>
    /// <returns>A list of top-level comments with nested replies.</returns>
    /// <response code="200">Returns the list of comments.</response>
    /// <response code="404">If the showcase is not found.</response>
    [HttpGet("projects/{projectId:guid}/showcase/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> GetShowcaseComments(
        Guid projectId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20, CancellationToken ct = default)
    {
        var showcase = await _dbContext.ShowcaseProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(sp => sp.ProjectId == projectId, ct);

        if (showcase == null)
        {
            return NotFound("Showcase not found");
        }

        var comments = await _dbContext.ShowcaseComments
            .Where(c => c.ShowcaseProjectId == showcase.Id && c.ParentCommentId == null && c.DeletedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Include(c => c.Author)
            .Include(c => c.Replies.Where(r => r.DeletedAt == null))
                .ThenInclude(r => r.Author)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.CreatedAt,
                c.UpdatedAt,
                c.IsEdited,
                Author = new { c.Author.Id, c.Author.FullName, c.Author.AvatarUrl },
                Replies = c.Replies
                    .Where(r => r.DeletedAt == null)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new
                    {
                        r.Id,
                        r.Content,
                        r.CreatedAt,
                        r.UpdatedAt,
                        r.IsEdited,
                        Author = new { r.Author.Id, r.Author.FullName, r.Author.AvatarUrl }
                    })
            })
            .ToListAsync(ct);

        var totalCount = await _dbContext.ShowcaseComments
            .CountAsync(c => c.ShowcaseProjectId == showcase.Id && c.ParentCommentId == null && c.DeletedAt == null);

        return Ok(new { data = comments, totalCount });
    }

    /// <summary>
    /// Creates a top-level or reply comment on a showcase, returning not found when the showcase or parent comment is missing.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="dto">The comment content and optional parent comment ID.</param>
    /// <param name="ct">Cancels showcase comment creation.</param>
    /// <returns>The created comment.</returns>
    /// <response code="201">Returns the created comment.</response>
    /// <response code="404">If the showcase is not found.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost("projects/{projectId:guid}/showcase/comments")]
    [Authorize]
    public async Task<IActionResult> CreateShowcaseComment(Guid projectId, [FromBody] CreateShowcaseCommentDto dto, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var showcase = await _dbContext.ShowcaseProjects
            .FirstOrDefaultAsync(sp => sp.ProjectId == projectId, ct);

        if (showcase == null)
        {
            return NotFound("Showcase not found");
        }

        // Validate parent comment if specified
        if (dto.ParentCommentId.HasValue)
        {
            var parentExists = await _dbContext.ShowcaseComments
                .AnyAsync(c => c.Id == dto.ParentCommentId.Value
                    && c.ShowcaseProjectId == showcase.Id
                    && c.DeletedAt == null, ct);

            if (!parentExists)
            {
                return NotFound("Parent comment not found");
            }
        }

        var comment = new ShowcaseComment
        {
            Id = Guid.NewGuid(),
            ShowcaseProjectId = showcase.Id,
            AuthorId = userId,
            Content = dto.Content,
            ParentCommentId = dto.ParentCommentId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ShowcaseComments.Add(comment);
        await _dbContext.SaveChangesAsync(ct);

        // Load author for response
        await _dbContext.Entry(comment).Reference(c => c.Author).LoadAsync();

        await _eventBus.PublishAsync(DomainEvents.ShowcaseCommentCreated(comment.Id, projectId, userId));
        await HttpContext.RequestServices.TriggerAchievementCheckAsync(userId, AchievementTrigger.ShowcaseCommentCreated);

        return CreatedAtAction(nameof(GetShowcaseComments), new { projectId }, new
        {
            comment.Id,
            comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt,
            comment.IsEdited,
            Author = new { comment.Author.Id, comment.Author.FullName, comment.Author.AvatarUrl },
            Replies = Array.Empty<object>()
        });
    }

    /// <summary>
    /// Updates a showcase comment when the caller is the author or an administrator.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="commentId">The unique identifier of the comment.</param>
    /// <param name="dto">The updated content.</param>
    /// <param name="ct">Cancels showcase comment update persistence.</param>
    /// <returns>The updated comment.</returns>
    /// <response code="200">Returns the updated comment.</response>
    /// <response code="404">If the comment is not found.</response>
    /// <response code="403">If the user is not the author.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPut("projects/{projectId:guid}/showcase/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateShowcaseComment(Guid projectId, Guid commentId, [FromBody] UpdateShowcaseCommentDto dto, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var isAdmin = SecurityHelpers.IsAdmin(User);

        var comment = await _dbContext.ShowcaseComments
            .Include(c => c.Author)
            .Include(c => c.ShowcaseProject)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.ShowcaseProject.ProjectId == projectId && c.DeletedAt == null, ct);

        if (comment == null)
        {
            return NotFound("Comment not found");
        }

        if (comment.AuthorId != userId && !isAdmin)
        {
            return Forbid("Only the author or admin can edit this comment");
        }

        comment.Content = SecurityHelpers.SanitizeHtml(dto.Content);
        comment.UpdatedAt = DateTime.UtcNow;
        comment.IsEdited = true;

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(DomainEvents.ShowcaseCommentUpdated(commentId, projectId, userId));

        return Ok(new
        {
            comment.Id,
            comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt,
            comment.IsEdited,
            Author = new { comment.Author.Id, comment.Author.FullName, comment.Author.AvatarUrl }
        });
    }

    /// <summary>
    /// Soft-deletes a showcase comment when the caller is the author or an administrator.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="commentId">The unique identifier of the comment.</param>
    /// <param name="ct">Cancels showcase comment deletion persistence.</param>
    /// <returns>No content.</returns>
    /// <response code="204">If the comment was successfully deleted.</response>
    /// <response code="404">If the comment is not found.</response>
    /// <response code="403">If the user is not the author or admin.</response>
    [HttpDelete("projects/{projectId:guid}/showcase/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteShowcaseComment(Guid projectId, Guid commentId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var isAdmin = SecurityHelpers.IsAdmin(User);

        var comment = await _dbContext.ShowcaseComments
            .Include(c => c.ShowcaseProject)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.ShowcaseProject.ProjectId == projectId && c.DeletedAt == null, ct);

        if (comment == null)
        {
            return NotFound("Comment not found");
        }

        if (comment.AuthorId != userId && !isAdmin)
        {
            return Forbid("Only the author or admin can delete this comment");
        }

        comment.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(DomainEvents.ShowcaseCommentDeleted(commentId, projectId, userId));

        return NoContent();
    }

    // B-10: centralised URL validation for showcase URL fields
    /// <summary>
    /// Validates all optional showcase URLs before they are persisted.
    /// </summary>
    private IActionResult? ValidateShowcaseUrls(string? demoUrl, string? demoVideoUrl, string? repositoryUrl)
    {
        if (!string.IsNullOrEmpty(demoUrl) && !SecurityHelpers.IsValidUrl(demoUrl))
            return BadRequest("Invalid DemoUrl.");
        if (!string.IsNullOrEmpty(demoVideoUrl) && !SecurityHelpers.IsValidUrl(demoVideoUrl))
            return BadRequest("Invalid DemoVideoUrl.");
        if (!string.IsNullOrEmpty(repositoryUrl) && !SecurityHelpers.IsValidUrl(repositoryUrl))
            return BadRequest("Invalid RepositoryUrl.");
        return null;
    }
}


/// <summary>
/// Data transfer object for creating a project showcase.
/// </summary>
public class CreateShowcaseDto
{
    /// <summary>Short showcase summary.</summary>
    [Required]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Optional demo URL.</summary>
    public string? DemoUrl { get; set; }

    /// <summary>Optional demo video URL.</summary>
    public string? DemoVideoUrl { get; set; }

    /// <summary>Optional screenshot URLs.</summary>
    public string[]? Screenshots { get; set; }

    /// <summary>Optional repository URL.</summary>
    public string? RepositoryUrl { get; set; }

    /// <summary>Optional showcase metrics payload.</summary>
    public Dictionary<string, object>? Metrics { get; set; }
}

/// <summary>
/// Data transfer object for updating a project showcase.
/// </summary>
public class UpdateShowcaseDto
{
    /// <summary>Updated showcase summary.</summary>
    [Required]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Updated demo URL.</summary>
    public string? DemoUrl { get; set; }

    /// <summary>Updated demo video URL.</summary>
    public string? DemoVideoUrl { get; set; }

    /// <summary>Updated screenshot URLs.</summary>
    public string[]? Screenshots { get; set; }

    /// <summary>Updated repository URL.</summary>
    public string? RepositoryUrl { get; set; }

    /// <summary>Updated showcase metrics payload.</summary>
    public Dictionary<string, object>? Metrics { get; set; }
}

/// <summary>
/// Data transfer object for creating a showcase comment.
/// </summary>
public class CreateShowcaseCommentDto
{
    /// <summary>Comment content (max 2000 characters).</summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional parent comment ID for replies.</summary>
    public Guid? ParentCommentId { get; set; }
}

/// <summary>
/// Data transfer object for updating a showcase comment.
/// </summary>
public class UpdateShowcaseCommentDto
{
    /// <summary>Updated comment content (max 2000 characters).</summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}
