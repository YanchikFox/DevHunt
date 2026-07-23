using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevHunt.AuthService.Models;
using DevHunt.AuthService.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Service for OAuth provider integrations (GitHub, Google).
/// </summary>
public interface IOAuthService
{
    /// <summary>Builds authorization URL for the OAuth provider.</summary>
    string BuildAuthorizationUrl(OAuthProvider provider, CallbackUrl callbackUrl, RedirectUri redirectUri);

    /// <summary>Exchanges authorization code for user info.</summary>
    Task<OAuthUserInfo?> ExchangeCodeAsync(OAuthCodeExchange exchange);

    /// <summary>Handles external user login/registration.</summary>
    Task<User?> HandleExternalUserAsync(OAuthProvider provider, OAuthUserInfo externalUser, CancellationToken ct = default);

    /// <summary>Validates redirect URI against allowed origins.</summary>
    RedirectUri ValidateRedirectUri(string? redirectUri);

    /// <summary>Builds redirect URL with a one-time exchange code query parameter.</summary>
    string BuildExchangeRedirect(RedirectUri redirectUri, string exchangeCode);

    /// <summary>Validates OAuth state parameter.</summary>
    string? ValidateState(string state);
}

/// <summary>
/// Implementation of OAuth service for GitHub and Google providers.
/// </summary>
public class OAuthService : IOAuthService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DevHuntDbContext _context;
    private readonly ILogger<OAuthService> _logger;
    private readonly Dictionary<OAuthProvider, OAuthProviderConfig> _providerConfigs;

    /// <summary>
    /// Stores OAuth configuration, HTTP client, database, and logging dependencies and builds provider configuration for supported providers.
    /// </summary>
    public OAuthService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        DevHuntDbContext context,
        ILogger<OAuthService> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _context = context;
        _logger = logger;
        _providerConfigs = BuildProviderConfigs();
    }

    /// <inheritdoc />
    public string BuildAuthorizationUrl(OAuthProvider provider, CallbackUrl callbackUrl, RedirectUri redirectUri)
    {
        var state = OAuthStateValidator.BuildState(redirectUri.Value, GetStateSecret());
        var providerConfig = GetProviderConfig(provider);

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = providerConfig.ClientId,
            ["redirect_uri"] = callbackUrl.Value,
            ["scope"] = providerConfig.Scope,
            ["state"] = state
        };

        foreach (var param in providerConfig.AdditionalParams)
            query[param.Key] = param.Value;

        return QueryHelpers.AddQueryString(providerConfig.AuthorizationEndpoint, query);
    }

    /// <inheritdoc />
    public async Task<OAuthUserInfo?> ExchangeCodeAsync(OAuthCodeExchange exchange)
    {
        var providerConfig = GetProviderConfig(exchange.Provider);
        var accessToken = await ExchangeCodeForTokenAsync(exchange, providerConfig);
        
        if (string.IsNullOrEmpty(accessToken))
            return null;

        return await FetchUserInfoAsync(exchange.Provider, providerConfig, accessToken);
    }

    /// <inheritdoc />
    public async Task<User?> HandleExternalUserAsync(OAuthProvider provider, OAuthUserInfo externalUser, CancellationToken ct = default)
    {
        // Step 1: Try to find existing user by provider ID
        var existingUser = await FindUserByProviderIdAsync(provider, externalUser.ProviderUserId, ct);
        if (existingUser != null)
            return existingUser;

        // Step 2: Try to find by email and link account
        var userByEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == externalUser.Email, ct);
        if (userByEmail != null)
            return await LinkExistingAccountAsync(provider, externalUser, userByEmail, ct);

        // Step 3: Create new user
        return await CreateNewExternalUserAsync(provider, externalUser, ct);
    }

    /// <inheritdoc />
    public RedirectUri ValidateRedirectUri(string? redirectUri)
    {
        var allowedOrigins = _config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var fallback = allowedOrigins.FirstOrDefault() ?? (_config["Frontend:BaseUrl"] ?? "http://localhost:3000");

        if (string.IsNullOrWhiteSpace(redirectUri))
            return new RedirectUri(fallback);

        if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
            && OAuthRedirectValidator.IsAllowedRedirectUri(uri, allowedOrigins))
        {
            return new RedirectUri(redirectUri);
        }

        _logger.LogWarning("Rejected redirectUri {RedirectUri}, falling back to {Fallback}", redirectUri, fallback);
        return new RedirectUri(fallback);
    }

    /// <inheritdoc />
    public string BuildExchangeRedirect(RedirectUri redirectUri, string exchangeCode)
    {
        var baseUri = redirectUri.Value.Split('#')[0].Split('?')[0];
        return QueryHelpers.AddQueryString(baseUri, "code", exchangeCode);
    }

    /// <inheritdoc />
    public string? ValidateState(string state) =>
        OAuthStateValidator.ValidateState(state, GetStateSecret());

    #region Configuration

    /// <summary>
    /// Builds GitHub and Google OAuth endpoint, credential, scope, and provider-specific authorization parameter configuration from app settings.
    /// </summary>
    private Dictionary<OAuthProvider, OAuthProviderConfig> BuildProviderConfigs()
    {
        return new Dictionary<OAuthProvider, OAuthProviderConfig>
        {
            [OAuthProvider.GitHub] = new(
                Credentials: new OAuthCredentials(
                    _config["Authentication:GitHub:ClientId"] ?? "",
                    _config["Authentication:GitHub:ClientSecret"] ?? ""),
                Endpoints: new OAuthEndpoints(
                    AuthorizationUrl: "https://github.com/login/oauth/authorize",
                    TokenUrl: "https://github.com/login/oauth/access_token",
                    UserInfoUrl: "https://api.github.com/user"),
                Scope: "read:user user:email")
            {
                AdditionalParams = new Dictionary<string, string>
                {
                    ["allow_signup"] = "true"
                }
            },
            [OAuthProvider.Google] = new(
                Credentials: new OAuthCredentials(
                    _config["Authentication:Google:ClientId"] ?? "",
                    _config["Authentication:Google:ClientSecret"] ?? ""),
                Endpoints: new OAuthEndpoints(
                    AuthorizationUrl: "https://accounts.google.com/o/oauth2/v2/auth",
                    TokenUrl: "https://oauth2.googleapis.com/token",
                    UserInfoUrl: "https://www.googleapis.com/oauth2/v2/userinfo"),
                Scope: "openid email profile")
            {
                AdditionalParams = new Dictionary<string, string>
                {
                    ["response_type"] = "code",
                    ["access_type"] = "offline",
                    ["prompt"] = "consent"
                }
            }
        };
    }

    /// <summary>
    /// Returns the configured provider settings or throws when either the client ID or client secret is missing.
    /// </summary>
    private OAuthProviderConfig GetProviderConfig(OAuthProvider provider)
    {
        var config = _providerConfigs[provider];
        if (string.IsNullOrEmpty(config.ClientId) || string.IsNullOrEmpty(config.ClientSecret))
            throw new InvalidOperationException($"{provider} OAuth is not configured.");
        return config;
    }

    #endregion

    #region Token Exchange

    /// <summary>
    /// Posts the authorization code to the provider token endpoint, adds Google's authorization-code grant type when needed, logs non-success status codes, and returns the extracted access token or null.
    /// </summary>
    private async Task<string?> ExchangeCodeForTokenAsync(OAuthCodeExchange exchange, OAuthProviderConfig config)
    {
        var httpClient = CreateConfiguredHttpClient();
        
        var tokenParams = new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["code"] = exchange.Code,
            ["redirect_uri"] = exchange.CallbackUrl
        };

        // Google requires grant_type
        if (exchange.Provider == OAuthProvider.Google)
            tokenParams["grant_type"] = "authorization_code";

        var response = await httpClient.PostAsync(config.TokenEndpoint, new FormUrlEncodedContent(tokenParams));
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("{Provider} token exchange failed with {Status}", exchange.Provider, response.StatusCode);
            return null;
        }

        return ExtractAccessToken(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Creates an HTTP client that requests JSON and identifies AuthService in the user-agent header for OAuth provider calls.
    /// </summary>
    private HttpClient CreateConfiguredHttpClient()
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DevHuntAuthService/1.0");
        return httpClient;
    }

    /// <summary>
    /// Parses the provider token JSON and returns the `access_token` value when present.
    /// </summary>
    private static string? ExtractAccessToken(string tokenJson)
    {
        using var tokenDoc = JsonDocument.Parse(tokenJson);
        return tokenDoc.RootElement.TryGetProperty("access_token", out var accessTokenElement)
            ? accessTokenElement.GetString()
            : null;
    }

    #endregion

    #region User Info Fetching

    /// <summary>
    /// Fetches provider user information with a bearer token, logs non-success status codes, and dispatches to the GitHub or Google parser.
    /// </summary>
    private async Task<OAuthUserInfo?> FetchUserInfoAsync(OAuthProvider provider, OAuthProviderConfig config, string accessToken)
    {
        var httpClient = CreateConfiguredHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync(config.UserInfoEndpoint);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("{Provider} user info failed with {Status}", provider, response.StatusCode);
            return null;
        }

        return provider switch
        {
            OAuthProvider.GitHub => await ParseGitHubUserInfoAsync(httpClient, await response.Content.ReadAsStringAsync()),
            OAuthProvider.Google => ParseGoogleUserInfo(await response.Content.ReadAsStringAsync()),
            _ => null
        };
    }

    /// <summary>
    /// Parses GitHub user JSON, falls back to the `/user/emails` endpoint when email is absent, logs and returns null if no email is available, and preserves the login as a suggested username.
    /// </summary>
    private async Task<OAuthUserInfo?> ParseGitHubUserInfoAsync(HttpClient httpClient, string json)
    {
        using var userDoc = JsonDocument.Parse(json);
        var id = userDoc.RootElement.GetProperty("id").GetRawText();
        var email = userDoc.RootElement.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
        var name = userDoc.RootElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var login = userDoc.RootElement.TryGetProperty("login", out var loginElement) ? loginElement.GetString() : null;

        // GitHub may not return email in user info, need to fetch from emails endpoint
        if (string.IsNullOrEmpty(email))
            email = await FetchGitHubPrimaryEmailAsync(httpClient);

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("GitHub did not return email for user id {Id}", id);
            return null;
        }

        return new OAuthUserInfo(email, id, name, login);
    }

    /// <summary>
    /// Parses Google user JSON and returns null when either the provider ID or email is missing.
    /// </summary>
    private OAuthUserInfo? ParseGoogleUserInfo(string json)
    {
        using var userDoc = JsonDocument.Parse(json);
        var id = userDoc.RootElement.GetProperty("id").GetString();
        var email = userDoc.RootElement.GetProperty("email").GetString();
        var name = userDoc.RootElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(id))
            return null;

        return new OAuthUserInfo(email, id, name);
    }

    /// <summary>
    /// Reads GitHub account emails and returns the primary email when available, otherwise the first listed email, or null on request failure.
    /// </summary>
    private async Task<string?> FetchGitHubPrimaryEmailAsync(HttpClient httpClient)
    {
        var response = await httpClient.GetAsync("https://api.github.com/user/emails");
        if (!response.IsSuccessStatusCode)
            return null;

        var emails = JsonSerializer.Deserialize<List<GitHubEmail>>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return emails?.FirstOrDefault(e => e.Primary)?.Email ?? emails?.FirstOrDefault()?.Email;
    }

    #endregion

    #region User Management

    /// <summary>
    /// Finds an existing user by the provider-specific GitHub or Google ID, returning null for unsupported providers or no match.
    /// </summary>
    private async Task<User?> FindUserByProviderIdAsync(OAuthProvider provider, string providerId, CancellationToken ct = default)
    {
        return provider switch
        {
            OAuthProvider.GitHub => await _context.Users.FirstOrDefaultAsync(u => u.GithubId == providerId, ct),
            OAuthProvider.Google => await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == providerId, ct),
            _ => null
        };
    }

    /// <summary>
    /// Links a provider ID to an existing email-matched user when that provider ID is empty, marks the account verified, and persists the update.
    /// </summary>
    private async Task<User> LinkExistingAccountAsync(OAuthProvider provider, OAuthUserInfo info, User user, CancellationToken ct = default)
    {
        switch (provider)
        {
            case OAuthProvider.GitHub when string.IsNullOrEmpty(user.GithubId):
                user.GithubId = info.ProviderUserId;
                break;
            case OAuthProvider.Google when string.IsNullOrEmpty(user.GoogleId):
                user.GoogleId = info.ProviderUserId;
                break;
        }

        user.IsVerified = true;
        user.IsEmailVerified = true;
        await _context.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>
    /// Creates a verified active participant from OAuth user info, auto-claims a valid unused suggested username, and stores the provider-specific ID.
    /// </summary>
    private async Task<User> CreateNewExternalUserAsync(OAuthProvider provider, OAuthUserInfo info, CancellationToken ct = default)
    {
        // Auto-claim the provider's login as the DevHunt username if it's available and valid
        string? autoUsername = null;
        if (!string.IsNullOrEmpty(info.SuggestedUsername) &&
            System.Text.RegularExpressions.Regex.IsMatch(info.SuggestedUsername, @"^[a-zA-Z0-9_\-\.]{3,50}$"))
        {
            var taken = await _context.Users.AnyAsync(u => u.Username == info.SuggestedUsername, ct);
            if (!taken) autoUsername = info.SuggestedUsername;
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = info.Email,
            FullName = info.FullName,
            Username = autoUsername,
            Role = "participant",
            GithubId = provider == OAuthProvider.GitHub ? info.ProviderUserId : null,
            GithubUsername = provider == OAuthProvider.GitHub ? info.SuggestedUsername : null,
            GoogleId = provider == OAuthProvider.Google ? info.ProviderUserId : null,
            IsVerified = true,
            IsEmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync(ct);
        return newUser;
    }

    #endregion

    private string GetStateSecret() => OAuthStateValidator.ResolveStateSecret(_config);
}
