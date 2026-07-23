using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// HTTP client for Integration Gateway microservice (Node.js).
/// Corresponds to architecture: Core API → Integration Gateway → External APIs (GitHub, GitLab, Jira).
/// </summary>
/// <remarks>
/// <para><strong>Integration Gateway Responsibilities</strong>:</para>
/// - OAuth 2.0 flow for GitHub/GitLab authentication
/// - Webhook event processing from external services
/// - Data synchronization (repositories, issues, commits)
/// - External API calls (create issues, update PRs, etc.)
///
/// <para><strong>Supported Integrations</strong>:</para>
/// - <b>GitHub</b>: Repository sync, issue tracking, commit webhooks
/// - <b>GitLab</b>: Repository sync, merge request tracking
/// - <b>Jira</b>: Issue synchronization (future)
/// - <b>Slack/Discord</b>: Notification webhooks (future)
///
/// <para><strong>Security</strong>:</para>
/// - Webhook signature verification (HMAC-SHA256) to prevent spoofing
/// - OAuth tokens stored encrypted in database
/// - Integration Gateway acts as proxy to isolate external API credentials from Core API
/// </remarks>
public interface IIntegrationGatewayClient
{
    /// <summary>
    /// Get OAuth authorization URL for integration setup.
    /// </summary>
    /// <param name="serviceType">Service type (github, gitlab, jira)</param>
    /// <param name="projectId">Project ID to associate integration with</param>
    /// <param name="redirectUri">Callback URL after OAuth completion</param>
    /// <returns>OAuth authorization URL to redirect user to</returns>
    Task<string> GetOAuthUrlAsync(string serviceType, Guid projectId, string redirectUri);

    /// <summary>
    /// Exchange OAuth authorization code for access token.
    /// </summary>
    /// <param name="serviceType">Service type (github, gitlab, jira)</param>
    /// <param name="code">Authorization code from OAuth callback</param>
    /// <param name="state">State parameter for CSRF protection</param>
    /// <returns>Access token for API calls</returns>
    Task<string> ExchangeOAuthCodeAsync(string serviceType, string code, string state);

    /// <summary>
    /// Trigger data synchronization for an integration.
    /// </summary>
    /// <param name="integrationId">Integration ID</param>
    /// <param name="serviceType">Service type (github, gitlab)</param>
    /// <param name="config">Integration configuration (repo URL, sync settings)</param>
    /// <param name="accessToken">Decrypted access token for external API</param>
    /// <param name="projectId">Project ID (passed to code analyzer)</param>
    Task<bool> SyncIntegrationAsync(Guid integrationId, string serviceType, Dictionary<string, object> config, string? accessToken = null, Guid? projectId = null);

    /// <summary>
    /// Create webhook in external service.
    /// </summary>
    /// <param name="integrationId">Integration ID</param>
    /// <param name="serviceType">Service type (github, gitlab)</param>
    /// <param name="webhookUrl">DevHunt webhook endpoint URL</param>
    /// <param name="events">Events to subscribe to (push, issues, pull_request, etc.)</param>
    /// <returns>Webhook ID from external service</returns>
    Task<string> CreateWebhookAsync(Guid integrationId, string serviceType, string webhookUrl, string[] events);

    /// <summary>
    /// Delete webhook from external service.
    /// </summary>
    Task DeleteWebhookAsync(Guid integrationId, string serviceType, string webhookId);

    /// <summary>
    /// Verify webhook signature (HMAC-SHA256) to prevent request spoofing.
    /// </summary>
    /// <param name="serviceType">Service type (github, gitlab)</param>
    /// <param name="payload">Webhook payload (JSON)</param>
    /// <param name="signature">Signature header from webhook request</param>
    /// <returns>True if signature is valid</returns>
    Task<bool> VerifyWebhookSignatureAsync(string serviceType, object payload, string signature);
}

/// <summary>
/// HTTP client implementation for Integration Gateway.
/// </summary>
public class IntegrationGatewayClient : IIntegrationGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IntegrationGatewayClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly bool _isEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationGatewayClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with the integration gateway base URL.</param>
    /// <param name="logger">Logger for gateway failures and disabled-service warnings.</param>
    /// <param name="configuration">Reads <c>IntegrationGateway:BaseUrl</c>.</param>
    public IntegrationGatewayClient(
        HttpClient httpClient,
        ILogger<IntegrationGatewayClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        var gatewayUrl = _configuration["IntegrationGateway:BaseUrl"] ?? "http://integration-gateway:5002";
        _httpClient.BaseAddress = new Uri(gatewayUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        _isEnabled = !string.IsNullOrEmpty(_configuration["IntegrationGateway:BaseUrl"]);

        if (!_isEnabled)
        {
            _logger.LogWarning("Integration Gateway disabled (IntegrationGateway:BaseUrl not configured)");
        }
    }

    /// <inheritdoc />
    public async Task<string> GetOAuthUrlAsync(string serviceType, Guid projectId, string redirectUri)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Integration Gateway disabled, returning placeholder OAuth URL");
            return $"https://{serviceType}.com/oauth/authorize?client_id=PLACEHOLDER&redirect_uri={redirectUri}";
        }

        try
        {
            var request = new
            {
                ServiceType = serviceType,
                ProjectId = projectId,
                RedirectUri = redirectUri
            };

            var response = await _httpClient.PostAsJsonAsync("/api/oauth/authorize", request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Integration Gateway returned {StatusCode} for OAuth URL", response.StatusCode);
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("oauthUrl").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get OAuth URL from Integration Gateway for service {ServiceType}", serviceType);
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async Task<string> ExchangeOAuthCodeAsync(string serviceType, string code, string state)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Integration Gateway disabled, cannot exchange OAuth code");
            return string.Empty;
        }

        try
        {
            var request = new
            {
                ServiceType = serviceType,
                Code = code,
                State = state
            };

            var response = await _httpClient.PostAsJsonAsync("/api/oauth/callback", request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Integration Gateway returned {StatusCode} for OAuth callback", response.StatusCode);
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("accessToken").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange OAuth code in Integration Gateway");
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SyncIntegrationAsync(Guid integrationId, string serviceType, Dictionary<string, object> config, string? accessToken = null, Guid? projectId = null)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Integration Gateway disabled, skipping sync");
            return false;
        }

        try
        {
            var request = new
            {
                IntegrationId = integrationId,
                ServiceType = serviceType,
                Config = config,
                AccessToken = accessToken,
                ProjectId = projectId
            };

            var response = await _httpClient.PostAsJsonAsync("/api/sync", request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Integration Gateway returned {StatusCode} for sync", response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync integration {IntegrationId}", integrationId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string> CreateWebhookAsync(Guid integrationId, string serviceType, string webhookUrl, string[] events)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Integration Gateway disabled, cannot create webhook");
            return string.Empty;
        }

        try
        {
            var request = new
            {
                IntegrationId = integrationId,
                ServiceType = serviceType,
                WebhookUrl = webhookUrl,
                Events = events
            };

            var response = await _httpClient.PostAsJsonAsync("/api/webhooks", request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Integration Gateway returned {StatusCode} for webhook creation", response.StatusCode);
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("webhookId").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create webhook in Integration Gateway");
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async Task DeleteWebhookAsync(Guid integrationId, string serviceType, string webhookId)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Integration Gateway disabled, cannot delete webhook");
            return;
        }

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/webhooks/{integrationId}/{serviceType}/{webhookId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Integration Gateway returned {StatusCode} for webhook deletion", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete webhook in Integration Gateway");
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyWebhookSignatureAsync(string serviceType, object payload, string signature)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Integration Gateway disabled, skipping webhook signature verification");
            return false;
        }

        try
        {
            var requestBody = JsonSerializer.Serialize(payload);
            var request = new
            {
                ServiceType = serviceType,
                Payload = requestBody,
                Signature = signature
            };

            var response = await _httpClient.PostAsJsonAsync("/api/webhooks/verify-signature", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                return result.TryGetProperty("isValid", out var isValid) && isValid.GetBoolean();
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying webhook signature");
            return false;
        }
    }
}

