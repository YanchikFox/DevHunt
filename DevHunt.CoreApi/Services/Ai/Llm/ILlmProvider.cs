using DevHunt.CoreApi.Services.Ai.Llm.Models;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Abstraction over a specific LLM provider (OpenAI, Anthropic, Gemini, ...).
/// Implementations translate between the canonical <see cref="LlmChatRequest"/>
/// shape and the provider's wire protocol, including streaming semantics.
///
/// Concrete adapters land in phase 2 - phase 1 only ships the contract.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Stable lowercase identifier - "openai", "anthropic", "gemini", "groq",
    /// "openrouter". Stored verbatim in <c>UserApiKey.Provider</c> and used
    /// for registry lookup.
    /// </summary>
    string ProviderId { get; }

    /// <summary>Human-readable name for UI ("OpenAI", "Anthropic").</summary>
    string DisplayName { get; }

    /// <summary>
    /// Issues a minimal authenticated request to verify the key is real and
    /// has at least basic access. Implementations should pick the cheapest
    /// possible call (typically <c>GET /models</c>).
    /// </summary>
    Task<KeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken ct);

    /// <summary>
    /// Lists models reported by the provider for this key. Used to cross-check
    /// the local <c>LlmModel</c> registry and surface availability to the user.
    /// May be slower / paginated - call sparingly and cache.
    /// </summary>
    Task<IReadOnlyList<ProviderModelInfo>> ListModelsAsync(string apiKey, CancellationToken ct);

    /// <summary>
    /// Streams a chat completion. Yields one <see cref="LlmChatChunk"/> per
    /// network event; the final chunk carries a non-<see cref="LlmFinishReason.InProgress"/>
    /// reason and (where the provider reports it) the final
    /// <see cref="LlmUsage"/>. Throws on transport / auth / rate-limit errors.
    /// </summary>
    IAsyncEnumerable<LlmChatChunk> StreamChatAsync(
        LlmChatRequest request,
        string apiKey,
        CancellationToken ct);
}

/// <summary>
/// Result of <see cref="ILlmProvider.ValidateKeyAsync"/>. <see cref="IsValid"/>
/// false when the provider returned 401/403 or the key is otherwise unusable;
/// transient network errors should bubble up as exceptions instead.
/// </summary>
/// <param name="IsValid">Whether the provider accepted the key.</param>
/// <param name="ErrorMessage">Safe user-facing error message when validation failed.</param>
/// <param name="AccountInfo">Optional account-level info ("Tier 1", remaining credits, etc.).</param>
public sealed record KeyValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    string? AccountInfo = null
);

/// <summary>Model metadata returned directly by a provider.</summary>
/// <param name="ModelId">Provider wire-format model id.</param>
/// <param name="DisplayName">Optional human-readable model name.</param>
/// <param name="ContextWindow">Optional context window reported by the provider.</param>
/// <param name="SupportsTools">Optional tool-calling capability reported by the provider.</param>
/// <param name="SupportsStreaming">Optional streaming capability reported by the provider.</param>
/// <param name="SupportsVision">Optional vision capability reported by the provider.</param>
/// <param name="InputPricePer1M">Optional USD price per 1,000,000 input tokens.</param>
/// <param name="OutputPricePer1M">Optional USD price per 1,000,000 output tokens.</param>
/// <param name="MaxOutputTokens">Optional maximum output tokens reported by the provider.</param>
public sealed record ProviderModelInfo(
    string ModelId,
    string? DisplayName = null,
    int? ContextWindow = null,
    bool? SupportsTools = null,
    bool? SupportsStreaming = null,
    bool? SupportsVision = null,
    decimal? InputPricePer1M = null,
    decimal? OutputPricePer1M = null,
    int? MaxOutputTokens = null
);
