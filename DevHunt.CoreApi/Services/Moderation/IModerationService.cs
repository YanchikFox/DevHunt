namespace DevHunt.CoreApi.Services.Moderation;

/// <summary>User report submission and moderator queue operations.</summary>
public interface IModerationService
{
    /// <summary>Creates a pending moderation report for review.</summary>
    Task<ModerationServiceResult<ModerationReportDto>> SubmitReportAsync(
        Guid reporterId, SubmitReportRequest request, CancellationToken ct = default);

    /// <summary>Lists pending reports for moderators.</summary>
    Task<ModerationServiceResult<IReadOnlyList<ModerationReportDto>>> GetQueueAsync(
        CancellationToken ct = default);

    /// <summary>Records a moderator decision on a pending report.</summary>
    Task<ModerationServiceResult<ModerationReportDto>> ProcessDecisionAsync(
        Guid moderatorId, DecisionRequest request, CancellationToken ct = default);
}

/// <summary>Generic moderation service result with optional error text and HTTP status.</summary>
public record ModerationServiceResult<T>(bool Success, T? Data, string? Error, int StatusCode = 200);

/// <summary>Payload for submitting a moderation report.</summary>
/// <param name="TargetType">Reported entity type.</param>
/// <param name="TargetId">Reported entity ID.</param>
/// <param name="Reason">Reporter-provided reason.</param>
public record SubmitReportRequest(string TargetType, Guid TargetId, string Reason);

/// <summary>Moderator decision payload.</summary>
/// <param name="ReportId">Report being resolved.</param>
/// <param name="ActionTaken">Action recorded for the target.</param>
/// <param name="Decision">Decision label; used when action taken is empty.</param>
public record DecisionRequest(Guid ReportId, string ActionTaken, string Decision);

/// <summary>Moderation report exposed to API clients.</summary>
/// <param name="Id">Report identifier.</param>
/// <param name="TargetType">Reported entity category used to route moderator review.</param>
/// <param name="TargetId">Identifier of the reported entity.</param>
/// <param name="Status">Current report status.</param>
/// <param name="Reason">Reporter reason text.</param>
/// <param name="ActionTaken">Moderator action recorded when the report is processed.</param>
/// <param name="CreatedAt">UTC time when the report entered the queue.</param>
/// <param name="ProcessedAt">UTC time when a moderator resolved the report, if any.</param>
/// <param name="ReporterId">User who submitted the report.</param>
/// <param name="ReporterName">Display name shown with the report when available.</param>
public record ModerationReportDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string Status,
    string Reason,
    string? ActionTaken,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    Guid ReporterId,
    string? ReporterName);
