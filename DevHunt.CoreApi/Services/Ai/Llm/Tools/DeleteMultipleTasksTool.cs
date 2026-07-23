using System.Text.Json;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Implements <c>delete_multiple_tasks</c>. Soft-delete only — same rationale
/// as <see cref="DeleteTaskTool"/>: an LLM hallucinating a deletion shouldn't
/// be able to nuke real rows. Two selection modes:
///   1. Explicit <c>taskIds[]</c>.
///   2. <c>filter: "all" | "column"</c>.
///
/// The same hard cap as MoveMultipleTasks (50) — and on top the user has
/// already confirmed in the tool dialog before this code runs.
/// </summary>
public sealed class DeleteMultipleTasksTool : IAiTool
{
    private const int MaxAffected = 50;

    private readonly DevHuntDbContext _db;
    private readonly IProjectPermissionService _permissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteMultipleTasksTool"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="permissions">Project permission service for authorization checks.</param>
    public DeleteMultipleTasksTool(DevHuntDbContext db, IProjectPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public string Name => "delete_multiple_tasks";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("delete_multiple_tasks can only be invoked from a project conversation.");
        }

        var perms = await _permissions.GetPermissionsAsync(projectId, ctx.UserId, isAdmin: false, ct);
        if (perms is null) return Fail("Project not found.");
        if (!perms.CanManageTasks) return Fail("You don't have permission to delete tasks in this project.");

        var (error, tasks) = await ResolveSelectionAsync(args, projectId, ct);
        if (error != null) return Fail(error);
        if (tasks.Count == 0) return Fail("No tasks matched the provided selection.");
        if (tasks.Count > MaxAffected)
        {
            return Fail($"Selection matches {tasks.Count} tasks, exceeding the {MaxAffected} cap. Narrow the filter or supply explicit taskIds.");
        }

        var now = DateTime.UtcNow;
        foreach (var task in tasks)
        {
            task.IsDeleted = true;
            task.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        var payload = new
        {
            deletedCount = tasks.Count,
            tasks = tasks.Select(t => new { taskId = t.Id, title = t.Title }),
        };

        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(payload),
            UserFacingSummary: $"Deleted {tasks.Count} tasks (soft).");
    }

    /// <summary>Resolves requested tasks from explicit ids or bulk filter arguments within the target project.</summary>
    private async Task<(string? Error, List<TaskItem> Tasks)> ResolveSelectionAsync(JsonElement args, Guid projectId, CancellationToken ct)
    {
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
            var tasks = await _db.Tasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .OrderBy(t => t.CreatedAt)
                .Take(MaxAffected + 1)
                .ToListAsync(ct);
            return (null, tasks);
        }

        if (filter == "column")
        {
            if (!args.TryGetProperty("columnName", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
            {
                return ("filter 'column' requires 'columnName'.", new());
            }
            var name = nameProp.GetString()?.Trim();
            if (string.IsNullOrEmpty(name)) return ("'columnName' must be non-empty.", new());

            var column = await _db.TaskColumns
                .FirstOrDefaultAsync(c => c.ProjectId == projectId
                    && EF.Functions.ILike(c.Name, name), ct);
            if (column == null) return ("Column not found in this project.", new());

            var tasks = await _db.Tasks
                .Where(t => t.ColumnId == column.Id && !t.IsDeleted)
                .OrderBy(t => t.PositionInColumn)
                .Take(MaxAffected + 1)
                .ToListAsync(ct);
            return (null, tasks);
        }

        return ("Unknown filter value. Use 'all' or 'column'.", new());
    }

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);
}
