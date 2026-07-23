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
/// Implements the <c>update_task</c> tool for mutating an existing <see cref="TaskItem"/> by id with permission checks.
/// </summary>
public sealed class UpdateTaskTool : IAiTool
{
    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        TaskPriority.Low.Value,
        TaskPriority.Medium.Value,
        TaskPriority.High.Value,
        TaskPriority.Urgent.Value,
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        TaskStatus.Todo.Value,
        TaskStatus.Doing.Value,
        TaskStatus.Review.Value,
        TaskStatus.Done.Value,
        TaskStatus.Archived.Value,
        TaskStatus.Cancelled.Value,
    };

    private readonly DevHuntDbContext _db;
    private readonly IProjectPermissionService _permissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTaskTool"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="permissions">Project permission service for authorization checks.</param>
    public UpdateTaskTool(DevHuntDbContext db, IProjectPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public string Name => "update_task";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("update_task can only be invoked from a project conversation.");
        }

        if (!args.TryGetProperty("taskId", out var idProp) || idProp.ValueKind != JsonValueKind.String
            || !Guid.TryParse(idProp.GetString(), out var taskId))
        {
            return Fail("Argument 'taskId' must be a valid GUID.");
        }

        // Pin to the conversation's project. Even if the LLM hands us a real
        // task id from another project, we refuse it.
        var task = await _db.Tasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (task == null) return Fail("Task not found in this project.");

        var perms = await _permissions.GetPermissionsAsync(projectId, ctx.UserId, isAdmin: false, ct);
        if (perms is null || !perms.CanManageTasks)
        {
            return Fail("You don't have permission to update tasks in this project.");
        }

        var changes = new List<string>();

        if (TryReadTrimmed(args, "title", 200, out var title))
        {
            if (string.IsNullOrEmpty(title)) return Fail("Argument 'title' must be non-empty when provided.");
            task.Title = title;
            changes.Add("title");
        }

        if (TryReadTrimmed(args, "description", 1000, out var description))
        {
            task.Description = string.IsNullOrWhiteSpace(description)
                ? null
                : SecurityHelpers.SanitizeHtml(description);
            changes.Add("description");
        }

        if (TryReadTrimmed(args, "priority", 32, out var priority))
        {
            if (!AllowedPriorities.Contains(priority))
            {
                return Fail($"Argument 'priority' must be one of: {string.Join(", ", AllowedPriorities)}.");
            }
            task.Priority = priority;
            changes.Add("priority");
        }

        if (TryReadTrimmed(args, "status", 50, out var status))
        {
            if (!AllowedStatuses.Contains(status))
            {
                return Fail($"Argument 'status' must be one of: {string.Join(", ", AllowedStatuses)}.");
            }
            task.Status = status;
            // Auto-stamp completion timestamp when transitioning to "done"
            // (matches TasksController's implicit contract; otherwise board
            // analytics under-count completions made via AI).
            if (string.Equals(status, TaskStatus.Done.Value, StringComparison.OrdinalIgnoreCase))
            {
                task.CompletedAt = DateTime.UtcNow;
            }
            changes.Add("status");
        }

        if (TryReadGuid(args, "assigneeId", out var assigneeId))
        {
            if (assigneeId is Guid newAssignee)
            {
                var ok = await _db.TeamMembers.AnyAsync(
                    tm => tm.ProjectId == projectId
                          && tm.UserId == newAssignee
                          && tm.Status == TeamMemberStatus.Active.Value, ct)
                    || await _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == newAssignee, ct);
                if (!ok) return Fail("Assignee is not a member of this project.");
            }
            task.AssignedToUserId = assigneeId;
            changes.Add("assigneeId");
        }

        if (TryReadFloat(args, "estimatedHours", out var estimated))
        {
            task.EstimatedHours = estimated;
            changes.Add("estimatedHours");
        }

        if (TryReadFloat(args, "actualHours", out var actual))
        {
            task.ActualHours = actual;
            changes.Add("actualHours");
        }

        if (TryReadDeadline(args, out var deadline))
        {
            task.Deadline = deadline;
            changes.Add("deadline");
        }

        if (TryReadTags(args, out var tags))
        {
            task.Tags = tags;
            changes.Add("tags");
        }

        if (changes.Count == 0)
        {
            return Fail("No updatable fields were provided.");
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(new
            {
                taskId = task.Id,
                title = task.Title,
                status = task.Status,
                priority = task.Priority,
                changedFields = changes,
            }),
            UserFacingSummary: $"Updated task \"{task.Title}\" ({string.Join(", ", changes)}).");
    }

    // --- arg helpers ------------------------------------------------------
    // The bool return signals "the field was present and successfully read";
    // tools branch on this to decide whether to mutate the entity. A null
    // value can still be a valid update (e.g. clearing assigneeId).

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);

    /// <summary>Reads an optional string property, trims it, and truncates it to the accepted length.</summary>
    private static bool TryReadTrimmed(JsonElement args, string name, int maxLength, out string value)
    {
        value = string.Empty;
        if (!args.TryGetProperty(name, out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Null) { value = string.Empty; return true; }
        if (prop.ValueKind != JsonValueKind.String) return false;
        var raw = prop.GetString() ?? string.Empty;
        var trimmed = raw.Trim();
        value = trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
        return true;
    }

    /// <summary>Reads an optional GUID property, treating explicit null as a valid clear operation.</summary>
    private static bool TryReadGuid(JsonElement args, string name, out Guid? value)
    {
        value = null;
        if (!args.TryGetProperty(name, out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Null) return true;
        if (prop.ValueKind != JsonValueKind.String) return false;
        if (!Guid.TryParse(prop.GetString(), out var parsed)) return false;
        value = parsed;
        return true;
    }

    /// <summary>Reads an optional numeric property as a single-precision value.</summary>
    private static bool TryReadFloat(JsonElement args, string name, out float? value)
    {
        value = null;
        if (!args.TryGetProperty(name, out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Null) return true;
        if (prop.ValueKind != JsonValueKind.Number) return false;
        if (!prop.TryGetSingle(out var f)) return false;
        value = f;
        return true;
    }

    /// <summary>Reads an optional deadline string and normalizes it to UTC when parseable.</summary>
    private static bool TryReadDeadline(JsonElement args, out DateTime? value)
    {
        value = null;
        if (!args.TryGetProperty("deadline", out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Null) return true;
        if (prop.ValueKind != JsonValueKind.String) return false;
        var raw = prop.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!DateTime.TryParse(raw, null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var dt)) return false;
        value = dt;
        return true;
    }

    /// <summary>Reads up to 20 string tags and joins them into the stored comma-separated format.</summary>
    private static bool TryReadTags(JsonElement args, out string? value)
    {
        value = null;
        if (!args.TryGetProperty("tags", out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Null) return true;
        if (prop.ValueKind != JsonValueKind.Array) return false;
        var values = prop.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(20)
            .ToList();
        var joined = string.Join(",", values);
        value = joined.Length > 500 ? joined[..500] : joined;
        return true;
    }
}
