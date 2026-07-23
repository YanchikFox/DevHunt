using DevHunt.CoreApi.Services.Badges;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Constants;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevHunt.CoreApi.Services.Moderation;

/// <summary>Submits user reports, serves the moderator queue, and records decisions.</summary>
public sealed class ModerationService : IModerationService
{
    private static readonly HashSet<string> ValidTargetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ReportTargetType.User,
        ReportTargetType.Project,
        ReportTargetType.Message,
        ReportTargetType.NewsPost,
        ReportTargetType.NewsComment,
        ReportTargetType.ShowcaseComment,
        ReportTargetType.CommunityPost,
        ReportTargetType.Task,
        ReportTargetType.Image,
    };

    private readonly DevHuntDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationHelperService _notifications;
    private readonly ILogger<ModerationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModerationService"/> class.
    /// </summary>
    /// <param name="db">Database context for moderation reports and user lookups.</param>
    /// <param name="serviceProvider">Service provider used to run achievement triggers in a scoped context.</param>
    /// <param name="notifications">Notification helper for moderators and reporters.</param>
    /// <param name="logger">Logger for report submission and processing events.</param>
    public ModerationService(
        DevHuntDbContext db,
        IServiceProvider serviceProvider,
        INotificationHelperService notifications,
        ILogger<ModerationService> logger)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>Creates a pending report when fewer than three active reports exist for the target.</summary>
    public async Task<ModerationServiceResult<ModerationReportDto>> SubmitReportAsync(
        Guid reporterId, SubmitReportRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TargetType) ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            request.TargetId == Guid.Empty)
        {
            return Fail<ModerationReportDto>("Bad parameters", 400);
        }

        if (!ValidTargetTypes.Contains(request.TargetType))
            return Fail<ModerationReportDto>($"Unknown target type: {request.TargetType}", 400);

        // B-08: single query, no TOCTOU
        var active = await _db.ModerationReports.CountAsync(
            x => x.TargetType == request.TargetType &&
                 x.TargetId == request.TargetId &&
                 x.Status == ModerationReportStatus.Pending, ct);

        if (active >= 3)
            return Fail<ModerationReportDto>("Content already has 3+ pending reports", 409);

        var report = new ModerationReport
        {
            Id = Guid.NewGuid(),
            ReporterId = reporterId,
            TargetType = request.TargetType.Trim().ToLowerInvariant(),
            TargetId = request.TargetId,
            Reason = request.Reason.Trim(),
            Status = ModerationReportStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        _db.ModerationReports.Add(report);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Report {ReportId} submitted by {ReporterId} for {TargetType}:{TargetId}",
            report.Id, reporterId, request.TargetType, request.TargetId);

        // Notify admins/curators about new report
        var adminIds = await _db.Users
            .Where(u => u.IsActive && (u.Role == "admin" || u.Role == "curator"))
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (adminIds.Count > 0)
        {
            await _notifications.SendBulkNotificationsAsync(
                adminIds, "moderation",
                $"New report: {request.TargetType}",
                $"Reason: {request.Reason}",
                relatedEntityType: "ModerationReport",
                relatedEntityId: report.Id,
                priority: "high", ct: ct);
        }

        return Ok(ToDto(report, null));
    }

    /// <summary>Returns pending reports ordered oldest first.</summary>
    public async Task<ModerationServiceResult<IReadOnlyList<ModerationReportDto>>> GetQueueAsync(
        CancellationToken ct = default)
    {
        var list = await _db.ModerationReports
            .AsNoTracking()
            .Where(x => x.Status == ModerationReportStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ModerationReportDto(
                x.Id,
                x.TargetType,
                x.TargetId,
                x.Status,
                x.Reason,
                x.ActionTaken,
                x.CreatedAt,
                x.ProcessedAt,
                x.ReporterId,
                x.Reporter != null ? x.Reporter.FullName ?? x.Reporter.Email : null))
            .ToListAsync(ct);

        return Ok<IReadOnlyList<ModerationReportDto>>(list);
    }

    /// <summary>Atomically resolves a pending report and notifies the reporter.</summary>
    public async Task<ModerationServiceResult<ModerationReportDto>> ProcessDecisionAsync(
        Guid moderatorId, DecisionRequest request, CancellationToken ct = default)
    {
        var actionTaken = string.IsNullOrWhiteSpace(request.ActionTaken)
            ? request.Decision
            : request.ActionTaken;

        // AP-02: Atomic update — WHERE status = pending prevents race condition
        var affected = await _db.ModerationReports
            .Where(x => x.Id == request.ReportId && x.Status == ModerationReportStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, ModerationReportStatus.Resolved)
                .SetProperty(x => x.ActionTaken, actionTaken)
                .SetProperty(x => x.ProcessedAt, DateTime.UtcNow)
                .SetProperty(x => x.ProcessedByUserId, moderatorId), ct);

        if (affected == 0)
            return Fail<ModerationReportDto>("Report not found or already processed", 404);

        // B-07: await achievement trigger, not fire-and-forget
        await _serviceProvider.TriggerAchievementCheckAsync(moderatorId, AchievementTrigger.ReportProcessed);

        // Reload for response DTO
        var report = await _db.ModerationReports
            .Include(x => x.Reporter)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ReportId, ct);

        _logger.LogInformation("Report {ReportId} processed by {ModeratorId}: {Decision}",
            request.ReportId, moderatorId, request.Decision);

        // Notify the reporter about the decision
        if (report?.ReporterId is Guid reporterId)
        {
            await _notifications.SendNotificationAsync(
                reporterId, "moderation",
                $"Your report has been reviewed: {actionTaken}",
                relatedEntityType: "ModerationReport",
                relatedEntityId: request.ReportId,
                priority: "medium", ct: ct);
        }

        return Ok(ToDto(report!, report?.Reporter?.FullName ?? report?.Reporter?.Email));
    }

    /// <summary>Maps an entity to the API DTO.</summary>
    private static ModerationReportDto ToDto(ModerationReport r, string? reporterName) =>
        new(r.Id, r.TargetType, r.TargetId, r.Status, r.Reason, r.ActionTaken,
            r.CreatedAt, r.ProcessedAt, r.ReporterId, reporterName);

    /// <summary>Wraps successful service output.</summary>
    private static ModerationServiceResult<T> Ok<T>(T data) =>
        new(true, data, null, 200);

    /// <summary>Wraps an error response with HTTP status code.</summary>
    private static ModerationServiceResult<T> Fail<T>(string error, int code) =>
        new(false, default, error, code);
}
