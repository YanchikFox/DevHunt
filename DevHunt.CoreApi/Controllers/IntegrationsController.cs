using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DevHunt.CoreApi.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Integrations;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing project integrations with external services (GitHub, GitLab, Jira).
/// Implements integration_module from C4 architecture.
///
/// Architecture: integration_module -> integration_gateway (Node.js) for external API calls.
/// This controller manages integration settings in the DB, while real GitHub/GitLab calls
/// are delegated to Integration Gateway via IntegrationGatewayClient.
///
/// Functionality:
/// - Create and configure integrations (CRUD in DB)
/// - OAuth flow delegated to Integration Gateway
/// - Webhook management via Integration Gateway
/// - Data synchronization via Integration Gateway
/// </summary>
[ApiController]
[Route("api/integrations")]
[Authorize]
public class IntegrationsController : ControllerBase
{
    private readonly DevHuntDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IIntegrationGatewayClient _integrationGatewayClient;
    private readonly ICacheService _cacheService;
    private readonly ILogger<IntegrationsController> _logger;
    private readonly IAuditService _auditService;
    private readonly IInternalServiceAuthenticator _serviceAuth;
    private readonly IIntegrationAuthorizationService _authService;
    private readonly IOAuthCallbackHandler _oauthHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationsController"/> class.
    /// </summary>
    /// <param name="context">Database context used to persist integration configuration.</param>
    /// <param name="encryptionService">Service used to encrypt and decrypt stored access tokens.</param>
    /// <param name="integrationGatewayClient">Client that delegates OAuth, webhook verification, and sync calls to the gateway.</param>
    /// <param name="cacheService">Cache used to store temporary OAuth state.</param>
    /// <param name="logger">Logger for integration setup, sync, and security events.</param>
    /// <param name="auditService">Audit service that records integration configuration changes.</param>
    /// <param name="serviceAuth">Authenticator for internal service and gateway calls.</param>
    /// <param name="authService">Authorization service for project integration access checks.</param>
    /// <param name="oauthHandler">Handler that validates OAuth callback state and persists exchanged tokens.</param>
    public IntegrationsController(
        DevHuntDbContext context,
        IEncryptionService encryptionService,
        IIntegrationGatewayClient integrationGatewayClient,
        ICacheService cacheService,
        ILogger<IntegrationsController> logger,
        IAuditService auditService,
        IInternalServiceAuthenticator serviceAuth,
        IIntegrationAuthorizationService authService,
        IOAuthCallbackHandler oauthHandler)
    {
        _context = context;
        _encryptionService = encryptionService;
        _integrationGatewayClient = integrationGatewayClient;
        _cacheService = cacheService;
        _logger = logger;
        _auditService = auditService;
        _serviceAuth = serviceAuth;
        _authService = authService;
        _oauthHandler = oauthHandler;
    }

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Gets project integrations visible to the caller, returning not found or forbidden from the authorization service.
    /// </summary>
    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetProjectIntegrations(Guid projectId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var access = await _authService.CanViewIntegrationsAsync(projectId, userId);
        if (!access.Allowed)
        {
            return access.Reason == "Project not found" ? NotFound(access.Reason) : Forbid(access.Reason);
        }

        var integrations = await _context.Integrations
            .Where(i => i.ProjectId == projectId)
            .ToListAsync(ct);

        return Ok(integrations.Select(IntegrationResponseMapper.ToDto));
    }

    /// <summary>
    /// Creates a supported integration for a project when the caller can manage integrations, encrypting any access token.
    /// </summary>
    [HttpPost("project/{projectId}")]
    public async Task<IActionResult> CreateIntegration(Guid projectId, [FromBody] CreateIntegrationDto dto, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var access = await _authService.CanManageIntegrationsAsync(projectId, userId);
        if (!access.Allowed)
        {
            return access.Reason == "Project not found" ? NotFound(access.Reason) : Forbid(access.Reason);
        }

        // I-05: Whitelist validation — reject unknown service types
        var allowedServiceTypes = new[] { "github", "gitlab", "jira" };
        var normalizedType = dto.ServiceType.ToLower(); // I-16: normalize to lowercase
        if (!allowedServiceTypes.Contains(normalizedType))
        {
            return BadRequest($"Unsupported service type '{dto.ServiceType}'. Allowed: github, gitlab, jira");
        }

        // I-04 (B-10): SSRF — validate base_url in GitLab/Jira config before storing.
        var baseUrlError = ValidateBaseUrlIfPresent(dto.Config);
        if (baseUrlError != null) return baseUrlError;

        // Encrypt access token if provided
        string? encryptedToken = null;
        if (!string.IsNullOrEmpty(dto.AccessToken))
        {
            encryptedToken = _encryptionService.Encrypt(dto.AccessToken);
        }

        var integration = new Integration
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ServiceType = normalizedType, // I-16: stored lowercase for consistency
            Config = dto.Config,
            AccessTokenEncrypted = encryptedToken,
            IsActive = dto.IsActive ?? true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Integrations.Add(integration);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // I-03 (B-08): Race condition — rely on DB unique index IX_Integrations_ProjectId_ServiceType
            return Conflict($"Integration '{normalizedType}' already exists for this project");
        }

        // SECURITY: Audit logging for integration creation (SEC-020)
        await _auditService.LogActionAsync(userId, "IntegrationsController.CreateIntegration", "Integration", integration.Id,
            $"Created integration {dto.ServiceType} for project {projectId}");

        _logger.LogInformation("Created integration {IntegrationId} for project {ProjectId} with service {ServiceType}",
            integration.Id, projectId, dto.ServiceType);

        return CreatedAtAction(
            nameof(GetIntegration),
            new { integrationId = integration.Id },
            IntegrationResponseMapper.ToDto(integration));
    }

    /// <summary>
    /// Gets integration details without exposing encrypted access tokens.
    /// </summary>
    [HttpGet("{integrationId}")]
    public async Task<IActionResult> GetIntegration(Guid integrationId)
    {
        var userId = GetRequiredUserId();

        var integration = await _authService.LoadIntegrationWithProjectAsync(integrationId);
        if (integration == null)
        {
            return NotFound("Integration not found");
        }

        var access = await _authService.CanViewIntegrationsAsync(integration.ProjectId, userId);
        if (!access.Allowed)
        {
            return Forbid(access.Reason);
        }

        return Ok(IntegrationResponseMapper.ToDto(integration));
    }

    /// <summary>
    /// Get the decrypted access token for internal integration-gateway service use.
    /// </summary>
    /// <remarks>
    /// This endpoint is protected by internal API key authentication.
    /// Only internal services (integration-gateway) should call this.
    /// SECURITY: Uses HMAC-based authentication instead of simple API key comparison
    /// to prevent timing attacks (CVH-001) and header spoofing (CVH-002).
    /// </remarks>
    [HttpGet("{integrationId}/token")]
    [AllowAnonymous] // Uses internal service authentication instead of JWT
    public async Task<IActionResult> GetIntegrationToken(Guid integrationId, CancellationToken ct = default)
    {
        // SECURITY FIX (CVH-001, CVH-002): Validate via IInternalServiceAuthenticator
        if (!_serviceAuth.ValidateRequest(HttpContext, integrationId.ToString()))
        {
            var serviceName = Request.Headers["X-Service-Name"].FirstOrDefault();
            _logger.LogWarning("Unauthorized token request for integration {IntegrationId} from {Service}",
                integrationId, serviceName ?? "unknown");
            return Unauthorized("Invalid internal service authentication");
        }

        var integration = await _context.Integrations.FindAsync(new object[] { integrationId }, ct);
        if (integration == null)
        {
            return NotFound("Integration not found");
        }

        if (string.IsNullOrEmpty(integration.AccessTokenEncrypted))
        {
            return NotFound(new { error = "No access token configured for this integration" });
        }

        try
        {
            var decryptedToken = _encryptionService.Decrypt(integration.AccessTokenEncrypted);
            _logger.LogDebug("Token retrieved for integration {IntegrationId}", integrationId);

            return Ok(new { accessToken = decryptedToken });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt token for integration {IntegrationId}", integrationId);
            return StatusCode(500, new { error = "Failed to decrypt access token" });
        }
    }

    /// <summary>
    /// List GitHub repositories accessible via the integration's OAuth token.
    /// Used after OAuth to let the user select which repository to connect.
    /// </summary>
    [HttpGet("{integrationId}/github-repos")]
    public async Task<IActionResult> ListGitHubRepos(Guid integrationId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var integration = await _authService.LoadIntegrationWithProjectAsync(integrationId);
        if (integration == null)
            return NotFound("Integration not found");

        var access = await _authService.CanManageIntegrationsAsync(integration.ProjectId, userId);
        if (!access.Allowed)
            return Forbid(access.Reason);

        if (integration.ServiceType != "github")
            return BadRequest("Only GitHub integrations support repository listing");

        if (string.IsNullOrEmpty(integration.AccessTokenEncrypted))
            return BadRequest("No access token configured for this integration");

        try
        {
            var token = _encryptionService.Decrypt(integration.AccessTokenEncrypted);
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "DevHunt");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            var response = await httpClient.GetAsync("https://api.github.com/user/repos?per_page=100&sort=updated&affiliation=owner,collaborator,organization_member", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API returned {StatusCode} when listing repos", response.StatusCode);
                return StatusCode(502, new { error = "Failed to fetch repositories from GitHub" });
            }

            var repos = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = repos.EnumerateArray().Select(r => new
            {
                fullName = r.GetProperty("full_name").GetString(),
                name = r.GetProperty("name").GetString(),
                owner = r.GetProperty("owner").GetProperty("login").GetString(),
                isPrivate = r.GetProperty("private").GetBoolean(),
                description = r.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                updatedAt = r.GetProperty("updated_at").GetString()
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list GitHub repos for integration {IntegrationId}", integrationId);
            return StatusCode(500, new { error = "Failed to fetch repositories" });
        }
    }

    /// <summary>
    /// Updates integration config, token, or active state when the caller can manage the integration's project.
    /// </summary>
    [HttpPut("{integrationId}")]
    public async Task<IActionResult> UpdateIntegration(Guid integrationId, [FromBody] UpdateIntegrationDto dto, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var integration = await _authService.LoadIntegrationWithProjectAsync(integrationId);
        if (integration == null)
        {
            return NotFound("Integration not found");
        }

        var access = await _authService.CanManageIntegrationsAsync(integration.ProjectId, userId);
        if (!access.Allowed)
        {
            return Forbid(access.Reason);
        }

        // Update config
        if (dto.Config != null)
        {
            // I-04 (B-10): SSRF — re-validate base_url on every update, not just on create.
            var baseUrlError = ValidateBaseUrlIfPresent(dto.Config);
            if (baseUrlError != null) return baseUrlError;

            integration.Config = dto.Config;
        }

        // Update access token if provided
        if (!string.IsNullOrEmpty(dto.AccessToken))
        {
            integration.AccessTokenEncrypted = _encryptionService.Encrypt(dto.AccessToken);
        }

        // Update active status
        if (dto.IsActive.HasValue)
        {
            integration.IsActive = dto.IsActive.Value;
        }

        integration.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        // SECURITY: Audit logging for integration token/config updates (SEC-020)
        await _auditService.LogActionAsync(userId, "IntegrationsController.UpdateIntegration", "Integration", integrationId,
            $"Updated integration {integration.ServiceType} for project {integration.ProjectId}. Token updated: {!string.IsNullOrEmpty(dto.AccessToken)}");

        _logger.LogInformation("Updated integration {IntegrationId} for project {ProjectId}",
            integrationId, integration.ProjectId);

        return Ok(IntegrationResponseMapper.ToDto(integration));
    }

    /// <summary>
    /// Toggles integration active state when the caller can manage the integration's project.
    /// </summary>
    [HttpPut("{integrationId}/toggle")]
    public async Task<IActionResult> ToggleIntegration(Guid integrationId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var integration = await _authService.LoadIntegrationWithProjectAsync(integrationId);
        if (integration == null)
        {
            return NotFound("Integration not found");
        }

        var access = await _authService.CanManageIntegrationsAsync(integration.ProjectId, userId);
        if (!access.Allowed)
        {
            return Forbid(access.Reason);
        }

        integration.IsActive = !integration.IsActive;
        integration.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { IsActive = integration.IsActive });
    }

    /// <summary>
    /// Triggers integration synchronization through the gateway after validating activity, repository config, token, and access.
    /// </summary>
    [HttpPost("{integrationId}/sync")]
    public async Task<IActionResult> SyncIntegration(Guid integrationId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var integration = await _authService.LoadIntegrationWithProjectAsync(integrationId);
        if (integration == null)
        {
            return NotFound("Integration not found");
        }

        if (!integration.IsActive)
        {
            return BadRequest("Integration is not active");
        }

        var access = await _authService.CanSyncIntegrationAsync(integration, userId);
        if (!access.Allowed)
        {
            return Forbid(access.Reason);
        }

        // Delegate sync to Integration Gateway
        var config = integration.Config ?? new Dictionary<string, object>();
        if (!config.ContainsKey("repository"))
        {
            return BadRequest("No repository configured. Please select a repository first.");
        }

        if (integration.AccessTokenEncrypted == null)
        {
            return BadRequest("No access token configured. Please reconnect GitHub.");
        }

        var decryptedToken = _encryptionService.Decrypt(integration.AccessTokenEncrypted);
        var syncSuccess = await _integrationGatewayClient.SyncIntegrationAsync(
            integrationId,
            integration.ServiceType,
            config,
            decryptedToken,
            integration.ProjectId);

        if (!syncSuccess)
        {
            return StatusCode(502, new { Message = "Sync failed in Integration Gateway" });
        }

        integration.LastSyncAt = DateTime.UtcNow;
        integration.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Synced integration {IntegrationId} for project {ProjectId} via Integration Gateway",
            integrationId, integration.ProjectId);

        return Ok(new { Message = "Sync initiated", LastSyncAt = integration.LastSyncAt });
    }

    /// <summary>
    /// Deletes an integration when the caller can manage the integration's project.
    /// </summary>
    [HttpDelete("{integrationId}")]
    public async Task<IActionResult> DeleteIntegration(Guid integrationId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var integration = await _authService.LoadIntegrationWithProjectAsync(integrationId);
        if (integration == null)
        {
            return NotFound("Integration not found");
        }

        var access = await _authService.CanManageIntegrationsAsync(integration.ProjectId, userId);
        if (!access.Allowed)
        {
            return Forbid(access.Reason);
        }

        _context.Integrations.Remove(integration);
        await _context.SaveChangesAsync(ct);

        // SECURITY: Audit logging for integration deletion (SEC-020)
        await _auditService.LogActionAsync(userId, "IntegrationsController.DeleteIntegration", "Integration", integrationId,
            $"Deleted integration {integration.ServiceType} for project {integration.ProjectId}");

        _logger.LogInformation("Deleted integration {IntegrationId} for project {ProjectId}",
            integrationId, integration.ProjectId);

        return NoContent();
    }

    /// <summary>
    /// Gets an OAuth authorization URL from the gateway and stores a short-lived state token for callback validation.
    /// </summary>
    [HttpGet("oauth/{serviceType}/authorize")]
    public async Task<IActionResult> GetOAuthUrl(string serviceType, [FromQuery] Guid projectId)
    {
        var userId = GetRequiredUserId();

        var access = await _authService.CanManageIntegrationsAsync(projectId, userId);
        if (!access.Allowed)
        {
            return access.Reason == "Project not found" ? NotFound(access.Reason) : Forbid(access.Reason);
        }

        // Use fixed callback URL (Request.Host may be wrong when behind proxy)
        var apiBaseUrl = Environment.GetEnvironmentVariable("API_PUBLIC_URL") ?? "http://localhost:7002";
        var redirectUri = $"{apiBaseUrl}/api/integrations/oauth/{serviceType}/callback";

        // SECURITY: Generate state token for CSRF protection
        var stateToken = Guid.NewGuid().ToString("N");
        var stateData = new OAuthStateDto
        {
            ProjectId = projectId,
            ServiceType = serviceType,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        // Store state token in cache (expires in 10 minutes)
        await _cacheService.SetAsync($"oauth_state:{stateToken}", stateData, TimeSpan.FromMinutes(10));

        // Delegate to Integration Gateway (state token will be included in OAuth URL)
        var oauthUrl = await _integrationGatewayClient.GetOAuthUrlAsync(serviceType, projectId, redirectUri);

        if (string.IsNullOrEmpty(oauthUrl))
        {
            return StatusCode(502, "Integration Gateway unavailable");
        }

        // Append state token to OAuth URL
        var separator = oauthUrl.Contains('?') ? "&" : "?";
        oauthUrl = $"{oauthUrl}{separator}state={stateToken}";

        return Ok(new { oauthUrl, provider = serviceType, state = stateToken });
    }

    /// <summary>
    /// Completes OAuth setup by validating state through the callback handler and redirecting the user back to the frontend.
    /// </summary>
    [HttpGet("oauth/{serviceType}/callback")]
    [AllowAnonymous] // OAuth callback can be without authorization (verification via state)
    public async Task<IActionResult> OAuthCallback(string serviceType, [FromQuery] string code, [FromQuery] string state)
    {
        var frontendBaseUrl = Environment.GetEnvironmentVariable("FRONTEND_BASE_URL") ?? "http://localhost:3000";

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Redirect($"{frontendBaseUrl}/dashboard/projects?error=missing_oauth_params");
        }

        var result = await _oauthHandler.ProcessCallbackAsync(serviceType, code, state);

        if (result.IsSuccess)
        {
            return Redirect($"{frontendBaseUrl}/dashboard/projects/{result.ProjectId}?integration=success");
        }

        // Handle error cases with appropriate redirect
        // I-14: Escape error string to prevent URL parameter injection
        var encodedError = Uri.EscapeDataString(result.Error ?? "unknown_error");
        var errorRedirect = result.ProjectId.HasValue
            ? $"{frontendBaseUrl}/dashboard/projects/{result.ProjectId}?error={encodedError}"
            : $"{frontendBaseUrl}/dashboard/projects?error={encodedError}";

        return Redirect(errorRedirect);
    }

    /// <summary>
    /// Receives external webhooks, requiring an active integration and a gateway-verified HMAC signature.
    /// </summary>
    [HttpPost("webhook/{integrationId}")]
    [AllowAnonymous] // Webhook can be without authorization (validation via signature)
    public async Task<IActionResult> Webhook(Guid integrationId, [FromBody] object payload, [FromHeader(Name = "X-Hub-Signature-256")] string? signature, CancellationToken ct = default)
    {
        var integration = await _context.Integrations
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == integrationId, ct);

        if (integration == null)
        {
            return NotFound("Integration not found");
        }

        if (!integration.IsActive)
        {
            return BadRequest("Integration is not active");
        }

        // SECURITY: Validate webhook signature to prevent forgery
        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Webhook request without signature for integration {IntegrationId}", integrationId);
            return Unauthorized("Missing signature");
        }

        var isValid = await _integrationGatewayClient.VerifyWebhookSignatureAsync(
            integration.ServiceType,
            payload,
            signature);

        if (!isValid)
        {
            _logger.LogWarning("Invalid webhook signature for integration {IntegrationId}", integrationId);
            return Unauthorized("Invalid signature");
        }

        // The controller validates the webhook and logs receipt; downstream event processing is not implemented here.
        _logger.LogInformation("Webhook received and validated for integration {IntegrationId}, service {ServiceType}",
            integrationId, integration.ServiceType);

        return Ok(new { Message = "Webhook processed" });
    }

    // ========================================================================
    // Internal API for Integration Gateway
    // ========================================================================

    /// <summary>
    /// Finds an active GitHub integration by repository name for internal gateway webhook routing.
    /// </summary>
    [HttpGet("by-repository")]
    [AllowAnonymous] // Internal service call
    public async Task<IActionResult> FindByRepository([FromQuery] string repository, CancellationToken ct = default)
    {
        if (!IsInternalServiceCall())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(repository))
        {
            return BadRequest("repository parameter is required");
        }

        // I-06: Safe JSONB search — load candidates server-side, filter client-side to avoid string injection.
        // Number of GitHub integrations per platform is small, so full client-side scan is acceptable.
        var candidates = await _context.Integrations
            .Where(i => i.IsActive && i.ServiceType == "github" && i.ConfigJson != null)
            .ToListAsync(ct);

        var integration = candidates.FirstOrDefault(i =>
        {
            if (string.IsNullOrEmpty(i.ConfigJson)) return false;
            try
            {
                using var doc = JsonDocument.Parse(i.ConfigJson);
                return doc.RootElement.TryGetProperty("repository", out var r) && r.GetString() == repository;
            }
            catch { return false; }
        });

        if (integration == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            integration.Id,
            integration.ProjectId,
            integration.ServiceType,
            integration.IsActive,
            Config = integration.Config,
            HasAccessToken = !string.IsNullOrEmpty(integration.AccessTokenEncrypted)
        });
    }

    /// <summary>
    /// Decrypts an active integration token for validated internal services, returning a null token when none is configured.
    /// </summary>
    [HttpGet("{id:guid}/decrypt-token")]
    [AllowAnonymous] // Internal service call — validated via IInternalServiceAuthenticator
    public async Task<IActionResult> DecryptToken(Guid id, CancellationToken ct = default)
    {
        if (!IsInternalServiceCall()) return Forbid();

        var integration = await _context.Integrations
            .FirstOrDefaultAsync(i => i.Id == id && i.IsActive, ct);

        if (integration == null) return NotFound();

        if (string.IsNullOrEmpty(integration.AccessTokenEncrypted))
        {
            return Ok(new { accessToken = (string?)null });
        }

        try
        {
            var decryptedToken = _encryptionService.Decrypt(integration.AccessTokenEncrypted);
            return Ok(new { accessToken = decryptedToken });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt access token for integration {IntegrationId}", id);
            return StatusCode(500, new { error = "Failed to decrypt token" });
        }
    }

    /// <summary>
    /// Gets active project integrations for validated internal services such as event consumers or sync jobs.
    /// </summary>
    [HttpGet("project/{projectId}/for-internal")]
    [AllowAnonymous] // Internal service call — validated via IInternalServiceAuthenticator
    public async Task<IActionResult> GetProjectIntegrationsInternal(Guid projectId, CancellationToken ct = default)
    {
        if (!IsInternalServiceCall()) return Forbid();

        var integrations = await _context.Integrations
            .Where(i => i.ProjectId == projectId && i.IsActive)
            .Select(i => new { i.Id, i.ProjectId, i.ServiceType, i.IsActive, Config = i.Config })
            .ToListAsync(ct);

        return Ok(integrations);
    }

    /// <summary>
    /// Validates the current request as an internal service call using the request path as the signing subject.
    /// </summary>
    private bool IsInternalServiceCall()
    {
        var requestPath = HttpContext.Request.Path.Value ?? "";
        return _serviceAuth.ValidateRequest(HttpContext, requestPath);
    }

    /// <summary>Detects PostgreSQL unique constraint violation (SQLSTATE 23505).</summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";

    /// <summary>
    /// Validates an optional <c>base_url</c> integration config value and returns a bad request for unsafe URLs.
    /// </summary>
    private IActionResult? ValidateBaseUrlIfPresent(Dictionary<string, object>? config)
    {
        if (config == null || !config.TryGetValue("base_url", out var rawBaseUrl))
            return null;
        var baseUrlStr = rawBaseUrl?.ToString();
        if (!string.IsNullOrEmpty(baseUrlStr) && !SecurityHelpers.IsValidUrl(baseUrlStr))
            return BadRequest("Invalid 'base_url' in config: must be a valid https:// URL.");
        return null;
    }

}

/// <summary>
/// Request body for creating a project integration.
/// </summary>
public class CreateIntegrationDto
{
    /// <summary>External service type (github, gitlab, jira).</summary>
    [Required]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty; // "github", "gitlab", "jira"

    /// <summary>Provider-specific configuration values.</summary>
    public Dictionary<string, object>? Config { get; set; }

    /// <summary>Access token to store for integration.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Whether the integration is active.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Request body for updating an existing project integration.
/// </summary>
public class UpdateIntegrationDto
{
    /// <summary>Updated provider configuration values.</summary>
    public Dictionary<string, object>? Config { get; set; }

    /// <summary>Updated access token (optional).</summary>
    public string? AccessToken { get; set; }

    /// <summary>Whether the integration should be active.</summary>
    public bool? IsActive { get; set; }
}

