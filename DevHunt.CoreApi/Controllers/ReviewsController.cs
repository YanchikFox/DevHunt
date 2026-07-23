using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Badges;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing project and user reviews.
/// </summary>
/// <remarks>
/// This controller implements a dual-purpose review system:
/// 1. Project reviews: Team members can review the overall project experience
/// 2. Peer reviews: Team members can review other team members they worked with
///
/// Core features:
/// - Review creation with 1-5 star ratings and optional text feedback
/// - Automatic rating aggregation for projects and users
/// - Edit window (24 hours after creation)
/// - Duplicate prevention (one review per project/user pair)
/// - Pagination support for review lists
///
/// Security:
/// - Only active team members can create reviews
/// - Users cannot review themselves
/// - Reviews can be edited only by the author within 24 hours
/// - Reviews can be deleted by the author or admins/curators
///
/// Routes: api/reviews/*
/// </remarks>
[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewsController"/> class.
    /// </summary>
    /// <param name="db">Database context used to persist reviews and recalculate aggregate ratings.</param>
    public ReviewsController(DevHuntDbContext db) => _db = db;

    /// <summary>Review item returned in API responses.</summary>
    /// <param name="Id">Review identifier.</param>
    /// <param name="ProjectId">Project identifier.</param>
    /// <param name="ReviewerId">Reviewer user ID.</param>
    /// <param name="ReviewedUserId">Reviewed user ID (null for project review).</param>
    /// <param name="Rating">Rating value (1-5).</param>
    /// <param name="ReviewText">Optional review text.</param>
    /// <param name="CreatedAt">Creation timestamp.</param>
    public record ReviewDto(Guid Id, Guid ProjectId, Guid ReviewerId, Guid? ReviewedUserId, int Rating, string? ReviewText, DateTime CreatedAt);

    /// <summary>Request to create a review.</summary>
    /// <param name="ProjectId">Target project identifier.</param>
    /// <param name="ReviewedUserId">Target user identifier (optional).</param>
    /// <param name="Rating">Rating value (1-5).</param>
    /// <param name="ReviewText">Optional review text.</param>
    public record CreateReviewRequest(Guid ProjectId, Guid? ReviewedUserId, int Rating, string? ReviewText);

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Retrieves all reviews for a specific project with pagination.
    /// </summary>
    /// <remarks>
    /// This endpoint returns reviews ordered by creation date (newest first).
    /// Includes pagination metadata for building UI controls.
    ///
    /// Security constraints:
    /// - Page size is capped at 100 to prevent excessive data transfer
    /// - Invalid pagination parameters are automatically corrected (not rejected)
    ///
    /// The response includes:
    /// - Review data (ID, ratings, text, timestamps)
    /// - Pagination metadata (current page, total pages, has next/previous)
    ///
    /// Accessible to anonymous users for public project transparency.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="page">Page number (minimum: 1, default: 1).</param>
    /// <param name="pageSize">Number of items per page (minimum: 1, maximum: 100, default: 20).</param>
    /// <param name="ct">Cancels the project reviews query.</param>
    /// <returns>Paginated list of project reviews.</returns>
    /// <response code="200">Returns the paginated review list.</response>
    [HttpGet("project/{projectId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProjectReviews(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        // SECURITY: Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // SECURITY: Limit max page size

        var query = _db.Reviews.AsNoTracking().Where(r => r.ProjectId == projectId);

        // Get total count for pagination
        var total = await query.CountAsync(ct);

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewDto(r.Id, r.ProjectId, r.ReviewerId, r.ReviewedUserId, r.Rating, r.ReviewText, r.CreatedAt))
            .ToListAsync(ct);

        // Standardized response format with pagination
        return Ok(new
        {
            Data = list,
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

    /// <summary>
    /// Creates a new review for a project or team member.
    /// </summary>
    /// <remarks>
    /// This endpoint creates a review and automatically updates aggregate ratings.
    ///
    /// Validation rules:
    /// - Rating must be between 1 and 5 (inclusive)
    /// - Reviewer must be an active team member of the project
    /// - Users cannot review themselves
    /// - Duplicate reviews are prevented (one review per project/user pair)
    ///
    /// Review types:
    /// - Project review: ReviewedUserId is null (reviews the overall project)
    /// - Peer review: ReviewedUserId is set (reviews a specific team member)
    ///
    /// Side effects:
    /// - Recalculates and updates the project's average rating
    /// - If reviewing a user, recalculates and updates that user's average rating
    ///
    /// All review text is trimmed; empty strings are stored as null.
    /// </remarks>
    /// <param name="req">The review creation request containing project ID, optional user ID, rating, and text.</param>
    /// <param name="ct">Cancels review creation and rating updates.</param>
    /// <returns>The created review DTO.</returns>
    /// <response code="200">Returns the created review.</response>
    /// <response code="400">If the rating is invalid or user tries to review themselves.</response>
    /// <response code="403">If the user is not a team member of the project.</response>
    /// <response code="409">If a review already exists for this project/user pair.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        if (req.Rating < 1 || req.Rating > 5) return BadRequest("Rating 1..5");
        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.ProjectId == req.ProjectId && tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value, ct);
        if (!isMember) return Forbid();
        if (req.ReviewedUserId != null && req.ReviewedUserId == userId) return BadRequest("Can't review yourself");
        var duplicate = await _db.Reviews.AnyAsync(r => r.ProjectId == req.ProjectId && r.ReviewerId == userId && r.ReviewedUserId == req.ReviewedUserId, ct);
        if (duplicate) return Conflict("Already reviewed");
        var review = new Review {
            Id = Guid.NewGuid(), ProjectId = req.ProjectId, ReviewerId = userId, ReviewedUserId = req.ReviewedUserId,
            Rating = req.Rating, ReviewText = string.IsNullOrWhiteSpace(req.ReviewText) ? null : SecurityHelpers.SanitizeHtml(req.ReviewText.Trim()), CreatedAt = DateTime.UtcNow
        };
        // Wrap review creation and rating aggregation in a single transaction.
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(ct);
        // Update aggregates (simplified: average rating for project and user)
        await RecalculateRatingsAsync(req.ProjectId, req.ReviewedUserId, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync();
        await HttpContext.RequestServices.TriggerAchievementCheckAsync(userId, AchievementTrigger.ReviewCreated);
        // Also check top_rated for the reviewed user
        if (req.ReviewedUserId != null)
            await HttpContext.RequestServices.TriggerAchievementCheckAsync(req.ReviewedUserId.Value, AchievementTrigger.ReviewCreated);
        return Ok(new ReviewDto(review.Id, review.ProjectId, review.ReviewerId, review.ReviewedUserId, review.Rating, review.ReviewText, review.CreatedAt));
    }

    /// <summary>Request to update a review.</summary>
    /// <param name="Rating">Updated rating (1-5).</param>
    /// <param name="ReviewText">Updated review text.</param>
    public record UpdateReviewRequest(int? Rating, string? ReviewText);

    /// <summary>
    /// Updates an existing review (rating and/or text).
    /// </summary>
    /// <remarks>
    /// This endpoint allows review authors to modify their reviews within a 24-hour window.
    ///
    /// Edit restrictions:
    /// - Only the original author can edit their review
    /// - Edits are only allowed within 24 hours of creation
    /// - After 24 hours, reviews become immutable (prevents gaming the system)
    ///
    /// Validation:
    /// - If rating is updated, it must still be between 1 and 5
    /// - Text updates are trimmed; empty strings are stored as null
    ///
    /// Side effects:
    /// - Recalculates and updates the project's average rating
    /// - If this is a peer review, recalculates the reviewed user's average rating
    ///
    /// Partial updates are supported: you can update just the rating, just the text, or both.
    /// </remarks>
    /// <param name="reviewId">The unique identifier of the review to update.</param>
    /// <param name="req">The update request containing optional new rating and/or review text.</param>
    /// <param name="ct">Cancels review update and rating recalculation.</param>
    /// <returns>The updated review DTO.</returns>
    /// <response code="200">Returns the updated review.</response>
    /// <response code="400">If the rating is invalid or the 24-hour edit window has expired.</response>
    /// <response code="403">If the user is not the review author.</response>
    /// <response code="404">If the review is not found.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPut("{reviewId:guid}")]
    public async Task<IActionResult> UpdateReview(Guid reviewId, [FromBody] UpdateReviewRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, ct);
        if (review == null) return NotFound();

        // Only author can edit
        if (review.ReviewerId != userId) return Forbid();

        // Can only edit within 24 hours
        if (review.CreatedAt.AddHours(24) < DateTime.UtcNow)
            return BadRequest("Cannot edit review after 24 hours");

        if (req.Rating.HasValue)
        {
            if (req.Rating.Value < 1 || req.Rating.Value > 5) return BadRequest("Rating 1..5");
            review.Rating = req.Rating.Value;
        }

        if (req.ReviewText != null)
        {
            review.ReviewText = string.IsNullOrWhiteSpace(req.ReviewText) ? null : SecurityHelpers.SanitizeHtml(req.ReviewText.Trim());
        }

        // Wrap rating updates and aggregation in a single transaction.
        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync(ct);

        // Recalculate ratings
        await RecalculateRatingsAsync(review.ProjectId, review.ReviewedUserId, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync();
        return Ok(new ReviewDto(review.Id, review.ProjectId, review.ReviewerId, review.ReviewedUserId, review.Rating, review.ReviewText, review.CreatedAt));
    }

    /// <summary>
    /// Deletes a review.
    /// </summary>
    /// <remarks>
    /// This endpoint permanently removes a review from the system.
    ///
    /// Authorization:
    /// - The review author can always delete their own review (no time restriction)
    /// - Admins and curators can delete any review (for moderation purposes)
    ///
    /// Side effects:
    /// - Recalculates and updates the project's average rating after deletion
    /// - If this was a peer review, recalculates the reviewed user's average rating
    /// - All aggregate ratings are updated to reflect the removal
    ///
    /// This operation cannot be undone.
    /// </remarks>
    /// <param name="reviewId">The unique identifier of the review to delete.</param>
    /// <param name="ct">Cancels review deletion and rating recalculation.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">If the review was successfully deleted.</response>
    /// <response code="403">If the user is neither the author nor an admin/curator.</response>
    /// <response code="404">If the review is not found.</response>
    [HttpDelete("{reviewId:guid}")]
    public async Task<IActionResult> DeleteReview(Guid reviewId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, ct);
        if (review == null) return NotFound();

        // Use a claims-based role check to avoid an extra database round trip.
        bool isAdmin = SecurityHelpers.IsAdminOrCurator(User);

        if (review.ReviewerId != userId && !isAdmin) return Forbid();

        var projectId = review.ProjectId;
        var reviewedUserId = review.ReviewedUserId;

        // Wrap review deletion and rating aggregation in a single transaction.
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);

        // Recalculate ratings
        await RecalculateRatingsAsync(projectId, reviewedUserId, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync();
        return NoContent();
    }

    /// <summary>
    /// Recalculates and persists the average rating for a project and, if provided, a reviewed user.
    /// Shared by Create, UpdateReview, and DeleteReview so aggregate rating logic stays in one place.
    /// </summary>
    private async Task RecalculateRatingsAsync(Guid projectId, Guid? reviewedUserId, CancellationToken ct)
    {
        var projAvg = await _db.Reviews.Where(r => r.ProjectId == projectId && r.ReviewedUserId == null).AverageAsync(r => (double?)r.Rating) ?? 0.0;
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project != null) project.Rating = (float)projAvg;

        if (reviewedUserId != null)
        {
            var userAvg = await _db.Reviews.Where(r => r.ReviewedUserId == reviewedUserId).AverageAsync(r => (double?)r.Rating) ?? 0.0;
            var u = await _db.Users.FirstOrDefaultAsync(u => u.Id == reviewedUserId, ct);
            if (u != null) u.Rating = (float)userAvg;
        }
    }
}
