using System.Collections.Generic;
using System.Linq;
using DevHunt.Infrastructure.Models;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Viewer access facts used by <see cref="ProjectNewsVisibilityHelper"/> to decide which
/// news visibility tiers may be queried or read.
/// </summary>
/// <param name="HasProjectAccess">True when the viewer is owner or active team member.</param>
/// <param name="IsSubscriber">True when the viewer follows the project.</param>
/// <param name="IsPrivilegedViewer">True for platform admins/curators who bypass tier checks.</param>
public record NewsViewerContext(bool HasProjectAccess, bool IsSubscriber, bool IsPrivilegedViewer);

/// <summary>
/// Authoring-side identity for news create/edit/delete permission checks in <see cref="ProjectNewsService"/>.
/// </summary>
/// <param name="ProjectId">Project owning the news post.</param>
/// <param name="UserId">Authenticated user performing the action.</param>
/// <param name="OwnerId">Project owner id for owner-only shortcuts.</param>
/// <param name="IsPrivileged">Platform admin/curator flag.</param>
public record NewsAuthoringContext(Guid ProjectId, Guid UserId, Guid OwnerId, bool IsPrivileged)
{
    /// <summary>True when <see cref="UserId"/> is the project owner.</summary>
    public bool IsOwner => UserId == OwnerId;
    /// <summary>True for admins/curators or the project owner.</summary>
    public bool IsAdminOrOwner => IsPrivileged || IsOwner;
    /// <summary>True when the acting user originally authored the post.</summary>
    /// <param name="authorId">Stored author id on the news row.</param>
    /// <returns>Whether the current user is that author.</returns>
    public bool IsAuthor(Guid authorId) => UserId == authorId;
}


/// <summary>
/// Pure helpers for normalizing and enforcing project news visibility tiers
/// (<c>public</c>, <c>subscribers</c>, <c>members</c>).
/// </summary>
public static class ProjectNewsVisibilityHelper
{
    private static readonly string[] AllowedVisibilities = { "public", "subscribers", "members" };

    /// <summary>Trims and lowercases a visibility string; returns null for blank input.</summary>
    public static string? NormalizeVisibility(string? visibility)
    {
        if (string.IsNullOrWhiteSpace(visibility)) return null;
        return visibility.Trim().ToLowerInvariant();
    }

    /// <summary>Returns true when the value is one of the allowed visibility tiers.</summary>
    public static bool IsVisibilityValid(string visibility)
    {
        return AllowedVisibilities.Contains(visibility);
    }

    /// <summary>Comma-separated list of allowed visibility values for error messages.</summary>
    public static string GetAllowedVisibilitiesString() => string.Join(", ", AllowedVisibilities);

    /// <summary>
    /// Builds the visibility filter set for a viewer: everyone sees <c>public</c>; subscribers
    /// also see <c>subscribers</c>; members/admins also see <c>members</c>.
    /// </summary>
    public static List<string> DetermineAllowedVisibilities(NewsViewerContext context)
    {
        var allowed = new List<string> { "public" };

        if (context.HasProjectAccess || context.IsPrivilegedViewer)
        {
            allowed.AddRange(new[] { "subscribers", "members" });
        }
        else if (context.IsSubscriber)
        {
            allowed.Add("subscribers");
        }

        return allowed;
    }

    /// <summary>
    /// Restricts an EF query to visibilities the viewer may see, optionally narrowing to one tier.
    /// </summary>
    public static IQueryable<ProjectNewsPost> ApplyVisibilityQueryFilter(
        IQueryable<ProjectNewsPost> query,
        NewsViewerContext context,
        string? requestedVisibility)
    {
        var allowedVisibilities = DetermineAllowedVisibilities(context);
        query = query.Where(pn => allowedVisibilities.Contains(pn.Visibility));

        if (requestedVisibility != null)
        {
            query = query.Where(pn => pn.Visibility == requestedVisibility);
        }

        return query;
    }

    /// <summary>
    /// Returns true when a single post's visibility tier is readable by the viewer (admins always pass).
    /// </summary>
    public static bool CanViewNewsPost(string postVisibility, NewsViewerContext context)
    {
        if (context.IsPrivilegedViewer) return true;
        return DetermineAllowedVisibilities(context).Contains(postVisibility);
    }
}
