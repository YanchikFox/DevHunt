using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Reads enabled models from the database and lists wired providers from <see cref="ILlmProviderRegistry"/>.
/// </summary>
public sealed class LlmModelRegistryService : ILlmModelRegistryService
{
    private readonly DevHuntDbContext _db;
    private readonly ILlmProviderRegistry _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmModelRegistryService"/> class.
    /// </summary>
    /// <param name="db">Database context for model rows.</param>
    /// <param name="providers">Registry of live provider implementations.</param>
    public LlmModelRegistryService(DevHuntDbContext db, ILlmProviderRegistry providers)
    {
        _db = db;
        _providers = providers;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LlmProviderView>> ListProvidersAsync(CancellationToken ct)
    {
        IReadOnlyList<LlmProviderView> providers = _providers.All
            .OrderBy(p => p.DisplayName)
            .Select(p => new LlmProviderView(
                p.ProviderId,
                p.DisplayName,
                SupportsKeyValidation: true,
                SupportsStreaming: true))
            .ToList();

        return Task.FromResult(providers);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmModelView>> ListModelsAsync(LlmModelQuery query, CancellationToken ct)
    {
        var provider = string.IsNullOrWhiteSpace(query.Provider)
            ? null
            : query.Provider.Trim().ToLowerInvariant();

        // Platform catalog (admin-managed, IsEnabled gate).
        var platformQ = _db.LlmModels
            .AsNoTracking()
            .Where(m => m.IsEnabled);

        if (provider != null) platformQ = platformQ.Where(m => m.Provider == provider);
        if (query.RequiresTools == true) platformQ = platformQ.Where(m => m.SupportsTools);
        if (query.RequiresVision == true) platformQ = platformQ.Where(m => m.SupportsVision);
        if (query.RequiresStreaming == true) platformQ = platformQ.Where(m => m.SupportsStreaming);

        var platformModels = await platformQ
            .OrderBy(m => m.Provider)
            .ThenBy(m => m.SortOrder)
            .ThenBy(m => m.DisplayName)
            .Select(m => new LlmModelView(
                m.Id, m.Provider, m.ModelId, m.DisplayName, m.Description, m.Tier,
                m.ContextWindow, m.MaxOutputTokens, m.SupportsTools, m.SupportsStreaming,
                m.SupportsVision, m.InputPricePer1M, m.OutputPricePer1M, m.SortOrder))
            .ToListAsync(ct);

        if (query.UserId is null)
            return platformModels;

        // User-synced models — augment platform list, skip duplicates already covered by platform.
        var platformKeys = platformModels
            .Select(m => $"{m.Provider}|{m.ModelId.ToLowerInvariant()}")
            .ToHashSet(StringComparer.Ordinal);

        var userQ = _db.UserLlmModels
            .AsNoTracking()
            .Where(m => m.UserId == query.UserId.Value);

        if (provider != null) userQ = userQ.Where(m => m.Provider == provider);
        if (query.RequiresTools == true) userQ = userQ.Where(m => m.SupportsTools);
        if (query.RequiresVision == true) userQ = userQ.Where(m => m.SupportsVision);
        if (query.RequiresStreaming == true) userQ = userQ.Where(m => m.SupportsStreaming);

        var userModels = await userQ
            .OrderBy(m => m.Provider)
            .ThenBy(m => m.DisplayName)
            .Select(m => new LlmModelView(
                m.Id, m.Provider, m.ModelId, m.DisplayName, m.Description, m.Tier,
                m.ContextWindow, m.MaxOutputTokens, m.SupportsTools, m.SupportsStreaming,
                m.SupportsVision, m.InputPricePer1M, m.OutputPricePer1M, SortOrder: 20_000))
            .ToListAsync(ct);

        var uniqueUserModels = userModels
            .Where(m => !platformKeys.Contains($"{m.Provider}|{m.ModelId.ToLowerInvariant()}"))
            .ToList();

        return platformModels.Concat(uniqueUserModels).ToList();
    }
}
