namespace DevHunt.AuthService.Security;

/// <summary>
/// Validates OAuth post-login redirect targets against an exact origin allowlist.
/// </summary>
public static class OAuthRedirectValidator
{
    /// <summary>
    /// Returns true when <paramref name="redirect"/> matches an allowed origin exactly (not prefix-based).
    /// </summary>
    public static bool IsAllowedRedirectUri(Uri redirect, IEnumerable<string> allowedOrigins)
    {
        foreach (var allowed in allowedOrigins)
        {
            if (string.IsNullOrWhiteSpace(allowed))
                continue;

            if (!Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri))
                continue;

            if (!string.Equals(redirect.Scheme, allowedUri.Scheme, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(redirect.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase))
                continue;

            if (redirect.Port != allowedUri.Port)
                continue;

            var allowedPath = allowedUri.AbsolutePath.TrimEnd('/');
            if (string.IsNullOrEmpty(allowedPath))
                allowedPath = "/";

            if (allowedPath == "/")
                return true;

            var redirectPath = redirect.AbsolutePath.TrimEnd('/');
            if (redirectPath.Equals(allowedPath, StringComparison.OrdinalIgnoreCase)
                || redirectPath.StartsWith(allowedPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
