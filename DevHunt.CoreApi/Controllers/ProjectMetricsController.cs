using System.Globalization;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
// B09-02: Alias to avoid ambiguity with System.Threading.Tasks.TaskStatus
using DomainTaskStatus = DevHunt.CoreApi.Models.TaskStatus;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for project metrics and analytics.
/// Extracted from ProjectsController (R12) to reduce complexity.
///
/// Endpoints:
/// - GET /api/projects/{id}/metrics - Full project metrics
/// - GET /api/projects/{id}/metrics/velocity - Team velocity over time
/// - GET /api/projects/{id}/metrics/contributions - Member contributions
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/metrics")]
[Authorize]
public class ProjectMetricsController : BaseProjectController
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectMetricsController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="auditService">The audit service.</param>
    /// <param name="notificationService">The notification service client.</param>
    /// <param name="eventBus">The event bus service.</param>
    /// <param name="cache">The cache service.</param>
    public ProjectMetricsController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        IEventBusService eventBus,
        ICacheService cache)
        : base(dbContext, auditService, notificationService, eventBus, cache)
    {
    }

    /// <summary>
    /// Gets comprehensive project metrics for active members or owners, combining task, velocity, contribution, burndown, and timeline data.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProjectMetrics(Guid projectId)
    {
        var (project, accessError) = await ValidateProjectAccessAsync(projectId);
        if (accessError != null) return accessError;

        // PERF-01: Run 5 independent DB queries in parallel instead of sequentially
        var taskStatsTask = GetTaskStatisticsAsync(projectId);
        var teamSizeTask = GetActiveTeamSizeAsync(projectId);
        var contributionsTask = GetContributionSummaryAsync(projectId);
        var velocityTask = GetVelocityDataAsync(projectId, weeks: 12);
        var timelineTask = GetTimelineAsync(projectId, limit: 50);

        await Task.WhenAll(taskStatsTask, teamSizeTask, contributionsTask, velocityTask, timelineTask);

        var taskStats = taskStatsTask.Result;
        var burndown = await GetBurndownDataAsync(project!, taskStats.Total);

        return Ok(new
        {
            ProjectId = projectId,
            ProjectTitle = project!.Title,
            Status = project.Status,
            TeamSize = teamSizeTask.Result,
            Tasks = taskStats,
            Velocity = velocityTask.Result,
            Contributions = contributionsTask.Result,
            Burndown = burndown,
            Timeline = timelineTask.Result
        });
    }

    /// <summary>
    /// Gets team velocity for a clamped one-to-fifty-two week window, grouping completed tasks by ISO week.
    /// </summary>
    [HttpGet("velocity")]
    public async Task<IActionResult> GetProjectVelocity(Guid projectId, [FromQuery] int weeks = 12, CancellationToken ct = default)
    {
        var (project, accessError) = await ValidateProjectAccessAsync(projectId);
        if (accessError != null) return accessError;

        weeks = Math.Clamp(weeks, 1, 52);
        var startDate = DateTime.UtcNow.AddDays(-weeks * 7);

        // BUG-01: ISOWeek.GetWeekOfYear cannot be translated by EF Core → filter on server, group on client
        // B09-02: DomainTaskStatus.Done.Value instead of "done"
        var rawTasks = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId &&
                       t.Status == DomainTaskStatus.Done.Value &&
                       !t.IsDeleted &&
                       t.CompletedAt >= startDate)
            .Select(t => new { t.CompletedAt, t.EstimatedHours, t.ActualHours })
            .ToListAsync(ct);

        // Client-side grouping with ISOWeek — safe after filtering on server
        var velocity = rawTasks
            .GroupBy(t => new
            {
                Year = t.CompletedAt!.Value.Year,
                Week = ISOWeek.GetWeekOfYear(t.CompletedAt.Value)
            })
            .Select(g => new
            {
                Week = $"{g.Key.Year}-W{g.Key.Week:D2}",
                TasksCompleted = g.Count(),
                EstimatedHours = g.Sum(t => t.EstimatedHours ?? 0),
                ActualHours = g.Sum(t => t.ActualHours ?? 0)
            })
            .OrderBy(x => x.Week)
            .ToList();

        return Ok(new
        {
            ProjectId = projectId,
            Weeks = weeks,
            Velocity = velocity,
            AverageTasksPerWeek = velocity.Count > 0 ? velocity.Average(v => v.TasksCompleted) : 0,
            AverageEstimatedHoursPerWeek = velocity.Count > 0 ? velocity.Average(v => v.EstimatedHours) : 0,
            AverageActualHoursPerWeek = velocity.Count > 0 ? velocity.Average(v => v.ActualHours) : 0
        });
    }

    /// <summary>
    /// Gets per-member task contribution totals and completion rates for the project.
    /// </summary>
    [HttpGet("contributions")]
    public async Task<IActionResult> GetProjectContributions(Guid projectId, CancellationToken ct = default)
    {
        var (_, accessError) = await ValidateProjectAccessAsync(projectId);
        if (accessError != null) return accessError;

        // B09-02: DomainTaskStatus Value Objects instead of string literals
        var contributions = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId && !t.IsDeleted && t.AssignedToUserId != null)
            .GroupBy(t => t.AssignedToUserId)
            .Select(g => new
            {
                UserId = g.Key!.Value,
                TotalTasks = g.Count(),
                CompletedTasks = g.Count(t => t.Status == DomainTaskStatus.Done.Value),
                InProgressTasks = g.Count(t => t.Status == DomainTaskStatus.InProgress.Value),
                TodoTasks = g.Count(t => t.Status == DomainTaskStatus.Todo.Value),
                TotalEstimatedHours = g.Sum(t => t.EstimatedHours ?? 0),
                TotalActualHours = g.Sum(t => t.ActualHours ?? 0),
                CompletionRate = g.Count() > 0 ? (double)g.Count(t => t.Status == DomainTaskStatus.Done.Value) / g.Count() * 100 : 0
            })
            .Join(_dbContext.Users,
                c => c.UserId,
                u => u.Id,
                (c, u) => new
                {
                    c.UserId,
                    UserName = u.FullName ?? u.Email,
                    UserAvatar = u.AvatarUrl,
                    c.TotalTasks,
                    c.CompletedTasks,
                    c.InProgressTasks,
                    c.TodoTasks,
                    c.TotalEstimatedHours,
                    c.TotalActualHours,
                    c.CompletionRate
                })
            .OrderByDescending(c => c.CompletedTasks)
            .ToListAsync(ct);

        return Ok(new { ProjectId = projectId, Contributions = contributions });
    }

    #region Private Helpers

    /// <summary>
    /// Loads the project and returns an error unless the authenticated user owns it or is an active member.
    /// </summary>
    private async Task<(Project? Project, IActionResult? Error)> ValidateProjectAccessAsync(Guid projectId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);

        if (project == null)
            return (null, NotFound("Project not found"));

        var isOwner = project.OwnerId == userId;
        // B09-02: TeamMemberStatus.Active.Value instead of "active"
        var isMember = await _dbContext.TeamMembers
            .AnyAsync(tm => tm.ProjectId == projectId && tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value, ct);

        if (!isOwner && !isMember)
            return (null, Forbid("Only project members can view metrics"));

        return (project, null);
    }

    /// <summary>
    /// Counts non-deleted tasks by status and calculates the completion percentage.
    /// </summary>
    private async Task<TaskStatistics> GetTaskStatisticsAsync(Guid projectId, CancellationToken ct = default)
    {
        // B09-02: DomainTaskStatus Value Objects instead of string literals
        var stats = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId && !t.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(t => t.Status == DomainTaskStatus.Done.Value),
                InProgress = g.Count(t => t.Status == DomainTaskStatus.InProgress.Value),
                Todo = g.Count(t => t.Status == DomainTaskStatus.Todo.Value)
            })
            .FirstOrDefaultAsync(ct);

        var total = stats?.Total ?? 0;
        var completed = stats?.Completed ?? 0;

        return new TaskStatistics
        {
            Total = total,
            Completed = completed,
            InProgress = stats?.InProgress ?? 0,
            Todo = stats?.Todo ?? 0,
            CompletionRate = total > 0 ? (double)completed / total * 100 : 0
        };
    }

    /// <summary>
    /// Counts active team members for the project.
    /// </summary>
    private async Task<int> GetActiveTeamSizeAsync(Guid projectId)
    {
        // B09-02: TeamMemberStatus.Active.Value instead of "active"
        return await _dbContext.TeamMembers
            .CountAsync(tm => tm.ProjectId == projectId && tm.Status == TeamMemberStatus.Active.Value);
    }

    /// <summary>
    /// Builds a contribution summary for users with completed assigned tasks.
    /// </summary>
    private async Task<object> GetContributionSummaryAsync(Guid projectId, CancellationToken ct = default)
    {
        // B09-02: DomainTaskStatus.Done.Value instead of "done"
        return await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId && t.Status == DomainTaskStatus.Done.Value && !t.IsDeleted && t.AssignedToUserId != null)
            .GroupBy(t => t.AssignedToUserId)
            .Select(g => new
            {
                UserId = g.Key!.Value,
                CompletedTasks = g.Count(),
                TotalEstimatedHours = g.Sum(t => t.EstimatedHours ?? 0),
                TotalActualHours = g.Sum(t => t.ActualHours ?? 0)
            })
            .Join(_dbContext.Users,
                c => c.UserId,
                u => u.Id,
                (c, u) => new
                {
                    c.UserId,
                    UserName = u.FullName ?? u.Email,
                    c.CompletedTasks,
                    c.TotalEstimatedHours,
                    c.TotalActualHours
                })
            .OrderByDescending(c => c.CompletedTasks)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Builds compact weekly completed-task velocity data for the supplied lookback window.
    /// </summary>
    private async Task<object> GetVelocityDataAsync(Guid projectId, int weeks, CancellationToken ct = default)
    {
        // BUG-02: Apply the sliding window — parameter was ignored before this fix
        var startDate = DateTime.UtcNow.AddDays(-weeks * 7);

        // BUG-01: ISOWeek.GetWeekOfYear cannot be translated by EF Core → filter on server, group on client
        // B09-02: DomainTaskStatus.Done.Value instead of "done"
        var rawTasks = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId
                     && t.Status == DomainTaskStatus.Done.Value
                     && !t.IsDeleted
                     && t.CompletedAt >= startDate)
            .Select(t => new { t.CompletedAt })
            .ToListAsync(ct);

        var tasksCompletedByWeek = rawTasks
            .GroupBy(t => new
            {
                Year = t.CompletedAt!.Value.Year,
                Week = ISOWeek.GetWeekOfYear(t.CompletedAt.Value)
            })
            .Select(g => new
            {
                Week = $"{g.Key.Year}-W{g.Key.Week:D2}",
                TasksCompleted = g.Count()
            })
            .OrderBy(x => x.Week)
            .ToList();

        return new
        {
            TasksCompletedByWeek = tasksCompletedByWeek,
            AverageTasksPerWeek = tasksCompletedByWeek.Count > 0
                ? tasksCompletedByWeek.Average(x => x.TasksCompleted)
                : 0
        };
    }

    /// <summary>
    /// Builds actual and ideal burndown series from project start through the last ninety days.
    /// </summary>
    private async Task<object> GetBurndownDataAsync(Project project, int totalTasks, CancellationToken ct = default)
    {
        var projectStartDate = project.StartDate ?? project.CreatedAt;
        var daysSinceStart = (DateTime.UtcNow - projectStartDate).Days;
        var maxDays = Math.Min(daysSinceStart, 90);

        var burndownData = new List<object>();

        // Batch query for performance
        var tasksByDate = await _dbContext.Tasks
            .Where(t => t.ProjectId == project.Id && !t.IsDeleted)
            .Select(t => new { t.CreatedAt, t.CompletedAt, t.Status })
            .ToListAsync(ct);

        for (int i = 0; i <= maxDays; i++)
        {
            var date = projectStartDate.AddDays(i);
            // B09-02: DomainTaskStatus.Done.Value instead of "done"
            var tasksRemaining = tasksByDate.Count(t =>
                t.CreatedAt <= date &&
                (t.Status != DomainTaskStatus.Done.Value || (t.CompletedAt.HasValue && t.CompletedAt.Value > date)));

            burndownData.Add(new { Date = date.ToString("yyyy-MM-dd"), TasksRemaining = tasksRemaining });
        }

        // Ideal burndown line
        var idealBurndown = totalTasks > 0 && daysSinceStart > 0
            ? Enumerable.Range(0, maxDays + 1)
                .Select(i => new
                {
                    Date = projectStartDate.AddDays(i).ToString("yyyy-MM-dd"),
                    TasksRemaining = Math.Max(0, totalTasks - (int)(totalTasks * (double)i / daysSinceStart))
                })
                .ToList<object>()
            : new List<object>();

        return new { Data = burndownData, IdealBurndown = idealBurndown };
    }

    /// <summary>
    /// Gets the most recent non-deleted task timeline entries up to the supplied limit.
    /// </summary>
    private async Task<object> GetTimelineAsync(Guid projectId, int limit, CancellationToken ct = default)
    {
        return await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId && !t.IsDeleted)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Status,
                t.CreatedAt,
                t.CompletedAt,
                t.EstimatedHours,
                t.ActualHours
            })
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Aggregated counts and completion percentage for a project's non-deleted tasks.
    /// </summary>
    private sealed class TaskStatistics
    {
        /// <summary>
        /// Total number of non-deleted tasks.
        /// </summary>
        public int Total { get; init; }
        /// <summary>
        /// Number of completed tasks.
        /// </summary>
        public int Completed { get; init; }
        /// <summary>
        /// Number of tasks currently in progress.
        /// </summary>
        public int InProgress { get; init; }
        /// <summary>
        /// Number of tasks still in the todo state.
        /// </summary>
        public int Todo { get; init; }
        /// <summary>
        /// Percentage of non-deleted tasks that are completed.
        /// </summary>
        public double CompletionRate { get; init; }
    }

    #endregion
}
