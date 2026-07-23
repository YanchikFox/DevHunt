using System.Data;
using DevHunt.CoreApi.Models;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Outcome status from <see cref="IAiPlanApplier.ApplyAsync"/>.
/// </summary>
public enum AiPlanApplyStatus
{
    Applied,
    AlreadyApplied,
    NotFound
}

/// <summary>
/// Result of applying an <see cref="AiPlanDraft"/> to a project board.
/// </summary>
/// <param name="Status">Whether apply succeeded, was skipped, or the plan was missing.</param>
/// <param name="TaskCount">Tasks created when status is <see cref="AiPlanApplyStatus.Applied"/>.</param>
/// <param name="LinkCount">Dependency links created on apply.</param>
/// <param name="AppliedAt">UTC timestamp when the plan row was marked applied.</param>
/// <param name="ConflictStatus">Current plan status when apply could not proceed (for example already applying).</param>
public sealed record AiPlanApplyResult(
    AiPlanApplyStatus Status,
    int TaskCount = 0,
    int LinkCount = 0,
    DateTime? AppliedAt = null,
    string? ConflictStatus = null
);

/// <summary>
/// Materializes a validated <see cref="AiPlanDraft"/> as board columns, tasks, and dependency links.
/// </summary>
public interface IAiPlanApplier
{
    /// <summary>
    /// Atomically transitions a draft plan to applying, creates columns and tasks, wires dependencies,
    /// and marks the plan applied inside a serializable transaction.
    /// </summary>
    /// <param name="projectId">Target project.</param>
    /// <param name="planId">AI plan row to apply.</param>
    /// <param name="userId">User performing the apply (task creator and link author).</param>
    /// <param name="draft">Validated plan content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Apply outcome with counts or conflict status.</returns>
    Task<AiPlanApplyResult> ApplyAsync(Guid projectId, Guid planId, Guid userId, AiPlanDraft draft, CancellationToken cancellationToken);
}

/// <summary>
/// Creates <see cref="TaskColumn"/> and <see cref="TaskItem"/> rows from an AI plan draft
/// and records dependency <see cref="TaskLink"/> edges. Invoked by <see cref="AiPlanningService.ApplyPlanAsync"/>.
/// </summary>
public sealed class AiPlanApplier : IAiPlanApplier
{
    private readonly DevHuntDbContext _db;
    private readonly AiPlanningOptions _options;
    private readonly ILogger<AiPlanApplier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiPlanApplier"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="options">Configuration options for AI plan limits.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public AiPlanApplier(
        DevHuntDbContext db,
        IOptions<AiPlanningOptions> options,
        ILogger<AiPlanApplier> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AiPlanApplyResult> ApplyAsync(
        Guid projectId,
        Guid planId,
        Guid userId,
        AiPlanDraft draft,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = DateTime.UtcNow;

        var updated = await _db.AiPlans
            .Where(p => p.Id == planId && p.ProjectId == projectId && p.Status == AiPlanStatus.Draft.Value)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.Status, AiPlanStatus.Applying.Value)
                    .SetProperty(p => p.UpdatedAt, now),
                cancellationToken);

        if (updated == 0)
        {
            var status = await _db.AiPlans
                .Where(p => p.Id == planId && p.ProjectId == projectId)
                .Select(p => p.Status)
                .FirstOrDefaultAsync(cancellationToken);

            await transaction.RollbackAsync(cancellationToken);

            return status == null
                ? new AiPlanApplyResult(AiPlanApplyStatus.NotFound)
                : new AiPlanApplyResult(AiPlanApplyStatus.AlreadyApplied, ConflictStatus: status);
        }

        var taskMap = new Dictionary<string, TaskItem>(StringComparer.OrdinalIgnoreCase);
        var createdTasks = new List<TaskItem>();
        var createdLinks = new List<TaskLink>();

        // Preload all existing columns in a single query instead of querying once per phase.
        var phaseNames = draft.Phases.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingColumns = await _db.TaskColumns
            .Where(c => c.ProjectId == projectId && phaseNames.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var phasePosition = 0;
        foreach (var phase in draft.Phases)
        {
            // Use the preloaded dictionary instead of querying once per phase.
            if (!existingColumns.TryGetValue(phase.Name, out var column))
            {
                column = new TaskColumn
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = phase.Name,
                    Position = phasePosition,
                    IsDefault = phasePosition == 0,
                    CreatedAt = now
                };
                _db.TaskColumns.Add(column);
                existingColumns[phase.Name] = column;
                // Do not save here; columns and tasks are saved in one batch below.
            }
            phasePosition++;

            var positions = await GetNextPositionsAsync(column.Id, cancellationToken);

            foreach (var task in phase.Tasks)
            {
                var taskId = task.Id.Trim();
                var trimmedTitle = TrimToLength(task.Title?.Trim(), _options.MaxTaskTitleLength);
                var priority = NormalizePriority(task.Priority);

                var taskEntity = new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Title = trimmedTitle,
                    Description = BuildTaskDescription(phase, task, _options.MaxTaskDescriptionLength),
                    Priority = priority,
                    Tags = task.Tags != null ? string.Join(",", task.Tags) : null,
                    Status = "todo",
                    CreatedByUserId = userId,
                    CreatedAt = now,
                    ColumnId = column.Id,
                    PositionInColumn = positions.NextPosition
                };

                positions.NextPosition += 1;
                taskMap[taskId] = taskEntity;
                createdTasks.Add(taskEntity);
            }
        }

        // Save all columns and tasks in one batch.
        _db.Tasks.AddRange(createdTasks);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var phase in draft.Phases)
        {
            foreach (var task in phase.Tasks)
            {
                var taskId = task.Id.Trim();
                if (!taskMap.TryGetValue(taskId, out var sourceTask)) continue;
                foreach (var dependencyId in (task.DependsOn ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var dependencyKey = dependencyId.Trim();
                    if (string.IsNullOrWhiteSpace(dependencyKey)) continue;
                    if (!taskMap.TryGetValue(dependencyKey, out var targetTask)) continue;
                    createdLinks.Add(new TaskLink
                    {
                        Id = Guid.NewGuid(),
                        SourceTaskId = sourceTask.Id,
                        TargetTaskId = targetTask.Id,
                        LinkType = "depends_on",
                        CreatedByUserId = userId,
                        CreatedAt = now
                    });
                }
            }
        }

        if (createdLinks.Count > 0)
        {
            _db.TaskLinks.AddRange(createdLinks);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await _db.AiPlans
            .Where(p => p.Id == planId && p.ProjectId == projectId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.Status, AiPlanStatus.Applied.Value)
                    .SetProperty(p => p.AppliedAt, now)
                    .SetProperty(p => p.AppliedByUserId, userId)
                    .SetProperty(p => p.UpdatedAt, now),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Applied AI plan {PlanId} for project {ProjectId}: {TaskCount} tasks, {LinkCount} links",
            planId,
            projectId,
            createdTasks.Count,
            createdLinks.Count);

        return new AiPlanApplyResult(AiPlanApplyStatus.Applied, createdTasks.Count, createdLinks.Count, now);
    }

    /// <summary>Maps planner priority labels to board priority values.</summary>
    private static string NormalizePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return "medium";
        var p = priority.Trim().ToLowerInvariant();
        return p switch
        {
            "urgent" or "critical" or "blocker" => "urgent",
            "high" => "high",
            "low" or "minor" or "trivial" => "low",
            _ => "medium"
        };
    }

    /// <summary>Prefixes task description with the phase name and truncates to configured max length.</summary>
    private static string BuildTaskDescription(AiPlanPhase phase, AiPlanTask task, int maxLength)
    {
        var phaseLabel = $"Phase: {phase.Name}";
        var combined = string.IsNullOrWhiteSpace(task.Description)
            ? phaseLabel
            : $"{phaseLabel}\n\n{task.Description.Trim()}";
        return TrimToLength(combined, maxLength);
    }

    /// <summary>Returns the next <c>PositionInColumn</c> for new tasks in the given column.</summary>
    private async Task<PositionTracker> GetNextPositionsAsync(Guid? columnId, CancellationToken cancellationToken)
    {
        if (!columnId.HasValue)
        {
            var maxUnassignedPosition = await _db.Tasks
                .Where(t => t.ColumnId == null && !t.IsDeleted)
                .MaxAsync(t => (int?)t.PositionInColumn, cancellationToken) ?? -1;
            return new PositionTracker { NextPosition = maxUnassignedPosition + 1 };
        }

        var maxColumnPosition = await _db.Tasks
            .Where(t => t.ColumnId == columnId && !t.IsDeleted)
            .MaxAsync(t => (int?)t.PositionInColumn, cancellationToken) ?? -1;

        return new PositionTracker { NextPosition = maxColumnPosition + 1 };
    }

    /// <summary>Truncates optional text fields to a maximum length.</summary>
    private static string TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Carries the next task position while tasks are appended to a board column.
    /// </summary>
    private sealed class PositionTracker
    {
        /// <summary>Next free position index within a column.</summary>
        public int NextPosition { get; set; }
    }
}
