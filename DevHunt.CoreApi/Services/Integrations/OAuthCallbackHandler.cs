using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevHunt.CoreApi.Services.Integrations;

/// <summary>
/// Result of OAuth callback processing.
/// </summary>
public record OAuthCallbackResult
{
    /// <summary>Whether OAuth completed successfully.</summary>
    public bool IsSuccess { get; init; }
    /// <summary>Project associated with the OAuth flow, when known.</summary>
    public Guid? ProjectId { get; init; }
    /// <summary>Error code when <see cref="IsSuccess"/> is false.</summary>
    public string? Error { get; init; }

    /// <summary>Creates a successful callback result.</summary>
    /// <param name="projectId">Project that completed OAuth setup.</param>
    /// <returns>Success result carrying the project ID.</returns>
    public static OAuthCallbackResult Success(Guid projectId) => new() { IsSuccess = true, ProjectId = projectId };

    /// <summary>Creates a failed callback result with an error code.</summary>
    /// <param name="error">Machine-readable failure code.</param>
    /// <param name="projectId">Related project ID when known.</param>
    /// <returns>Failure result with optional project context.</returns>
    public static OAuthCallbackResult Failure(string error, Guid? projectId = null) => new() { IsSuccess = false, Error = error, ProjectId = projectId };
}

/// <summary>
/// Handles OAuth callback processing for integration setup.
/// Extracted from IntegrationsController to reduce cyclomatic complexity.
/// </summary>
public interface IOAuthCallbackHandler
{
    /// <summary>Process OAuth callback: validate state, exchange code, create/update integration.</summary>
    Task<OAuthCallbackResult> ProcessCallbackAsync(string serviceType, string code, string state, CancellationToken ct = default);
}

/// <summary>
/// Implementation of OAuth callback handler.
/// </summary>
public class OAuthCallbackHandler : IOAuthCallbackHandler
{
    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cacheService;
    private readonly IIntegrationGatewayClient _gatewayClient;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<OAuthCallbackHandler> _logger;
    private readonly IAuditService _auditService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthCallbackHandler"/> class.
    /// </summary>
    /// <param name="db">Database context for projects and integrations.</param>
    /// <param name="cacheService">Cache used for one-time OAuth state tokens.</param>
    /// <param name="gatewayClient">Integration gateway client for token exchange.</param>
    /// <param name="encryptionService">Encrypts access tokens before persistence.</param>
    /// <param name="logger">Logger for OAuth flow diagnostics.</param>
    /// <param name="auditService">Audit trail for created or updated integrations.</param>
    public OAuthCallbackHandler(
        DevHuntDbContext db,
        ICacheService cacheService,
        IIntegrationGatewayClient gatewayClient,
        IEncryptionService encryptionService,
        ILogger<OAuthCallbackHandler> logger,
        IAuditService auditService)
    {
        _db = db;
        _cacheService = cacheService;
        _gatewayClient = gatewayClient;
        _encryptionService = encryptionService;
        _logger = logger;
        _auditService = auditService;
    }

    /// <summary>Validates cached OAuth state, exchanges the code, and creates or updates the integration.</summary>
    public async Task<OAuthCallbackResult> ProcessCallbackAsync(string serviceType, string code, string state, CancellationToken ct = default)
    {
        // Step 1: Validate state token
        var stateKey = $"oauth_state:{state}";
        var cachedState = await _cacheService.GetAsync<OAuthStateDto>(stateKey);
        if (cachedState == null || cachedState.ServiceType != serviceType)
        {
            _logger.LogWarning("Invalid or expired OAuth state token");
            return OAuthCallbackResult.Failure("invalid_state");
        }

        var projectId = cachedState.ProjectId;

        // Step 2: Remove state token (one-time use)
        await _cacheService.RemoveAsync(stateKey);

        // Step 3: Exchange code for access token
        var accessToken = await _gatewayClient.ExchangeOAuthCodeAsync(serviceType, code, state);
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogError("Failed to exchange OAuth code for {ServiceType}", serviceType);
            return OAuthCallbackResult.Failure("oauth_exchange_failed", projectId);
        }

        // Step 4: Verify project exists
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null)
        {
            return OAuthCallbackResult.Failure("project_not_found");
        }

        // Step 5: Create or update integration
        var integrationId = await CreateOrUpdateIntegrationAsync(projectId, serviceType, accessToken, ct);

        // I-08: Audit log — OAuth-created integrations were silently skipped before
        await _auditService.LogActionAsync(cachedState.UserId, "OAuthCallbackHandler.ProcessCallbackAsync",
            "Integration", integrationId, $"OAuth integration created/updated for project {projectId}, service {serviceType}");

        _logger.LogInformation("OAuth integration completed for project {ProjectId}, service {ServiceType}",
            projectId, serviceType);

        return OAuthCallbackResult.Success(projectId);
    }

    /// <summary>Persists encrypted tokens for a new or existing project integration.</summary>
    private async Task<Guid> CreateOrUpdateIntegrationAsync(Guid projectId, string serviceType, string accessToken, CancellationToken ct = default)
    {
        var existingIntegration = await _db.Integrations
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.ServiceType == serviceType, ct);

        if (existingIntegration != null)
        {
            existingIntegration.AccessTokenEncrypted = _encryptionService.Encrypt(accessToken);
            existingIntegration.IsActive = true;
            existingIntegration.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return existingIntegration.Id;
        }
        else
        {
            var integration = new Integration
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ServiceType = serviceType,
                AccessTokenEncrypted = _encryptionService.Encrypt(accessToken),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Integrations.Add(integration);
            await _db.SaveChangesAsync(ct);
            return integration.Id;
        }
    }

}

/// <summary>OAuth state payload used to validate external auth callbacks.</summary>
public record OAuthStateDto
{
        /// <summary>Project the OAuth flow is connecting.</summary>
    public Guid ProjectId { get; init; }
    /// <summary>External provider key, e.g. <c>github</c>.</summary>
    public string ServiceType { get; init; } = string.Empty;
    /// <summary>User who initiated the OAuth flow.</summary>
    public Guid UserId { get; init; }
    /// <summary>UTC timestamp when the state token was created.</summary>
    public DateTime CreatedAt { get; init; }
}
