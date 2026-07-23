using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevHunt.CoreApi.Security;
using Microsoft.EntityFrameworkCore;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Models;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// EF-backed implementation of <see cref="IProjectIssuesService"/>. Handles in-project moderation
/// issues with participant/admin access rules, notifications, and cache invalidation on updates.
/// </summary>
public class ProjectIssuesService : IProjectIssuesService
{
    private readonly DevHuntDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly INotificationHelperService _notificationService;
    private readonly ICacheService _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectIssuesService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for issues and related entities.</param>
    /// <param name="auditService">Audit trail for issue lifecycle events.</param>
    /// <param name="notificationService">Notifies owners and admins on new issues.</param>
    /// <param name="cache">Project list cache invalidated after updates.</param>
    public ProjectIssuesService(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationHelperService notificationService,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _notificationService = notificationService;
        _cache = cache;
    }

    /// <summary>True when the user owns the project.</summary>
    private async Task<bool> IsProjectOwnerAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        return project?.OwnerId == userId;
    }

    /// <summary>True when the user has an active team membership row.</summary>
    private async Task<bool> IsProjectMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.TeamMembers
            .AnyAsync(tm => tm.ProjectId == projectId && tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value, ct);
    }

    /// <summary>True for project owners or active team members.</summary>
    private async Task<bool> HasProjectAccessAsync(Guid projectId, Guid userId)
    {
        var isOwner = await IsProjectOwnerAsync(projectId, userId);
        if (isOwner) return true;
        return await IsProjectMemberAsync(projectId, userId);
    }

    /// <summary>Clears cached project detail and catalog entries after issue mutations.</summary>
    private async Task InvalidateProjectCacheAsync(Guid projectId)
    {
        await _cache.RemoveAsync($"project:{projectId}");
        await _cache.RemoveByPatternAsync("projects:*");
    }

    /// <summary>Notifies the project owner (when not the reporter) and all active admins/curators.</summary>
    private async Task NotifyIssueCreatedAsync(Project project, ProjectIssue issue, string issueType, CancellationToken ct = default)
    {
        // Notify owner if not the reporter
        if (project.OwnerId != issue.ReporterId)
        {
            await _notificationService.SendNotificationAsync(
                project.OwnerId,
                "admin",
                "Utworzono problem w projekcie",
                $"W projekcie '{project.Title}' utworzono problem: {issue.Title}. Wymagana interwencja administratora.",
                "ProjectIssue",
                issue.Id,
                "high"
            );
        }

        // Notify admins
        var adminUsers = await _dbContext.Users
            .Where(u => (u.Role == UserRoles.Admin || u.Role == UserRoles.Curator) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (adminUsers.Any())
        {
            await _notificationService.SendBulkNotificationsAsync(
                adminUsers,
                "admin",
                $"Nowy problem w projekcie: {project.Title}",
                $"W projekcie '{project.Title}' utworzono problem typu '{issueType}': {issue.Title}. Wymagana interwencja administratora.",
                "ProjectIssue",
                issue.Id,
                issue.Priority == "urgent" || issue.Priority == "high" ? "high" : "medium"
            );
        }
    }

    /// <inheritdoc />
    public async Task<IssueResult<object>> CreateIssueAsync(Guid projectId, CreateIssueRequest request, Guid userId, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return IssueResult<object>.Failure("Project not found", 404);

        if (!await HasProjectAccessAsync(projectId, userId))
            return IssueResult<object>.Failure("Only project owner or team members can create issues", 403);

        var typeError = ProjectIssuesHelper.ValidateIssueType(request.Type);
        if (typeError != null) return IssueResult<object>.Failure(typeError, 400);

        var relatedUserError = await ProjectIssuesHelper.ValidateRelatedUserAsync(_dbContext, projectId, request.RelatedUserId);
        if (relatedUserError != null) return IssueResult<object>.Failure(relatedUserError, 400);

        var issue = new ProjectIssue
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReporterId = userId,
            Type = request.Type.ToLowerInvariant(),
            Title = SecurityHelpers.SanitizeHtml(request.Title.Trim()),
            Description = SecurityHelpers.SanitizeHtml(request.Description.Trim()),
            Status = "open",
            Priority = "medium",
            RelatedUserId = request.RelatedUserId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ProjectIssues.Add(issue);
        await _dbContext.SaveChangesAsync(ct);

        await NotifyIssueCreatedAsync(project, issue, request.Type);

        await _auditService.LogActionAsync(userId, "ProjectIssuesController.CreateIssue", "ProjectIssue", issue.Id,
            $"Created issue in project {projectId}: {request.Type} - {request.Title}");

        return IssueResult<object>.Created(new
        {
            issue.Id,
            issue.Type,
            issue.Title,
            issue.Status,
            issue.Priority,
            issue.CreatedAt
        });
    }

    /// <inheritdoc />
    public async Task<IssueResult<object>> GetProjectIssuesAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return IssueResult<object>.Failure("Project not found", 404);

        var hasProjectAccess = await HasProjectAccessAsync(projectId, userId);

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        var isAdmin = user == UserRoles.Admin || user == UserRoles.Curator;

        if (!hasProjectAccess && !isAdmin)
        {
            return IssueResult<object>.Failure("Only project participants or admins can view issues", 403);
        }

        var issues = await _dbContext.ProjectIssues
            .AsNoTracking()
            .Where(i => i.ProjectId == projectId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.Type,
                i.Title,
                i.Status,
                i.Priority,
                ReporterId = i.ReporterId,
                ReporterEmail = i.Reporter != null ? i.Reporter.Email : "Unknown",
                RelatedUserId = i.RelatedUserId,
                RelatedUserEmail = i.RelatedUser != null ? i.RelatedUser.Email : (string?)null,
                i.CreatedAt,
                i.ResolvedAt,
                i.AdminResolution
            })
            .ToListAsync(ct);

        return IssueResult<object>.Success(issues);
    }

    /// <inheritdoc />
    public async Task<IssueResult> CancelIssueAsync(Guid projectId, Guid issueId, CancelIssueRequest? request, Guid userId, CancellationToken ct = default)
    {
        var issue = await _dbContext.ProjectIssues.FindAsync(new object[] { issueId }, ct);
        if (issue == null) return IssueResult.Failure("Issue not found", 404);

        if (issue.ProjectId != projectId) return IssueResult.Failure("Issue does not belong to this project", 400);

        if (issue.ReporterId != userId) return IssueResult.Failure("Forbidden", 403);

        if (issue.Status != "open") return IssueResult.Failure("Can only cancel issues in 'open' status", 400);

        issue.Status = "dismissed";
        issue.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "ProjectIssuesController.CancelIssue", "ProjectIssue", issueId,
            $"Cancelled issue: {issue.Title}. Reason: {request?.Reason ?? "Not specified"}");

        return IssueResult.Success();
    }

    /// <inheritdoc />
    public async Task<IssueResult<object>> UpdateIssueAsync(UpdateIssueContext context, CancellationToken ct = default)
    {
        var (issue, validationError) = await GetAndValidateIssueForUpdateAsync(context.ProjectId, context.IssueId, context.UserId, context.IsAdmin);
        if (validationError != null) return validationError;

        if (IsRequestEmpty(context.Request))
        {
            return IssueResult<object>.Failure("At least one editable field must be provided", 400);
        }

        var (error, statusCode, updatedFields) = await ProjectIssuesHelper.ProcessIssueUpdatesAsync(_dbContext, issue!, context.Request, context.IsAdmin);
        if (error != null) return IssueResult<object>.Failure(error, statusCode ?? 400);

        if (updatedFields.Count == 0) return IssueResult<object>.Failure("No valid fields provided for update", 400);

        issue!.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(context.ProjectId);

        await _auditService.LogActionAsync(context.UserId, "ProjectIssuesController.UpdateIssue", "ProjectIssue", context.IssueId,
            $"Updated issue fields: {string.Join(", ", updatedFields)}");

        return IssueResult<object>.Success(new
        {
            issue.Id,
            issue.ProjectId,
            issue.Title,
            issue.Description,
            issue.Status,
            issue.Priority,
            issue.RelatedUserId,
            issue.AssignedToAdminId,
            issue.UpdatedAt
        });
    }

    /// <summary>Loads the issue and verifies participant or admin access for updates.</summary>
    private async Task<(ProjectIssue? Issue, IssueResult<object>? Error)> GetAndValidateIssueForUpdateAsync(Guid projectId, Guid issueId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var issue = await _dbContext.ProjectIssues.FindAsync(new object[] { issueId }, ct);
        if (issue == null) return (null, IssueResult<object>.Failure("Issue not found", 404));
        if (issue.ProjectId != projectId) return (null, IssueResult<object>.Failure("Issue does not belong to this project", 400));

        var hasProjectAccess = await HasProjectAccessAsync(projectId, userId);
        if (!isAdmin && !hasProjectAccess)
            return (null, IssueResult<object>.Failure("Only project participants or admins can update issues", 403));

        return (issue, null);
    }

    /// <summary>True when the update request omitted every editable field.</summary>
    private static bool IsRequestEmpty(UpdateIssueRequest request)
    {
        return request.Title == null &&
               request.Description == null &&
               request.Status == null &&
               request.Priority == null &&
               request.RelatedUserId == null;
    }
}
