using System.Text.Json;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Implements <c>delete_task</c>: soft-delete only. Hard delete stays out
/// of AI tool reach: the cost of an LLM hallucinating a deletion is too
/// high, and IsDeleted=true gives the team a chance to undo from the trash UI.
/// </summary>
public sealed class DeleteTaskTool : IAiTool
{
    private readonly DevHuntDbContext _db;
    private readonly IProjectPermissionService _permissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTaskTool"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="permissions">Project permission service for authorization checks.</param>
    public DeleteTaskTool(DevHuntDbContext db, IProjectPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public string Name => "delete_task";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("delete_task can only be invoked from a project conversation.");
        }

        if (!args.TryGetProperty("taskId", out var idProp) || idProp.ValueKind != JsonValueKind.String
            || !Guid.TryParse(idProp.GetString(), out var taskId))
        {
            return Fail("Argument 'taskId' must be a valid GUID.");
        }

        var task = await _db.Tasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (task == null) return Fail("Task not found in this project (or already deleted).");

        var perms = await _permissions.GetPermissionsAsync(projectId, ctx.UserId, isAdmin: false, ct);
        if (perms is null || !perms.CanManageTasks)
        {
            return Fail("You don't have permission to delete tasks in this project.");
        }

        task.IsDeleted = true;
        task.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(new
            {
                taskId = task.Id,
                title = task.Title,
                softDeleted = true,
            }),
            UserFacingSummary: $"Deleted \"{task.Title}\".");
    }

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);
}
