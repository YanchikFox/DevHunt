using System;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Project news feed with visibility tiers (<c>public</c>, <c>subscribers</c>, <c>members</c>),
/// attachments, and activity-log sync. Implemented by <see cref="ProjectNewsService"/>.
/// </summary>
public interface IProjectNewsService
{
    /// <summary>
    /// Returns a paginated, visibility-filtered list of news posts for the requester's access level.
    /// </summary>
    Task<NewsResult<object>> GetProjectNewsAsync(GetProjectNewsQuery query, CancellationToken ct = default);

    /// <summary>Fetches a single post when its visibility is allowed for the requester.</summary>
    Task<NewsResult<object>> GetProjectNewsPostAsync(Guid projectId, Guid newsId, Guid? requesterId, bool isPrivileged, CancellationToken ct = default);

    /// <summary>
    /// Creates a post, validates attachments, notifies subscribers, and logs public activity when applicable.
    /// </summary>
    Task<NewsResult<ProjectNewsResponse>> CreateProjectNewsAsync(CreateProjectNewsCommand command, CancellationToken ct = default);

    /// <summary>Updates post fields, attachments, and visibility; refreshes linked activity records.</summary>
    Task<NewsResult<object>> UpdateProjectNewsAsync(UpdateProjectNewsCommand command, CancellationToken ct = default);

    /// <summary>Deletes a post and removes matching <c>project.news_published</c> activity rows.</summary>
    Task<NewsResult> DeleteProjectNewsAsync(DeleteProjectNewsCommand command, CancellationToken ct = default);
}
