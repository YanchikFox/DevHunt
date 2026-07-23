using System.Text.Json;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskStatus = DevHunt.CoreApi.Models.TaskStatus;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Implements <c>move_task</c>. Accepts either a column id or a column name
/// The latter is friendlier for the LLM which usually only sees board
/// labels in chat. We resolve the column inside the conversation's project
/// only, so cross-project IDOR via guessed column ids is impossible.
/// </summary>
public sealed class MoveTaskTool : IAiTool
{
    private readonly DevHuntDbContext _db;
    private readonly IProjectPermissionService _permissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoveTaskTool"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="permissions">Project permission service for authorization checks.</param>
    public MoveTaskTool(DevHuntDbContext db, IProjectPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public string Name => "move_task";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("move_task can only be invoked from a project conversation.");
        }

        if (!args.TryGetProperty("taskId", out var taskIdProp) || taskIdProp.ValueKind != JsonValueKind.String
            || !Guid.TryParse(taskIdProp.GetString(), out var taskId))
        {
            return Fail("Argument 'taskId' must be a valid GUID.");
        }

        var task = await _db.Tasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (task == null) return Fail("Task not found in this project.");

        var perms = await _permissions.GetPermissionsAsync(projectId, ctx.UserId, isAdmin: false, ct);
        if (perms is null || !perms.CanManageTasks)
        {
            return Fail("You don't have permission to move tasks in this project.");
        }

        Guid? targetColumnId = null;
        string? targetColumnName = null;

        if (args.TryGetProperty("columnId", out var colIdProp)
            && colIdProp.ValueKind == JsonValueKind.String
            && Guid.TryParse(colIdProp.GetString(), out var colId))
        {
            targetColumnId = colId;
        }
        else if (args.TryGetProperty("columnName", out var colNameProp)
            && colNameProp.ValueKind == JsonValueKind.String)
        {
            targetColumnName = colNameProp.GetString()?.Trim();
        }

        if (!targetColumnId.HasValue && string.IsNullOrEmpty(targetColumnName))
        {
            return Fail("Provide either 'columnId' or 'columnName'.");
        }

        var column = targetColumnId.HasValue
            ? await _db.TaskColumns.FirstOrDefaultAsync(c => c.Id == targetColumnId.Value && c.ProjectId == projectId, ct)
            : await _db.TaskColumns.FirstOrDefaultAsync(c => c.ProjectId == projectId
                && EF.Functions.ILike(c.Name, targetColumnName!), ct);

        if (column == null) return Fail("Target column not found in this project.");

        // Snap to either an explicit position or the bottom of the column.
        // We re-count rather than trusting the LLM's number. It might pass
        // 999 or -1 hoping to insert "first/last".
        int position;
        if (args.TryGetProperty("position", out var posProp) && posProp.ValueKind == JsonValueKind.Number
            && posProp.TryGetInt32(out var requested) && requested >= 0)
        {
            var currentSize = await _db.Tasks.CountAsync(
                t => t.ColumnId == column.Id && !t.IsDeleted && t.Id != task.Id, ct);
            position = Math.Min(requested, currentSize);
        }
        else
        {
            position = await GetNextPositionInColumnAsync(column.Id, task.Id, ct);
        }

        var sourceColumnId = task.ColumnId;
        task.ColumnId = column.Id;
        task.PositionInColumn = position;
        task.UpdatedAt = DateTime.UtcNow;

        // Mirror the column-to-status mapping that TasksController applies on
        // drag-and-drop: moving into a "done" column should mark the task done.
        if (column.Name.Contains(TaskStatus.Done.Value, StringComparison.OrdinalIgnoreCase)
            && task.Status != TaskStatus.Done.Value)
        {
            task.Status = TaskStatus.Done.Value;
            task.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(new
            {
                taskId = task.Id,
                fromColumnId = sourceColumnId,
                toColumnId = column.Id,
                toColumnName = column.Name,
                position,
                status = task.Status,
            }),
            UserFacingSummary: $"Moved \"{task.Title}\" to {column.Name}.");
    }

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);

    /// <summary>Returns the next position in a column while ignoring the task being moved.</summary>
    private async Task<int> GetNextPositionInColumnAsync(Guid columnId, Guid excludeTaskId, CancellationToken ct)
    {
        return (await _db.Tasks
            .Where(t => t.ColumnId == columnId && !t.IsDeleted && t.Id != excludeTaskId)
            .MaxAsync(t => (int?)t.PositionInColumn, ct) ?? -1) + 1;
    }
}
