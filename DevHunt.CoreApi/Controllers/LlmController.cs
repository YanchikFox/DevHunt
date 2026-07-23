using DevHunt.CoreApi.Services.Ai.Llm;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Exposes LLM provider and model catalog endpoints for BYOK AI features.
/// </summary>
[ApiController]
[Route("api/llm")]
[Authorize]
[RequireFeatureFlag("ai_features")]
public class LlmController : ControllerBase
{
    private readonly ILlmModelRegistryService _registry;
    private readonly ILlmModelSyncService _sync;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmController"/> class.
    /// </summary>
    /// <param name="registry">Registry used to list supported providers and model capabilities.</param>
    /// <param name="sync">Service that refreshes provider models from the current user's API key.</param>
    public LlmController(ILlmModelRegistryService registry, ILlmModelSyncService sync)
    {
        _registry = registry;
        _sync = sync;
    }

    /// <summary>
    /// Lists LLM providers available in the shared model registry.
    /// </summary>
    /// <param name="ct">Cancellation token for the registry query.</param>
    /// <returns>The provider list for authenticated users.</returns>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var providers = await _registry.ListProvidersAsync(ct);
        return Ok(providers);
    }

    /// <summary>
    /// Lists registered LLM models, optionally filtered by provider and required capabilities.
    /// </summary>
    /// <remarks>
    /// Returns the shared platform catalog merged with any models the current user discovered
    /// via BYOK sync — user-synced models that are already in the platform catalog are deduplicated.
    /// </remarks>
    /// <param name="provider">Optional provider identifier to restrict the model list.</param>
    /// <param name="requiresTools">When set, filters models by tool-calling support.</param>
    /// <param name="requiresVision">When set, filters models by image input support.</param>
    /// <param name="requiresStreaming">When set, filters models by streaming support.</param>
    /// <param name="ct">Cancellation token for the registry query.</param>
    /// <returns>The filtered model list including any user-synced models.</returns>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels(
        [FromQuery] string? provider,
        [FromQuery] bool? requiresTools,
        [FromQuery] bool? requiresVision,
        [FromQuery] bool? requiresStreaming,
        CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        var models = await _registry.ListModelsAsync(
            new LlmModelQuery(provider, requiresTools, requiresVision, requiresStreaming, userId),
            ct);

        return Ok(models);
    }

    /// <summary>
    /// Refreshes the calling user's personal model list from their BYOK credentials for the given provider.
    /// </summary>
    /// <remarks>
    /// Results are written to the per-user <c>UserLlmModels</c> table and do not affect the shared platform catalog.
    /// Any authenticated user with a saved key for the provider may call this endpoint.
    /// </remarks>
    /// <param name="provider">Provider whose models should be synchronized.</param>
    /// <param name="ct">Cancellation token for the provider sync.</param>
    /// <returns>
    /// Sync statistics (models seen, created, updated); 401 without a valid user claim;
    /// 400 when the provider is unsupported or no key is saved.
    /// </returns>
    [HttpPost("providers/{provider}/models/sync")]
    public async Task<IActionResult> SyncProviderModels(string provider, CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();

        try
        {
            var result = await _sync.SyncFromUserKeyAsync(userId.Value, provider, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
