using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Badges;
using DevHunt.CoreApi.Services.Tasks;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Constants;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TaskStatus = DevHunt.CoreApi.Models.TaskStatus;
using TaskPriority = DevHunt.CoreApi.Models.TaskPriority;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing project tasks and Kanban board operations.
/// </summary>
/// <remarks>
/// This controller implements Use Case UC-4: "Working on Projects" from the system requirements.
///
/// Core functionality:
/// - Task lifecycle management with Kanban workflow (todo → doing → review → done)
/// - Task assignment to team members
/// - Time tracking (estimated vs actual hours)
/// - Soft-delete with restore capability
/// - Deadline management
/// - Priority management (low, medium, high, urgent)
///
/// Access control hierarchy:
/// - Create tasks: Any project owner or active team member
/// - Edit tasks: Task creator, assignee, project owner, or team leader
/// - Delete tasks: Task creator, project owner, or team leader
/// - Restore tasks: Task creator or team leader
///
/// Status flow:
/// - todo: Not yet started
/// - doing: Work in progress
/// - review: Awaiting review/approval
/// - done: Completed (auto-sets CompletedAt timestamp)
/// - archived: Stored for historical reference
/// - cancelled: Work abandoned
///
/// Corresponds to SRS v1.0 Section 4: "Company Projects and Sponsorship"
/// and devhunt_sequence.puml diagram (workspace collaboration).
///
/// Routes: api/projects/{projectId}/tasks/*
/// </remarks>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;
    private readonly IActivityLogService _activityLogService;
    private readonly ITaskAuthorizationService _taskAuth;

    /// <summary>
    /// Initializes a new instance of the <see cref="TasksController"/> class.
    /// </summary>
    /// <param name="db">Database context used for projects, tasks, columns, team members, and assignee lookups.</param>
    /// <param name="cache">Cache service used to invalidate affected project entries after task changes.</param>
    /// <param name="activityLogService">Activity logger used to record task lifecycle events.</param>
    /// <param name="taskAuth">Authorization service that evaluates task and project permissions.</param>
    public TasksController(
        DevHuntDbContext db,
        ICacheService cache,
        IActivityLogService activityLogService,
        ITaskAuthorizationService taskAuth)
    {
        _db = db;
        _cache = cache;
        _activityLogService = activityLogService;
        _taskAuth = taskAuth;
    }

    // ========================================================================
    // DTOs
    // ========================================================================

    /// <summary>Task data transfer object for API responses.</summary>
    /// <param name="Id">Unique task identifier.</param>
    /// <param name="ProjectId">Parent project ID.</param>
    /// <param name="Title">Task title.</param>
    /// <param name="Description">Task description (markdown supported).</param>
    /// <param name="Status">Current status: todo, doing, review, done, archived, cancelled.</param>
    /// <param name="Priority">Priority level: low, medium, high, urgent.</param>
    /// <param name="AssignedToUserId">Assigned user ID (null = unassigned).</param>
    /// <param name="AssignedToUserName">Assignee's display name.</param>
    /// <param name="AssignedToAvatarUrl">Assignee's avatar URL.</param>
    /// <param name="CreatedByUserId">User who created the task.</param>
    /// <param name="CreatedAt">Task creation timestamp.</param>
    /// <param name="Deadline">Task deadline (null = no deadline).</param>
    /// <param name="EstimatedHours">Estimated hours to complete.</param>
    /// <param name="ActualHours">Actual hours spent.</param>
    /// <param name="CompletedAt">Completion timestamp (set when status = done).</param>
    /// <param name="ColumnId">Custom Kanban column ID (null = default column).</param>
    /// <param name="PositionInColumn">Position for ordering within column.</param>
    /// <param name="LinkCount">Number of linked tasks.</param>
    /// <param name="AttachmentCount">Number of file attachments.</param>
    /// <param name="Tags">Comma-separated tags for filtering.</param>
    /// <param name="GitHubIssueId">GitHub issue ID (null if not synced).</param>
    /// <param name="GitHubIssueNumber">GitHub issue number (null if not synced).</param>
    /// <param name="GitHubIssueUrl">GitHub issue URL (null if not synced).</param>
    public record TaskDto(
        Guid Id,
        Guid ProjectId,
        string Title,
        string? Description,
        string Status,
        string? Priority,
        Guid? AssignedToUserId,
        string? AssignedToUserName,
        string? AssignedToAvatarUrl,
        Guid CreatedByUserId,
        DateTime CreatedAt,
        DateTime? Deadline,
        float? EstimatedHours,
        float? ActualHours,
        DateTime? CompletedAt,
        Guid? ColumnId,
        int PositionInColumn,
        int LinkCount,
        int AttachmentCount,
        string? Tags = null,
        // GitHub Issue sync fields
        long? GitHubIssueId = null,
        int? GitHubIssueNumber = null,
        string? GitHubIssueUrl = null);

    /// <summary>Request to create a new task.</summary>
    /// <param name="ProjectId">Target project (optional, can be inferred from route).</param>
    /// <param name="Title">Task title (required, max 200 chars).</param>
    /// <param name="Description">Task description (optional, markdown).</param>
    /// <param name="Priority">Priority: low, medium (default), high, urgent.</param>
    /// <param name="AssignedToUserId">User to assign task to (must be team member).</param>
    /// <param name="Deadline">Task deadline.</param>
    /// <param name="EstimatedHours">Estimated hours to complete.</param>
    /// <param name="ColumnId">Target Kanban column (null = default/first column).</param>
    /// <param name="Tags">Comma-separated tags (e.g., "frontend,backend,design").</param>
    public record CreateTaskRequest(
        Guid? ProjectId,
        string Title,
        string? Description,
        string? Priority,
        Guid? AssignedToUserId,
        DateTime? Deadline,
        float? EstimatedHours,
        Guid? ColumnId,
        string? Tags = null);

    /// <summary>Request to update an existing task.</summary>
    /// <param name="Title">New title (null = keep current).</param>
    /// <param name="Description">New description.</param>
    /// <param name="Status">New status: todo, doing, review, done.</param>
    /// <param name="Priority">New priority level.</param>
    /// <param name="AssignedToUserId">New assignee (null = unassign).</param>
    /// <param name="Deadline">New deadline (null = remove deadline).</param>
    /// <param name="EstimatedHours">Updated estimate.</param>
    /// <param name="ActualHours">Actual hours spent.</param>
    /// <param name="ColumnId">Move to different column.</param>
    /// <param name="PositionInColumn">New position in column.</param>
    /// <param name="Tags">Comma-separated tags (e.g., "frontend,backend,design").</param>
    public record UpdateTaskRequest(
        string? Title,
        string? Description,
        string? Status,
        string? Priority,
        Guid? AssignedToUserId,
        DateTime? Deadline,
        float? EstimatedHours,
        float? ActualHours,
        Guid? ColumnId,
        int? PositionInColumn,
        string? Tags = null);

    /// <summary>Request to reorder multiple tasks (drag-and-drop).</summary>
    /// <param name="Updates">List of position updates to apply atomically.</param>
    public record ReorderTasksRequest(List<TaskPositionUpdate> Updates);

    /// <summary>Single task position update.</summary>
    /// <param name="TaskId">Task to move.</param>
    /// <param name="ColumnId">Target column (null = default).</param>
    /// <param name="Position">New position in column.</param>
    public record TaskPositionUpdate(Guid TaskId, Guid? ColumnId, int Position);

    /// <summary>
    /// Reads the authenticated user's identifier from claims and fails fast when authentication middleware did not provide it.
    /// </summary>
    /// <returns>The current user's ID.</returns>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Retrieves all tasks for a specific project.
    /// </summary>
    /// <remarks>
    /// Returns tasks with optional status filtering.
    ///
    /// Access control:
    /// - Project owner: Can view all tasks
    /// - Active team members: Can view all tasks
    /// - Others: Access denied (403)
    ///
    /// Status filtering:
    /// - Valid statuses: todo, doing, review, done, archived, cancelled
    /// - Input validation enforced via RegularExpression attribute (security control SEC-009)
    ///
    /// Results are ordered by:
    /// 1. Status (logical workflow order)
    /// 2. Deadline (earliest first)
    ///
    /// Only non-deleted tasks are returned (soft-delete filtering).
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of tasks matching the criteria.</returns>
    /// <response code="200">Returns the task list.</response>
    /// <response code="403">If the user is not authorized to view project tasks.</response>
    /// <response code="404">If the project is not found.</response>
    [HttpGet]
    public async Task<IActionResult> GetProjectTasks(
        Guid projectId,
        [FromQuery][RegularExpression("^(todo|doing|review|done|archived|cancelled)$", ErrorMessage = "Status must be one of: todo, doing, review, done, archived, cancelled")] string? status = null, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        // Authorization check
        var authResult = await _taskAuth.CanViewTasksAsync(projectId, userId, ct);
        if (!authResult.Allowed)
            return authResult.Reason == "Project not found" ? NotFound(authResult.Reason) : StatusCode(403, new { error = "Access denied", message = authResult.Reason });

        var q = _db.Tasks.Where(t => t.ProjectId == projectId && !t.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status))
        {
            // SECURITY: Validation already enforced by RegularExpression attribute (SEC-009)
            q = q.Where(x => x.Status == status);
        }
        var list = await q
            .Include(t => t.AssignedToUser)
            .Include(t => t.Column)
            .OrderBy(x => x.Column != null ? x.Column.Position : 999)
            .ThenBy(x => x.PositionInColumn)
            .ThenBy(x => x.Deadline)
            .Select(t => new TaskDto(
                t.Id,
                t.ProjectId,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.AssignedToUserId,
                t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
                t.AssignedToUser != null ? t.AssignedToUser.AvatarUrl : null,
                t.CreatedByUserId,
                t.CreatedAt,
                t.Deadline,
                t.EstimatedHours,
                t.ActualHours,
                t.CompletedAt,
                t.ColumnId,
                t.PositionInColumn,
                t.OutgoingLinks.Count + t.IncomingLinks.Count,
                t.Attachments.Count,
                t.Tags,
                t.GitHubIssueId,
                t.GitHubIssueNumber,
                t.GitHubIssueUrl))
            .ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// Creates a new task in the project.
    /// </summary>
    /// <remarks>
    /// This endpoint allows team collaboration by enabling task creation for all active participants.
    ///
    /// Access control:
    /// - Project owner: Can create tasks
    /// - Active team members: Can create tasks
    /// - Others: Access denied (403)
    ///
    /// Validation:
    /// - Title is required and trimmed
    /// - If AssignedToUserId is specified, assignee must be project owner or active team member
    /// - If body ProjectId is provided, it must match route projectId
    /// - Default status: "todo"
    /// - Default priority: "medium"
    ///
    /// DateTime handling:
    /// - Deadline is converted to UTC to prevent "DateTime Kind=Unspecified" errors
    ///
    /// Side effects:
    /// 1. Creates task record with CreatedByUserId = current user
    /// 2. Invalidates project cache (triggers cache refresh)
    ///
    /// Time tracking:
    /// - EstimatedHours is optional and can be set at creation
    /// - ActualHours starts as null, updated as work progresses
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="req">Task creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created task.</returns>
    /// <response code="200">Returns the created task.</response>
    /// <response code="400">If validation fails (e.g., assignee not on team, mismatched project IDs).</response>
    /// <response code="403">If the user is not authorized to create tasks.</response>
    /// <response code="404">If the project is not found.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateTaskRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var validationError = await ValidateTaskCreationAsync(projectId, userId, req, ct);
        if (validationError != null) return validationError;

        TaskItem task;
        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var (columnId, position) = await ResolveColumnAndPositionAsync(projectId, req.ColumnId, ct);
            if (req.ColumnId.HasValue && !columnId.HasValue)
                return BadRequest("Column not found in project");

            task = BuildTaskItem(new TaskCreationContext(projectId, userId, req, columnId, position));
            _db.Tasks.Add(task);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        await InvalidateProjectCacheAsync(projectId, ct);
        await LogTaskCreatedAsync(projectId, userId, task, ct);

        return Ok(await BuildTaskDtoAsync(task, ct));
    }

    /// <summary>
    /// Updates an existing task.
    /// </summary>
    /// <remarks>
    /// This endpoint implements granular access control for task modifications.
    ///
    /// Access control (user must be one of):
    /// 1. Project owner
    /// 2. Task creator (CreatedByUserId)
    /// 3. Task assignee (AssignedToUserId)
    /// 4. Team leader (IsLeader=true)
    ///
    /// Otherwise: Access denied (403)
    ///
    /// Updatable fields (all optional):
    /// - Title, Description
    /// - Status (triggers CompletedAt if changed to "done")
    /// - Priority
    /// - AssignedToUserId (validated: must be owner or active team member)
    /// - Deadline
    /// - EstimatedHours, ActualHours
    ///
    /// Automatic behaviors:
    /// - UpdatedAt timestamp is always set
    /// - Changing status to "done" sets CompletedAt to current UTC time
    ///
    /// Side effects:
    /// 1. Updates task record
    /// 2. Invalidates project cache
    ///
    /// Validation:
    /// - Task must belong to the specified project
    /// - Task must not be soft-deleted
    /// - New assignee (if specified) must be on the project team
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="req">Task update data (all fields optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <response code="200">If the task was successfully updated.</response>
    /// <response code="400">If the task doesn't belong to the project or validation fails.</response>
    /// <response code="403">If the user is not authorized to edit this task.</response>
    /// <response code="404">If the task or project is not found.</response>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid taskId, [FromBody] UpdateTaskRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId && !x.IsDeleted, ct);
        if (task == null) return NotFound();
        if (task.ProjectId != projectId) return BadRequest("Task does not belong to the provided project");

        var authError = await ValidateTaskEditAuthorizationAsync(task, userId, ct);
        if (authError != null) return authError;

        var (previousStatus, previousTitle) = (task.Status, task.Title);

        var updateError = await ApplyTaskUpdatesAsync(task, req, ct);
        if (updateError != null) return updateError;

        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(task.ProjectId, ct);
        await LogTaskUpdateAsync(new TaskUpdateLogContext(task, userId, previousStatus, previousTitle, req), ct);
        await TryAwardCompletionAchievementAsync(previousStatus, task);

        return Ok();
    }

    /// <summary>
    /// Deletes a task (soft-delete).
    /// </summary>
    /// <remarks>
    /// This endpoint performs a soft-delete by setting IsDeleted=true rather than removing the record.
    ///
    /// Access control (user must be one of):
    /// 1. Project owner
    /// 2. Task creator (CreatedByUserId)
    /// 3. Team leader (IsLeader=true)
    ///
    /// Otherwise: Access denied (403)
    ///
    /// Soft-delete benefits:
    /// - Tasks can be restored if deleted by mistake (see RestoreTask)
    /// - Historical data is preserved for reporting and auditing
    /// - Related entities (comments, time logs) remain intact
    ///
    /// Side effects:
    /// 1. Sets IsDeleted=true
    /// 2. Updates UpdatedAt timestamp
    /// 3. Invalidates project cache
    ///
    /// Note: Deleted tasks are automatically filtered out from GetProjectTasks results.
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">If the task was successfully deleted.</response>
    /// <response code="400">If the task doesn't belong to the project.</response>
    /// <response code="403">If the user is not authorized to delete this task.</response>
    /// <response code="404">If the task or project is not found.</response>
    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId && !x.IsDeleted, ct);
        if (task == null) return NotFound();
        if (task.ProjectId != projectId) return BadRequest("Task does not belong to the provided project");

        // Authorization check
        var authResult = await _taskAuth.CanDeleteTaskAsync(task, userId, ct);
        if (!authResult.Allowed)
            return authResult.Reason?.Contains("not found") == true ? NotFound(authResult.Reason) : StatusCode(403, new { message = authResult.Reason });

        task.IsDeleted = true;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(task.ProjectId, ct);
        await LogTaskDeletedAsync(task, userId, ct);

        return NoContent();
    }

    /// <summary>
    /// Restores a previously soft-deleted task.
    /// </summary>
    /// <remarks>
    /// This endpoint allows recovery of accidentally deleted tasks.
    ///
    /// Access control (user must be one of):
    /// 1. Task creator (CreatedByUserId)
    /// 2. Team leader (IsLeader=true and active team member)
    ///
    /// Otherwise: Access denied (403)
    ///
    /// Validation:
    /// - Task must exist and be soft-deleted (IsDeleted=true)
    /// - Task must belong to the specified project
    ///
    /// Side effects:
    /// 1. Sets IsDeleted=false
    /// 2. Updates UpdatedAt timestamp
    /// 3. Invalidates project cache
    ///
    /// The restored task retains all its original data (status, assignee, dates, etc.).
    /// </remarks>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task to restore.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The restored task.</returns>
    /// <response code="200">Returns the restored task.</response>
    /// <response code="400">If the task doesn't belong to the project.</response>
    /// <response code="403">If the user is not authorized to restore this task.</response>
    /// <response code="404">If the task is not found or not deleted.</response>
    [HttpPost("{taskId:guid}/restore")]
    public async Task<IActionResult> RestoreTask(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId && x.IsDeleted, ct);
        if (task == null) return NotFound("Task not found or not deleted");
        if (task.ProjectId != projectId) return BadRequest("Task does not belong to the provided project");

        // Authorization check
        var authResult = await _taskAuth.CanRestoreTaskAsync(task, userId, ct);
        if (!authResult.Allowed) return StatusCode(403, new { message = authResult.Reason });

        task.IsDeleted = false;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(task.ProjectId, ct);
        await LogTaskRestoredAsync(task, userId, ct);

        return Ok(await BuildTaskDtoAsync(task, ct));
    }

    /// <summary>
    /// Applies a batch of drag-and-drop task position updates within a project board.
    /// </summary>
    /// <param name="projectId">Project whose tasks are being reordered.</param>
    /// <param name="req">Task IDs, target columns, and positions to apply together.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// The number of updated tasks; returns 400 when updates are empty, reference missing tasks, or reference foreign columns,
    /// 403 when the user is not an owner or active team member, or 404 when the project is missing.
    /// </returns>
    [HttpPost("reorder")]
    public async Task<IActionResult> ReorderTasks(Guid projectId, [FromBody] ReorderTasksRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var authError = await ValidateReorderAuthorizationAsync(projectId, userId, ct);
        if (authError != null) return authError;

        if (req.Updates == null || req.Updates.Count == 0)
            return BadRequest("No updates provided");

        var taskIds = req.Updates.Select(u => u.TaskId).ToList();
        var tasks = await _db.Tasks
            .Where(t => taskIds.Contains(t.Id) && t.ProjectId == projectId && !t.IsDeleted)
            .ToListAsync(ct);

        if (tasks.Count != taskIds.Count)
            return BadRequest("Some tasks not found or don't belong to this project");

        var columnError = await ValidateReorderColumnsAsync(projectId, req.Updates, ct);
        if (columnError != null) return columnError;

        ApplyReorderUpdates(tasks, req.Updates);
        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId, ct);

        return Ok(new { updated = req.Updates.Count });
    }

    /// <summary>
    /// Removes cached project details and project-list entries affected by task changes.
    /// </summary>
    /// <param name="projectId">Project whose cache entries should be removed.</param>
    /// <param name="ct">Cancellation token for cache calls.</param>
    private async Task InvalidateProjectCacheAsync(Guid projectId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync($"project:{projectId}");
        await _cache.RemoveByPatternAsync("projects:*");
    }

    #region Helper Methods (R12 refactoring - reduce cyclomatic complexity)

    /// <summary>
    /// Validates route/body project consistency, create permission, and requested assignee membership before creating a task.
    /// </summary>
    /// <param name="projectId">Project receiving the task.</param>
    /// <param name="userId">User creating the task.</param>
    /// <param name="req">Task creation request to validate.</param>
    /// <param name="ct">Cancellation token for authorization checks.</param>
    /// <returns>An action result for the first validation failure, or <see langword="null"/> when creation can continue.</returns>
    private async Task<IActionResult?> ValidateTaskCreationAsync(Guid projectId, Guid userId, CreateTaskRequest req, CancellationToken ct = default)
    {
        if (req.ProjectId.HasValue && req.ProjectId.Value != projectId)
            return BadRequest("Route projectId must match body projectId if provided");

        var authResult = await _taskAuth.CanCreateTaskAsync(projectId, userId, ct);
        if (!authResult.Allowed)
            return authResult.Reason == "Project not found" ? NotFound(authResult.Reason) : StatusCode(403, new { message = authResult.Reason });

        if (req.AssignedToUserId.HasValue && !await _taskAuth.IsValidAssigneeAsync(projectId, req.AssignedToUserId.Value, ct))
            return BadRequest("Assigned user must be project owner or active team member");

        return null;
    }

    /// <summary>
    /// Converts the task authorization service result for edits into the controller's HTTP response shape.
    /// </summary>
    /// <param name="task">Task the user wants to edit.</param>
    /// <param name="userId">User requesting the edit.</param>
    /// <param name="ct">Cancellation token for authorization checks.</param>
    /// <returns><see langword="null"/> when editing is allowed; otherwise 403 or 404 depending on the authorization reason.</returns>
    private async Task<IActionResult?> ValidateTaskEditAuthorizationAsync(TaskItem task, Guid userId, CancellationToken ct = default)
    {
        var authResult = await _taskAuth.CanEditTaskAsync(task, userId, ct);
        if (!authResult.Allowed)
            return authResult.Reason?.Contains("not found") == true ? NotFound(authResult.Reason) : StatusCode(403, new { message = authResult.Reason });
        return null;
    }

    /// <summary>
    /// Applies a task update request, including basic fields, assignee changes, column moves, and time tracking values.
    /// </summary>
    /// <param name="task">Task entity being updated.</param>
    /// <param name="req">Partial update values supplied by the client.</param>
    /// <param name="ct">Cancellation token for assignee and column validation.</param>
    /// <returns>An action result for invalid status, priority, assignee, or column values; otherwise <see langword="null"/>.</returns>
    private async Task<IActionResult?> ApplyTaskUpdatesAsync(TaskItem task, UpdateTaskRequest req, CancellationToken ct = default)
    {
        var statusError = ApplyBasicFieldUpdates(task, req);
        if (statusError != null) return statusError;

    // TB-05: Support explicit unassign (Guid.Empty) and reject invalid assignees
        if (req.AssignedToUserId.HasValue)
        {
            if (req.AssignedToUserId.Value == Guid.Empty)
            {
                task.AssignedToUserId = null; // explicit unassign
            }
            else if (await _taskAuth.IsValidAssigneeAsync(task.ProjectId, req.AssignedToUserId.Value, ct))
            {
                task.AssignedToUserId = req.AssignedToUserId;
            }
            else
            {
                return BadRequest("Assigned user must be project owner or active team member");
            }
        }

        var columnError = await ApplyColumnChangesAsync(task, req, ct);
        if (columnError != null) return columnError;

        ApplyTimeUpdates(task, req);
        return null;
    }

    /// <summary>
    /// Validates that the project exists and the user is either its owner or an active team member before reordering tasks.
    /// </summary>
    /// <param name="projectId">Project whose tasks are being reordered.</param>
    /// <param name="userId">User requesting the reorder.</param>
    /// <param name="ct">Cancellation token for project and membership lookups.</param>
    /// <returns><see langword="null"/> when reordering is allowed; otherwise 404 for a missing project or 403 for denied access.</returns>
    private async Task<IActionResult?> ValidateReorderAuthorizationAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        var isOwner = project.OwnerId == userId;
        var member = await _db.TeamMembers.FirstOrDefaultAsync(tm =>
            tm.ProjectId == projectId && tm.UserId == userId && tm.Status == TeamMemberStatus.Active, ct);

        if (!isOwner && member == null)
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can reorder tasks" });

        return null;
    }

    /// <summary>
    /// Ensures every target column referenced by a reorder request belongs to the same project.
    /// </summary>
    /// <param name="projectId">Project whose columns are valid reorder targets.</param>
    /// <param name="updates">Reorder updates that may reference column IDs.</param>
    /// <param name="ct">Cancellation token for the column lookup.</param>
    /// <returns><see langword="null"/> when all referenced columns are valid; otherwise a 400 response.</returns>
    private async Task<IActionResult?> ValidateReorderColumnsAsync(Guid projectId, List<TaskPositionUpdate> updates, CancellationToken ct = default)
    {
        var columnIds = updates.Where(u => u.ColumnId.HasValue).Select(u => u.ColumnId!.Value).Distinct().ToList();
        if (columnIds.Count == 0) return null;

        var validColumns = await _db.TaskColumns
            .Where(c => columnIds.Contains(c.Id) && c.ProjectId == projectId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (validColumns.Count != columnIds.Count)
            return BadRequest("Some columns not found or don't belong to this project");

        return null;
    }

    /// <summary>
    /// Mutates tracked task entities with their requested column and position values.
    /// </summary>
    /// <param name="tasks">Tracked task entities loaded from the target project.</param>
    /// <param name="updates">Client-provided position updates.</param>
    private static void ApplyReorderUpdates(List<TaskItem> tasks, List<TaskPositionUpdate> updates)
    {
        var taskDict = tasks.ToDictionary(t => t.Id);
        foreach (var update in updates)
        {
            if (taskDict.TryGetValue(update.TaskId, out var task))
            {
                task.ColumnId = update.ColumnId;
                task.PositionInColumn = update.Position;
                task.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Resolves the target column and next position for a new task from an explicit column or the project's default column.
    /// </summary>
    /// <param name="projectId">Project receiving the task.</param>
    /// <param name="requestedColumnId">Optional client-selected column.</param>
    /// <param name="ct">Cancellation token for column and task queries.</param>
    /// <returns>The resolved column and append position; a null column means no valid column was available.</returns>
    private async Task<(Guid? ColumnId, int Position)> ResolveColumnAndPositionAsync(Guid projectId, Guid? requestedColumnId, CancellationToken ct = default)
    {
        if (requestedColumnId.HasValue)
            return await ResolveRequestedColumnAsync(projectId, requestedColumnId.Value, ct);

        return await ResolveDefaultColumnAsync(projectId, ct);
    }

    /// <summary>
    /// Validates a requested project column and computes the next task position within it.
    /// </summary>
    /// <param name="projectId">Project that must own the column.</param>
    /// <param name="columnId">Requested task column.</param>
    /// <param name="ct">Cancellation token for column and task queries.</param>
    /// <returns>The requested column and next position, or a null column when it does not belong to the project.</returns>
    private async Task<(Guid? ColumnId, int Position)> ResolveRequestedColumnAsync(Guid projectId, Guid columnId, CancellationToken ct = default)
    {
        var columnExists = await _db.TaskColumns.AnyAsync(c => c.Id == columnId && c.ProjectId == projectId, ct);
        if (!columnExists) return (null, 0);

        var position = await GetNextPositionInColumnAsync(columnId, ct: ct);
        return (columnId, position);
    }

    /// <summary>
    /// Selects the project's default column, falling back to the lowest-position column, and computes its next task position.
    /// </summary>
    /// <param name="projectId">Project receiving the task.</param>
    /// <param name="ct">Cancellation token for column and task queries.</param>
    /// <returns>The selected column and append position, or a null column when the project has no columns.</returns>
    private async Task<(Guid? ColumnId, int Position)> ResolveDefaultColumnAsync(Guid projectId, CancellationToken ct = default)
    {
        var defaultColumn = await _db.TaskColumns
            .Where(c => c.ProjectId == projectId)
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.Position)
            .FirstOrDefaultAsync(ct);

        if (defaultColumn == null) return (null, 0);

        var pos = await GetNextPositionInColumnAsync(defaultColumn.Id, ct: ct);
        return (defaultColumn.Id, pos);
    }

    /// <summary>
    /// Computes the next append position in a column while optionally ignoring the task currently being moved.
    /// </summary>
    /// <param name="columnId">Column whose active tasks determine the next position.</param>
    /// <param name="excludeTaskId">Task to exclude from the position calculation.</param>
    /// <param name="ct">Cancellation token for the task query.</param>
    /// <returns>One greater than the maximum active task position, or zero for an empty column.</returns>
    private async Task<int> GetNextPositionInColumnAsync(Guid columnId, Guid? excludeTaskId = null, CancellationToken ct = default)
    {
        var query = _db.Tasks.Where(t => t.ColumnId == columnId && !t.IsDeleted);
        if (excludeTaskId.HasValue)
            query = query.Where(t => t.Id != excludeTaskId.Value);
        return (await query.MaxAsync(t => (int?)t.PositionInColumn, ct) ?? -1) + 1;
    }

    /// <summary>
    /// Carries the validated route, user, request, and board-placement data needed to create a task entity.
    /// </summary>
    /// <param name="ProjectId">Project receiving the task.</param>
    /// <param name="UserId">User creating the task.</param>
    /// <param name="Req">Validated creation request.</param>
    /// <param name="ColumnId">Resolved column for the task, if any.</param>
    /// <param name="Position">Resolved position within the column.</param>
    private record TaskCreationContext(Guid ProjectId, Guid UserId, CreateTaskRequest Req, Guid? ColumnId, int Position);

    /// <summary>
    /// Builds a new <see cref="TaskItem"/> with sanitized description, normalized deadline, default status, and board placement.
    /// </summary>
    /// <param name="context">Validated task creation context.</param>
    /// <returns>A new unsaved task entity.</returns>
    private static TaskItem BuildTaskItem(TaskCreationContext context)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = context.ProjectId,
            Title = context.Req.Title?.Trim() ?? throw new ArgumentException("Title required"),
            Description = context.Req.Description is not null ? SecurityHelpers.SanitizeHtml(context.Req.Description) : null,
            Priority = context.Req.Priority ?? TaskPriority.Medium,
            Status = TaskStatus.Todo,
            AssignedToUserId = context.Req.AssignedToUserId,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTime.UtcNow,
            Deadline = NormalizeDeadline(context.Req.Deadline),
            EstimatedHours = context.Req.EstimatedHours,
            ColumnId = context.ColumnId,
            PositionInColumn = context.Position,
            Tags = context.Req.Tags != null && context.Req.Tags.Trim().Length > 500
                ? context.Req.Tags.Trim()[..500]
                : context.Req.Tags?.Trim()
        };
    }

    /// <summary>
    /// Converts a valid deadline value to UTC kind and treats missing or minimum dates as no deadline.
    /// </summary>
    /// <param name="deadline">Deadline supplied by the client.</param>
    /// <returns>A UTC-kind deadline, or <see langword="null"/> when no usable deadline was supplied.</returns>
    private static DateTime? NormalizeDeadline(DateTime? deadline)
    {
        if (!deadline.HasValue || deadline.Value <= DateTime.MinValue) return null;
        return DateTime.SpecifyKind(deadline.Value, DateTimeKind.Utc);
    }

    /// <summary>
    /// Applies title, sanitized description, validated priority, and validated status changes to a task.
    /// </summary>
    /// <param name="task">Task entity being updated.</param>
    /// <param name="req">Update request containing basic field values.</param>
    /// <returns>A 400 response for invalid priority or status values; otherwise <see langword="null"/>.</returns>
    private IActionResult? ApplyBasicFieldUpdates(TaskItem task, UpdateTaskRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.Title)) task.Title = req.Title;
        // TB-04: Allow clearing description — check for null, not IsNullOrWhiteSpace
        if (req.Description is not null)
            task.Description = string.IsNullOrWhiteSpace(req.Description) ? null : SecurityHelpers.SanitizeHtml(req.Description.Trim());
        // TB-08: Validate priority
        if (!string.IsNullOrWhiteSpace(req.Priority))
        {
            var normalizedPriority = TaskPriority.FromString(req.Priority.Trim());
            if (normalizedPriority == null)
                return BadRequest("Priority must be one of: low, medium, high, urgent");
            task.Priority = normalizedPriority;
        }

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            try
            {
                task.Status = TaskStatus.FromString(req.Status.Trim());
            }
            catch (ArgumentException)
            {
                return BadRequest("Status must be one of: todo, doing, review, done, archived, cancelled");
            }
        }
        return null;
    }

    /// <summary>
    /// Moves a task to a new project column or updates its position within the current column.
    /// </summary>
    /// <param name="task">Task entity being updated.</param>
    /// <param name="req">Update request containing column or position values.</param>
    /// <param name="ct">Cancellation token for column validation.</param>
    /// <returns>A 400 response when the requested column is not in the project; otherwise <see langword="null"/>.</returns>
    private async Task<IActionResult?> ApplyColumnChangesAsync(TaskItem task, UpdateTaskRequest req, CancellationToken ct = default)
    {
        if (req.ColumnId.HasValue && req.ColumnId != task.ColumnId)
        {
            var columnExists = await _db.TaskColumns.AnyAsync(c => c.Id == req.ColumnId && c.ProjectId == task.ProjectId, ct);
            if (!columnExists) return BadRequest("Column not found in project");

            task.ColumnId = req.ColumnId;
            task.PositionInColumn = req.PositionInColumn ?? await GetNextPositionInColumnAsync(req.ColumnId.Value, task.Id);
        }
        else if (req.PositionInColumn.HasValue)
        {
            task.PositionInColumn = req.PositionInColumn.Value;
        }
        return null;
    }

    /// <summary>
    /// Applies estimate, actual-hours, update timestamp, and completion timestamp changes.
    /// </summary>
    /// <param name="task">Task entity being updated.</param>
    /// <param name="req">Update request containing time or status values.</param>
    private static void ApplyTimeUpdates(TaskItem task, UpdateTaskRequest req)
    {
        task.UpdatedAt = DateTime.UtcNow;
        if (req.EstimatedHours != null) task.EstimatedHours = req.EstimatedHours;
        if (req.ActualHours != null) task.ActualHours = req.ActualHours;

        if (req.Status != null)
            task.CompletedAt = task.Status == TaskStatus.Done ? DateTime.UtcNow : null;
    }

    /// <summary>
    /// Triggers completion achievements when a task first transitions to done and has an assignee.
    /// </summary>
    /// <param name="previousStatus">Task status before the update.</param>
    /// <param name="task">Updated task entity.</param>
    private async Task TryAwardCompletionAchievementAsync(string previousStatus, TaskItem task)
    {
        if (previousStatus == TaskStatus.Done || task.Status != TaskStatus.Done || task.AssignedToUserId == null)
            return;

        await HttpContext.RequestServices.TriggerAchievementCheckAsync(
            task.AssignedToUserId.Value, AchievementTrigger.TaskCompleted);
    }

    /// <summary>
    /// Logs a project activity entry for a newly created task.
    /// </summary>
    /// <param name="projectId">Project where the task was created.</param>
    /// <param name="userId">User who created the task.</param>
    /// <param name="task">Created task entity.</param>
    /// <param name="ct">Cancellation token for activity logging.</param>
    private async Task LogTaskCreatedAsync(Guid projectId, Guid userId, TaskItem task, CancellationToken ct = default)
    {
        await LogTaskActivityAsync(new TaskActivityLog(
            projectId, userId, "task.created",
            $"Created task: {task.Title}",
            new { taskId = task.Id, title = task.Title, status = task.Status, priority = task.Priority }));
    }

    /// <summary>
    /// Carries previous and current task state needed to choose the appropriate update activity event.
    /// </summary>
    /// <param name="Task">Updated task entity.</param>
    /// <param name="UserId">User who made the update.</param>
    /// <param name="PreviousStatus">Status before the update.</param>
    /// <param name="PreviousTitle">Title before the update.</param>
    /// <param name="Req">Original update request.</param>
    private record TaskUpdateLogContext(TaskItem Task, Guid UserId, string PreviousStatus, string PreviousTitle, UpdateTaskRequest Req);

    /// <summary>
    /// Logs either a status-change event or a general update event when the update is visible in project activity.
    /// </summary>
    /// <param name="ctx">Task update context with previous state and request values.</param>
    /// <param name="ct">Cancellation token for the project lookup.</param>
    private async Task LogTaskUpdateAsync(TaskUpdateLogContext ctx, CancellationToken ct = default)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == ctx.Task.ProjectId, ct);
        if (project == null) return;

        if (HasStatusChanged(ctx.PreviousStatus, ctx.Task.Status))
            await LogStatusChangeAsync(new StatusChangeLog(ctx.Task, ctx.UserId, ctx.PreviousStatus, project.Visibility));
        else if (HasSignificantChanges(ctx.PreviousTitle, ctx.Task.Title, ctx.Req))
            await LogGeneralUpdateAsync(new GeneralUpdateLog(ctx.Task, ctx.UserId, project.Visibility));
    }

    /// <summary>
    /// Compares status values using case-insensitive task status semantics.
    /// </summary>
    /// <param name="previousStatus">Status before the update.</param>
    /// <param name="currentStatus">Status after the update.</param>
    /// <returns><see langword="true"/> when the task moved to a different status.</returns>
    private static bool HasStatusChanged(string previousStatus, string currentStatus) =>
        !string.Equals(previousStatus, currentStatus, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether a non-status update should appear in the project activity feed.
    /// </summary>
    /// <param name="previousTitle">Title before the update.</param>
    /// <param name="currentTitle">Title after the update.</param>
    /// <param name="req">Original update request.</param>
    /// <returns><see langword="true"/> when title, description, priority, assignee, deadline, or time fields changed.</returns>
    private static bool HasSignificantChanges(string previousTitle, string currentTitle, UpdateTaskRequest req)
    {
        var titleChanged = !string.Equals(previousTitle, currentTitle, StringComparison.Ordinal);
        var hasOtherChanges = req.Description != null || req.Priority != null || req.AssignedToUserId != null
            || req.Deadline != null || req.EstimatedHours != null || req.ActualHours != null;
        return titleChanged || hasOtherChanges;
    }

    /// <summary>
    /// Carries data for a task status-change activity entry.
    /// </summary>
    /// <param name="Task">Task whose status changed.</param>
    /// <param name="UserId">User who changed the status.</param>
    /// <param name="PreviousStatus">Status before the update.</param>
    /// <param name="ProjectVisibility">Project visibility used to derive activity visibility.</param>
    private record StatusChangeLog(TaskItem Task, Guid UserId, string PreviousStatus, string ProjectVisibility);
    /// <summary>
    /// Carries data for a general task update activity entry.
    /// </summary>
    /// <param name="Task">Task that was updated.</param>
    /// <param name="UserId">User who updated the task.</param>
    /// <param name="ProjectVisibility">Project visibility used to derive activity visibility.</param>
    private record GeneralUpdateLog(TaskItem Task, Guid UserId, string ProjectVisibility);

    /// <summary>
    /// Writes a project activity entry describing a task status transition.
    /// </summary>
    /// <param name="log">Status-change activity data.</param>
    private async Task LogStatusChangeAsync(StatusChangeLog log)
    {
        await _activityLogService.LogProjectEventAsync(
            log.Task.ProjectId, log.UserId, "task.status_changed",
            $"Moved task: {log.Task.Title} ({log.PreviousStatus} → {log.Task.Status})",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(log.ProjectVisibility),
            payload: new { taskId = log.Task.Id, title = log.Task.Title, from = log.PreviousStatus, to = log.Task.Status });
    }

    /// <summary>
    /// Writes a project activity entry for a task update that did not change status.
    /// </summary>
    /// <param name="log">General update activity data.</param>
    private async Task LogGeneralUpdateAsync(GeneralUpdateLog log)
    {
        await _activityLogService.LogProjectEventAsync(
            log.Task.ProjectId, log.UserId, "task.updated",
            $"Updated task: {log.Task.Title}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(log.ProjectVisibility),
            payload: new { taskId = log.Task.Id, title = log.Task.Title, status = log.Task.Status });
    }

    /// <summary>
    /// Logs a project activity entry for a soft-deleted task.
    /// </summary>
    /// <param name="task">Task that was deleted.</param>
    /// <param name="userId">User who deleted the task.</param>
    /// <param name="ct">Cancellation token for activity logging.</param>
    private async Task LogTaskDeletedAsync(TaskItem task, Guid userId, CancellationToken ct = default)
    {
        await LogTaskActivityAsync(new TaskActivityLog(
            task.ProjectId, userId, "task.deleted",
            $"Deleted task: {task.Title}",
            new { taskId = task.Id, title = task.Title, status = task.Status }));
    }

    /// <summary>
    /// Logs a project activity entry for a restored task.
    /// </summary>
    /// <param name="task">Task that was restored.</param>
    /// <param name="userId">User who restored the task.</param>
    /// <param name="ct">Cancellation token for activity logging.</param>
    private async Task LogTaskRestoredAsync(TaskItem task, Guid userId, CancellationToken ct = default)
    {
        await LogTaskActivityAsync(new TaskActivityLog(
            task.ProjectId, userId, "task.restored",
            $"Restored task: {task.Title}",
            new { taskId = task.Id, title = task.Title, status = task.Status }));
    }

    /// <summary>
    /// Carries common data for task activity entries that require project visibility.
    /// </summary>
    /// <param name="ProjectId">Project receiving the activity entry.</param>
    /// <param name="UserId">Actor user ID.</param>
    /// <param name="EventType">Activity event type.</param>
    /// <param name="Message">Human-readable activity message.</param>
    /// <param name="Payload">Structured payload stored with the activity.</param>
    private record TaskActivityLog(Guid ProjectId, Guid UserId, string EventType, string Message, object Payload);

    /// <summary>
    /// Loads project visibility and writes a task activity event when the project still exists.
    /// </summary>
    /// <param name="log">Task activity data to write.</param>
    /// <param name="ct">Cancellation token for the project lookup.</param>
    private async Task LogTaskActivityAsync(TaskActivityLog log, CancellationToken ct = default)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == log.ProjectId, ct);
        if (project == null) return;

        await _activityLogService.LogProjectEventAsync(
            log.ProjectId, log.UserId, log.EventType, log.Message,
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: log.Payload);
    }

    /// <summary>
    /// Builds a <see cref="TaskDto"/> from a task entity and enriches it with assignee display data when available.
    /// </summary>
    /// <param name="t">Task entity to project.</param>
    /// <param name="ct">Cancellation token for the assignee lookup.</param>
    /// <returns>The API response DTO for the task.</returns>
    private async Task<TaskDto> BuildTaskDtoAsync(TaskItem t, CancellationToken ct = default)
    {
        var assignee = t.AssignedToUserId.HasValue
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == t.AssignedToUserId, ct)
            : null;

        return new TaskDto(
            t.Id, t.ProjectId, t.Title, t.Description, t.Status, t.Priority,
            t.AssignedToUserId, assignee?.FullName, assignee?.AvatarUrl,
            t.CreatedByUserId, t.CreatedAt, t.Deadline, t.EstimatedHours, t.ActualHours,
            t.CompletedAt, t.ColumnId, t.PositionInColumn, 0, 0, t.Tags,
            t.GitHubIssueId, t.GitHubIssueNumber, t.GitHubIssueUrl);
    }

    #endregion

    // NOTE: GitHub integration endpoints moved to GitHubTaskSyncController (R12 refactoring)
    // See: api/internal/github-tasks/*
}
