namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Provider summary for model-picker UI, derived from registered <see cref="ILlmProvider"/> instances.
/// </summary>
/// <param name="ProviderId">Stable provider id (for example <c>openai</c>).</param>
/// <param name="DisplayName">Human-readable provider name.</param>
/// <param name="SupportsKeyValidation">Whether keys can be validated via <see cref="ILlmProvider.ValidateKeyAsync"/>.</param>
/// <param name="SupportsStreaming">Whether chat streaming is supported.</param>
public sealed record LlmProviderView(
    string ProviderId,
    string DisplayName,
    bool SupportsKeyValidation,
    bool SupportsStreaming);

/// <summary>
/// Enabled model row from the local <c>LlmModels</c> registry exposed to clients.
/// </summary>
/// <param name="Id">Database row id.</param>
/// <param name="Provider">Provider id owning the model.</param>
/// <param name="ModelId">Wire-format model id passed to providers.</param>
/// <param name="DisplayName">UI label.</param>
/// <param name="Description">Optional marketing or capability blurb.</param>
/// <param name="Tier">Pricing or capability tier label.</param>
/// <param name="ContextWindow">Advertised context window in tokens.</param>
/// <param name="MaxOutputTokens">Optional max completion tokens.</param>
/// <param name="SupportsTools">Whether tool calling is available.</param>
/// <param name="SupportsStreaming">Whether streaming completions are available.</param>
/// <param name="SupportsVision">Whether image inputs are supported.</param>
/// <param name="InputPricePer1M">USD price per million input tokens, when known.</param>
/// <param name="OutputPricePer1M">USD price per million output tokens, when known.</param>
/// <param name="SortOrder">UI sort key within a provider group.</param>
public sealed record LlmModelView(
    Guid Id,
    string Provider,
    string ModelId,
    string DisplayName,
    string? Description,
    string Tier,
    int ContextWindow,
    int? MaxOutputTokens,
    bool SupportsTools,
    bool SupportsStreaming,
    bool SupportsVision,
    decimal? InputPricePer1M,
    decimal? OutputPricePer1M,
    int SortOrder);

/// <summary>
/// Filters for <see cref="ILlmModelRegistryService.ListModelsAsync"/>.
/// </summary>
/// <param name="Provider">Optional provider id filter.</param>
/// <param name="RequiresTools">When true, only models with tool support.</param>
/// <param name="RequiresVision">When true, only vision-capable models.</param>
/// <param name="RequiresStreaming">When true, only streaming-capable models.</param>
/// <param name="UserId">When set, merges this user's synced models with the platform catalog.</param>
public sealed record LlmModelQuery(
    string? Provider = null,
    bool? RequiresTools = null,
    bool? RequiresVision = null,
    bool? RequiresStreaming = null,
    Guid? UserId = null);
