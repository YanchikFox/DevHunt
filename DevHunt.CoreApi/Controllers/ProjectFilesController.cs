using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.IO;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Constants;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using ProjectVisibility = DevHunt.CoreApi.Models.ProjectVisibility;
using TeamMemberStatus = DevHunt.CoreApi.Models.TeamMemberStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Project files, including gallery.
/// Routes: api/projects/{projectId}/files/* and /gallery
/// </summary>
[ApiController]
[Route("api/projects")]
public class ProjectFilesController : BaseProjectController
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectFilesController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="auditService">The audit service.</param>
    /// <param name="notificationService">The notification service client.</param>
    /// <param name="eventBus">The event bus service.</param>
    /// <param name="cache">The cache service.</param>
    public ProjectFilesController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        IEventBusService eventBus,
        ICacheService cache)
        : base(dbContext, auditService, notificationService, eventBus, cache)
    {
    }

    /// <summary>Request to upload a file to a project.</summary>
    /// <param name="Description">Optional file description.</param>
    /// <param name="Category">Optional category label.</param>
    public record UploadFileRequest(
        [MaxLength(1000)] string? Description,
        [MaxLength(50)] string? Category);

    /// <summary>Shared query parameters for paginated file listings.</summary>
    public class FileListQuery
    {
        /// <summary>
        /// Filters files by category when supplied.
        /// </summary>
        public string? Category { get; set; }
        /// <summary>
        /// One-based page number for file listings.
        /// </summary>
        public int Page { get; set; } = 1;
        /// <summary>
        /// Number of files requested per page before controller clamping.
        /// </summary>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>Coarse file access tier resolved before per-action permission checks.</summary>
    private enum FileAccessLevel { None, Subscriber, Member, OwnerOrPrivileged }

    /// <summary>Resolved file access tier plus team permission flags used by file and gallery endpoints.</summary>
    private record FileAccessResult(FileAccessLevel Level, bool CanManageFiles, bool CanManageGallery)
    {
        /// <summary>
        /// Indicates whether the access level or explicit team permissions allow uploading files.
        /// </summary>
        public bool CanUploadFiles => Level == FileAccessLevel.OwnerOrPrivileged || CanManageFiles || CanManageGallery;
    }

    /// <summary>
    /// Resolves the caller's file access level from project ownership, privileged roles, team permissions, or subscription status.
    /// </summary>
    private async Task<FileAccessResult> GetAccessAsync(Guid projectId, Guid? userId, CancellationToken ct = default)
    {
        if (!userId.HasValue) return new(FileAccessLevel.None, false, false);

        var project = await _dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return new(FileAccessLevel.None, false, false);

        if (SecurityHelpers.IsSuperAdmin(User) || project.OwnerId == userId.Value)
            return new(FileAccessLevel.OwnerOrPrivileged, true, true);

        var teamMember = await _dbContext.TeamMembers.AsNoTracking()
            .FirstOrDefaultAsync(tm => tm.ProjectId == projectId && tm.UserId == userId.Value && tm.Status == TeamMemberStatus.Active, ct);
        if (teamMember != null)
            return new(FileAccessLevel.Member, teamMember.CanManageFiles, teamMember.CanManageGallery);

        var isSubscriber = await _dbContext.ProjectSubscriptions.AsNoTracking()
            .AnyAsync(ps => ps.ProjectId == projectId && ps.UserId == userId.Value, ct);
        return new(isSubscriber ? FileAccessLevel.Subscriber : FileAccessLevel.None, false, false);
    }

    /// <summary>
    /// Maps a caller's file access level to the project file visibility values they may read.
    /// </summary>
    private static List<string> BuildAllowedVisibilities(FileAccessLevel level) => level switch
    {
        FileAccessLevel.OwnerOrPrivileged or FileAccessLevel.Member =>
            new() { ProjectVisibility.Public, ProjectVisibility.Subscribers, ProjectVisibility.Members, ProjectVisibility.Private },
        FileAccessLevel.Subscriber =>
            new() { ProjectVisibility.Public, ProjectVisibility.Subscribers },
        _ => new() { ProjectVisibility.Public }
    };

    /// <summary>
    /// Normalizes invalid pagination values and caps page size at 100.
    /// </summary>
    private static void ClampPagination(FileListQuery q)
    {
        if (q.Page < 1) q.Page = 1;
        if (q.PageSize < 1) q.PageSize = 20;
        if (q.PageSize > 100) q.PageSize = 100;
    }

    /// <summary>
    /// Builds the shared paginated response envelope used by file and gallery listings.
    /// </summary>
    private static object BuildPaginatedResponse(object data, int page, int pageSize, int total) => new
    {
        Data = data,
        Pagination = new
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            HasNext = page * pageSize < total,
            HasPrevious = page > 1
        }
    };

    /// <summary>
    /// Validates that an upload exists, fits the size limit, and does not use a blocked extension.
    /// </summary>
    private static IActionResult? ValidateUploadFile(IFormFile? file)
    {
        if (file == null || file.Length == 0) return new BadRequestObjectResult("File is required");
        if (file.Length > 100_000_000) return new BadRequestObjectResult("File size exceeds 100 MB limit");
        if (SecurityHelpers.HasDangerousExtension(file.FileName)) return new BadRequestObjectResult("File contains a blocked extension");
        return null;
    }

    /// <summary>
    /// Requires authentication before reading files or gallery items from non-public projects.
    /// </summary>
    private static IActionResult? RequireAuthForNonPublicProject(Guid? userId, Project project, string message)
    {
        if (!userId.HasValue && project.Visibility != ProjectVisibility.Public.Value)
            return new UnauthorizedObjectResult(message);
        return null;
    }

    private static readonly List<string> _galleryCategories = new() { "image", "photo", "screenshot", "design", "video", "gallery" };

    private static readonly System.Linq.Expressions.Expression<Func<ProjectFile, bool>> _isGalleryMedia = pf =>
        (pf.Category != null && _galleryCategories.Contains(pf.Category.ToLower())) ||
        (pf.ContentType != null && (pf.ContentType.StartsWith("image/") || pf.ContentType.StartsWith("video/")));

    /// <summary>
    /// Applies an exact normalized category filter to a gallery media query when requested.
    /// </summary>
    private static IQueryable<ProjectFile> ApplyGalleryCategoryFilter(IQueryable<ProjectFile> query, string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return query;
        var normalized = category.Trim().ToLowerInvariant();
        return query.Where(pf => pf.Category != null && pf.Category.ToLower() == normalized);
    }

    /// <summary>
    /// Builds the visible gallery media query for images, videos, and gallery-tagged files in a project.
    /// </summary>
    private IQueryable<ProjectFile> BuildGalleryMediaQuery(Guid projectId, List<string> allowedVis, string? category)
    {
        var query = _dbContext.ProjectFiles
            .AsNoTracking()
            .Include(pf => pf.UploadedBy)
            .Where(pf => pf.ProjectId == projectId && pf.DeletedAt == null && allowedVis.Contains(pf.Visibility))
            .Where(_isGalleryMedia);

        return ApplyGalleryCategoryFilter(query, category);
    }

    /// <summary>
    /// Uploads a persisted file record to object storage and removes the database row if storage upload fails.
    /// </summary>
    private async Task UploadToStorageAsync(string storageKey, IFormFile file, ProjectFile rollbackEntity, CancellationToken ct)
    {
        var objectStorage = HttpContext.RequestServices.GetRequiredService<IObjectStorageService>();
        try
        {
            using var fileStream = file.OpenReadStream();
            await objectStorage.UploadAsync(ObjectStorageBuckets.ProjectFiles, storageKey, fileStream, file.ContentType);
        }
        catch
        {
            _dbContext.ProjectFiles.Remove(rollbackEntity);
            await _dbContext.SaveChangesAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Verifies the caller can read a non-public file based on project access and file visibility.
    /// </summary>
    private async Task<IActionResult?> EnsureFileVisibilityAccessAsync(ProjectFile projectFile, Guid projectId, Guid? userId, CancellationToken ct)
    {
        if (projectFile.Visibility == ProjectVisibility.Public.Value) return null;
        if (!userId.HasValue) return Unauthorized("Authentication required for non-public files");

        var access = await GetAccessAsync(projectId, userId, ct);
        var allowedVis = BuildAllowedVisibilities(access.Level);
        return !allowedVis.Contains(projectFile.Visibility) ? Forbid() : null;
    }

    /// <summary>
    /// Removes header-breaking characters from the original file name before writing download headers.
    /// </summary>
    private static string SanitizeFileName(string fileName) =>
        fileName.Replace("\"", "").Replace("\r", "").Replace("\n", "");

    /// <summary>
    /// Builds a file response with a safe content disposition, using inline display for images and attachment for other files.
    /// </summary>
    private IActionResult BuildFileDownloadResponse(Stream fileStream, ProjectFile projectFile)
    {
        var safeFileName = SanitizeFileName(projectFile.FileName);
        var encodedFileName = Uri.EscapeDataString(safeFileName);
        var isImage = projectFile.ContentType?.StartsWith("image/") == true;
        var disposition = isImage ? "inline" : "attachment";
        Response.Headers["Content-Disposition"] =
            $"{disposition}; filename=\"{safeFileName}\"; filename*=UTF-8''{encodedFileName}";

        return File(fileStream, projectFile.ContentType ?? "application/octet-stream", projectFile.FileName);
    }

    /// <summary>
    /// Checks whether the caller uploaded the file, owns the project, or has an administrative role.
    /// </summary>
    private bool CanDeleteFile(ProjectFile file, Project project, Guid userId) =>
        file.UploadedById == userId || project.OwnerId == userId || SecurityHelpers.IsAdminOrCurator(User);

    /// <summary>
    /// Gets paginated project files visible to the authenticated caller, optionally filtered by category.
    /// </summary>
    [HttpGet("{projectId:guid}/files")]
    [Authorize]
    public async Task<IActionResult> GetProjectFiles(
        Guid projectId,
        [FromQuery] FileListQuery query, CancellationToken ct = default)
    {
        ClampPagination(query);

        var userId = SecurityHelpers.GetUserId(User);
        var project = await _dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        var access = await GetAccessAsync(projectId, userId);
        var allowedVis = BuildAllowedVisibilities(access.Level);

        var baseQuery = _dbContext.ProjectFiles
            .AsNoTracking()
            .Include(pf => pf.UploadedBy)
            .Where(pf => pf.ProjectId == projectId && pf.DeletedAt == null && allowedVis.Contains(pf.Visibility));

        if (!string.IsNullOrWhiteSpace(query.Category))
            baseQuery = baseQuery.Where(pf => pf.Category == query.Category);

        var total = await baseQuery.CountAsync(ct);

        var files = await baseQuery
            .OrderByDescending(pf => pf.UploadedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(pf => new
            {
                pf.Id,
                pf.FileName,
                pf.ContentType,
                pf.FileSize,
                pf.Description,
                pf.Category,
                pf.Visibility,
                pf.UploadedAt,
                pf.DownloadCount,
                UploadedBy = new
                {
                    pf.UploadedBy.Id,
                    pf.UploadedBy.FullName,
                    pf.UploadedBy.AvatarUrl
                }
            })
            .ToListAsync(ct);

        return Ok(BuildPaginatedResponse(files, query.Page, query.PageSize, total));
    }

    /// <summary>
    /// Gets paginated public gallery media, requiring authentication for non-public projects and enforcing file visibility.
    /// </summary>
    [HttpGet("{projectId:guid}/gallery")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProjectGallery(
        Guid projectId,
        [FromQuery] FileListQuery query, CancellationToken ct = default)
    {
        ClampPagination(query);

        var userId = SecurityHelpers.GetUserId(User);
        var project = await _dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        var authError = RequireAuthForNonPublicProject(userId, project, "Authentication required to access this project's gallery");
        if (authError != null) return authError;

        var access = await GetAccessAsync(projectId, userId);
        var allowedVis = BuildAllowedVisibilities(access.Level);

        var galleryQuery = BuildGalleryMediaQuery(projectId, allowedVis, query.Category);
        var total = await galleryQuery.CountAsync(ct);

        var media = await galleryQuery
            .OrderByDescending(pf => pf.UploadedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(pf => new
            {
                pf.Id,
                pf.FileName,
                pf.ContentType,
                pf.FileSize,
                pf.Category,
                pf.Visibility,
                pf.UploadedAt,
                pf.DownloadCount,
                DownloadUrl = $"/api/projects/{projectId}/files/{pf.Id}",
                UploadedBy = new
                {
                    pf.UploadedBy.Id,
                    pf.UploadedBy.FullName,
                    pf.UploadedBy.AvatarUrl
                }
            })
            .ToListAsync(ct);

        return Ok(BuildPaginatedResponse(media, query.Page, query.PageSize, total));
    }

    /// <summary>
    /// Uploads a project file when the caller has upload permission, persists metadata, stores content, and logs activity.
    /// </summary>
    [HttpPost("{projectId:guid}/files")]
    [Authorize]
    [RequestSizeLimit(100_000_000)] // 100 MB limit
    public async Task<IActionResult> UploadProjectFile(
        Guid projectId,
        IFormFile file,
        [FromForm] UploadFileRequest? request = null, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var fileError = ValidateUploadFile(file);
        if (fileError != null) return fileError;

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        var access = await GetAccessAsync(projectId, userId);
        if (!access.CanUploadFiles)
            return Forbid("Not allowed to upload files");

        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(file.FileName);
        var storageKey = $"projects/{projectId}/files/{fileId}{extension}";

        var projectFile = new ProjectFile
        {
            Id = fileId,
            ProjectId = projectId,
            UploadedById = userId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            StorageKey = storageKey,
            Description = request?.Description,
            Category = request?.Category ?? "other",
            UploadedAt = DateTime.UtcNow,
            Visibility = ActivityVisibilityHelper.FromProjectVisibility(project.Visibility)
        };

        _dbContext.ProjectFiles.Add(projectFile);
        await _dbContext.SaveChangesAsync(ct);

        await UploadToStorageAsync(storageKey, file, projectFile, ct);

        await _auditService.LogActionAsync(userId, "ProjectFilesController.UploadProjectFile", "ProjectFile", fileId,
            $"Uploaded file: {file.FileName}");

        var activityLogService = HttpContext.RequestServices.GetRequiredService<IActivityLogService>();
        await activityLogService.LogProjectEventAsync(
            projectId,
            userId,
            "project.media_uploaded",
            $"Uploaded media {projectFile.FileName}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { projectFile.Id, projectFile.Category });

        return CreatedAtAction(nameof(GetProjectFiles), new { projectId }, new
        {
            projectFile.Id,
            projectFile.FileName,
            projectFile.ContentType,
            projectFile.FileSize,
            projectFile.UploadedAt
        });
    }

    /// <summary>
    /// Downloads a project file.
    /// </summary>
    /// <remarks>
    /// This endpoint streams file content from object storage to the client.
    ///
    /// Access control:
    /// - Respects file visibility settings (public, team, private)
    /// - Uses the same access logic as ListProjectFiles
    /// - Public files can be accessed without authentication
    ///
    /// Side effects:
    /// - Increments DownloadCount for analytics
    ///
    /// Response:
    /// - Sets Content-Disposition header for file download
    /// - Returns file stream with appropriate content type
    /// - Preserves original filename
    ///
    /// Only non-deleted files are accessible.
    /// </remarks>
    [HttpGet("{projectId:guid}/files/{fileId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadProjectFile(Guid projectId, Guid fileId, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        var authError = RequireAuthForNonPublicProject(userId, project, "Authentication required to access files from this project");
        if (authError != null) return authError;

        var projectFile = await _dbContext.ProjectFiles
            .FirstOrDefaultAsync(pf => pf.Id == fileId && pf.ProjectId == projectId && pf.DeletedAt == null, ct);
        if (projectFile == null) return NotFound("File not found");

        var visError = await EnsureFileVisibilityAccessAsync(projectFile, projectId, userId, ct);
        if (visError != null) return visError;

        var objectStorage = HttpContext.RequestServices.GetRequiredService<IObjectStorageService>();
        var fileStream = await objectStorage.DownloadAsync(ObjectStorageBuckets.ProjectFiles, projectFile.StorageKey);
        if (fileStream == null) return NotFound("File not found in storage");

        // U-08: Atomic increment to prevent race condition on concurrent downloads
        await _dbContext.ProjectFiles
            .Where(f => f.Id == projectFile.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.DownloadCount, f => f.DownloadCount + 1), ct);

        return BuildFileDownloadResponse(fileStream, projectFile);
    }

    /// <summary>
    /// Deletes a project file (soft-delete).
    /// </summary>
    /// <remarks>
    /// This endpoint performs a soft-delete by setting DeletedAt timestamp.
    ///
    /// Access control:
    /// - Project owner: Can delete any file
    /// - File uploader: Can delete their own files
    /// - Team members with CanManageFiles permission: Can delete files
    ///
    /// Soft-delete benefits:
    /// - Files can be restored if deleted by mistake
    /// - Storage cleanup happens asynchronously
    /// - Audit trail is preserved
    ///
    /// Only non-deleted files can be deleted (prevents double-deletion).
    /// </remarks>
    [HttpDelete("{projectId:guid}/files/{fileId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteProjectFile(Guid projectId, Guid fileId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        var projectFile = await _dbContext.ProjectFiles
            .FirstOrDefaultAsync(pf => pf.Id == fileId && pf.ProjectId == projectId && pf.DeletedAt == null, ct);
        if (projectFile == null) return NotFound("File not found");

        if (!CanDeleteFile(projectFile, project, userId))
            return Forbid("Only file uploader or project owner can delete file");

        projectFile.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "ProjectFilesController.DeleteProjectFile", "ProjectFile", fileId,
            $"Deleted file: {projectFile.FileName}");

        return NoContent();
    }
}
