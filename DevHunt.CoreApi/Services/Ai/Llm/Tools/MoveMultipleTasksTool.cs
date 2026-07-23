using System.Text.Json;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using TaskStatus = DevHunt.CoreApi.Models.TaskStatus;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Implements <c>move_multiple_tasks</c>. Two selection modes:
///   1. Explicit <c>taskIds[]</c>.
///   2. <c>filter: "all" | "column"</c> with optional <c>sourceColumnName</c>.
///
/// Filter mode is dangerous (one tool call could move every card in the
/// project), so we cap the affected set at <see cref="MaxAffected"/> and
/// refuse with a clear message if the filter would touch more rows. The
/// user always confirms via the existing tool-confirmation flow before
/// anything runs, but the cap is a second line of defense.
/// </summary>
public sealed class MoveMultipleTasksTool : IAiTool
{
    private const int MaxAffected = 50;

    private readonly DevHuntDbContext _db;
    private readonly IProjectPermissionService _permissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoveMultipleTasksTool"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="permissions">Project permission service for authorization checks.</param>
    public MoveMultipleTasksTool(DevHuntDbContext db, IProjectPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public string Name => "move_multiple_tasks";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("move_multiple_tasks can only be invoked from a project conversation.");
        }

        var perms = await _permissions.GetPermissionsAsync(projectId, ctx.UserId, isAdmin: false, ct);
        if (perms is null) return Fail("Project not found.");
        if (!perms.CanManageTasks) return Fail("You don't have permission to move tasks in this project.");

        var targetColumn = await ResolveTargetColumnAsync(args, projectId, ct);
        if (targetColumn.Error != null) return Fail(targetColumn.Error);
        if (targetColumn.Column is null) return Fail("Target column could not be resolved.");

        var selection = await ResolveSelectionAsync(args, projectId, ct);
        if (selection.Error != null) return Fail(selection.Error);

        var tasks = selection.Tasks;
        if (tasks.Count == 0) return Fail("No tasks matched the provided selection.");
        if (tasks.Count > MaxAffected)
        {
            return Fail($"Selection matches {tasks.Count} tasks, exceeding the {MaxAffected} cap. Narrow the filter or supply explicit taskIds.");
        }

        // Position each moved task at the bottom of the target column. We
        // recount once and then increment locally — same atomicity guarantee
        // as MoveTaskTool, just batched.
        var basePosition = await _db.Tasks.CountAsync(
            t => t.ColumnId == targetColumn.Column.Id && !t.IsDeleted
                && !tasks.Select(x => x.Id).Contains(t.Id), ct);

        var moved = new List<object>(tasks.Count);
        var doneColumn = targetColumn.Column.Name.Contains(TaskStatus.Done.Value, StringComparison.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var sourceColumnId = task.ColumnId;
            task.ColumnId = targetColumn.Column.Id;
            task.PositionInColumn = basePosition + i;
            task.UpdatedAt = now;
            if (doneColumn && task.Status != TaskStatus.Done.Value)
            {
                task.Status = TaskStatus.Done.Value;
                task.CompletedAt = now;
            }

            moved.Add(new
            {
                taskId = task.Id,
                title = task.Title,
                fromColumnId = sourceColumnId,
                status = task.Status,
            });
        }

        await _db.SaveChangesAsync(ct);

        var payload = new
        {
            movedCount = moved.Count,
            toColumnId = targetColumn.Column.Id,
            toColumnName = targetColumn.Column.Name,
            tasks = moved,
        };

        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(payload),
            UserFacingSummary: $"Moved {moved.Count} tasks to {targetColumn.Column.Name}.");
    }

    // --- selection resolvers ---------------------------------------------

    /// <summary>Resolves requested tasks from explicit ids or bulk filter arguments within the target project.</summary>
    private async Task<(string? Error, List<TaskItem> Tasks)> ResolveSelectionAsync(JsonElement args, Guid projectId, CancellationToken ct)
    {
        // Explicit ids win when both forms are present — least surprising.
        if (args.TryGetProperty("taskIds", out var idsProp) && idsProp.ValueKind == JsonValueKind.Array)
        {
            var ids = idsProp.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => Guid.TryParse(e.GetString(), out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToList();
            if (ids.Count == 0) return ("'taskIds' must contain at least one valid GUID.", new());
            if (ids.Count > MaxAffected)
            {
                return ($"'taskIds' contains {ids.Count} ids, exceeding the {MaxAffected} cap.", new());
            }

            var tasks = await _db.Tasks
                .Where(t => ids.Contains(t.Id) && t.ProjectId == projectId && !t.IsDeleted)
                .ToListAsync(ct);
            return (null, tasks);
        }

        if (!args.TryGetProperty("filter", out var filterProp) || filterProp.ValueKind != JsonValueKind.String)
        {
            return ("Provide either 'taskIds' or 'filter'.", new());
        }

        var filter = filterProp.GetString()?.ToLowerInvariant();
        if (filter == "all")
        {
            // Cap+1 trick: pull at most MaxAffected+1 rows and bail if the
            // extra one materialises. Avoids loading the whole project board
            // just to count.
            var tasks = await _db.Tasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .OrderBy(t => t.CreatedAt)
                .Take(MaxAffected + 1)
                .ToListAsync(ct);
            return (null, tasks);
        }

        if (filter == "column")
        {
            if (!args.TryGetProperty("sourceColumnName", out var srcProp) || srcProp.ValueKind != JsonValueKind.String)
            {
                return ("filter 'column' requires 'sourceColumnName'.", new());
            }
            var srcName = srcProp.GetString()?.Trim();
            if (string.IsNullOrEmpty(srcName)) return ("'sourceColumnName' must be non-empty.", new());

            var sourceColumn = await _db.TaskColumns
                .FirstOrDefaultAsync(c => c.ProjectId == projectId
                    && EF.Functions.ILike(c.Name, srcName), ct);
            if (sourceColumn == null) return ("Source column not found in this project.", new());

            var tasks = await _db.Tasks
                .Where(t => t.ColumnId == sourceColumn.Id && !t.IsDeleted)
                .OrderBy(t => t.PositionInColumn)
                .Take(MaxAffected + 1)
                .ToListAsync(ct);
            return (null, tasks);
        }

        return ("Unknown filter value. Use 'all' or 'column'.", new());
    }

    /// <summary>Resolves the destination column from id or name within the target project.</summary>
    private async Task<(TaskColumn? Column, string? Error)> ResolveTargetColumnAsync(JsonElement args, Guid projectId, CancellationToken ct)
    {
        if (args.TryGetProperty("targetColumnId", out var idProp)
            && idProp.ValueKind == JsonValueKind.String
            && Guid.TryParse(idProp.GetString(), out var colId))
        {
            var col = await _db.TaskColumns
                .FirstOrDefaultAsync(c => c.Id == colId && c.ProjectId == projectId, ct);
            return col == null ? (null, "targetColumnId not found in this project.") : (col, null);
        }

        if (args.TryGetProperty("targetColumnName", out var nameProp)
            && nameProp.ValueKind == JsonValueKind.String)
        {
            var name = nameProp.GetString()?.Trim();
            if (string.IsNullOrEmpty(name)) return (null, "'targetColumnName' must be non-empty.");
            var col = await _db.TaskColumns
                .FirstOrDefaultAsync(c => c.ProjectId == projectId
                    && EF.Functions.ILike(c.Name, name), ct);
            return col == null ? (null, "Target column not found in this project.") : (col, null);
        }

        return (null, "Provide either 'targetColumnId' or 'targetColumnName'.");
    }

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);
}
