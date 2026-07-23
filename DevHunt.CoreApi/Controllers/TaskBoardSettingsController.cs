using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing task board display settings.
/// </summary>
/// <remarks>
/// Manages per-project board settings:
/// - View mode (board/canvas)
/// - Canvas zoom and pan position
/// - Default column for new tasks
/// - Show/hide completed tasks
///
/// Routes: api/projects/{projectId}/board-settings
/// </remarks>
[ApiController]
[Route("api/projects/{projectId:guid}/board-settings")]
[Authorize]
public class TaskBoardSettingsController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskBoardSettingsController"/> class.
    /// </summary>
    /// <param name="db">Database context used to read projects, memberships, settings, and default columns.</param>
    /// <param name="cache">Cache service used to invalidate project entries after settings change.</param>
    public TaskBoardSettingsController(DevHuntDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>Board settings response DTO.</summary>
    /// <param name="Id">Settings identifier.</param>
    /// <param name="ProjectId">Project identifier.</param>
    /// <param name="ViewMode">View mode (board or canvas).</param>
    /// <param name="CanvasZoom">Canvas zoom level.</param>
    /// <param name="CanvasPanX">Canvas X pan offset.</param>
    /// <param name="CanvasPanY">Canvas Y pan offset.</param>
    /// <param name="ShowCompletedTasks">Whether completed tasks are shown.</param>
    /// <param name="DefaultColumnId">Default column for new tasks.</param>
    public record BoardSettingsDto(
        Guid Id,
        Guid ProjectId,
        string ViewMode,
        float CanvasZoom,
        float CanvasPanX,
        float CanvasPanY,
        bool ShowCompletedTasks,
        Guid? DefaultColumnId);

    /// <summary>Request to update board settings.</summary>
    /// <param name="ViewMode">Updated view mode.</param>
    /// <param name="CanvasZoom">Updated zoom level.</param>
    /// <param name="CanvasPanX">Updated X pan offset.</param>
    /// <param name="CanvasPanY">Updated Y pan offset.</param>
    /// <param name="ShowCompletedTasks">Updated completed-tasks flag.</param>
    /// <param name="DefaultColumnId">Updated default column.</param>
    public record UpdateBoardSettingsRequest(
        string? ViewMode = null,
        float? CanvasZoom = null,
        float? CanvasPanX = null,
        float? CanvasPanY = null,
        bool? ShowCompletedTasks = null,
        Guid? DefaultColumnId = null);

    /// <summary>
    /// Reads the authenticated user's identifier from claims and fails fast when authentication middleware did not provide it.
    /// </summary>
    /// <returns>The current user's ID.</returns>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Returns a project's board settings, creating the default board-mode settings when none exist yet.
    /// </summary>
    /// <param name="projectId">Project whose board settings should be returned.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// The board settings for owners and active team members, 403 when the user lacks access, or 404 when the project is missing.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetSettings(Guid projectId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await HasProjectAccessAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can view board settings" });
        }

        var settings = await _db.TaskBoardSettings.FirstOrDefaultAsync(s => s.ProjectId == projectId, ct);

        // Create default settings if none exist
        if (settings == null)
        {
            settings = new TaskBoardSettings
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ViewMode = "board",
                CanvasZoom = 1.0f,
                CanvasPanX = 0,
                CanvasPanY = 0,
                ShowCompletedTasks = true,
                UpdatedAt = DateTime.UtcNow
            };
            _db.TaskBoardSettings.Add(settings);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new BoardSettingsDto(
            settings.Id,
            settings.ProjectId,
            settings.ViewMode,
            settings.CanvasZoom,
            settings.CanvasPanX,
            settings.CanvasPanY,
            settings.ShowCompletedTasks,
            settings.DefaultColumnId));
    }

    /// <summary>
    /// Updates a project's board settings, creating defaults first when the settings row does not exist.
    /// </summary>
    /// <param name="projectId">Project whose board settings should be updated.</param>
    /// <param name="req">Partial settings update for view mode, canvas state, completed-task visibility, or default column.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// The updated board settings; returns 400 for invalid view modes or default columns,
    /// 403 when the user lacks project access, or 404 when the project is missing.
    /// </returns>
    [HttpPut]
    public async Task<IActionResult> UpdateSettings(Guid projectId, [FromBody] UpdateBoardSettingsRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await HasProjectAccessAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can update board settings" });
        }

        var settings = await _db.TaskBoardSettings.FirstOrDefaultAsync(s => s.ProjectId == projectId, ct);

        // Create default settings if none exist
        if (settings == null)
        {
            settings = new TaskBoardSettings
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ViewMode = "board",
                CanvasZoom = 1.0f,
                CanvasPanX = 0,
                CanvasPanY = 0,
                ShowCompletedTasks = true,
                UpdatedAt = DateTime.UtcNow
            };
            _db.TaskBoardSettings.Add(settings);
        }

        if (!string.IsNullOrWhiteSpace(req.ViewMode))
        {
            if (!TaskBoardSettings.ValidViewModes.Contains(req.ViewMode))
            {
                return BadRequest($"Invalid view mode. Valid modes: {string.Join(", ", TaskBoardSettings.ValidViewModes)}");
            }
            settings.ViewMode = req.ViewMode;
        }

        if (req.CanvasZoom.HasValue)
        {
            settings.CanvasZoom = Math.Clamp(req.CanvasZoom.Value, 0.1f, 3.0f);
        }

        if (req.CanvasPanX.HasValue) settings.CanvasPanX = req.CanvasPanX.Value;
        if (req.CanvasPanY.HasValue) settings.CanvasPanY = req.CanvasPanY.Value;
        if (req.ShowCompletedTasks.HasValue) settings.ShowCompletedTasks = req.ShowCompletedTasks.Value;

        if (req.DefaultColumnId.HasValue)
        {
            var columnExists = await _db.TaskColumns.AnyAsync(c => c.Id == req.DefaultColumnId && c.ProjectId == projectId, ct);
            if (!columnExists)
            {
                return BadRequest("Default column not found in project");
            }
            settings.DefaultColumnId = req.DefaultColumnId;
        }

        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId);

        return Ok(new BoardSettingsDto(
            settings.Id,
            settings.ProjectId,
            settings.ViewMode,
            settings.CanvasZoom,
            settings.CanvasPanX,
            settings.CanvasPanY,
            settings.ShowCompletedTasks,
            settings.DefaultColumnId));
    }

    /// <summary>
    /// Checks whether the user is the project owner or an active team member who can read and update board settings.
    /// </summary>
    /// <param name="projectId">Project being accessed.</param>
    /// <param name="userId">User requesting access.</param>
    /// <param name="ownerId">Project owner's user ID.</param>
    /// <param name="ct">Cancellation token for the membership lookup.</param>
    /// <returns><see langword="true"/> when the user can access board settings.</returns>
    private async Task<bool> HasProjectAccessAsync(Guid projectId, Guid userId, Guid ownerId, CancellationToken ct = default)
    {
        if (ownerId == userId) return true;
        return await _db.TeamMembers.AnyAsync(tm =>
            tm.ProjectId == projectId &&
            tm.UserId == userId &&
            tm.Status == TeamMemberStatus.Active.Value, ct);
    }

    /// <summary>
    /// Removes cached project details and project-list entries affected by board settings changes.
    /// </summary>
    /// <param name="projectId">Project whose cache entries should be removed.</param>
    private async Task InvalidateProjectCacheAsync(Guid projectId)
    {
        await _cache.RemoveAsync($"project:{projectId}");
        await _cache.RemoveByPatternAsync("projects:*");
    }
}
