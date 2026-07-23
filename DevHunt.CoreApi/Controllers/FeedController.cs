using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Personalized activity feed for authenticated users.
/// </summary>
/// <remarks>
/// Aggregates project news and related activity for followed users and projects.
/// Routes: api/feed/*
/// </remarks>
[ApiController]
[Route("api/feed")]
[Authorize]
public class FeedController : BaseProjectController
{
    /// <summary>
    /// Creates the personalized feed controller using the shared project-controller services.
    /// </summary>
    /// <param name="dbContext">Database context used to query activity records, follows, memberships, subscriptions, and news data.</param>
    /// <param name="auditService">Audit service passed to <see cref="BaseProjectController"/>.</param>
    /// <param name="notificationService">Notification client passed to <see cref="BaseProjectController"/>.</param>
    /// <param name="eventBus">Event bus passed to <see cref="BaseProjectController"/>.</param>
    /// <param name="cache">Cache service passed to <see cref="BaseProjectController"/>.</param>
    public FeedController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        IEventBusService eventBus,
        ICacheService cache)
        : base(dbContext, auditService, notificationService, eventBus, cache)
    {
    }

    /// <summary>Activity item returned in the personalized feed.</summary>
    /// <param name="Id">Activity identifier.</param>
    /// <param name="ProjectId">Related project identifier (optional).</param>
    /// <param name="ProjectTitle">Related project title.</param>
    /// <param name="ActorId">Actor user ID.</param>
    /// <param name="ActorName">Actor display name.</param>
    /// <param name="ActorAvatarUrl">Actor avatar URL.</param>
    /// <param name="EventType">Event type key.</param>
    /// <param name="EventGroup">Event group label.</param>
    /// <param name="Summary">Human-readable summary.</param>
    /// <param name="Visibility">Visibility scope.</param>
    /// <param name="PayloadJson">Optional JSON payload.</param>
    /// <param name="TargetUserId">Target user ID (optional).</param>
    /// <param name="TargetUserName">Target user display name.</param>
    /// <param name="CreatedAt">Event timestamp (UTC).</param>
    /// <param name="NewsPostId">Project news post ID when the activity is enriched from news.</param>
    /// <param name="NewsTitle">Project news title when available.</param>
    /// <param name="NewsContent">Project news body content when available.</param>
    /// <param name="LikesCount">Number of likes on the related news post.</param>
    /// <param name="CommentsCount">Number of comments on the related news post.</param>
    /// <param name="IsLikedByCurrentUser">Whether the current user liked the related news post.</param>
    /// <param name="NewsAttachments">Resolved project news attachments.</param>
    public record FeedActivityResponse(
        Guid Id,
        Guid? ProjectId,
        string? ProjectTitle,
        Guid ActorId,
        string? ActorName,
        string? ActorAvatarUrl,
        string EventType,
        string EventGroup,
        string Summary,
        string Visibility,
        string? PayloadJson,
        Guid? TargetUserId,
        string? TargetUserName,
        DateTime CreatedAt,
        // Enriched news fields
        Guid? NewsPostId = null,
        string? NewsTitle = null,
        string? NewsContent = null,
        int LikesCount = 0,
        int CommentsCount = 0,
        bool IsLikedByCurrentUser = false,
        IReadOnlyCollection<ProjectNewsAttachmentResponse>? NewsAttachments = null);

    /// <summary>
    /// Returns the authenticated user's personalized feed filtered by follows, memberships, subscriptions, and visibility.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="limit">Page size (max 50).</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>Paginated feed items enriched with project news content and attachment metadata when available.</returns>
    [HttpGet]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        var userId = GetRequiredUserId();

        var followedUserIds = await _dbContext.UserFollows
            .AsNoTracking()
            .Where(f => f.FollowerId == userId)
            .Select(f => f.FollowedId)
            .ToListAsync(ct);
        var followedUserSet = followedUserIds.ToHashSet();

        var memberProjectIds = await _dbContext.Projects
            .AsNoTracking()
            .Where(p => p.OwnerId == userId)
            .Select(p => p.Id)
            .Union(_dbContext.TeamMembers
                .AsNoTracking()
                // Active membership is represented by the TeamMemberStatus value object.
                .Where(tm => tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value)
                .Select(tm => tm.ProjectId))
            .Distinct()
            .ToListAsync(ct);
        var memberProjectSet = memberProjectIds.ToHashSet();

        var subscriberProjectIds = await _dbContext.ProjectSubscriptions
            .AsNoTracking()
            .Where(ps => ps.UserId == userId)
            .Select(ps => ps.ProjectId)
            .ToListAsync(ct);
        var subscriberProjectSet = subscriberProjectIds.ToHashSet();
        var accessibleProjectIds = memberProjectSet.Concat(subscriberProjectSet).ToHashSet();

        // Prefetch existing project IDs to avoid a correlated subquery inside Where.
        var existingProjectIds = await _dbContext.Projects
            .AsNoTracking()
            .Select(p => p.Id)
            .ToHashSetAsync(ct);

        // Only show meaningful content in the feed, not technical logs
        var allowedEventTypes = new HashSet<string>
        {
            "project.news_published"  // Main content - project news/updates
        };

        var baseQuery = _dbContext.ActivityRecords
            .AsNoTracking()
            .Include(ar => ar.Actor)
            .Include(ar => ar.Project)
            .Include(ar => ar.TargetUser)
            .Where(ar => allowedEventTypes.Contains(ar.EventType))  // Filter by allowed event types
            .Where(ar => !ar.ProjectId.HasValue || existingProjectIds.Contains(ar.ProjectId.Value)) // Exclude orphaned records (project deleted)
            .Where(ar =>
                ar.Visibility == "public" ||
                (ar.Visibility == "followers" && followedUserSet.Contains(ar.ActorId)) ||
                (ar.Visibility == "members" && ar.ProjectId.HasValue && memberProjectSet.Contains(ar.ProjectId.Value)) ||
                (ar.Visibility == "subscribers" && ar.ProjectId.HasValue && subscriberProjectSet.Contains(ar.ProjectId.Value)) ||
                (ar.Visibility == "private" && (ar.ActorId == userId || ar.TargetUserId == userId)))
            .Where(ar =>
                ar.Visibility == "public" ||
                ar.ActorId == userId ||
                followedUserSet.Contains(ar.ActorId) ||
                ar.TargetUserId == userId ||
                (ar.ProjectId.HasValue && accessibleProjectIds.Contains(ar.ProjectId.Value)));

        var total = await baseQuery.CountAsync(ct);

        var activities = await baseQuery
            .OrderByDescending(ar => ar.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(ar => new FeedActivityResponse(
                ar.Id,
                ar.ProjectId,
                ar.Project != null ? ar.Project.Title : null,
                ar.ActorId,
                ar.Actor.FullName,
                ar.Actor.AvatarUrl,
                ar.EventType,
                ar.EventGroup,
                ar.Summary,
                ar.Visibility,
                ar.PayloadJson,
                ar.TargetUserId,
                ar.TargetUser != null ? ar.TargetUser.FullName : null,
                ar.CreatedAt))
            .ToListAsync(ct);

        // Enrich news activities with full content from ProjectNewsPosts
        var enrichedActivities = await EnrichNewsActivitiesAsync(activities, userId, ct);

        return Ok(new
        {
            Data = enrichedActivities,
            Pagination = new
            {
                Page = page,
                PageSize = limit,
                Total = total,
                TotalPages = (int)Math.Ceiling((double)total / limit),
                HasNext = page * limit < total,
                HasPrevious = page > 1
            }
        });
    }

    /// <summary>
    /// Enriches project news activities with news post content, like state, comment counts, and resolved attachments.
    /// </summary>
    private async Task<List<FeedActivityResponse>> EnrichNewsActivitiesAsync(
        List<FeedActivityResponse> activities, Guid currentUserId, CancellationToken ct)
    {
        // Extract PostId from PayloadJson for news events
        var postIdMap = new Dictionary<Guid, Guid>(); // activityId → newsPostId
        foreach (var activity in activities.Where(a => a.EventType == "project.news_published"))
        {
            var postId = ExtractPostId(activity.PayloadJson);
            if (postId.HasValue)
                postIdMap[activity.Id] = postId.Value;
        }

        if (postIdMap.Count == 0)
            return activities;

        var newsPostIds = postIdMap.Values.Distinct().ToList();

        // Batch-fetch news posts with author info
        var newsPosts = await _dbContext.ProjectNewsPosts
            .AsNoTracking()
            .Include(np => np.Author)
            .Where(np => newsPostIds.Contains(np.Id))
            .ToDictionaryAsync(np => np.Id, ct);

        // Batch-fetch current user's likes
        var likedPostIdsList = await _dbContext.NewsPostLikes
            .AsNoTracking()
            .Where(l => newsPostIds.Contains(l.NewsPostId) && l.UserId == currentUserId)
            .Select(l => l.NewsPostId)
            .ToListAsync(ct);
        var likedPostIds = likedPostIdsList.ToHashSet();

        // Batch-resolve file attachments
        var allAttachments = new Dictionary<Guid, IReadOnlyCollection<ProjectNewsAttachmentDto>>();
        var allFileIds = new List<Guid>();
        foreach (var np in newsPosts.Values)
        {
            var attachments = ProjectNewsAttachmentHelper.DeserializeAttachments(np.AttachmentsJson);
            allAttachments[np.Id] = attachments;
            allFileIds.AddRange(attachments.Where(a => a.Type == "file" && a.FileId.HasValue).Select(a => a.FileId!.Value));
        }

        var files = allFileIds.Count > 0
            ? await _dbContext.ProjectFiles
                .AsNoTracking()
                .Where(pf => allFileIds.Contains(pf.Id) && pf.DeletedAt == null)
                .ToDictionaryAsync(pf => pf.Id, ct)
            : new Dictionary<Guid, Infrastructure.Models.ProjectFile>();

        // Build enriched list
        var result = new List<FeedActivityResponse>(activities.Count);
        foreach (var activity in activities)
        {
            if (postIdMap.TryGetValue(activity.Id, out var newsPostId) && newsPosts.TryGetValue(newsPostId, out var post))
            {
                var attachments = allAttachments.GetValueOrDefault(newsPostId, Array.Empty<ProjectNewsAttachmentDto>());
                var attachmentResponses = attachments
                    .Select(a => ProjectNewsAttachmentHelper.MapAttachmentToResponse(a, post.ProjectId, files))
                    .ToArray();

                result.Add(activity with
                {
                    NewsPostId = newsPostId,
                    NewsTitle = post.Title,
                    NewsContent = post.Content,
                    CreatedAt = post.CreatedAt,
                    LikesCount = post.LikesCount,
                    CommentsCount = post.CommentsCount,
                    IsLikedByCurrentUser = likedPostIds.Contains(newsPostId),
                    NewsAttachments = attachmentResponses
                });
            }
            else
            {
                result.Add(activity);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts a project news post identifier from activity payload JSON, accepting both PostId and postId keys.
    /// </summary>
    private static Guid? ExtractPostId(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("PostId", out var prop) ||
                doc.RootElement.TryGetProperty("postId", out prop))
            {
                return prop.TryGetGuid(out var id) ? id : null;
            }
        }
        catch (JsonException)
        {
            // Malformed JSON, skip
        }

        return null;
    }
}
