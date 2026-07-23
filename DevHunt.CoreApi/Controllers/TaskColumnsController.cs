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
/// Controller for managing custom task board columns.
/// </summary>
/// <remarks>
/// Enables Kanban-style board customization with:
/// - Custom column creation (names, colors, WIP limits)
/// - Column reordering
/// - Column deletion (with task migration)
/// - Canvas mode positioning
///
/// Routes: api/projects/{projectId}/columns/*
/// </remarks>
[ApiController]
[Route("api/projects/{projectId:guid}/columns")]
[Authorize]
public class TaskColumnsController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;
    private readonly IActivityLogService _activityLogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskColumnsController"/> class.
    /// </summary>
    /// <param name="db">Database context used to read projects, team membership, columns, and tasks.</param>
    /// <param name="cache">Cache service used to invalidate project and project-list entries after board changes.</param>
    /// <param name="activityLogService">Activity logger used to record column creation and deletion events.</param>
    public TaskColumnsController(DevHuntDbContext db, ICacheService cache, IActivityLogService activityLogService)
    {
        _db = db;
        _cache = cache;
        _activityLogService = activityLogService;
    }

    /// <summary>Column DTO with task counts and layout metadata.</summary>
    /// <param name="Id">Column identifier.</param>
    /// <param name="ProjectId">Project identifier.</param>
    /// <param name="Name">Column name.</param>
    /// <param name="Position">Ordering position.</param>
    /// <param name="Color">Optional color.</param>
    /// <param name="IsDefault">Whether this is the default column.</param>
    /// <param name="IsCompleted">Whether this column represents completed tasks.</param>
    /// <param name="WipLimit">Optional WIP limit.</param>
    /// <param name="CanvasX">Canvas X coordinate.</param>
    /// <param name="CanvasY">Canvas Y coordinate.</param>
    /// <param name="CanvasWidth">Canvas width.</param>
    /// <param name="CanvasHeight">Canvas height.</param>
    /// <param name="TaskCount">Number of tasks in the column.</param>
    public record ColumnDto(
        Guid Id,
        Guid ProjectId,
        string Name,
        int Position,
        string? Color,
        bool IsDefault,
        bool IsCompleted,
        int? WipLimit,
        float? CanvasX,
        float? CanvasY,
        float? CanvasWidth,
        float? CanvasHeight,
        int TaskCount);

    /// <summary>Request to create a new task column.</summary>
    /// <param name="Name">Column name.</param>
    /// <param name="Color">Optional color.</param>
    /// <param name="WipLimit">Optional WIP limit.</param>
    /// <param name="Position">Optional explicit position.</param>
    public record CreateColumnRequest(
        string Name,
        string? Color = null,
        int? WipLimit = null,
        int? Position = null);

    /// <summary>Request to update a task column.</summary>
    /// <param name="Name">Updated name.</param>
    /// <param name="Color">Updated color.</param>
    /// <param name="WipLimit">Updated WIP limit.</param>
    /// <param name="IsCompleted">Updated completed flag.</param>
    /// <param name="CanvasX">Updated canvas X.</param>
    /// <param name="CanvasY">Updated canvas Y.</param>
    /// <param name="CanvasWidth">Updated canvas width.</param>
    /// <param name="CanvasHeight">Updated canvas height.</param>
    public record UpdateColumnRequest(
        string? Name = null,
        string? Color = null,
        int? WipLimit = null,
        bool? IsCompleted = null,
        float? CanvasX = null,
        float? CanvasY = null,
        float? CanvasWidth = null,
        float? CanvasHeight = null);

    /// <summary>Request to reorder columns by ID list.</summary>
    /// <param name="ColumnIds">Ordered list of column IDs.</param>
    public record ReorderColumnsRequest(List<Guid> ColumnIds);

    /// <summary>
    /// Reads the authenticated user's identifier from claims and fails fast when authentication middleware did not provide it.
    /// </summary>
    /// <returns>The current user's ID.</returns>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Returns a project's task columns ordered for board display, including non-deleted task counts.
    /// </summary>
    /// <param name="projectId">Project whose columns should be listed.</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>
    /// Column metadata for owners and active team members, 404 when the project is missing, or 403 when the user lacks project access.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetColumns(Guid projectId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await HasProjectAccessAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can view columns" });
        }

        var columns = await _db.TaskColumns
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Position)
            .Select(c => new ColumnDto(
                c.Id,
                c.ProjectId,
                c.Name,
                c.Position,
                c.Color,
                c.IsDefault,
                c.IsCompleted,
                c.WipLimit,
                c.CanvasX,
                c.CanvasY,
                c.CanvasWidth,
                c.CanvasHeight,
                c.Tasks.Count(t => !t.IsDeleted)))
            .ToListAsync(ct);

        return Ok(columns);
    }

    /// <summary>
    /// Creates a project task column and shifts existing positions when an insertion position is supplied.
    /// </summary>
    /// <param name="projectId">Project that receives the new column.</param>
    /// <param name="req">Column name, color, WIP limit, and optional insertion position.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// The created column with a zero task count; returns 400 for a blank name, 403 for users who cannot manage columns,
    /// or 404 when the project is missing.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> CreateColumn(Guid projectId, [FromBody] CreateColumnRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await CanManageColumnsAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner or team leaders can manage columns" });
        }

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest("Column name is required");
        }

        // Get next position if not specified
        var maxPosition = await _db.TaskColumns
            .Where(c => c.ProjectId == projectId)
            .MaxAsync(c => (int?)c.Position, ct) ?? -1;

        var position = req.Position ?? (maxPosition + 1);

        // TB-01: Wrap shift + add in a single transaction to prevent partial position corruption
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Shift existing columns via tracked entities (not ExecuteUpdateAsync)
        if (req.Position.HasValue)
        {
            var columnsToShift = await _db.TaskColumns
                .Where(c => c.ProjectId == projectId && c.Position >= position)
                .ToListAsync(ct);
            foreach (var c in columnsToShift) c.Position += 1;
        }

        var column = new TaskColumn
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = req.Name.Trim(),
            Position = position,
            Color = req.Color,
            WipLimit = req.WipLimit,
            IsDefault = false,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.TaskColumns.Add(column);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await InvalidateProjectCacheAsync(projectId);

        await _activityLogService.LogProjectEventAsync(
            projectId,
            userId,
            "column.created",
            $"Created column: {column.Name}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { columnId = column.Id, name = column.Name, position = column.Position });

        return Ok(new ColumnDto(
            column.Id,
            column.ProjectId,
            column.Name,
            column.Position,
            column.Color,
            column.IsDefault,
            column.IsCompleted,
            column.WipLimit,
            column.CanvasX,
            column.CanvasY,
            column.CanvasWidth,
            column.CanvasHeight,
            0));
    }

    /// <summary>
    /// Updates editable column metadata such as name, color, completion flag, WIP limit, and canvas layout values.
    /// </summary>
    /// <param name="projectId">Project that owns the column.</param>
    /// <param name="columnId">Column to update.</param>
    /// <param name="req">Partial column update values.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// The updated column including its task count, 403 when the user cannot manage columns, or 404 when the project or column is missing.
    /// </returns>
    [HttpPut("{columnId:guid}")]
    public async Task<IActionResult> UpdateColumn(Guid projectId, Guid columnId, [FromBody] UpdateColumnRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await CanManageColumnsAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner or team leaders can manage columns" });
        }

        var columnData = await _db.TaskColumns
            .Where(c => c.Id == columnId && c.ProjectId == projectId)
            .Select(c => new { Column = c, TaskCount = c.Tasks.Count(t => !t.IsDeleted) })
            .FirstOrDefaultAsync(ct);
        if (columnData == null) return NotFound("Column not found");

        var column = columnData.Column;

        if (!string.IsNullOrWhiteSpace(req.Name)) column.Name = req.Name.Trim();
        if (req.Color != null) column.Color = req.Color;
        if (req.WipLimit.HasValue) column.WipLimit = req.WipLimit.Value > 0 ? req.WipLimit.Value : null;
        if (req.IsCompleted.HasValue) column.IsCompleted = req.IsCompleted.Value;
        if (req.CanvasX.HasValue) column.CanvasX = req.CanvasX;
        if (req.CanvasY.HasValue) column.CanvasY = req.CanvasY;
        if (req.CanvasWidth.HasValue) column.CanvasWidth = req.CanvasWidth;
        if (req.CanvasHeight.HasValue) column.CanvasHeight = req.CanvasHeight;

        column.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId);

        return Ok(new ColumnDto(
            column.Id,
            column.ProjectId,
            column.Name,
            column.Position,
            column.Color,
            column.IsDefault,
            column.IsCompleted,
            column.WipLimit,
            column.CanvasX,
            column.CanvasY,
            column.CanvasWidth,
            column.CanvasHeight,
            columnData.TaskCount));
    }

    /// <summary>
    /// Deletes a column, optionally moving active tasks to another project column before removing it.
    /// </summary>
    /// <param name="projectId">Project that owns the column.</param>
    /// <param name="columnId">Column to delete.</param>
    /// <param name="moveTasksTo">Target column for active tasks; required when the column contains active tasks.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// 204 after deletion; returns 400 when active tasks need a target or the target is invalid,
    /// 403 when the user cannot manage columns, or 404 when the project or column is missing.
    /// </returns>
    [HttpDelete("{columnId:guid}")]
    public async Task<IActionResult> DeleteColumn(Guid projectId, Guid columnId, [FromQuery] Guid? moveTasksTo = null, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await CanManageColumnsAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner or team leaders can manage columns" });
        }

        var column = await _db.TaskColumns.FirstOrDefaultAsync(c => c.Id == columnId && c.ProjectId == projectId, ct);
        if (column == null) return NotFound("Column not found");

        // TB-02: Wrap entire delete in RepeatableRead transaction to prevent race conditions
        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, ct);

        // Check if column has tasks
        var taskCount = await _db.Tasks.CountAsync(t => t.ColumnId == columnId && !t.IsDeleted, ct);
        if (taskCount > 0)
        {
            if (!moveTasksTo.HasValue)
            {
                return BadRequest(new { error = "Column has tasks", taskCount, message = "Specify moveTasksTo parameter to move tasks to another column" });
            }

            var targetColumn = await _db.TaskColumns.FirstOrDefaultAsync(c => c.Id == moveTasksTo && c.ProjectId == projectId, ct);
            if (targetColumn == null)
            {
                return BadRequest("Target column not found");
            }

            // Get max position in target column
            var maxPosition = await _db.Tasks
                .Where(t => t.ColumnId == moveTasksTo && !t.IsDeleted)
                .MaxAsync(t => (int?)t.PositionInColumn, ct) ?? -1;

            // Move tasks to target column
            var tasksToMove = await _db.Tasks
                .Where(t => t.ColumnId == columnId && !t.IsDeleted)
                .OrderBy(t => t.PositionInColumn)
                .ToListAsync(ct);

            var newPosition = maxPosition + 1;
            foreach (var task in tasksToMove)
            {
                task.ColumnId = moveTasksTo;
                task.PositionInColumn = newPosition++;
                task.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Also handle soft-deleted tasks that still reference this column (FK constraint)
        var deletedTasks = await _db.Tasks
            .Where(t => t.ColumnId == columnId && t.IsDeleted)
            .ToListAsync(ct);
        if (deletedTasks.Count > 0)
        {
            // Move soft-deleted tasks to the target column, or if no target — set ColumnId to null via hard delete
            if (moveTasksTo.HasValue)
            {
                foreach (var t in deletedTasks) t.ColumnId = moveTasksTo;
            }
            else
            {
                _db.Tasks.RemoveRange(deletedTasks);
            }
        }

        // Shift positions via tracked entities (not ExecuteUpdateAsync)
        var columnsAfter = await _db.TaskColumns
            .Where(c => c.ProjectId == projectId && c.Position > column.Position)
            .ToListAsync(ct);
        foreach (var c in columnsAfter) c.Position -= 1;

        _db.TaskColumns.Remove(column);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await InvalidateProjectCacheAsync(projectId);

        await _activityLogService.LogProjectEventAsync(
            projectId,
            userId,
            "column.deleted",
            $"Deleted column: {column.Name}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { columnId = column.Id, name = column.Name, tasksMoved = taskCount });

        return NoContent();
    }

    /// <summary>
    /// Reassigns column positions from a client-provided ordered list of column IDs.
    /// </summary>
    /// <param name="projectId">Project whose columns are being reordered.</param>
    /// <param name="req">Ordered column identifiers to assign positions from zero.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// The reordered columns with live task counts, 400 for missing or foreign column IDs,
    /// 403 when the user cannot manage columns, or 404 when the project is missing.
    /// </returns>
    [HttpPost("reorder")]
    public async Task<IActionResult> ReorderColumns(Guid projectId, [FromBody] ReorderColumnsRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await CanManageColumnsAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner or team leaders can manage columns" });
        }

        if (req.ColumnIds == null || req.ColumnIds.Count == 0)
        {
            return BadRequest("Column IDs are required");
        }

        var columns = await _db.TaskColumns
            .Where(c => c.ProjectId == projectId)
            .ToListAsync(ct);

        // Validate all column IDs belong to this project
        var columnDict = columns.ToDictionary(c => c.Id);
        foreach (var colId in req.ColumnIds)
        {
            if (!columnDict.ContainsKey(colId))
            {
                return BadRequest($"Column {colId} not found in project");
            }
        }

        // Update positions
        for (int i = 0; i < req.ColumnIds.Count; i++)
        {
            if (columnDict.TryGetValue(req.ColumnIds[i], out var col))
            {
                col.Position = i;
                col.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId);

        // TB-13: Load actual task counts instead of returning hardcoded 0
        var taskCounts = await _db.Tasks
            .Where(t => t.ProjectId == projectId && !t.IsDeleted && t.ColumnId != null)
            .GroupBy(t => t.ColumnId)
            .Select(g => new { ColumnId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ColumnId!.Value, g => g.Count, ct);

        return Ok(columns.OrderBy(c => c.Position).Select(c => new ColumnDto(
            c.Id,
            c.ProjectId,
            c.Name,
            c.Position,
            c.Color,
            c.IsDefault,
            c.IsCompleted,
            c.WipLimit,
            c.CanvasX,
            c.CanvasY,
            c.CanvasWidth,
            c.CanvasHeight,
            taskCounts.GetValueOrDefault(c.Id, 0))));
    }

    /// <summary>
    /// Checks whether the user is the project owner or an active team member who can view board data.
    /// </summary>
    /// <param name="projectId">Project being accessed.</param>
    /// <param name="userId">User requesting access.</param>
    /// <param name="ownerId">Project owner's user ID.</param>
    /// <param name="ct">Cancellation token for the membership lookup.</param>
    /// <returns><see langword="true"/> when the user can view project columns.</returns>
    private async Task<bool> HasProjectAccessAsync(Guid projectId, Guid userId, Guid ownerId, CancellationToken ct = default)
    {
        if (ownerId == userId) return true;
        return await _db.TeamMembers.AnyAsync(tm =>
            tm.ProjectId == projectId &&
            tm.UserId == userId &&
            tm.Status == TeamMemberStatus.Active.Value, ct);
    }

    /// <summary>
    /// Checks whether the user is the project owner or an active team member with task-management column permissions.
    /// </summary>
    /// <param name="projectId">Project whose columns may be changed.</param>
    /// <param name="userId">User requesting the change.</param>
    /// <param name="ownerId">Project owner's user ID.</param>
    /// <param name="ct">Cancellation token for the membership lookup.</param>
    /// <returns><see langword="true"/> when the user can create, update, delete, or reorder columns.</returns>
    private async Task<bool> CanManageColumnsAsync(Guid projectId, Guid userId, Guid ownerId, CancellationToken ct = default)
    {
        if (ownerId == userId) return true;
        return await _db.TeamMembers.AnyAsync(tm =>
            tm.ProjectId == projectId &&
            tm.UserId == userId &&
            tm.Status == TeamMemberStatus.Active.Value &&
            (tm.IsLeader || tm.CanManageTasks), ct);
    }

    /// <summary>
    /// Removes cached project details and project-list entries affected by board column changes.
    /// </summary>
    /// <param name="projectId">Project whose cache entries should be removed.</param>
    private async Task InvalidateProjectCacheAsync(Guid projectId)
    {
        await _cache.RemoveAsync($"project:{projectId}");
        await _cache.RemoveByPatternAsync("projects:*");
    }
}
