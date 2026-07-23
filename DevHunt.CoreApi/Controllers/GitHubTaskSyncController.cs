using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = DevHunt.CoreApi.Models.TaskStatus;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Internal API for GitHub task synchronization (used by integration-gateway).
/// Extracted from TasksController to reduce code duplication (R12 refactoring).
/// </summary>
/// <remarks>
/// These endpoints are called by the integration-gateway service when GitHub webhooks
/// are received. They require X-Service-Name header authentication.
///
/// Routes: api/internal/github-tasks/*
/// </remarks>
[ApiController]
[Route("api/internal/github-tasks")]
[Authorize]
public class GitHubTaskSyncController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;
    private readonly IInternalServiceAuthenticator _serviceAuth;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubTaskSyncController"/> class.
    /// </summary>
    /// <param name="db">Database context used to create and update synchronized tasks.</param>
    /// <param name="cache">Cache service used to invalidate project data after task changes.</param>
    /// <param name="serviceAuth">Authenticator that validates integration-gateway internal requests.</param>
    public GitHubTaskSyncController(DevHuntDbContext db, ICacheService cache, IInternalServiceAuthenticator serviceAuth)
    {
        _db = db;
        _cache = cache;
        _serviceAuth = serviceAuth;
    }

    #region DTOs

    /// <summary>Request to link a task to a GitHub Issue.</summary>
    public record GitHubLinkRequest(long GitHubIssueId, int GitHubIssueNumber, string GitHubIssueUrl);

    /// <summary>Request to create a task from a GitHub Issue.</summary>
    public record CreateFromGitHubRequest(
        Guid ProjectId, string Title, string? Description, string? Priority, string? Tags,
        long GitHubIssueId, int GitHubIssueNumber, string GitHubIssueUrl);

    /// <summary>Request to update task content from GitHub Issue.</summary>
    public record UpdateFromGitHubRequest(string? Title, string? Description);

    /// <summary>Request to update task labels/priority from GitHub.</summary>
    public record UpdateLabelsRequest(string? Priority, string? Tags);

    #endregion

    /// <summary>Links an existing task to a GitHub issue after internal service authentication succeeds.</summary>
    [HttpPatch("projects/{projectId:guid}/tasks/{taskId:guid}/link")]
    public Task<IActionResult> LinkToGitHub(Guid projectId, Guid taskId, [FromBody] GitHubLinkRequest req) =>
        ExecuteTaskUpdateAsync(projectId, taskId, task =>
        {
            task.GitHubIssueId = req.GitHubIssueId;
            task.GitHubIssueNumber = req.GitHubIssueNumber;
            task.GitHubIssueUrl = req.GitHubIssueUrl;
        }, invalidateCache: false);

    /// <summary>
    /// Creates a task from a GitHub issue opened webhook, returning the existing task when the issue is already linked.
    /// </summary>
    [HttpPost("projects/{projectId:guid}/tasks")]
    public async Task<IActionResult> CreateFromGitHub(Guid projectId, [FromBody] CreateFromGitHubRequest req, CancellationToken ct = default)
    {
        if (!IsInternalServiceCall()) return Forbid();
        if (req.ProjectId != projectId) return BadRequest("ProjectId mismatch");

        var existing = await _db.Tasks.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.GitHubIssueId == req.GitHubIssueId, ct);
        if (existing != null) return Ok(new { id = existing.Id, alreadyExists = true });

        var defaultColumn = await GetDefaultColumnAsync(projectId);

        // I-02: Sanitize content from GitHub (XSS protection)
        var sanitizedTitle = SecurityHelpers.SanitizeHtml(req.Title);
        var sanitizedDesc = SecurityHelpers.SanitizeHtml(req.Description ?? "");

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = sanitizedTitle.Length > 200 ? sanitizedTitle[..200] : sanitizedTitle,
            Description = sanitizedDesc,
            Priority = req.Priority ?? "medium",
            Tags = req.Tags,
            Status = DomainTaskStatus.Todo.Value, // B-09: Value Object
            ColumnId = defaultColumn?.Id,
            PositionInColumn = 0,
            GitHubIssueId = req.GitHubIssueId,
            GitHubIssueNumber = req.GitHubIssueNumber,
            GitHubIssueUrl = req.GitHubIssueUrl,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = Guid.Empty,
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId);

        return Ok(new { id = task.Id });
    }

    /// <summary>Marks a linked task as completed for a GitHub issue closed webhook.</summary>
    [HttpPatch("projects/{projectId:guid}/tasks/{taskId:guid}/complete")]
    public Task<IActionResult> CompleteFromGitHub(Guid projectId, Guid taskId) =>
        ExecuteTaskUpdateAsync(projectId, taskId, async task =>
        {
            var doneColumn = await GetCompletedColumnAsync(projectId);
            task.Status = DomainTaskStatus.Done.Value; // B-09
            task.ColumnId = doneColumn?.Id ?? task.ColumnId;
            task.CompletedAt = DateTime.UtcNow;
        });

    /// <summary>Reopens a linked task for a GitHub issue reopened webhook and moves it back to the default column when available.</summary>
    [HttpPatch("projects/{projectId:guid}/tasks/{taskId:guid}/reopen")]
    public Task<IActionResult> ReopenFromGitHub(Guid projectId, Guid taskId) =>
        ExecuteTaskUpdateAsync(projectId, taskId, async task =>
        {
            var todoColumn = await GetDefaultColumnAsync(projectId);
            task.Status = DomainTaskStatus.Todo.Value; // B-09
            task.ColumnId = todoColumn?.Id ?? task.ColumnId;
            task.CompletedAt = null;
        });

    /// <summary>Updates linked task title and description from a GitHub issue edited webhook, sanitizing supplied content.</summary>
    [HttpPatch("projects/{projectId:guid}/tasks/{taskId:guid}/content")]
    public Task<IActionResult> UpdateContentFromGitHub(Guid projectId, Guid taskId, [FromBody] UpdateFromGitHubRequest req) =>
        ExecuteTaskUpdateAsync(projectId, taskId, task =>
        {
            // I-02: Sanitize content from GitHub before storing
            task.Title = req.Title != null ? SecurityHelpers.SanitizeHtml(req.Title) : task.Title;
            task.Description = req.Description != null ? SecurityHelpers.SanitizeHtml(req.Description) : task.Description;
        });

    /// <summary>Updates linked task priority and tags from GitHub label changes.</summary>
    [HttpPatch("projects/{projectId:guid}/tasks/{taskId:guid}/labels")]
    public Task<IActionResult> UpdateLabelsFromGitHub(Guid projectId, Guid taskId, [FromBody] UpdateLabelsRequest req) =>
        ExecuteTaskUpdateAsync(projectId, taskId, task =>
        {
            task.Priority = req.Priority ?? task.Priority;
            task.Tags = req.Tags ?? task.Tags;
        });

    /// <summary>Finds a non-deleted project task by GitHub issue identifier for internal gateway routing.</summary>
    [HttpGet("projects/{projectId:guid}/tasks/by-issue/{gitHubIssueId:long}")]
    public async Task<IActionResult> FindByGitHubIssue(Guid projectId, long gitHubIssueId, CancellationToken ct = default)
    {
        if (!IsInternalServiceCall()) return Forbid();

        var task = await _db.Tasks
            .Where(t => t.ProjectId == projectId && t.GitHubIssueId == gitHubIssueId && !t.IsDeleted)
            .Select(t => new { t.Id, t.Title, t.Status, t.GitHubIssueNumber })
            .FirstOrDefaultAsync(ct);

        return task == null ? NotFound() : Ok(task);
    }

    #region Private Helpers

    /// <summary>Runs an authenticated task update that only needs synchronous mutation logic.</summary>
    private Task<IActionResult> ExecuteTaskUpdateAsync(
        Guid projectId, Guid taskId, Action<TaskItem> updateAction, bool invalidateCache = true) =>
        ExecuteTaskUpdateCoreAsync(projectId, taskId, task => { updateAction(task); return Task.CompletedTask; }, invalidateCache);

    /// <summary>Runs an authenticated task update that needs asynchronous mutation logic.</summary>
    private Task<IActionResult> ExecuteTaskUpdateAsync(
        Guid projectId, Guid taskId, Func<TaskItem, Task> updateActionAsync, bool invalidateCache = true) =>
        ExecuteTaskUpdateCoreAsync(projectId, taskId, updateActionAsync, invalidateCache);

    /// <summary>Authenticates the internal request, loads the task, applies the update, persists it, and optionally clears project caches.</summary>
    private async Task<IActionResult> ExecuteTaskUpdateCoreAsync(
        Guid projectId, Guid taskId, Func<TaskItem, Task> updateActionAsync, bool invalidateCache, CancellationToken ct = default)
    {
        if (!IsInternalServiceCall()) return Forbid();

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, ct);
        if (task == null) return NotFound();

        await updateActionAsync(task);
        task.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        if (invalidateCache) await InvalidateProjectCacheAsync(projectId);

        return Ok();
    }

    /// <summary>
    /// Gets the first task column by board position for newly opened or reopened GitHub issues.
    /// </summary>
    private Task<TaskColumn?> GetDefaultColumnAsync(Guid projectId) =>
        _db.TaskColumns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).FirstOrDefaultAsync();

    /// <summary>
    /// Gets the completed task column used when a GitHub issue closes.
    /// </summary>
    private Task<TaskColumn?> GetCompletedColumnAsync(Guid projectId) =>
        _db.TaskColumns.Where(c => c.ProjectId == projectId && c.IsCompleted).FirstOrDefaultAsync();

    // I-01: Use HMAC-based authenticator (same as IntegrationsController) instead of trivial string comparison
    /// <summary>
    /// Validates the current request as an internal service call using the request path as the signing subject.
    /// </summary>
    private bool IsInternalServiceCall() =>
        _serviceAuth.ValidateRequest(HttpContext, HttpContext.Request.Path.Value ?? "");

    /// <summary>
    /// Removes cached project detail and project list entries after synchronized task changes.
    /// </summary>
    private async Task InvalidateProjectCacheAsync(Guid projectId)
    {
        await _cache.RemoveAsync($"project:{projectId}");
        await _cache.RemoveByPatternAsync("projects:*");
    }

    #endregion
}
