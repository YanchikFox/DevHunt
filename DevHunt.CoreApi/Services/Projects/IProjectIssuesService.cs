using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// CRUD-style operations for project-scoped moderation issues reported by team members.
/// Implemented by <see cref="ProjectIssuesService"/>.
/// </summary>
public interface IProjectIssuesService
{
    /// <summary>
    /// Opens a new issue after validating type and optional related participant. Notifies owner and admins.
    /// </summary>
    Task<IssueResult<object>> CreateIssueAsync(Guid projectId, CreateIssueRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lists issues for a project when the caller is a participant or platform admin/curator.
    /// </summary>
    Task<IssueResult<object>> GetProjectIssuesAsync(Guid projectId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Allows the original reporter to dismiss an open issue, recording an optional reason.
    /// </summary>
    Task<IssueResult> CancelIssueAsync(Guid projectId, Guid issueId, CancelIssueRequest? request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Applies partial field updates with stricter status rules for non-admin participants.
    /// </summary>
    Task<IssueResult<object>> UpdateIssueAsync(UpdateIssueContext context, CancellationToken ct = default);
}
