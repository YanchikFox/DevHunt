namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Resolves registered <see cref="ILlmProvider"/> implementations by provider id.
/// </summary>
public interface ILlmProviderRegistry
{
    /// <summary>All providers wired up via DI, for "list supported providers" UIs.</summary>
    IReadOnlyList<ILlmProvider> All { get; }

    /// <summary>
    /// Resolves a provider by its <see cref="ILlmProvider.ProviderId"/>. Returns
    /// null if no implementation for that id is registered, so callers can
    /// surface "provider not supported" instead of throwing on user input.
    /// </summary>
    ILlmProvider? Get(string providerId);
}

/// <summary>
/// DI-driven registry: every <see cref="ILlmProvider"/> registered in the
/// container gets indexed by its <c>ProviderId</c>. Phase 1 ships zero
/// providers — phase 2 adds OpenAI-compatible, Anthropic and Gemini adapters,
/// and they will register themselves the same way.
/// </summary>
public sealed class LlmProviderRegistry : ILlmProviderRegistry
{
    private readonly Dictionary<string, ILlmProvider> _byId;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderRegistry"/> class.
    /// </summary>
    /// <param name="providers">Registry of supported LLM providers.</param>
    public LlmProviderRegistry(IEnumerable<ILlmProvider> providers)
    {
        _byId = providers.ToDictionary(p => p.ProviderId, StringComparer.OrdinalIgnoreCase);
        All = _byId.Values.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ILlmProvider> All { get; }

    /// <inheritdoc />
    public ILlmProvider? Get(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return null;
        return _byId.TryGetValue(providerId, out var provider) ? provider : null;
    }
}
