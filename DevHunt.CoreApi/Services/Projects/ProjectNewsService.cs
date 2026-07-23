using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// EF-backed implementation of <see cref="IProjectNewsService"/>. Enforces visibility tiers via
/// <see cref="ProjectNewsVisibilityHelper"/>, manages attachments through
/// <see cref="ProjectNewsAttachmentHelper"/>, and syncs public posts to the activity log.
/// </summary>
public class ProjectNewsService : IProjectNewsService
{
    private readonly DevHuntDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly INotificationHelperService _notificationService;
    private readonly IActivityLogService _activityLogService;
    private readonly ICacheService _cache;
    private readonly ILogger<ProjectNewsService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectNewsService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for projects, news posts, subscriptions, users, files, and activity rows.</param>
    /// <param name="auditService">Audit writer for news create and update actions.</param>
    /// <param name="notificationService">Sends subscriber notifications for newly published updates.</param>
    /// <param name="activityLogService">Writes public news posts into the project activity feed.</param>
    /// <param name="cache">Cache service available for project/news invalidation paths.</param>
    /// <param name="logger">Logger for notification failures and news diagnostics.</param>
    public ProjectNewsService(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationHelperService notificationService,
        IActivityLogService activityLogService,
        ICacheService cache,
        ILogger<ProjectNewsService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _notificationService = notificationService;
        _activityLogService = activityLogService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>Maps a post using preloaded author and file dictionaries for list endpoints.</summary>
    private static ProjectNewsResponse MapPostToBatchResponse(
        ProjectNewsPost post,
        Guid projectId,
        Dictionary<Guid, User> authors,
        Dictionary<Guid, ProjectFile> files)
    {
        var author = authors.GetValueOrDefault(post.AuthorId);
        var attachments = ProjectNewsAttachmentHelper.DeserializeAttachments(post.AttachmentsJson);
        var attachmentResponses = attachments
            .Select(a => ProjectNewsAttachmentHelper.MapAttachmentToResponse(a, projectId, files))
            .ToArray();

        return new ProjectNewsResponse(
            post.Id,
            post.ProjectId,
            post.AuthorId,
            author?.FullName ?? author?.Email ?? string.Empty,
            author?.AvatarUrl ?? string.Empty,
            post.Title,
            post.Content,
            post.Visibility,
            post.IsPinned,
            post.CreatedAt,
            post.UpdatedAt,
            attachmentResponses);
    }

    /// <summary>Builds anonymous pagination metadata for news list responses.</summary>
    private static object BuildPaginationInfo(int page, int pageSize, int total) => new
    {
        Page = page,
        PageSize = pageSize,
        Total = total,
        TotalPages = (int)Math.Ceiling((double)total / pageSize),
        HasNext = page * pageSize < total,
        HasPrevious = page > 1
    };

    /// <summary>Loads author and attachment file data for a single post response.</summary>
    private async Task<ProjectNewsResponse> MapToResponseAsync(ProjectNewsPost post, CancellationToken ct = default)
    {
        var author = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == post.AuthorId, ct);
        var attachments = ProjectNewsAttachmentHelper.DeserializeAttachments(post.AttachmentsJson);
        var attachmentResponses = await ProjectNewsAttachmentHelper.BuildAttachmentResponsesAsync(_dbContext, post.ProjectId, attachments);

        return new ProjectNewsResponse(
            post.Id,
            post.ProjectId,
            post.AuthorId,
            author?.FullName ?? author?.Email ?? string.Empty,
            author?.AvatarUrl ?? string.Empty,
            post.Title,
            post.Content,
            post.Visibility,
            post.IsPinned,
            post.CreatedAt,
            post.UpdatedAt,
            attachmentResponses);
    }

    /// <summary>Batch-loads authors and non-deleted project files referenced by a page of posts.</summary>
    private async Task<(Dictionary<Guid, User> Authors, Dictionary<Guid, ProjectFile> Files)> LoadBatchDataAsync(
        Guid projectId,
        List<ProjectNewsPost> posts)
    {
        var authorIds = posts.Select(p => p.AuthorId).Distinct().ToList();
        var authors = await _dbContext.Users
            .AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var allFileIds = posts
            .SelectMany(p => ProjectNewsAttachmentHelper.DeserializeAttachments(p.AttachmentsJson))
            .Where(a => a.Type == "file" && a.FileId.HasValue)
            .Select(a => a.FileId!.Value)
            .Distinct()
            .ToList();

        var files = await _dbContext.ProjectFiles
            .AsNoTracking()
            .Where(pf => pf.ProjectId == projectId && allFileIds.Contains(pf.Id) && pf.DeletedAt == null)
            .ToDictionaryAsync(pf => pf.Id);

        return (authors, files);
    }

    /// <summary>True for owner/admin or members with <see cref="TeamMember.CanPublishNews"/>.</summary>
    private async Task<bool> CanPublishNewsAsync(NewsAuthoringContext context, CancellationToken ct = default)
    {
        if (context.IsAdminOrOwner)
            return true;

        var teamMember = await _dbContext.TeamMembers
            .FirstOrDefaultAsync(tm => tm.ProjectId == context.ProjectId && tm.UserId == context.UserId && tm.Status == TeamMemberStatus.Active.Value, ct);

        return teamMember?.CanPublishNews == true;
    }

    /// <summary>True for author, owner/admin, or members with delegated news permission.</summary>
    private async Task<bool> CanEditNewsAsync(NewsAuthoringContext context, Guid authorId, CancellationToken ct = default)
    {
        if (context.IsAdminOrOwner || context.IsAuthor(authorId))
            return true;

        var teamMember = await _dbContext.TeamMembers
            .FirstOrDefaultAsync(tm => tm.ProjectId == context.ProjectId && tm.UserId == context.UserId && tm.Status == TeamMemberStatus.Active.Value, ct);

        return teamMember?.CanPublishNews == true;
    }

    /// <summary>Sends bulk notifications to project subscribers except the author.</summary>
    private async Task NotifySubscribersAsync(Guid projectId, Guid authorId, string projectTitle, string postTitle, CancellationToken ct = default)
    {
        try
        {
            var subscriberIds = await _dbContext.ProjectSubscriptions
                .Where(s => s.ProjectId == projectId && s.UserId != authorId)
                .Select(s => s.UserId)
                .ToListAsync(ct);

            if (subscriberIds.Count > 0)
            {
                await _notificationService.SendBulkNotificationsAsync(
                    subscriberIds,
                    "project_news",
                    $"New update in {projectTitle}",
                    postTitle,
                    "Project",
                    projectId,
                    "normal");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notifications");
        }
    }

    /// <summary>Writes a <c>project.news_published</c> activity record for public visibility posts.</summary>
    private async Task LogPublicNewsActivityAsync(
        Guid userId,
        ProjectNewsPost post,
        List<ProjectNewsAttachmentDto> attachments)
    {
        var attachmentResponses = await ProjectNewsAttachmentHelper.BuildAttachmentResponsesAsync(_dbContext, post.ProjectId, attachments);
        var imageUrls = attachmentResponses
            .Where(a => a.ContentType?.StartsWith("image/") == true || a.Type == "url")
            .Select(a => a.Url ?? a.DownloadUrl)
            .Where(url => !string.IsNullOrEmpty(url))
            .ToList();

        await _activityLogService.LogProjectEventAsync(
            post.ProjectId,
            userId,
            "project.news_published",
            $"Published news: {post.Title}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(post.Visibility),
            payload: new
            {
                PostId = post.Id,
                Title = post.Title,
                Content = post.Content,
                IsPinned = post.IsPinned,
                Visibility = post.Visibility,
                Images = imageUrls
            });
    }

    /// <summary>Removes old activity rows for a post and re-logs when visibility is public.</summary>
     private async Task SyncNewsActivityRecordsAsync(ProjectNewsPost post, List<ProjectNewsAttachmentDto> attachments)
    {
        await CleanupNewsActivityRecordsAsync(post.ProjectId, post.Id);

        if (post.Visibility == "public")
        {
            await LogPublicNewsActivityAsync(post.AuthorId, post, attachments);
        }
    }

    /// <summary>Deletes activity records whose payload references the given news post id.</summary>
    private async Task CleanupNewsActivityRecordsAsync(Guid projectId, Guid newsId, CancellationToken ct = default)
    {
        var activityRecords = (await _dbContext.ActivityRecords
                .Where(ar => ar.ProjectId == projectId &&
                             ar.EventType == "project.news_published" &&
                             ar.PayloadJson != null)
                .ToListAsync(ct))
            .Where(ar => ar.PayloadJson?.Contains(newsId.ToString(), StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (activityRecords.Count > 0)
        {
            _dbContext.ActivityRecords.RemoveRange(activityRecords);
        }
    }

    /// <summary>True when the user owns the project.</summary>
    private async Task<bool> IsProjectOwnerAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        return project?.OwnerId == userId;
    }
    /// <summary>True when the user has an active team membership row.</summary>
    private async Task<bool> IsProjectMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.TeamMembers.AnyAsync(tm => tm.ProjectId == projectId && tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value, ct);
    }
    /// <summary>True when the user follows the project.</summary>
    private async Task<bool> IsProjectSubscriberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.ProjectSubscriptions.AnyAsync(ps => ps.ProjectId == projectId && ps.UserId == userId, ct);
    }
    /// <summary>True for project owners or active team members.</summary>
    private async Task<bool> HasProjectAccessAsync(Guid projectId, Guid userId)
    {
        var isOwner = await IsProjectOwnerAsync(projectId, userId);
        if (isOwner) return true;
        return await IsProjectMemberAsync(projectId, userId);
    }

    /// <summary>Builds <see cref="NewsViewerContext"/> from membership, subscription, and admin flags.</summary>
    private async Task<NewsViewerContext> BuildViewerContextAsync(Guid projectId, Guid? requesterId, bool isPrivileged)
    {
        bool hasAccess = requesterId.HasValue && await HasProjectAccessAsync(projectId, requesterId.Value);
        bool isSubscriber = requesterId.HasValue && await IsProjectSubscriberAsync(projectId, requesterId.Value);

        return new NewsViewerContext(
            HasProjectAccess: hasAccess,
            IsSubscriber: isSubscriber,
            IsPrivilegedViewer: isPrivileged);
    }

    /// <inheritdoc />
    public async Task<NewsResult<object>> GetProjectNewsAsync(GetProjectNewsQuery query, CancellationToken ct = default)
    {
        var (projectId, visibility, page, pageSize, requesterId, isPrivileged) = query;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NewsResult<object>.Failure("Project not found", 404);

        var viewerContext = await BuildViewerContextAsync(projectId, requesterId, isPrivileged);

        var normalizedVisibility = ProjectNewsVisibilityHelper.NormalizeVisibility(visibility);
        var accessCheck = ValidateNewsAccess(normalizedVisibility, viewerContext, requesterId.HasValue);
        if (!accessCheck.IsSuccess)
            return NewsResult<object>.Failure(accessCheck.ErrorMessage!, accessCheck.StatusCode);

        var postsQuery = _dbContext.ProjectNewsPosts
            .AsNoTracking()
            .Where(pn => pn.ProjectId == projectId);

        postsQuery = ProjectNewsVisibilityHelper.ApplyVisibilityQueryFilter(postsQuery, viewerContext, normalizedVisibility);

        var total = await postsQuery.CountAsync(ct);
        var posts = await postsQuery
            .OrderByDescending(pn => pn.IsPinned)
            .ThenByDescending(pn => pn.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var (authors, files) = await LoadBatchDataAsync(projectId, posts);

        var response = posts
            .Select(post => MapPostToBatchResponse(post, projectId, authors, files))
            .ToList();

        return NewsResult<object>.Success(new
        {
            Data = response,
            Pagination = BuildPaginationInfo(page, pageSize, total)
        });
    }

    /// <inheritdoc />
    public async Task<NewsResult<object>> GetProjectNewsPostAsync(Guid projectId, Guid newsId, Guid? requesterId, bool isPrivileged, CancellationToken ct = default)
    {
         var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NewsResult<object>.Failure("Project not found", 404);

        var post = await _dbContext.ProjectNewsPosts.FindAsync(new object[] { newsId }, ct);
        if (post == null || post.ProjectId != projectId) return NewsResult<object>.Failure("News item not found", 404);

        var viewerContext = await BuildViewerContextAsync(projectId, requesterId, isPrivileged);

        if (!ProjectNewsVisibilityHelper.CanViewNewsPost(post.Visibility, viewerContext))
            return NewsResult<object>.Failure("Forbidden", 403);

        var response = await MapToResponseAsync(post);
        return NewsResult<object>.Success(response);
    }

    /// <inheritdoc />
    public async Task<NewsResult<ProjectNewsResponse>> CreateProjectNewsAsync(CreateProjectNewsCommand command, CancellationToken ct = default)
    {
        var (projectId, request, userId, isPrivileged) = command;
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NewsResult<ProjectNewsResponse>.Failure("Project not found", 404);

        var authContext = new NewsAuthoringContext(projectId, userId, project.OwnerId, isPrivileged);
        if (!await CanPublishNewsAsync(authContext))
            return NewsResult<ProjectNewsResponse>.Failure("Only project owner or members with news permission can post news", 403);

        var normalizedVisibility = ProjectNewsVisibilityHelper.NormalizeVisibility(request.Visibility) ?? "public";
        if (!ProjectNewsVisibilityHelper.IsVisibilityValid(normalizedVisibility))
            return NewsResult<ProjectNewsResponse>.Failure($"Visibility must be one of: {ProjectNewsVisibilityHelper.GetAllowedVisibilitiesString()}", 400);

        var attachments = ProjectNewsAttachmentHelper.MergeAttachments(request.AttachmentIds, request.AttachmentUrls);
        var attachmentError = await ProjectNewsAttachmentHelper.ValidateAttachmentsAsync(_dbContext, projectId, attachments);
        if (attachmentError != null) return NewsResult<ProjectNewsResponse>.Failure(attachmentError, 400);

        var post = new ProjectNewsPost
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AuthorId = userId,
            Title = SecurityHelpers.SanitizeHtml(request.Title).Trim(),
            Content = SecurityHelpers.SanitizeHtml(request.Content).Trim(),
            Visibility = normalizedVisibility,
            IsPinned = request.IsPinned,
            AttachmentsJson = ProjectNewsAttachmentHelper.SerializeAttachments(attachments),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.ProjectNewsPosts.Add(post);
        await _dbContext.SaveChangesAsync(ct);

        await NotifySubscribersAsync(projectId, userId, project.Title, post.Title);
        await _auditService.LogActionAsync(userId, "ProjectNewsController.CreateProjectNews", "ProjectNewsPost", post.Id,
            $"Created news post: {post.Title}");

        if (post.Visibility == "public")
            await LogPublicNewsActivityAsync(userId, post, attachments);

        var response = await MapToResponseAsync(post);
        return NewsResult<ProjectNewsResponse>.Created(response);
    }

    /// <inheritdoc />
    public async Task<NewsResult<object>> UpdateProjectNewsAsync(UpdateProjectNewsCommand command, CancellationToken ct = default)
    {
        var (projectId, newsId, request, userId, isPrivileged) = command;
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NewsResult<object>.Failure("Project not found", 404);

        var post = await _dbContext.ProjectNewsPosts.FindAsync(new object[] { newsId }, ct);
        if (post == null || post.ProjectId != projectId) return NewsResult<object>.Failure("News item not found", 404);

        var authContext = new NewsAuthoringContext(projectId, userId, project.OwnerId, isPrivileged);
        if (!await CanEditNewsAsync(authContext, post.AuthorId))
             return NewsResult<object>.Failure("Only the author, owner, or members with news permission can edit news", 403);

        // Apply updates
        ApplyBasicNewsUpdates(post, request);

        var visibilityResult = UpdateNewsVisibility(post, request.Visibility);
        if (!visibilityResult.IsSuccess)
            return NewsResult<object>.Failure(visibilityResult.ErrorMessage!, visibilityResult.StatusCode);

        var attachmentResult = await UpdateNewsAttachmentsAsync(post, projectId, request.AttachmentIds, request.AttachmentUrls);
        if (!attachmentResult.IsSuccess)
            return NewsResult<object>.Failure(attachmentResult.ErrorMessage!, attachmentResult.StatusCode);

        post.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "ProjectNewsController.UpdateProjectNews", "ProjectNewsPost", post.Id,
            $"Updated news post: {post.Title}");

        var updatedAttachments = ProjectNewsAttachmentHelper.DeserializeAttachments(post.AttachmentsJson).ToList();
        await SyncNewsActivityRecordsAsync(post, updatedAttachments);

        return NewsResult<object>.Success(await MapToResponseAsync(post));
    }

    /// <summary>Applies title, content, and pin changes with HTML sanitization.</summary>
    private static void ApplyBasicNewsUpdates(ProjectNewsPost post, UpdateNewsPostRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Title))
            post.Title = SecurityHelpers.SanitizeHtml(request.Title).Trim();

        if (!string.IsNullOrWhiteSpace(request.Content))
            post.Content = SecurityHelpers.SanitizeHtml(request.Content).Trim();

        if (request.IsPinned.HasValue)
            post.IsPinned = request.IsPinned.Value;
    }

    /// <summary>Validates and normalizes visibility tier changes on update.</summary>
    private static NewsResult UpdateNewsVisibility(ProjectNewsPost post, string? visibility)
    {
        if (string.IsNullOrWhiteSpace(visibility))
            return NewsResult.Success();

        var normalizedVisibility = ProjectNewsVisibilityHelper.NormalizeVisibility(visibility);
        if (normalizedVisibility == null || !ProjectNewsVisibilityHelper.IsVisibilityValid(normalizedVisibility))
            return NewsResult.Failure($"Visibility must be one of: {ProjectNewsVisibilityHelper.GetAllowedVisibilitiesString()}", 400);

        post.Visibility = normalizedVisibility;
        return NewsResult.Success();
    }

    /// <summary>Replaces attachments when ids or urls are supplied in the update request.</summary>
    private async Task<NewsResult> UpdateNewsAttachmentsAsync(ProjectNewsPost post, Guid projectId, List<Guid>? attachmentIds, List<string>? attachmentUrls)
    {
        if (attachmentIds == null && attachmentUrls == null)
            return NewsResult.Success();

        var attachments = ProjectNewsAttachmentHelper.MergeAttachments(attachmentIds, attachmentUrls);
        var attachmentError = await ProjectNewsAttachmentHelper.ValidateAttachmentsAsync(_dbContext, projectId, attachments);
        if (attachmentError != null)
            return NewsResult.Failure(attachmentError, 400);

        post.AttachmentsJson = ProjectNewsAttachmentHelper.SerializeAttachments(attachments);
        return NewsResult.Success();
    }

    /// <inheritdoc />
    public async Task<NewsResult> DeleteProjectNewsAsync(DeleteProjectNewsCommand command, CancellationToken ct = default)
    {
        var (projectId, newsId, userId, isPrivileged) = command;
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NewsResult.Failure("Project not found", 404);

        var post = await _dbContext.ProjectNewsPosts.FindAsync(new object[] { newsId }, ct);
        if (post == null || post.ProjectId != projectId) return NewsResult.Failure("News item not found", 404);

        var authContext = new NewsAuthoringContext(projectId, userId, project.OwnerId, isPrivileged);
        if (!await CanEditNewsAsync(authContext, post.AuthorId))
             return NewsResult.Failure("Only author, owner, or members with news permission can delete this news post", 403);

        await CleanupNewsActivityRecordsAsync(projectId, newsId);

        _dbContext.ProjectNewsPosts.Remove(post);
        await _dbContext.SaveChangesAsync(ct);

        return NewsResult.Success();
    }

    /// <summary>Ensures requested visibility filter is allowed for the viewer's access level.</summary>
    private static NewsResult ValidateNewsAccess(string? normalizedVisibility, NewsViewerContext viewerContext, bool isAuthenticated)
    {
        if (normalizedVisibility != null && !ProjectNewsVisibilityHelper.IsVisibilityValid(normalizedVisibility))
            return NewsResult.Failure($"Visibility must be one of: {ProjectNewsVisibilityHelper.GetAllowedVisibilitiesString()}", 400);

        var allowedVisibilities = ProjectNewsVisibilityHelper.DetermineAllowedVisibilities(viewerContext);
        if (normalizedVisibility != null && !allowedVisibilities.Contains(normalizedVisibility))
        {
            return isAuthenticated ? NewsResult.Failure("Forbidden", 403) : NewsResult.Failure("Unauthorized", 401);
        }

        return NewsResult.Success();
    }
}
