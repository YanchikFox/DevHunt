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
/// Controller for managing typed relationships between tasks.
/// </summary>
/// <remarks>
/// Supports various link types:
/// - blocks: Source task blocks the target
/// - blocked_by: Source is blocked by target
/// - depends_on: Source depends on target completion
/// - related_to: Informational link
/// - duplicate_of: Source is a duplicate of target
/// - parent_of: Source is a parent task (epic/story)
/// - child_of: Source is a subtask
///
/// Routes: api/projects/{projectId}/tasks/{taskId}/links
/// </remarks>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/links")]
[Authorize]
public class TaskLinksController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;
    private readonly IActivityLogService _activityLogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskLinksController"/> class.
    /// </summary>
    /// <param name="db">Database context used to read projects, tasks, memberships, and task links.</param>
    /// <param name="cache">Cache service used to invalidate affected project entries after link changes.</param>
    /// <param name="activityLogService">Activity logger used to record link creation and deletion events.</param>
    public TaskLinksController(DevHuntDbContext db, ICacheService cache, IActivityLogService activityLogService)
    {
        _db = db;
        _cache = cache;
        _activityLogService = activityLogService;
    }

    /// <summary>Task link response DTO.</summary>
    /// <param name="Id">Link identifier.</param>
    /// <param name="SourceTaskId">Source task ID.</param>
    /// <param name="SourceTaskTitle">Source task title.</param>
    /// <param name="TargetTaskId">Target task ID.</param>
    /// <param name="TargetTaskTitle">Target task title.</param>
    /// <param name="LinkType">Link type (blocks, depends_on, etc.).</param>
    /// <param name="CreatedByUserId">Creator user ID.</param>
    /// <param name="CreatedAt">Creation timestamp.</param>
    public record TaskLinkDto(
        Guid Id,
        Guid SourceTaskId,
        string SourceTaskTitle,
        Guid TargetTaskId,
        string TargetTaskTitle,
        string LinkType,
        Guid CreatedByUserId,
        DateTime CreatedAt);

    /// <summary>Request to create a task link.</summary>
    /// <param name="TargetTaskId">Target task identifier.</param>
    /// <param name="LinkType">Link type.</param>
    public record CreateTaskLinkRequest(
        Guid TargetTaskId,
        string LinkType);

    private static readonly HashSet<string> DependencyLinkTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "depends_on",
        "blocked_by",
        "blocks",
    };

    /// <summary>
    /// Reads the authenticated user's identifier from claims and fails fast when authentication middleware did not provide it.
    /// </summary>
    /// <returns>The current user's ID.</returns>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Lists outgoing and incoming links for a non-deleted task in a project the user can access.
    /// </summary>
    /// <param name="projectId">Project containing the task.</param>
    /// <param name="taskId">Task whose links should be returned.</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>
    /// Separate outgoing and incoming link collections; returns 403 without project access, 404 when the project or task is missing.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetLinks(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await HasProjectAccessAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can view task links" });
        }

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (task == null) return NotFound("Task not found");

        // Get both outgoing and incoming links
        var outgoingLinks = await _db.TaskLinks
            .Where(l => l.SourceTaskId == taskId)
            .Include(l => l.TargetTask)
            .Select(l => new TaskLinkDto(
                l.Id,
                l.SourceTaskId,
                l.SourceTask!.Title,
                l.TargetTaskId,
                l.TargetTask!.Title,
                l.LinkType,
                l.CreatedByUserId,
                l.CreatedAt))
            .ToListAsync(ct);

        var incomingLinks = await _db.TaskLinks
            .Where(l => l.TargetTaskId == taskId)
            .Include(l => l.SourceTask)
            .Select(l => new TaskLinkDto(
                l.Id,
                l.SourceTaskId,
                l.SourceTask!.Title,
                l.TargetTaskId,
                l.TargetTask!.Title,
                l.LinkType,
                l.CreatedByUserId,
                l.CreatedAt))
            .ToListAsync(ct);

        return Ok(new
        {
            outgoing = outgoingLinks,
            incoming = incomingLinks
        });
    }

    /// <summary>
    /// Creates a typed link from the route task to a target task, adding an inverse link when the type defines one.
    /// </summary>
    /// <param name="projectId">Project containing the source task and used for authorization.</param>
    /// <param name="taskId">Source task for the new link.</param>
    /// <param name="req">Target task ID and link type.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>
    /// The created source link; returns 400 for invalid types, self-links, duplicates, or dependency cycles,
    /// 403 when the user cannot edit tasks, and 404 when the project or either task is missing.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> CreateLink(Guid projectId, Guid taskId, [FromBody] CreateTaskLinkRequest req, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await CanEditTasksAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can create task links" });
        }

        // Validate link type
        if (!TaskLink.ValidLinkTypes.Contains(req.LinkType))
        {
            return BadRequest($"Invalid link type. Valid types: {string.Join(", ", TaskLink.ValidLinkTypes)}");
        }

        // Validate source task
        var sourceTask = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted, ct);
        if (sourceTask == null) return NotFound("Source task not found");

        // Validate target task (can be in the same or different project)
        var targetTask = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == req.TargetTaskId && !t.IsDeleted, ct);
        if (targetTask == null) return NotFound("Target task not found");

        // Prevent self-linking
        if (taskId == req.TargetTaskId)
        {
            return BadRequest("A task cannot link to itself");
        }

        // Check for duplicate link
        var existingLink = await _db.TaskLinks.FirstOrDefaultAsync(l =>
            l.SourceTaskId == taskId &&
            l.TargetTaskId == req.TargetTaskId &&
            l.LinkType == req.LinkType, ct);

        if (existingLink != null)
        {
            return BadRequest("This link already exists");
        }

        if (await WouldCreateDependencyCycleAsync(projectId, taskId, req.TargetTaskId, req.LinkType))
        {
            return BadRequest("Cannot create link because it would introduce a dependency cycle.");
        }

        var link = new TaskLink
        {
            Id = Guid.NewGuid(),
            SourceTaskId = taskId,
            TargetTaskId = req.TargetTaskId,
            LinkType = req.LinkType,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.TaskLinks.Add(link);

        // Create inverse link for bidirectional relationships
        var inverseLinkType = TaskLink.GetInverseLinkType(req.LinkType);
        if (inverseLinkType != null)
        {
            var inverseExists = await _db.TaskLinks.AnyAsync(l =>
                l.SourceTaskId == req.TargetTaskId &&
                l.TargetTaskId == taskId &&
                l.LinkType == inverseLinkType, ct);

            if (!inverseExists)
            {
                var inverseLink = new TaskLink
                {
                    Id = Guid.NewGuid(),
                    SourceTaskId = req.TargetTaskId,
                    TargetTaskId = taskId,
                    LinkType = inverseLinkType,
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _db.TaskLinks.Add(inverseLink);
            }
        }

        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId);

        await _activityLogService.LogProjectEventAsync(
            projectId,
            userId,
            "task.linked",
            $"Linked task: {sourceTask.Title} ({req.LinkType}) → {targetTask.Title}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { sourceTaskId = taskId, targetTaskId = req.TargetTaskId, linkType = req.LinkType });

        return Ok(new TaskLinkDto(
            link.Id,
            link.SourceTaskId,
            sourceTask.Title,
            link.TargetTaskId,
            targetTask.Title,
            link.LinkType,
            link.CreatedByUserId,
            link.CreatedAt));
    }

    /// <summary>
    /// Deletes a task link attached to the route task and removes the matching inverse link when present.
    /// </summary>
    /// <param name="projectId">Project used for authorization and cache invalidation.</param>
    /// <param name="taskId">Task that must participate in the link.</param>
    /// <param name="linkId">Link to delete.</param>
    /// <param name="ct">Cancellation token for database work.</param>
    /// <returns>204 after deletion, 403 when the user cannot edit tasks, or 404 when the project or link is missing.</returns>
    [HttpDelete("{linkId:guid}")]
    public async Task<IActionResult> DeleteLink(Guid projectId, Guid taskId, Guid linkId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return NotFound("Project not found");

        if (!await CanEditTasksAsync(projectId, userId, project.OwnerId))
        {
            return StatusCode(403, new { error = "Access denied", message = "Only project owner and team members can delete task links" });
        }

        var link = await _db.TaskLinks
            .Include(l => l.SourceTask)
            .Include(l => l.TargetTask)
            .FirstOrDefaultAsync(l => l.Id == linkId && (l.SourceTaskId == taskId || l.TargetTaskId == taskId), ct);

        if (link == null) return NotFound("Link not found");

        // Also remove inverse link if it exists
        var inverseLinkType = TaskLink.GetInverseLinkType(link.LinkType);
        if (inverseLinkType != null)
        {
            var inverseLink = await _db.TaskLinks.FirstOrDefaultAsync(l =>
                l.SourceTaskId == link.TargetTaskId &&
                l.TargetTaskId == link.SourceTaskId &&
                l.LinkType == inverseLinkType, ct);

            if (inverseLink != null)
            {
                _db.TaskLinks.Remove(inverseLink);
            }
        }

        var sourceTitle = link.SourceTask?.Title ?? "Unknown";
        var targetTitle = link.TargetTask?.Title ?? "Unknown";
        var linkType = link.LinkType;

        _db.TaskLinks.Remove(link);
        await _db.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId);

        await _activityLogService.LogProjectEventAsync(
            projectId,
            userId,
            "task.unlinked",
            $"Unlinked task: {sourceTitle} ({linkType}) → {targetTitle}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { sourceTaskId = link.SourceTaskId, targetTaskId = link.TargetTaskId, linkType });

        return NoContent();
    }

    /// <summary>
    /// Checks whether the user is the project owner or an active team member who can view links.
    /// </summary>
    /// <param name="projectId">Project being accessed.</param>
    /// <param name="userId">User requesting access.</param>
    /// <param name="ownerId">Project owner's user ID.</param>
    /// <param name="ct">Cancellation token for the membership lookup.</param>
    /// <returns><see langword="true"/> when the user can view task links.</returns>
    private async Task<bool> HasProjectAccessAsync(Guid projectId, Guid userId, Guid ownerId, CancellationToken ct = default)
    {
        if (ownerId == userId) return true;
        return await _db.TeamMembers.AnyAsync(tm =>
            tm.ProjectId == projectId &&
            tm.UserId == userId &&
            tm.Status == TeamMemberStatus.Active.Value, ct);
    }

    /// <summary>
    /// Checks whether the user is the project owner or an active team member who can modify task links.
    /// </summary>
    /// <param name="projectId">Project whose links may be changed.</param>
    /// <param name="userId">User requesting the change.</param>
    /// <param name="ownerId">Project owner's user ID.</param>
    /// <param name="ct">Cancellation token for the membership lookup.</param>
    /// <returns><see langword="true"/> when the user can create or delete links.</returns>
    private async Task<bool> CanEditTasksAsync(Guid projectId, Guid userId, Guid ownerId, CancellationToken ct = default)
    {
        if (ownerId == userId) return true;
        return await _db.TeamMembers.AnyAsync(tm =>
            tm.ProjectId == projectId &&
            tm.UserId == userId &&
            tm.Status == TeamMemberStatus.Active.Value, ct);
    }

    /// <summary>
    /// Converts a dependency-style link into a directed graph edge from dependent task to prerequisite task.
    /// </summary>
    /// <param name="linkType">Task link type to interpret.</param>
    /// <param name="sourceTaskId">Source task stored on the link.</param>
    /// <param name="targetTaskId">Target task stored on the link.</param>
    /// <returns>The directed dependency edge, or <see langword="null"/> when the link type is not dependency-bearing.</returns>
    private static (Guid From, Guid To)? GetDependencyEdge(string linkType, Guid sourceTaskId, Guid targetTaskId)
    {
        return linkType switch
        {
            "depends_on" => (sourceTaskId, targetTaskId),
            "blocked_by" => (sourceTaskId, targetTaskId),
            "blocks" => (targetTaskId, sourceTaskId),
            _ => null,
        };
    }

    /// <summary>
    /// Determines whether a directed path exists between two tasks in the dependency graph.
    /// </summary>
    /// <param name="adjacency">Dependency graph keyed by task ID.</param>
    /// <param name="start">Task where traversal begins.</param>
    /// <param name="target">Task that would complete the path.</param>
    /// <returns><see langword="true"/> when the target can be reached from the start task.</returns>
    private static bool HasPath(Dictionary<Guid, HashSet<Guid>> adjacency, Guid start, Guid target)
    {
        if (start == target) return true;

        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current)) continue;
            if (current == target) return true;

            if (!adjacency.TryGetValue(current, out var neighbors)) continue;
            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    stack.Push(neighbor);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether adding a dependency-style link would create a cycle among non-deleted tasks in the same project.
    /// </summary>
    /// <param name="projectId">Project whose dependency graph is evaluated.</param>
    /// <param name="sourceTaskId">Source task for the proposed link.</param>
    /// <param name="targetTaskId">Target task for the proposed link.</param>
    /// <param name="linkType">Proposed link type.</param>
    /// <param name="ct">Cancellation token for loading existing links.</param>
    /// <returns><see langword="true"/> when the proposed link would introduce a dependency cycle.</returns>
    private async Task<bool> WouldCreateDependencyCycleAsync(Guid projectId, Guid sourceTaskId, Guid targetTaskId, string linkType, CancellationToken ct = default)
    {
        var edge = GetDependencyEdge(linkType, sourceTaskId, targetTaskId);
        if (edge == null) return false;

        var dependencyLinks = await _db.TaskLinks
            .Where(l => DependencyLinkTypes.Contains(l.LinkType))
            .Include(l => l.SourceTask)
            .Include(l => l.TargetTask)
            .Where(l =>
                l.SourceTask != null &&
                l.TargetTask != null &&
                l.SourceTask.ProjectId == projectId &&
                l.TargetTask.ProjectId == projectId &&
                !l.SourceTask.IsDeleted &&
                !l.TargetTask.IsDeleted)
            .Select(l => new { l.LinkType, l.SourceTaskId, l.TargetTaskId })
            .ToListAsync(ct);

        var adjacency = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var link in dependencyLinks)
        {
            var existingEdge = GetDependencyEdge(link.LinkType, link.SourceTaskId, link.TargetTaskId);
            if (existingEdge == null) continue;
            var (from, to) = existingEdge.Value;

            if (!adjacency.TryGetValue(from, out var neighbors))
            {
                neighbors = new HashSet<Guid>();
                adjacency[from] = neighbors;
            }

            neighbors.Add(to);
        }

        var (newFrom, newTo) = edge.Value;
        return HasPath(adjacency, newTo, newFrom);
    }

    /// <summary>
    /// Removes cached project details and project-list entries affected by task link changes.
    /// </summary>
    /// <param name="projectId">Project whose cache entries should be removed.</param>
    private async Task InvalidateProjectCacheAsync(Guid projectId)
    {
        await _cache.RemoveAsync($"project:{projectId}");
        await _cache.RemoveByPatternAsync("projects:*");
    }
}
