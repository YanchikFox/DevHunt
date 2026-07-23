namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Read-only view of a stored user API key for LLM providers.
/// Served by <see cref="ILlmModelRegistryService"/> and related controllers.
/// </summary>
public interface ILlmModelRegistryService
{
    /// <summary>
    /// Lists providers registered in <see cref="ILlmProviderRegistry"/> for the model picker UI.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<LlmProviderView>> ListProvidersAsync(CancellationToken ct);

    /// <summary>
    /// Lists enabled models from the database, optionally filtered by provider and capability flags.
    /// </summary>
    /// <param name="query">Optional filters.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<LlmModelView>> ListModelsAsync(LlmModelQuery query, CancellationToken ct);
}
