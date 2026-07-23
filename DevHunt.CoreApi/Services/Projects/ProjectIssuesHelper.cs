using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevHunt.CoreApi.Models;
using Microsoft.EntityFrameworkCore;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Validation and field-application helpers shared by <see cref="ProjectIssuesService"/> when
/// creating and updating moderation issues.
/// </summary>
public static class ProjectIssuesHelper
{
    private static readonly string[] AllowedIssuePriorities = { "low", "medium", "high", "urgent" };
    private static readonly string[] AllowedIssueStatuses = { "open", "investigating", "resolved", "dismissed", "escalated" };
    private static readonly string[] ParticipantEditableStatuses = { "open", "dismissed", "escalated" };

    /// <summary>
    /// Validates issue type against the fixed taxonomy; returns an error message or null.
    /// </summary>
    public static string? ValidateIssueType(string type)
    {
        var allowedTypes = new[] { "technical", "legal", "conflict", "abuse", "violation", "security", "other" };
        if (!allowedTypes.Contains(type.ToLowerInvariant()))
        {
            return $"Invalid type. Allowed: {string.Join(", ", allowedTypes)}";
        }
        return null;
    }

    /// <summary>Applies a validated priority change and records the field name in <paramref name="updatedFields"/>.</summary>
    public static string? ApplyPriorityUpdate(string? priorityInput, ProjectIssue issue, List<string> updatedFields)
    {
        if (string.IsNullOrWhiteSpace(priorityInput)) return null;

        var priority = priorityInput.Trim().ToLowerInvariant();
        if (!AllowedIssuePriorities.Contains(priority))
        {
            return $"Invalid priority. Allowed: {string.Join(", ", AllowedIssuePriorities)}";
        }

        issue.Priority = priority;
        updatedFields.Add("priority");
        return null;
    }

    /// <summary>
    /// Applies a status transition, setting <see cref="ProjectIssue.ResolvedAt"/> when resolved.
    /// Non-admins may only set participant-editable statuses.
    /// </summary>
    public static string? ApplyStatusUpdate(string? statusInput, ProjectIssue issue, List<string> updatedFields, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(statusInput)) return null;

        var status = statusInput.Trim().ToLowerInvariant();
        if (!AllowedIssueStatuses.Contains(status))
        {
            return $"Invalid status. Allowed: {string.Join(", ", AllowedIssueStatuses)}";
        }

        if (!isAdmin && !ParticipantEditableStatuses.Contains(status))
        {
            return "Forbidden: Only admins can set this status";
        }

        issue.Status = status;
        issue.ResolvedAt = status == "resolved" ? DateTime.UtcNow : null;
        updatedFields.Add("status");
        return null;
    }

    /// <summary>Ensures an optional related user is the project owner or an active team member.</summary>
    public static async Task<string?> ValidateRelatedUserAsync(DevHuntDbContext context, Guid projectId, Guid? relatedUserId, CancellationToken ct = default)
    {
        if (!relatedUserId.HasValue) return null;

        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return "Project not found"; // Should be validated before

        var isOwner = project.OwnerId == relatedUserId.Value;
        var isMember = await context.TeamMembers.AnyAsync(tm => tm.ProjectId == projectId && tm.UserId == relatedUserId.Value && tm.Status == TeamMemberStatus.Active.Value, ct);

        if (!isOwner && !isMember)
        {
            return "Related user must be a project participant";
        }
        return null;
    }

    /// <summary>
    /// Applies all non-null fields from <see cref="UpdateIssueRequest"/> and returns validation errors.
    /// </summary>
    public static async Task<(string? Error, int? StatusCode, List<string> UpdatedFields)> ProcessIssueUpdatesAsync(
        DevHuntDbContext dbContext,
        ProjectIssue issue,
        UpdateIssueRequest request,
        bool isAdmin)
    {
        var updatedFields = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            issue.Title = request.Title.Trim();
            updatedFields.Add("title");
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            issue.Description = request.Description.Trim();
            updatedFields.Add("description");
        }

        var priorityError = ApplyPriorityUpdate(request.Priority, issue, updatedFields);
        if (priorityError != null) return (priorityError, 400, updatedFields);

        var statusError = ApplyStatusUpdate(request.Status, issue, updatedFields, isAdmin);
        if (statusError != null)
        {
            var code = statusError.StartsWith("Forbidden") ? 403 : 400;
            return (statusError, code, updatedFields);
        }

        if (request.RelatedUserId.HasValue)
        {
            var relatedError = await ValidateRelatedUserAsync(dbContext, issue.ProjectId, request.RelatedUserId);
            if (relatedError != null) return (relatedError, 400, updatedFields);

            issue.RelatedUserId = request.RelatedUserId.Value;
            updatedFields.Add("relatedUserId");
        }

        return (null, null, updatedFields);
    }
}
