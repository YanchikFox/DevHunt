using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Constants;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Community feedback endpoints for bugs, suggestions, features, questions, votes, comments, and admin status updates.
/// </summary>
[ApiController]
[Route("api/community")]
public class CommunityController : ControllerBase
{
    private readonly DevHuntDbContext _context;
    private readonly IAuditService _auditService;

    /// <summary>
    /// Creates the community controller with feedback storage and audit logging services.
    /// </summary>
    /// <param name="context">Database context used for feedback, votes, and comments.</param>
    /// <param name="auditService">Audit service used to record feedback changes and moderation actions.</param>
    public CommunityController(DevHuntDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    /// <summary>
    /// Attempts to read the authenticated user identifier from the current claims principal.
    /// </summary>
    private Guid? GetUserId()
    {
        return SecurityHelpers.GetUserId(User);
    }

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return GetUserId() ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>Request to create a feedback item.</summary>
    /// <param name="Type">Feedback type (bug, suggestion, feature, question).</param>
    /// <param name="Title">Short title.</param>
    /// <param name="Description">Detailed description.</param>
    /// <param name="RelatedProjectId">Optional related project ID.</param>
    public record CreateFeedbackRequest(
        [Required, MaxLength(50)] string Type, // bug, suggestion, feature, question
        [Required, MaxLength(200)] string Title,
        [Required, MaxLength(5000)] string Description,
        Guid? RelatedProjectId);

    /// <summary>Summary response for feedback lists.</summary>
    /// <param name="Id">Feedback identifier.</param>
    /// <param name="Type">Feedback type.</param>
    /// <param name="Title">Feedback title.</param>
    /// <param name="Status">Status (open, in_progress, completed).</param>
    /// <param name="Priority">Priority level.</param>
    /// <param name="VoteCount">Number of votes.</param>
    /// <param name="CommentCount">Number of comments.</param>
    /// <param name="AuthorId">Author user ID.</param>
    /// <param name="AuthorName">Author display name.</param>
    /// <param name="CreatedAt">Creation timestamp.</param>
    /// <param name="HasUserVoted">Whether current user voted.</param>
    public record FeedbackResponse(
        Guid Id, string Type, string Title, string Status, string Priority,
        int VoteCount, int CommentCount, Guid AuthorId, string? AuthorName,
        DateTime CreatedAt, bool HasUserVoted);

    /// <summary>Detailed response for a single feedback item.</summary>
    /// <param name="Id">Feedback identifier.</param>
    /// <param name="Type">Feedback type.</param>
    /// <param name="Title">Feedback title.</param>
    /// <param name="Description">Feedback description.</param>
    /// <param name="Status">Status value.</param>
    /// <param name="Priority">Priority level.</param>
    /// <param name="VoteCount">Number of votes.</param>
    /// <param name="CommentCount">Number of comments.</param>
    /// <param name="AuthorId">Author user ID.</param>
    /// <param name="AuthorName">Author display name.</param>
    /// <param name="RelatedProjectId">Related project ID (optional).</param>
    /// <param name="AssignedToUserId">Assigned admin/user ID (optional).</param>
    /// <param name="CreatedAt">Creation timestamp.</param>
    /// <param name="UpdatedAt">Last update timestamp.</param>
    /// <param name="CompletedAt">Completion timestamp.</param>
    /// <param name="Comments">Feedback comments.</param>
    /// <param name="HasUserVoted">Whether current user voted.</param>
    public record FeedbackDetailResponse(
        Guid Id, string Type, string Title, string Description, string Status, string Priority,
        int VoteCount, int CommentCount, Guid AuthorId, string? AuthorName,
        Guid? RelatedProjectId, Guid? AssignedToUserId,
        DateTime CreatedAt, DateTime? UpdatedAt, DateTime? CompletedAt,
        IEnumerable<FeedbackCommentResponse> Comments, bool HasUserVoted);

    /// <summary>Comment item returned in feedback details.</summary>
    /// <param name="Id">Comment identifier.</param>
    /// <param name="AuthorId">Author user ID.</param>
    /// <param name="AuthorName">Author display name.</param>
    /// <param name="Content">Comment text.</param>
    /// <param name="CreatedAt">Creation timestamp.</param>
    /// <param name="UpdatedAt">Last update timestamp.</param>
    public record FeedbackCommentResponse(
        Guid Id, Guid AuthorId, string AuthorName, string Content,
        DateTime CreatedAt, DateTime? UpdatedAt);

    /// <summary>Request to add a comment to feedback.</summary>
    /// <param name="Content">Comment body.</param>
    public record AddCommentRequest([Required, MaxLength(2000)] string Content);

    /// <summary>Request to update feedback status or priority.</summary>
    /// <param name="Status">New status value.</param>
    /// <param name="Priority">Optional priority value.</param>
    /// <param name="AssignedToUserId">Optional assignee user ID.</param>
    public record UpdateFeedbackStatusRequest(
        [Required, MaxLength(50)] string Status,
        [MaxLength(50)] string? Priority,
        Guid? AssignedToUserId);

    /// <summary>
    /// Creates a sanitized feedback item for the authenticated user and audits the submission.
    /// </summary>
    /// <param name="request">Feedback data (type, title, description)</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Rejects unsupported types; otherwise returns the created feedback summary.</returns>
    /// <response code="201">Feedback successfully created</response>
    /// <response code="400">Invalid type or data</response>
    /// <response code="401">User not authorized</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost("feedback")]
    [Authorize]
    [ProducesResponseType(typeof(FeedbackResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateFeedback([FromBody] CreateFeedbackRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        var allowedTypes = new[] { "bug", "suggestion", "feature", "question" };
        if (!allowedTypes.Contains(request.Type.ToLower(CultureInfo.InvariantCulture)))
        {
            return BadRequest($"Invalid type. Allowed: {string.Join(", ", allowedTypes)}");
        }

        // Sanitize rich-text fields against XSS.
        var feedback = new FeedbackItem
        {
            Id = Guid.NewGuid(),
            AuthorId = userId,
            Type = request.Type.ToLower(CultureInfo.InvariantCulture),
            Title = SecurityHelpers.SanitizeHtml(request.Title).Trim(),
            Description = SecurityHelpers.SanitizeHtml(request.Description).Trim(),
            Status = FeedbackStatus.Open,
            Priority = "medium",
            VoteCount = 0,
            CommentCount = 0,
            RelatedProjectId = request.RelatedProjectId,
            CreatedAt = DateTime.UtcNow
        };

        // Preload the author before saving to avoid an extra lookup after SaveChangesAsync.
        var author = await _context.Users.FindAsync(new object[] { userId }, ct);

        _context.FeedbackItems.Add(feedback);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "CommunityController.CreateFeedback", "FeedbackItem", feedback.Id,
            $"Created feedback: {request.Type} - {request.Title}");

        return CreatedAtAction(nameof(GetFeedback), new { feedbackId = feedback.Id },
            new FeedbackResponse(feedback.Id, feedback.Type, feedback.Title, feedback.Status, feedback.Priority,
                feedback.VoteCount, feedback.CommentCount, feedback.AuthorId,
                author?.FullName ?? author?.Email, feedback.CreatedAt, false));
    }

    /// <summary>
    /// Lists feedback items with optional type/status filters, sorting, pagination, and current-user vote state.
    /// </summary>
    [HttpGet("feedback")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeedback(
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sortBy = "votes", // votes, created, updated
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();

        var query = _context.FeedbackItems.AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(f => f.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        // Sorting
        query = (sortBy ?? "votes").ToLower(CultureInfo.InvariantCulture) switch
        {
            "votes" => query.OrderByDescending(f => f.VoteCount).ThenByDescending(f => f.CreatedAt),
            "created" => query.OrderByDescending(f => f.CreatedAt),
            "updated" => query.OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt),
            _ => query.OrderByDescending(f => f.VoteCount)
        };

        var total = await query.CountAsync(ct);
        var feedbacks = await query
            .Include(f => f.Author)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FeedbackResponse(
                f.Id, f.Type, f.Title, f.Status, f.Priority,
                f.VoteCount, f.CommentCount, f.AuthorId,
                f.Author != null ? f.Author.FullName ?? f.Author.Email : "Unknown",
                f.CreatedAt,
                userId.HasValue && f.Votes.Any(v => v.UserId == userId.Value)))
            .ToListAsync(ct);

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Data = feedbacks });
    }

    /// <summary>
    /// Returns feedback details, non-deleted comments, and current-user vote state.
    /// </summary>
    [HttpGet("feedback/{feedbackId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeedback(Guid feedbackId, CancellationToken ct)
    {
        var userId = GetUserId();

        var feedback = await _context.FeedbackItems
            .Include(f => f.Author)
            .Include(f => f.Comments).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(f => f.Id == feedbackId, ct);

        if (feedback == null) return NotFound();

        var hasUserVoted = userId.HasValue &&
            await _context.FeedbackVotes.AnyAsync(v => v.FeedbackId == feedbackId && v.UserId == userId.Value, ct);

        var comments = feedback.Comments
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new FeedbackCommentResponse(
                c.Id, c.AuthorId, c.Author != null ? c.Author.FullName ?? c.Author.Email : "Unknown",
                c.Content, c.CreatedAt, c.UpdatedAt))
            .ToList();

        var response = new FeedbackDetailResponse(
            feedback.Id, feedback.Type, feedback.Title, feedback.Description,
            feedback.Status, feedback.Priority, feedback.VoteCount, feedback.CommentCount,
            feedback.AuthorId, feedback.Author != null ? feedback.Author.FullName ?? feedback.Author.Email : "Unknown",
            feedback.RelatedProjectId, feedback.AssignedToUserId,
            feedback.CreatedAt, feedback.UpdatedAt, feedback.CompletedAt,
            comments, hasUserVoted);

        return Ok(response);
    }

    /// <summary>
    /// Adds, flips, or cancels the current user's vote and recalculates the authoritative vote count.
    /// </summary>
    [HttpPost("feedback/{feedbackId:guid}/vote")]
    [Authorize]
    public async Task<IActionResult> VoteFeedback(Guid feedbackId, [FromBody] bool isUpvote = true, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        // Use a repeatable-read transaction and CountAsync to keep VoteCount consistent.
        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, ct);

        var feedback = await _context.FeedbackItems.FirstOrDefaultAsync(f => f.Id == feedbackId, ct);
        if (feedback == null) return NotFound();

        var existingVote = await _context.FeedbackVotes
            .FirstOrDefaultAsync(v => v.FeedbackId == feedbackId && v.UserId == userId, ct);

        if (existingVote != null)
        {
            if (existingVote.IsUpvote == isUpvote)
            {
                // Cancel vote (double click)
                _context.FeedbackVotes.Remove(existingVote);
            }
            else
            {
                // Change vote direction
                existingVote.IsUpvote = isUpvote;
            }
        }
        else
        {
            var vote = new FeedbackVote
            {
                Id = Guid.NewGuid(),
                FeedbackId = feedbackId,
                UserId = userId,
                IsUpvote = isUpvote,
                CreatedAt = DateTime.UtcNow
            };

            _context.FeedbackVotes.Add(vote);
        }

        await _context.SaveChangesAsync(ct);
        // Recount from DB to get the authoritative value
        feedback.VoteCount = await _context.FeedbackVotes.CountAsync(v => v.FeedbackId == feedbackId, ct);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(new { VoteCount = feedback.VoteCount });
    }

    /// <summary>
    /// Removes the current user's vote from feedback and recalculates the authoritative vote count.
    /// </summary>
    [HttpDelete("feedback/{feedbackId:guid}/vote")]
    [Authorize]
    public async Task<IActionResult> UnvoteFeedback(Guid feedbackId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        // Use a repeatable-read transaction and CountAsync to keep VoteCount consistent.
        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, ct);

        var feedback = await _context.FeedbackItems.FirstOrDefaultAsync(f => f.Id == feedbackId, ct);
        if (feedback == null) return NotFound();

        var existingVote = await _context.FeedbackVotes
            .FirstOrDefaultAsync(v => v.FeedbackId == feedbackId && v.UserId == userId, ct);

        if (existingVote == null)
        {
            return BadRequest("You haven't voted for this feedback");
        }

        _context.FeedbackVotes.Remove(existingVote);
        await _context.SaveChangesAsync(ct);
        // Recount from DB to get the authoritative value
        feedback.VoteCount = await _context.FeedbackVotes.CountAsync(v => v.FeedbackId == feedbackId, ct);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(new { Message = "Vote removed", VoteCount = feedback.VoteCount });
    }

    /// <summary>
    /// Adds a sanitized comment to feedback, increments the comment count, and returns the created comment.
    /// </summary>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost("feedback/{feedbackId:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(Guid feedbackId, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        var feedback = await _context.FeedbackItems.FindAsync(new object[] { feedbackId }, ct);
        if (feedback == null) return NotFound();

        var comment = new FeedbackComment
        {
            Id = Guid.NewGuid(),
            FeedbackId = feedbackId,
            AuthorId = userId,
            Content = SecurityHelpers.SanitizeHtml(request.Content.Trim()),
            CreatedAt = DateTime.UtcNow
        };

        // Preload the author before saving.
        var author = await _context.Users.FindAsync(new object[] { userId }, ct);

        _context.FeedbackComments.Add(comment);
        feedback.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        // DEV-117: atomic increment avoids lost updates when comments are added concurrently.
        await _context.FeedbackItems
            .Where(f => f.Id == feedbackId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.CommentCount, f => f.CommentCount + 1), ct);

        return Ok(new FeedbackCommentResponse(comment.Id, comment.AuthorId,
            author?.FullName ?? author?.Email ?? "User", comment.Content,
            comment.CreatedAt, null));
    }

    /// <summary>
    /// Updates feedback status, optional priority, and optional assignee for admins or curators.
    /// </summary>
    [HttpPut("feedback/{feedbackId:guid}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateFeedbackStatus(Guid feedbackId, [FromBody] UpdateFeedbackStatusRequest request, CancellationToken ct)
    {
        // Use claims-based role checks to avoid an extra database round trip.
        if (!SecurityHelpers.IsAdminOrCurator(User)) return Forbid();

        var feedback = await _context.FeedbackItems.FindAsync(new object[] { feedbackId }, ct);
        if (feedback == null) return NotFound();

        var allowedStatuses = new[] { FeedbackStatus.Open, FeedbackStatus.UnderReview, FeedbackStatus.Planned, FeedbackStatus.InProgress, FeedbackStatus.Completed, FeedbackStatus.Rejected, FeedbackStatus.Duplicate };
        var normalizedStatus = request.Status.ToLower(CultureInfo.InvariantCulture);
        if (!allowedStatuses.Contains(normalizedStatus))
        {
            return BadRequest($"Invalid status. Allowed: {string.Join(", ", allowedStatuses)}");
        }

        feedback.Status = normalizedStatus;
        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            feedback.Priority = request.Priority.ToLower(CultureInfo.InvariantCulture);
        }
        if (request.AssignedToUserId.HasValue)
        {
            feedback.AssignedToUserId = request.AssignedToUserId;
        }

        if (normalizedStatus == FeedbackStatus.Completed)
        {
            feedback.CompletedAt = DateTime.UtcNow;
        }

        feedback.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var adminUserId = GetUserId();
        if (adminUserId.HasValue)
        {
            await _auditService.LogActionAsync(adminUserId.Value, "CommunityController.UpdateFeedbackStatus",
                "FeedbackItem", feedbackId, $"Updated status to {request.Status}");
        }

        return Ok(new { Status = feedback.Status, Priority = feedback.Priority });
    }

    /// <summary>Request to update feedback title/description.</summary>
    /// <param name="Title">Updated title.</param>
    /// <param name="Description">Updated description.</param>
    public record UpdateFeedbackRequest(string? Title, string? Description);

    /// <summary>
    /// Lets the author edit feedback title or description while the item is still open.
    /// </summary>
    [HttpPut("feedback/{feedbackId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateFeedback(Guid feedbackId, [FromBody] UpdateFeedbackRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        var feedback = await _context.FeedbackItems.FindAsync(new object[] { feedbackId }, ct);
        if (feedback == null) return NotFound();

        // Only author can edit
        if (feedback.AuthorId != userId) return Forbid();

        // Can only edit in 'open' status
        if (feedback.Status != FeedbackStatus.Open) return BadRequest("Can only edit feedback in 'open' status");

        // Preload the author before saving.
        var author = await _context.Users.FindAsync(new object[] { userId }, ct);

        // Sanitize rich-text fields against XSS.
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            feedback.Title = SecurityHelpers.SanitizeHtml(request.Title).Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            feedback.Description = SecurityHelpers.SanitizeHtml(request.Description).Trim();
        }

        feedback.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "CommunityController.UpdateFeedback", "FeedbackItem", feedbackId,
            $"Updated feedback: {feedback.Title}");

        return Ok(new FeedbackResponse(feedback.Id, feedback.Type, feedback.Title, feedback.Status, feedback.Priority,
            feedback.VoteCount, feedback.CommentCount, feedback.AuthorId,
            author?.FullName ?? author?.Email, feedback.CreatedAt, false));
    }

    /// <summary>
    /// Deletes feedback when requested by its author or an admin/curator and records an audit entry.
    /// </summary>
    [HttpDelete("feedback/{feedbackId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteFeedback(Guid feedbackId, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        var feedback = await _context.FeedbackItems.FindAsync(new object[] { feedbackId }, ct);
        if (feedback == null) return NotFound();

        // Use claims-based role checks to avoid an extra database round trip.
        bool isAdmin = SecurityHelpers.IsAdminOrCurator(User);

        // Only author or admin can delete
        if (feedback.AuthorId != userId && !isAdmin) return Forbid();

        _context.FeedbackItems.Remove(feedback);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "CommunityController.DeleteFeedback", "FeedbackItem", feedbackId,
            $"Deleted feedback: {feedback.Title}");

        return NoContent();
    }

    /// <summary>Request to update a feedback comment.</summary>
    /// <param name="Content">Updated comment content.</param>
    public record UpdateCommentRequest([Required, MaxLength(2000)] string Content);

    /// <summary>
    /// Lets the comment author edit a non-deleted comment within one hour of creation.
    /// </summary>
    [HttpPut("feedback/{feedbackId:guid}/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateComment(Guid feedbackId, Guid commentId, [FromBody] UpdateCommentRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        var comment = await _context.FeedbackComments.FirstOrDefaultAsync(c => c.Id == commentId && c.FeedbackId == feedbackId && c.DeletedAt == null, ct);
        if (comment == null) return NotFound();

        // Only author can edit
        if (comment.AuthorId != userId) return Forbid();

        // Can only edit within 1 hour
        if (comment.CreatedAt.AddHours(1) < DateTime.UtcNow)
            return BadRequest("Cannot edit comment after 1 hour");

        // Preload the author before saving.
        var author = await _context.Users.FindAsync(new object[] { userId }, ct);

        comment.Content = request.Content.Trim();
        comment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new FeedbackCommentResponse(comment.Id, comment.AuthorId,
            author?.FullName ?? author?.Email ?? "Unknown",
            comment.Content, comment.CreatedAt, comment.UpdatedAt));
    }

    /// <summary>
    /// Soft-deletes a comment when requested by its author or an admin/curator and decrements the feedback count.
    /// </summary>
    [HttpDelete("feedback/{feedbackId:guid}/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(Guid feedbackId, Guid commentId, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        var comment = await _context.FeedbackComments.FirstOrDefaultAsync(c => c.Id == commentId && c.FeedbackId == feedbackId && c.DeletedAt == null, ct);
        if (comment == null) return NotFound();

        // Use claims-based role checks to avoid an extra database round trip.
        bool isAdmin = SecurityHelpers.IsAdminOrCurator(User);

        // Only author or admin can delete
        if (comment.AuthorId != userId && !isAdmin) return Forbid();

        // Soft delete
        comment.DeletedAt = DateTime.UtcNow;

        var feedback = await _context.FeedbackItems.FindAsync(new object[] { feedbackId }, ct);
        if (feedback != null)
        {
            feedback.CommentCount = Math.Max(0, feedback.CommentCount - 1);
        }

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
