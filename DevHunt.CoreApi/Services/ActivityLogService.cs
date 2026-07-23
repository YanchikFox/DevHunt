using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Service for writing activity feed records.
/// </summary>
public interface IActivityLogService
{
    /// <summary>
    /// Log a generic activity record with optional project and target user context.
    /// </summary>
    /// <param name="projectId">Related project ID (optional).</param>
    /// <param name="actorId">User who performed the action.</param>
    /// <param name="eventType">Event type key.</param>
    /// <param name="summary">Human-readable summary.</param>
    /// <param name="visibility">Visibility scope (public, subscribers, members, private).</param>
    /// <param name="eventGroup">Optional event group override.</param>
    /// <param name="targetUserId">Optional target user ID.</param>
    /// <param name="payload">Optional payload object (serialized to JSON).</param>
    /// <param name="ct">Cancels activity persistence.</param>
    /// <returns>The created or deduplicated activity record.</returns>
    Task<ActivityRecord> LogAsync(
        Guid? projectId,
        Guid actorId,
        string eventType,
        string summary,
        string visibility = "public",
        string? eventGroup = null,
        Guid? targetUserId = null,
        object? payload = null, CancellationToken ct = default);

    /// <summary>
    /// Log a project-scoped activity event.
    /// </summary>
    /// <param name="projectId">Project identifier.</param>
    /// <param name="actorId">User who performed the action.</param>
    /// <param name="eventType">Event type key.</param>
    /// <param name="summary">Human-readable summary.</param>
    /// <param name="visibility">Visibility scope.</param>
    /// <param name="targetUserId">Optional target user ID.</param>
    /// <param name="payload">Optional payload object.</param>
    /// <returns>The created or deduplicated activity record.</returns>
    Task<ActivityRecord> LogProjectEventAsync(
        Guid projectId,
        Guid actorId,
        string eventType,
        string summary,
        string visibility = "public",
        Guid? targetUserId = null,
        object? payload = null);

    /// <summary>
    /// Log a user-scoped activity event.
    /// </summary>
    /// <param name="actorId">User who performed the action.</param>
    /// <param name="eventType">Event type key.</param>
    /// <param name="summary">Human-readable summary.</param>
    /// <param name="visibility">Visibility scope.</param>
    /// <param name="targetUserId">Optional target user ID.</param>
    /// <param name="payload">Optional payload object.</param>
    /// <returns>The created or deduplicated activity record.</returns>
    Task<ActivityRecord> LogUserEventAsync(
        Guid actorId,
        string eventType,
        string summary,
        string visibility = "public",
        Guid? targetUserId = null,
        object? payload = null);
}

/// <summary>
/// Default implementation of <see cref="IActivityLogService"/> with deduplication and event publishing.
/// </summary>
public class ActivityLogService : IActivityLogService
{
    private static readonly IReadOnlySet<string> AllowedVisibilities =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "public",
            "subscribers",
            "members",
            "private"
        };

    private static readonly IReadOnlyDictionary<string, string> EventTypeToGroup =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["user.follow"] = "social",
            ["user.unfollow"] = "social",
            ["user.created_project"] = "user",
            ["user.joined_project"] = "user",
            ["user.left_project"] = "user",
            ["user.updated_profile"] = "user",
            ["project.news_published"] = "project",
            ["project.media_uploaded"] = "project",
            ["project.state_changed"] = "project",
            ["project.published"] = "project",
            ["project.activated"] = "project",
            ["project.completed"] = "project",
            ["project.cancelled"] = "project",
            ["news.post.created"] = "project",

            ["task.created"] = "tasks",
            ["task.updated"] = "tasks",
            ["task.status_changed"] = "tasks",
            ["task.deleted"] = "tasks",
            ["task.restored"] = "tasks"
        };

    private readonly DevHuntDbContext _dbContext;
    private readonly IEventBusService _eventBus;
    private readonly ILogger<ActivityLogService> _logger;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityLogService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for activity records.</param>
    /// <param name="eventBus">Event bus used to publish activity-created events.</param>
    /// <param name="logger">Logger for activity logging diagnostics.</param>
    public ActivityLogService(
        DevHuntDbContext dbContext,
        IEventBusService eventBus,
        ILogger<ActivityLogService> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ActivityRecord> LogProjectEventAsync(
        Guid projectId,
        Guid actorId,
        string eventType,
        string summary,
        string visibility = "public",
        Guid? targetUserId = null,
        object? payload = null)
    {
        return LogAsync(projectId, actorId, eventType, summary, visibility, null, targetUserId, payload);
    }

    /// <inheritdoc />
    public Task<ActivityRecord> LogUserEventAsync(
        Guid actorId,
        string eventType,
        string summary,
        string visibility = "public",
        Guid? targetUserId = null,
        object? payload = null)
    {
        return LogAsync(null, actorId, eventType, summary, visibility, null, targetUserId, payload);
    }

    /// <inheritdoc />
    public async Task<ActivityRecord> LogAsync(
        Guid? projectId,
        Guid actorId,
        string eventType,
        string summary,
        string visibility = "public",
        string? eventGroup = null,
        Guid? targetUserId = null,
        object? payload = null, CancellationToken ct = default)
    {
        var normalizedVisibility = NormalizeVisibility(visibility);
        var normalizedGroup = NormalizeEventGroup(eventType, eventGroup);
        var normalizedSummary = (summary ?? string.Empty).Trim();

        var duplicate = await FindDuplicateAsync(projectId, actorId, eventType, normalizedSummary, normalizedVisibility, targetUserId, ct);
        if (duplicate != null)
        {
            return duplicate;
        }

        var record = new ActivityRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ActorId = actorId,
            TargetUserId = targetUserId,
            EventType = eventType,
            Summary = normalizedSummary,
            Visibility = normalizedVisibility,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            EventGroup = normalizedGroup,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ActivityRecords.Add(record);
        await _dbContext.SaveChangesAsync(ct);

        // B-07: PublishAsync must not be swallowed — OutboxEventBusDecorator guarantees at-least-once delivery
        await _eventBus.PublishAsync(DomainEvents.ActivityRecordCreated(
            record.Id, projectId ?? Guid.Empty, actorId, eventType, normalizedVisibility, summary ?? string.Empty));

        return record;
    }

    /// <summary>
    /// Find a recent duplicate activity record to avoid noisy repeats.
    /// </summary>
    private async Task<ActivityRecord?> FindDuplicateAsync(Guid? projectId, Guid actorId, string eventType, string summary, string visibility, Guid? targetUserId, CancellationToken ct = default)
    {
        var threshold = DateTime.UtcNow.Subtract(DuplicateWindow);

        var query = _dbContext.ActivityRecords
            .AsNoTracking()
            .Where(ar =>
                ar.ActorId == actorId &&
                ar.EventType == eventType &&
                ar.Visibility == visibility &&
                ar.Summary == summary &&
                ar.CreatedAt >= threshold);

        query = projectId.HasValue
            ? query.Where(ar => ar.ProjectId == projectId.Value)
            : query.Where(ar => ar.ProjectId == null);

        query = targetUserId.HasValue
            ? query.Where(ar => ar.TargetUserId == targetUserId.Value)
            : query.Where(ar => ar.TargetUserId == null);

        return await query
            .OrderByDescending(ar => ar.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Normalize a visibility value to an allowed scope.
    /// </summary>
    private string NormalizeVisibility(string visibility)
    {
        var trimmed = visibility?.Trim().ToLowerInvariant() ?? string.Empty;
        return AllowedVisibilities.Contains(trimmed) ? trimmed : "public";
    }

    /// <summary>
    /// Resolve event group from explicit override or event type mapping.
    /// </summary>
    private string NormalizeEventGroup(string eventType, string? eventGroup)
    {
        if (!string.IsNullOrWhiteSpace(eventGroup))
        {
            return eventGroup.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(eventType) && EventTypeToGroup.TryGetValue(eventType.Trim().ToLowerInvariant(), out var mapped))
        {
            return mapped;
        }

        return "project";
    }
}
