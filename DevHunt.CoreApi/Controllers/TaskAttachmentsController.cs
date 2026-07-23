using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Constants;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing task file attachments.
/// </summary>
/// <remarks>
/// Supports two attachment modes:
/// - Direct upload: Upload a new file to the task
/// - Link existing: Reference an existing ProjectFile
///
/// Routes: api/projects/{projectId}/tasks/{taskId}/attachments
/// </remarks>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/attachments")]
[Authorize]
public class TaskAttachmentsController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;
    private readonly IObjectStorageService _objectStorage;
    private readonly IActivityLogService _activityLogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskAttachmentsController"/> class.
    /// </summary>
    /// <param name="db">Database context used to read projects, tasks, project files, attachments, and memberships.</param>
    /// <param name="cache">Cache service used to invalidate affected project entries after attachment changes.</param>
    /// <param name="objectStorage">Object storage service used for direct attachment uploads, downloads, and deletes.</param>
    /// <param name="activityLogService">Activity logger used to record attachment lifecycle events.</param>
    public TaskAttachmentsController(
        DevHuntDbContext db,
        ICacheService cache,
        IObjectStorageService objectStorage,
        IActivityLogService activityLogService)
    {
        _db = db;
        _cache = cache;
        _objectStorage = objectStorage;
        _activityLogService = activityLogService;
    }

    /// <summary>Task attachment response item.</summary>
    /// <param name="Id">Attachment identifier.</param>
    /// <param name="TaskId">Task identifier.</param>
    /// <param name="ProjectFileId">Linked project file ID (optional).</param>
    /// <param name="FileName">File name.</param>
    /// <param name="ContentType">Content type.</param>
    /// <param name="FileSize">File size in bytes.</param>
    /// <param name="AttachedByUserId">User who attached the file.</param>
    /// <param name="AttachedByUserName">Attacher display name.</param>
    /// <param name="AttachedAt">Attachment timestamp.</param>
    public record AttachmentDto(
        Guid Id,
        Guid TaskId,
        Guid? ProjectFileId,
        string FileName,
        string? ContentType,
        long? FileSize,
        Guid AttachedByUserId,
        string? AttachedByUserName,
        DateTime AttachedAt);

    /// <summary>Request to link an existing project file to a task.</summary>
    /// <param name="ProjectFileId">Project file identifier.</param>
    public record LinkProjectFileRequest(Guid ProjectFileId);

    /// <summary>
    /// Reads the authenticated user's identifier from claims and fails fast when authentication middleware did not provide it.
    /// </summary>
    /// <returns>The current user's ID.</returns>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Lists direct uploads and linked project files attached to a non-deleted task.
    /// </summary>
    /// <param name="projectId">Project containing the task.</param>
    /// <param name="taskId">Task whose attachments should be listed.</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>
    /// Attachments ordered newest first for owners and active team members, 403 when access is denied,
    /// or 404 when the project or task is missing.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetAttachments(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await HasProjectAccessAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can view attachments" });
        }

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (task == null) return NotFound("Task not found");

        var attachments = await _db.TaskAttachments
            .Where(a => a.TaskId == taskId)
            .Include(a => a.ProjectFile)
            .Include(a => a.AttachedByUser)
            .OrderByDescending(a => a.AttachedAt)
            .Select(a => new AttachmentDto(
                a.Id,
                a.TaskId,
                a.ProjectFileId,
                a.ProjectFile != null ? a.ProjectFile.FileName : a.FileName ?? "Unknown",
                a.ProjectFile != null ? a.ProjectFile.ContentType : a.ContentType,
                a.ProjectFile != null ? a.ProjectFile.FileSize : a.FileSize,
                a.AttachedByUserId,
                a.AttachedByUser != null ? a.AttachedByUser.FullName : null,
                a.AttachedAt))
            .ToListAsync(ct);

        return Ok(attachments);
    }

    /// <summary>
    /// Uploads a new task attachment to object storage and records the attachment metadata.
    /// </summary>
    /// <param name="projectId">Project containing the task.</param>
    /// <param name="taskId">Task receiving the uploaded file.</param>
    /// <param name="file">Form file to validate, store, and attach.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// The created attachment; returns 400 for missing, oversized, or blocked files, 403 when the user cannot manage attachments,
    /// or 404 when the project or task is missing. Metadata is rolled back if object storage upload fails.
    /// </returns>
    [HttpPost]
    [RequestSizeLimit(50_000_000)] // 50 MB limit for task attachments
    public async Task<IActionResult> UploadAttachment(Guid projectId, Guid taskId, IFormFile file, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        if (file == null || file.Length == 0)
            return BadRequest("File is required");

        if (file.Length > 50_000_000)
            return BadRequest("File size exceeds 50 MB limit");

        // U-12: Block files with dangerous extensions anywhere in the filename
        if (SecurityHelpers.HasDangerousExtension(file.FileName))
            return BadRequest("File contains a blocked extension");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await CanManageAttachmentsAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can add attachments" });
        }

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (task == null) return NotFound("Task not found");

        var attachmentId = Guid.NewGuid();
        var extension = Path.GetExtension(file.FileName);
        var storageKey = $"projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}{extension}";

        var attachment = new TaskAttachment
        {
            Id = attachmentId,
            TaskId = taskId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            StorageKey = storageKey,
            AttachedByUserId = userId,
            AttachedAt = DateTime.UtcNow
        };

        _db.TaskAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        try
        {
            using var fileStream = file.OpenReadStream();
            await _objectStorage.UploadAsync(
                ObjectStorageBuckets.ProjectFiles,
                storageKey,
                fileStream,
                file.ContentType);
        }
        catch
        {
            _db.TaskAttachments.Remove(attachment);
            await _db.SaveChangesAsync(ct);
            throw;
        }
        await InvalidateProjectCacheAsync(projectId, ct);

        var user = await _db.Users.FindAsync(new object[] { userId }, ct);

        await _activityLogService.LogProjectEventAsync(
            projectId,
            userId,
            "task.attachment_added",
            $"Added attachment to task: {task.Title}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { taskId, attachmentId, fileName = file.FileName });

        return Ok(new AttachmentDto(
            attachment.Id,
            attachment.TaskId,
            null,
            attachment.FileName!,
            attachment.ContentType,
            attachment.FileSize,
            attachment.AttachedByUserId,
            user?.FullName,
            attachment.AttachedAt));
    }

    /// <summary>
    /// Links an existing project file to a task without duplicating the stored object.
    /// </summary>
    /// <param name="projectId">Project containing both the task and project file.</param>
    /// <param name="taskId">Task receiving the project-file link.</param>
    /// <param name="req">Project file to attach.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// The created attachment link; returns 400 when the file is already attached, 403 when the user cannot manage attachments,
    /// or 404 when the project, task, or project file is missing.
    /// </returns>
    [HttpPost("link")]
    public async Task<IActionResult> LinkProjectFile(Guid projectId, Guid taskId, [FromBody] LinkProjectFileRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await CanManageAttachmentsAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can add attachments" });
        }

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (task == null) return NotFound("Task not found");

        var projectFile = await _db.ProjectFiles.FirstOrDefaultAsync(f => f.Id == req.ProjectFileId && f.ProjectId == projectId && f.DeletedAt == null, ct);
        if (projectFile == null) return NotFound("Project file not found");

        // Check if already attached
        var existingLink = await _db.TaskAttachments.AnyAsync(a => a.TaskId == taskId && a.ProjectFileId == req.ProjectFileId, ct);
        if (existingLink)
        {
            return BadRequest("This file is already attached to the task");
        }

        var attachment = new TaskAttachment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ProjectFileId = req.ProjectFileId,
            AttachedByUserId = userId,
            AttachedAt = DateTime.UtcNow
        };

        _db.TaskAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId, ct);

        var user = await _db.Users.FindAsync(new object[] { userId }, ct);

        await _activityLogService.LogProjectEventAsync(
            projectId,
            userId,
            "task.file_linked",
            $"Linked file to task: {task.Title}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { taskId, attachmentId = attachment.Id, projectFileId = req.ProjectFileId, fileName = projectFile.FileName });

        return Ok(new AttachmentDto(
            attachment.Id,
            attachment.TaskId,
            attachment.ProjectFileId,
            projectFile.FileName,
            projectFile.ContentType,
            projectFile.FileSize,
            attachment.AttachedByUserId,
            user?.FullName,
            attachment.AttachedAt));
    }

    /// <summary>
    /// Downloads a direct attachment or linked project file after project and task access checks.
    /// </summary>
    /// <param name="projectId">Project containing the task.</param>
    /// <param name="taskId">Task containing the attachment.</param>
    /// <param name="attachmentId">Attachment to download.</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>
    /// The attachment stream with an inline disposition for images and an attachment disposition for other files;
    /// returns 403 for denied access or 404 when the project, task, attachment, or stored object is missing.
    /// </returns>
    [HttpGet("{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid projectId, Guid taskId, Guid attachmentId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await HasProjectAccessAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can download attachments" });
        }

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (task == null) return NotFound("Task not found");

        var attachment = await _db.TaskAttachments
            .Include(a => a.ProjectFile)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskId == taskId, ct);

        if (attachment == null) return NotFound("Attachment not found");

        string storageKey;
        string fileName;
        string contentType;

        if (attachment.ProjectFileId.HasValue && attachment.ProjectFile != null)
        {
            // Linked project file
            storageKey = attachment.ProjectFile.StorageKey;
            fileName = attachment.ProjectFile.FileName;
            contentType = attachment.ProjectFile.ContentType ?? "application/octet-stream";
        }
        else
        {
            // Direct upload
            storageKey = attachment.StorageKey ?? throw new InvalidOperationException("Attachment has no storage key");
            fileName = attachment.FileName ?? "attachment";
            contentType = attachment.ContentType ?? "application/octet-stream";
        }

        var fileStream = await _objectStorage.DownloadAsync(ObjectStorageBuckets.ProjectFiles, storageKey);
        if (fileStream == null) return NotFound("File not found in storage");

        // U-02: Sanitize filename to prevent header injection
        var safeFileName = fileName
            .Replace("\"", "")
            .Replace("\r", "")
            .Replace("\n", "");
        var encodedFileName = Uri.EscapeDataString(safeFileName);
        var isImage = contentType.StartsWith("image/");
        var disposition = isImage ? "inline" : "attachment";
        Response.Headers["Content-Disposition"] =
            $"{disposition}; filename=\"{safeFileName}\"; filename*=UTF-8''{encodedFileName}";

        return File(fileStream, contentType, fileName);
    }

    /// <summary>
    /// Removes an attachment record and deletes its stored object when it was uploaded directly to the task.
    /// </summary>
    /// <param name="projectId">Project containing the task.</param>
    /// <param name="taskId">Task containing the attachment.</param>
    /// <param name="attachmentId">Attachment to remove.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// 204 after deletion, 403 when the user cannot manage attachments, or 404 when the project, task, or attachment is missing.
    /// Linked project files are detached but not removed from object storage.
    /// </returns>
    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid projectId, Guid taskId, Guid attachmentId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await CanManageAttachmentsAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can delete attachments" });
        }

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (task == null) return NotFound("Task not found");

        var attachment = await _db.TaskAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskId == taskId, ct);
        if (attachment == null) return NotFound("Attachment not found");

        var fileName = attachment.FileName ?? "file";

        // Only delete from storage if it's a direct upload (not a linked project file)
        if (!attachment.ProjectFileId.HasValue && !string.IsNullOrEmpty(attachment.StorageKey))
        {
            await _objectStorage.DeleteAsync(ObjectStorageBuckets.ProjectFiles, attachment.StorageKey);
        }

        _db.TaskAttachments.Remove(attachment);
        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId, ct);

        await _activityLogService.LogProjectEventAsync(
            projectId,
            userId,
            "task.attachment_removed",
            $"Removed attachment from task: {task.Title}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { taskId, attachmentId, fileName });

        return NoContent();
    }

    // U-06: Use TeamMemberStatus constant
    /// <summary>
    /// Checks whether the user is the project owner or an active team member who can view attachments.
    /// </summary>
    /// <param name="projectId">Project being accessed.</param>
    /// <param name="userId">User requesting access.</param>
    /// <param name="ownerId">Project owner's user ID.</param>
    /// <param name="ct">Cancellation token for the membership lookup.</param>
    /// <returns><see langword="true"/> when the user can view or download task attachments.</returns>
    private async Task<bool> HasProjectAccessAsync(Guid projectId, Guid userId, Guid ownerId, CancellationToken ct = default)
    {
        if (ownerId == userId) return true;
        return await _db.TeamMembers.AnyAsync(tm =>
            tm.ProjectId == projectId &&
            tm.UserId == userId &&
            tm.Status == TeamMemberStatus.Active, ct);
    }

    // U-05: Renamed + checks CanManageFiles/CanManageTasks for granular permissions
    /// <summary>
    /// Checks whether the user is the project owner or an active team member with file or task management permission.
    /// </summary>
    /// <param name="projectId">Project whose attachments may be changed.</param>
    /// <param name="userId">User requesting the change.</param>
    /// <param name="ownerId">Project owner's user ID.</param>
    /// <param name="ct">Cancellation token for the membership lookup.</param>
    /// <returns><see langword="true"/> when the user can upload, link, or delete task attachments.</returns>
    private async Task<bool> CanManageAttachmentsAsync(Guid projectId, Guid userId, Guid ownerId, CancellationToken ct = default)
    {
        if (ownerId == userId) return true;
        return await _db.TeamMembers.AnyAsync(tm =>
            tm.ProjectId == projectId &&
            tm.UserId == userId &&
            tm.Status == TeamMemberStatus.Active &&
            (tm.CanManageFiles || tm.CanManageTasks), ct);
    }

    /// <summary>
    /// Removes cached project details and project-list entries affected by attachment changes.
    /// </summary>
    /// <param name="projectId">Project whose cache entries should be removed.</param>
    /// <param name="ct">Cancellation token for cache calls.</param>
    private async Task InvalidateProjectCacheAsync(Guid projectId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync($"project:{projectId}");
        await _cache.RemoveByPatternAsync("projects:*");
    }
}
