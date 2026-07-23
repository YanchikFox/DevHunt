namespace DevHunt.CoreApi.Services;

/// <summary>
/// Maps project visibility settings to activity feed visibility scopes.
/// </summary>
public static class ActivityVisibilityHelper
{
    /// <summary>
    /// Converts a project visibility value to an activity visibility scope.
    /// </summary>
    /// <param name="visibility">Project visibility (public, private, unlisted, members, subscribers).</param>
    /// <returns>Activity visibility scope.</returns>
    public static string FromProjectVisibility(string? visibility) =>
        visibility?.Trim().ToLowerInvariant() switch
        {
            "private" => "members",
            "unlisted" => "subscribers",
            "public" => "public",
            "members" => "members",
            "subscribers" => "subscribers",
            _ => "public"
        };
}
