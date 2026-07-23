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
/// Implements the <c>create_task</c> tool. Creates a real <see cref="TaskItem"/>
/// inside the project bound to the current conversation. Authorization is
/// enforced server-side via <see cref="IProjectPermissionService"/>. Never
/// trust the LLM's argument for "which project".
/// </summary>
public sealed class CreateTaskTool : IAiTool
{
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
    /// Initializes a new instance of the <see cref="CreateTaskTool"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="permissions">Project permission service for authorization checks.</param>
    public CreateTaskTool(DevHuntDbContext db, IProjectPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public string Name => "create_task";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("create_task can only be invoked from a project conversation.");
        }

        // Authorization is anchored to the conversation's project. The LLM
        // could otherwise pass any project id in arguments.
        var perms = await _permissions.GetPermissionsAsync(projectId, ctx.UserId, isAdmin: false, ct);
        if (perms is null) return Fail("Project not found.");
        if (!perms.CanManageTasks) return Fail("You don't have permission to create tasks in this project.");

        if (!args.TryGetProperty("title", out var titleProp) || titleProp.ValueKind != JsonValueKind.String)
        {
            return Fail("Argument 'title' is required.");
        }

        var title = (titleProp.GetString() ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 200)
        {
            return Fail("Argument 'title' must be 1-200 characters.");
        }

        var description = ReadStringOrNull(args, "description", maxLength: 1000);
        if (description != null)
        {
            description = SecurityHelpers.SanitizeHtml(description);
        }
        var priority = ReadStringOrNull(args, "priority", maxLength: 32);
        if (priority != null && !AllowedPriorities.Contains(priority))
        {
            return Fail($"Argument 'priority' must be one of: {string.Join(", ", AllowedPriorities)}.");
        }
        priority ??= TaskPriority.Medium.Value;

        var deadline = ReadDeadline(args);
        var assigneeId = ReadGuidOrNull(args, "assigneeId");
        var columnId = ReadGuidOrNull(args, "columnId");
        var tags = ReadTags(args);
        var estimatedHours = ReadFloatOrNull(args, "estimatedHours");

        if (assigneeId.HasValue)
        {
            // Cross-check: assignee must be an active member of THIS project.
            // Otherwise the LLM could attach tasks to outsiders.
            var assigneeIsMember = await _db.TeamMembers.AnyAsync(
                tm => tm.ProjectId == projectId
                      && tm.UserId == assigneeId.Value
                      && tm.Status == TeamMemberStatus.Active.Value,
                ct);
            var assigneeIsOwner = await _db.Projects.AnyAsync(
                p => p.Id == projectId && p.OwnerId == assigneeId.Value, ct);
            if (!assigneeIsMember && !assigneeIsOwner)
            {
                return Fail("Assignee is not a member of this project.");
            }
        }

        Guid? resolvedColumnId = null;
        var position = 0;
        if (columnId.HasValue)
        {
            var column = await _db.TaskColumns
                .FirstOrDefaultAsync(c => c.Id == columnId.Value && c.ProjectId == projectId, ct);
            if (column == null) return Fail("columnId not found in this project.");
            resolvedColumnId = column.Id;
            position = await GetNextPositionInColumnAsync(column.Id, ct);
        }
        else
        {
            // Default: first column for this project, if any. We don't crash
            // on missing column setup. TasksController allows null and
            // surfaces the task in the implicit default lane.
            var firstColumn = await _db.TaskColumns
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.Position)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(ct);
            if (firstColumn != null)
            {
                resolvedColumnId = firstColumn.Id;
                position = await GetNextPositionInColumnAsync(firstColumn.Id, ct);
            }
        }

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            Description = description,
            Priority = priority,
            Status = TaskStatus.Todo.Value,
            CreatedByUserId = ctx.UserId,
            AssignedToUserId = assigneeId,
            Deadline = deadline,
            EstimatedHours = estimatedHours,
            ColumnId = resolvedColumnId,
            PositionInColumn = position,
            Tags = tags,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);

        var resultPayload = new
        {
            taskId = task.Id,
            title = task.Title,
            status = task.Status,
            priority = task.Priority,
            columnId = task.ColumnId,
            assigneeId = task.AssignedToUserId,
        };

        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(resultPayload),
            UserFacingSummary: $"Created task \"{task.Title}\".");
    }

    // --- argument helpers --------------------------------------------------

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);

    /// <summary>Reads a non-blank string property and truncates it to the accepted length.</summary>
    private static string? ReadStringOrNull(JsonElement args, string name, int maxLength)
    {
        if (!args.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.String) return null;
        var value = prop.GetString();
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    /// <summary>Reads a GUID string property when present and parseable.</summary>
    private static Guid? ReadGuidOrNull(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.String) return null;
        return Guid.TryParse(prop.GetString(), out var g) ? g : null;
    }

    /// <summary>Reads a deadline string and converts it to UTC when parseable.</summary>
    private static DateTime? ReadDeadline(JsonElement args)
    {
        if (!args.TryGetProperty("deadline", out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.String) return null;
        var raw = prop.GetString();
        return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt)
            ? dt
            : null;
    }

    /// <summary>Reads a numeric property as a nullable single-precision value.</summary>
    private static float? ReadFloatOrNull(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.Number) return null;
        return prop.TryGetSingle(out var f) ? f : null;
    }

    /// <summary>Reads string tags, limits their count, and joins them for storage.</summary>
    private static string? ReadTags(JsonElement args)
    {
        if (!args.TryGetProperty("tags", out var prop)) return null;
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

    /// <summary>Returns the next position at the end of a task column.</summary>
    private async Task<int> GetNextPositionInColumnAsync(Guid columnId, CancellationToken ct)
    {
        return (await _db.Tasks
            .Where(t => t.ColumnId == columnId && !t.IsDeleted)
            .MaxAsync(t => (int?)t.PositionInColumn, ct) ?? -1) + 1;
    }
}
