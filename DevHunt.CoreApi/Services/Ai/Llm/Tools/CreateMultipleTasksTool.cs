using System.Text.Json;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using TaskStatus = DevHunt.CoreApi.Models.TaskStatus;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Implements <c>create_multiple_tasks</c>. Takes a list of task definitions
/// + an optional shared <c>columnId</c> and creates them in one transactional
/// batch. Authorization mirrors the single-task tool: scoped to the
/// conversation's project, gated on <c>CanManageTasks</c>.
///
/// We hard-cap the batch at <see cref="MaxBatchSize"/> tasks. The cap is a
/// safety rail against an LLM going wild ("create 500 stories for the next
/// year") and an artificial bound on transaction size. Larger asks are
/// rejected outright so the user sees a clear error and can split it.
/// </summary>
public sealed class CreateMultipleTasksTool : IAiTool
{
    private const int MaxBatchSize = 50;

    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        TaskPriority.Low.Value,
        TaskPriority.Medium.Value,
        TaskPriority.High.Value,
        TaskPriority.Urgent.Value,
    };

    private readonly DevHuntDbContext _db;
    private readonly IProjectPermissionService _permissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateMultipleTasksTool"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="permissions">Project permission service for authorization checks.</param>
    public CreateMultipleTasksTool(DevHuntDbContext db, IProjectPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public string Name => "create_multiple_tasks";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("create_multiple_tasks can only be invoked from a project conversation.");
        }

        if (!args.TryGetProperty("tasks", out var tasksProp) || tasksProp.ValueKind != JsonValueKind.Array)
        {
            return Fail("Argument 'tasks' must be a non-empty array.");
        }

        var taskDefs = tasksProp.EnumerateArray().ToList();
        if (taskDefs.Count == 0) return Fail("Argument 'tasks' must contain at least one task.");
        if (taskDefs.Count > MaxBatchSize)
        {
            return Fail($"Cannot create more than {MaxBatchSize} tasks in one call. Split the request.");
        }

        var perms = await _permissions.GetPermissionsAsync(projectId, ctx.UserId, isAdmin: false, ct);
        if (perms is null) return Fail("Project not found.");
        if (!perms.CanManageTasks) return Fail("You don't have permission to create tasks in this project.");

        // Resolve the optional shared column. Same fallback logic as the
        // single-task tool: if the user gave a column id we validate it
        // belongs to this project; otherwise we drop into the first column.
        Guid? sharedColumnId = ReadGuidOrNull(args, "columnId");
        Guid? resolvedSharedColumnId = null;
        var sharedBasePosition = 0;

        if (sharedColumnId.HasValue)
        {
            var column = await _db.TaskColumns
                .FirstOrDefaultAsync(c => c.Id == sharedColumnId.Value && c.ProjectId == projectId, ct);
            if (column == null) return Fail("columnId not found in this project.");
            resolvedSharedColumnId = column.Id;
            sharedBasePosition = await _db.Tasks
                .CountAsync(t => t.ColumnId == column.Id && !t.IsDeleted, ct);
        }
        else
        {
            var firstColumn = await _db.TaskColumns
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.Position)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(ct);
            if (firstColumn != null)
            {
                resolvedSharedColumnId = firstColumn.Id;
                sharedBasePosition = await _db.Tasks
                    .CountAsync(t => t.ColumnId == firstColumn.Id && !t.IsDeleted, ct);
            }
        }

        var now = DateTime.UtcNow;
        var created = new List<TaskItem>(taskDefs.Count);
        var failed = new List<object>();

        for (var i = 0; i < taskDefs.Count; i++)
        {
            var def = taskDefs[i];
            if (def.ValueKind != JsonValueKind.Object)
            {
                failed.Add(new { index = i, error = "Each entry in 'tasks' must be an object." });
                continue;
            }

            if (!def.TryGetProperty("title", out var titleProp) || titleProp.ValueKind != JsonValueKind.String)
            {
                failed.Add(new { index = i, error = "Missing or invalid 'title'." });
                continue;
            }

            var title = (titleProp.GetString() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(title))
            {
                failed.Add(new { index = i, error = "'title' must be non-empty." });
                continue;
            }
            if (title.Length > 200) title = title[..200];

            var description = ReadStringOrNull(def, "description", maxLength: 1000);
            if (description != null) description = SecurityHelpers.SanitizeHtml(description);

            var priority = ReadStringOrNull(def, "priority", maxLength: 32);
            if (priority != null && !AllowedPriorities.Contains(priority))
            {
                failed.Add(new { index = i, error = $"'priority' must be one of: {string.Join(", ", AllowedPriorities)}." });
                continue;
            }
            priority ??= TaskPriority.Medium.Value;

            var tags = ReadTags(def);

            // Position increments per task within the same shared column so
            // they stack in the order the LLM provided them; otherwise they'd
            // collide on the same PositionInColumn value.
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = title,
                Description = description,
                Priority = priority,
                Status = TaskStatus.Todo.Value,
                CreatedByUserId = ctx.UserId,
                ColumnId = resolvedSharedColumnId,
                PositionInColumn = sharedBasePosition + created.Count,
                Tags = tags,
                CreatedAt = now,
            };

            _db.Tasks.Add(task);
            created.Add(task);
        }

        if (created.Count == 0)
        {
            return Fail($"All {taskDefs.Count} task definitions failed validation. See errors: {JsonSerializer.Serialize(failed)}");
        }

        await _db.SaveChangesAsync(ct);

        var resultPayload = new
        {
            createdCount = created.Count,
            failedCount = failed.Count,
            tasks = created.Select(t => new { taskId = t.Id, title = t.Title }),
            failures = failed,
        };

        var summary = failed.Count == 0
            ? $"Created {created.Count} tasks."
            : $"Created {created.Count} tasks, skipped {failed.Count}.";

        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(resultPayload),
            UserFacingSummary: summary);
    }

    // --- arg helpers ------------------------------------------------------

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);

    /// <summary>Reads a non-blank string property and truncates it to the accepted length.</summary>
    private static string? ReadStringOrNull(JsonElement obj, string name, int maxLength)
    {
        if (!obj.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.String) return null;
        var value = prop.GetString();
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    /// <summary>Reads a GUID string property when present and parseable.</summary>
    private static Guid? ReadGuidOrNull(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.String) return null;
        return Guid.TryParse(prop.GetString(), out var g) ? g : null;
    }

    /// <summary>Reads string tags, limits their count, and joins them for storage.</summary>
    private static string? ReadTags(JsonElement obj)
    {
        if (!obj.TryGetProperty("tags", out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.Array) return null;
        var values = prop.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(20)
            .ToList();
        if (values.Count == 0) return null;
        var joined = string.Join(",", values);
        return joined.Length > 500 ? joined[..500] : joined;
    }
}
