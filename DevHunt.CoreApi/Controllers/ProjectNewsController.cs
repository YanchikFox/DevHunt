using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Project news posts controller (updates, announcements, attachments, likes, comments).
/// </summary>
/// <remarks>
/// Routes: api/projects/{projectId}/news/*
/// </remarks>
[ApiController]
[Route("api/projects/{projectId:guid}/news")]
public class ProjectNewsController : ControllerBase
{
    private readonly IProjectNewsService _newsService;
    private readonly DevHuntDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectNewsController"/> class.
    /// </summary>
    /// <param name="newsService">Service that applies project news authorization and mutation rules.</param>
    /// <param name="dbContext">Database context used for likes and comments managed directly by the controller.</param>
    public ProjectNewsController(IProjectNewsService newsService, DevHuntDbContext dbContext)
    {
        _newsService = newsService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Converts a successful news service payload to <see cref="OkObjectResult"/> or 201, preserving service errors.
    /// </summary>
    private IActionResult MapResult<T>(NewsResult<T> result)
    {
        if (result.IsSuccess)
        {
            if (result.StatusCode == 201)
                return StatusCode(201, result.Data);
            return Ok(result.Data);
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Converts a successful no-payload news service result to <see cref="NoContentResult"/> or preserves service errors.
    /// </summary>
    private IActionResult MapResult(NewsResult result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Detects a PostgreSQL duplicate-key error raised during concurrent like creation.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";

    /// <summary>
    /// Gets paginated project news visible to the caller, with optional visibility filtering.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetProjectNews(
        Guid projectId,
        [FromQuery] string? visibility,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetProjectNewsQuery(projectId, visibility, page, pageSize, SecurityHelpers.GetUserId(User), SecurityHelpers.IsAdminOrCurator(User));
        var result = await _newsService.GetProjectNewsAsync(query);
        return MapResult(result);
    }

    /// <summary>
    /// Gets a single project news post visible to the caller.
    /// </summary>
    [HttpGet("{newsId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProjectNewsPost(Guid projectId, Guid newsId)
    {
        var result = await _newsService.GetProjectNewsPostAsync(projectId, newsId, SecurityHelpers.GetUserId(User), SecurityHelpers.IsAdminOrCurator(User));
        return MapResult(result);
    }

    /// <summary>
    /// Creates a project news post for an authorized caller and returns a creation link on success.
    /// </summary>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateProjectNews(Guid projectId, [FromBody] CreateNewsPostRequest request)
    {
        var command = new CreateProjectNewsCommand(projectId, request, GetRequiredUserId(), SecurityHelpers.IsSuperAdmin(User));
        var result = await _newsService.CreateProjectNewsAsync(command);

        // TYPE-01: result.Data is now typed as ProjectNewsResponse — no (dynamic) cast needed
        if (result.IsSuccess)
        {
            return CreatedAtAction(nameof(GetProjectNewsPost), new { projectId, newsId = result.Data!.Id }, result.Data);
        }
        return MapResult(result);
    }

    /// <summary>
    /// Updates a project news post through the news service, preserving service authorization and lookup failures.
    /// </summary>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPut("{newsId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateProjectNews(Guid projectId, Guid newsId, [FromBody] UpdateNewsPostRequest request)
    {
        var command = new UpdateProjectNewsCommand(projectId, newsId, request, GetRequiredUserId(), SecurityHelpers.IsSuperAdmin(User));
        var result = await _newsService.UpdateProjectNewsAsync(command);
        return MapResult(result);
    }

    /// <summary>
    /// Deletes a project news post through the news service, using admin or curator status for moderation rights.
    /// </summary>
    [HttpDelete("{newsId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteProjectNews(Guid projectId, Guid newsId)
    {
        var command = new DeleteProjectNewsCommand(projectId, newsId, GetRequiredUserId(), SecurityHelpers.IsAdminOrCurator(User));
        var result = await _newsService.DeleteProjectNewsAsync(command);
        return MapResult(result);
    }

    // ── Likes ─────────────────────────────────────────

    /// <summary>
    /// Toggles the authenticated user's like on a news post and recounts likes from the source table.
    /// </summary>
    [HttpPost("{newsId:guid}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleLike(Guid projectId, Guid newsId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var post = await _dbContext.ProjectNewsPosts
            .FirstOrDefaultAsync(p => p.Id == newsId && p.ProjectId == projectId, ct);
        if (post == null) return NotFound();

        var existing = await _dbContext.NewsPostLikes
            .FirstOrDefaultAsync(l => l.NewsPostId == newsId && l.UserId == userId, ct);

        // RACE-01: Remove manual LikesCount ±1 (counter drifts under concurrent requests).
        // Unique index IX_NewsPostLikes_(NewsPostId,UserId) is already defined in NewsPostConfiguration.
        bool isLiked;
        if (existing != null)
        {
            _dbContext.NewsPostLikes.Remove(existing);
            isLiked = false;
        }
        else
        {
            _dbContext.NewsPostLikes.Add(new NewsPostLike
            {
                Id = Guid.NewGuid(),
                NewsPostId = newsId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            isLiked = true;
        }

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && isLiked)
        {
            // Concurrent like — another request already inserted it; treat as success (idempotent)
            isLiked = true;
            _dbContext.ChangeTracker.Clear();
        }

        // Recount from source of truth to fix any accumulated counter drift
        var likesCount = await _dbContext.NewsPostLikes.CountAsync(l => l.NewsPostId == newsId);

        // ExecuteUpdateAsync does not require tracked entity — safe after ChangeTracker.Clear()
        await _dbContext.ProjectNewsPosts
            .Where(p => p.Id == newsId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LikesCount, likesCount));

        return Ok(new { IsLiked = isLiked, LikesCount = likesCount });
    }

    /// <summary>
    /// Gets the current like count and whether the authenticated user has liked the news post.
    /// </summary>
    [HttpGet("{newsId:guid}/like")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLikeStatus(Guid projectId, Guid newsId, CancellationToken ct = default)
    {
        var post = await _dbContext.ProjectNewsPosts
            .AsNoTracking()
            .Where(p => p.Id == newsId && p.ProjectId == projectId)
            .Select(p => new { p.LikesCount })
            .FirstOrDefaultAsync(ct);
        if (post == null) return NotFound();

        var userId = SecurityHelpers.GetUserId(User);
        bool isLiked = userId.HasValue && await _dbContext.NewsPostLikes
            .AnyAsync(l => l.NewsPostId == newsId && l.UserId == userId.Value, ct);

        return Ok(new { IsLiked = isLiked, LikesCount = post.LikesCount });
    }

    // ── Comments ──────────────────────────────────────

    /// <summary>
    /// Comment payload returned for project news comment endpoints.
    /// </summary>
    /// <param name="Id">Comment identifier.</param>
    /// <param name="AuthorId">User identifier of the comment author.</param>
    /// <param name="AuthorName">Display name or email for the author.</param>
    /// <param name="AuthorAvatarUrl">Optional avatar URL for the author.</param>
    /// <param name="Content">Sanitized comment content.</param>
    /// <param name="CreatedAt">UTC creation timestamp.</param>
    /// <param name="UpdatedAt">UTC update timestamp, when edited.</param>
    public record NewsCommentResponse(
        Guid Id, Guid AuthorId, string AuthorName, string? AuthorAvatarUrl,
        string Content, DateTime CreatedAt, DateTime? UpdatedAt);

    /// <summary>
    /// Request body for adding a project news comment.
    /// </summary>
    /// <param name="Content">Comment content, limited to 2000 characters.</param>
    public record AddNewsCommentRequest([Required, MaxLength(2000)] string Content);

    /// <summary>
    /// Request body for updating a project news comment.
    /// </summary>
    /// <param name="Content">Replacement comment content, limited to 2000 characters.</param>
    public record UpdateNewsCommentRequest([Required, MaxLength(2000)] string Content);

    /// <summary>
    /// Gets non-deleted comments for a news post, requiring authentication when the post is subscribers-only or members-only.
    /// </summary>
    [HttpGet("{newsId:guid}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> GetComments(Guid projectId, Guid newsId, CancellationToken ct = default)
    {
        var post = await _dbContext.ProjectNewsPosts
            .AsNoTracking()
            .Select(p => new { p.Id, p.ProjectId, p.Visibility })
            .FirstOrDefaultAsync(p => p.Id == newsId && p.ProjectId == projectId, ct);
        if (post == null) return NotFound();

        // QUERY-01: subscribers-only or members-only posts are not public — require authentication
        if (post.Visibility is "subscribers" or "members")
        {
            var requesterId = SecurityHelpers.GetUserId(User);
            if (requesterId == null) return Unauthorized("Authentication required to read comments on this post.");
        }

        var comments = await _dbContext.NewsPostComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.NewsPostId == newsId && c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new NewsCommentResponse(
                c.Id,
                c.AuthorId,
                c.Author.FullName ?? c.Author.Email,
                c.Author.AvatarUrl,
                c.Content,
                c.CreatedAt,
                c.UpdatedAt))
            .ToListAsync(ct);

        return Ok(comments);
    }

    /// <summary>
    /// Adds a sanitized comment to a news post and increments the post comment count.
    /// </summary>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost("{newsId:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(Guid projectId, Guid newsId, [FromBody] AddNewsCommentRequest request, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        // N+1-01: Include Author to avoid a separate Users query after SaveChangesAsync
        var post = await _dbContext.ProjectNewsPosts
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == newsId && p.ProjectId == projectId, ct);
        if (post == null) return NotFound();

        var comment = new NewsPostComment
        {
            Id = Guid.NewGuid(),
            NewsPostId = newsId,
            AuthorId = userId,
            // XSS-02: Sanitize user-provided rich-text before persisting
            Content = SecurityHelpers.SanitizeHtml(request.Content),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.NewsPostComments.Add(comment);
        await _dbContext.SaveChangesAsync(ct);

        // DEV-117: atomic increment avoids lost updates under concurrent comments.
        await _dbContext.ProjectNewsPosts
            .Where(p => p.Id == newsId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CommentsCount, p => p.CommentsCount + 1), ct);

        // N+1-01: Use already-loaded Author from the Include above — no extra round-trip
        var author = post.Author;

        return StatusCode(201, new NewsCommentResponse(
            comment.Id,
            comment.AuthorId,
            author?.FullName ?? author?.Email ?? string.Empty,
            author?.AvatarUrl,
            comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt));
    }

    /// <summary>
    /// Updates the caller's own news comment within the one-hour edit window.
    /// </summary>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPut("{newsId:guid}/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateComment(Guid projectId, Guid newsId, Guid commentId, [FromBody] UpdateNewsCommentRequest request, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        // DATA-02: Include Author so we can return actual name/avatar in the response
        var comment = await _dbContext.NewsPostComments
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.NewsPostId == newsId && c.DeletedAt == null, ct);
        if (comment == null) return NotFound();
        if (comment.AuthorId != userId) return Forbid();

        if ((DateTime.UtcNow - comment.CreatedAt).TotalHours > 1)
            return StatusCode(403, "Can only edit comments within 1 hour of creation");

        // XSS-02: Sanitize on update too
        comment.Content = SecurityHelpers.SanitizeHtml(request.Content);
        comment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        // DATA-02: Return actual author data instead of empty strings
        return Ok(new NewsCommentResponse(
            comment.Id,
            comment.AuthorId,
            comment.Author?.FullName ?? comment.Author?.Email ?? string.Empty,
            comment.Author?.AvatarUrl,
            comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt));
    }

    /// <summary>
    /// Soft-deletes a news comment when requested by the author or an admin/curator and decrements the post count.
    /// </summary>
    [HttpDelete("{newsId:guid}/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(Guid projectId, Guid newsId, Guid commentId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var comment = await _dbContext.NewsPostComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.NewsPostId == newsId && c.DeletedAt == null, ct);
        if (comment == null) return NotFound();

        bool isAdmin = SecurityHelpers.IsAdminOrCurator(User);
        if (comment.AuthorId != userId && !isAdmin) return Forbid();

        comment.DeletedAt = DateTime.UtcNow;

        var post = await _dbContext.ProjectNewsPosts.FirstOrDefaultAsync(p => p.Id == newsId, ct);
        if (post != null)
            post.CommentsCount = Math.Max(0, post.CommentsCount - 1);

        await _dbContext.SaveChangesAsync(ct);
        return NoContent();
    }
}
