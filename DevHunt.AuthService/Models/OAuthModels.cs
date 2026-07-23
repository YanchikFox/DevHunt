namespace DevHunt.AuthService.Models;

/// <summary>
/// Supported OAuth providers.
/// </summary>
public enum OAuthProvider
{
    GitHub,
    Google
}

/// <summary>
/// OAuth endpoint URLs for a provider.
/// </summary>
public record OAuthEndpoints(
    string AuthorizationUrl,
    string TokenUrl,
    string UserInfoUrl);

/// <summary>
/// OAuth credentials for a provider.
/// </summary>
public record OAuthCredentials(
    string ClientId,
    string ClientSecret);

/// <summary>
/// OAuth configuration for a specific provider.
/// Groups related settings to reduce parameter count.
/// </summary>
public record OAuthProviderConfig(
    OAuthCredentials Credentials,
    OAuthEndpoints Endpoints,
    string Scope)
{
    /// <summary>Additional OAuth parameters for this provider.</summary>
    public IDictionary<string, string> AdditionalParams { get; init; } = new Dictionary<string, string>();

    // Convenience accessors
    public string ClientId => Credentials.ClientId;
    public string ClientSecret => Credentials.ClientSecret;
    public string AuthorizationEndpoint => Endpoints.AuthorizationUrl;
    public string TokenEndpoint => Endpoints.TokenUrl;
    public string UserInfoEndpoint => Endpoints.UserInfoUrl;
}

/// <summary>
/// OAuth authorization code and callback URL.
/// </summary>
public record OAuthCodeExchange(
    OAuthProvider Provider,
    string Code,
    CallbackUrl CallbackUrl);

/// <summary>
/// OAuth token response from provider.
/// </summary>
public record OAuthTokenResponse(string AccessToken);

/// <summary>
/// Validated callback URL for OAuth flow.
/// </summary>
public record CallbackUrl
{
    public string Value { get; }
    
    public CallbackUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Callback URL cannot be empty", nameof(url));
        Value = url;
    }
    
    public static implicit operator string(CallbackUrl url) => url.Value;
    public override string ToString() => Value;
}

/// <summary>
/// Validated redirect URI for post-OAuth redirect.
/// </summary>
public record RedirectUri
{
    public string Value { get; }
    
    public RedirectUri(string uri)
    {
        Value = uri ?? throw new ArgumentNullException(nameof(uri));
    }
    
    public static implicit operator string(RedirectUri uri) => uri.Value;
    public override string ToString() => Value;
}

/// <summary>
/// Extension methods for OAuth types.
/// </summary>
public static class OAuthProviderExtensions
{
    /// <summary>Parses provider string to enum.</summary>
    public static OAuthProvider? ParseProvider(string? provider)
    {
        return provider?.ToLowerInvariant() switch
        {
            "github" => OAuthProvider.GitHub,
            "google" => OAuthProvider.Google,
            _ => null
        };
    }

    /// <summary>Gets provider name as lowercase string.</summary>
    public static string ToProviderString(this OAuthProvider provider)
        => provider.ToString().ToLowerInvariant();
}
