using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Merges provider-reported model metadata into the local <c>LlmModels</c> registry using a user's BYOK key.
/// </summary>
public sealed class LlmModelSyncService : ILlmModelSyncService
{
    private readonly DevHuntDbContext _db;
    private readonly IUserApiKeyService _keys;
    private readonly ILlmProviderRegistry _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmModelSyncService"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="keys">Service for storing and resolving user LLM API keys.</param>
    /// <param name="providers">Registry of supported LLM providers.</param>
    public LlmModelSyncService(
        DevHuntDbContext db,
        IUserApiKeyService keys,
        ILlmProviderRegistry providers)
    {
        _db = db;
        _keys = keys;
        _providers = providers;
    }

    /// <inheritdoc />
    public async Task<LlmModelSyncResult> SyncFromUserKeyAsync(Guid userId, string providerId, CancellationToken ct)
    {
        var normalizedProvider = providerId.Trim().ToLowerInvariant();
        var provider = _providers.Get(normalizedProvider)
            ?? throw new InvalidOperationException($"Provider '{normalizedProvider}' is not supported.");

        var apiKey = await _keys.ResolveDecryptedKeyAsync(userId, normalizedProvider, ct)
            ?? throw new InvalidOperationException($"No active API key saved for provider '{normalizedProvider}'.");

        var providerModels = await provider.ListModelsAsync(apiKey, ct);
        var modelIds = providerModels
            .Select(m => m.ModelId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Load ALL existing rows for this user+provider (not just matching ones),
        // so we can delete rows that the provider no longer returns.
        var allExisting = await _db.UserLlmModels
            .Where(m => m.UserId == userId && m.Provider == normalizedProvider)
            .ToDictionaryAsync(m => m.ModelId, StringComparer.OrdinalIgnoreCase, ct);

        var now = DateTime.UtcNow;
        var created = 0;
        var updated = 0;
        var deleted = 0;
        var missingPricing = 0;

        foreach (var providerModel in providerModels)
        {
            if (string.IsNullOrWhiteSpace(providerModel.ModelId)) continue;

            if (!providerModel.InputPricePer1M.HasValue || !providerModel.OutputPricePer1M.HasValue)
            {
                missingPricing++;
            }

            if (!allExisting.TryGetValue(providerModel.ModelId, out var row))
            {
                var newRow = new DevHunt.Infrastructure.Models.UserLlmModel
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Provider = normalizedProvider,
                    ModelId = providerModel.ModelId,
                    DisplayName = providerModel.DisplayName ?? providerModel.ModelId,
                    Tier = GuessTier(providerModel.InputPricePer1M, providerModel.OutputPricePer1M),
                    ContextWindow = providerModel.ContextWindow ?? 0,
                    MaxOutputTokens = providerModel.MaxOutputTokens,
                    SupportsTools = providerModel.SupportsTools ?? false,
                    SupportsStreaming = providerModel.SupportsStreaming ?? true,
                    SupportsVision = providerModel.SupportsVision ?? false,
                    InputPricePer1M = providerModel.InputPricePer1M,
                    OutputPricePer1M = providerModel.OutputPricePer1M,
                    CreatedAt = now,
                };

                _db.UserLlmModels.Add(newRow);
                created++;
            }
            else
            {
                var changed = ApplyUserModelMetadata(row, providerModel, now);
                if (changed) updated++;
                // Mark as seen so we don't delete it below.
                allExisting.Remove(providerModel.ModelId, out _);
            }
        }

        // Remove rows the provider no longer lists — they were filtered out or deleted upstream.
        foreach (var stale in allExisting.Values)
        {
            _db.UserLlmModels.Remove(stale);
            deleted++;
        }

        await _db.SaveChangesAsync(ct);
        return new LlmModelSyncResult(normalizedProvider, providerModels.Count, created, updated, missingPricing);
    }

    /// <summary>Updates an existing user model row when provider metadata changed.</summary>
    private static bool ApplyUserModelMetadata(DevHunt.Infrastructure.Models.UserLlmModel row, ProviderModelInfo providerModel, DateTime now)
    {
        var changed = false;

        var displayName = providerModel.DisplayName ?? row.DisplayName;
        if (row.DisplayName != displayName) { row.DisplayName = displayName; changed = true; }

        if (providerModel.ContextWindow.HasValue && row.ContextWindow != providerModel.ContextWindow.Value)
        { row.ContextWindow = providerModel.ContextWindow.Value; changed = true; }

        if (providerModel.MaxOutputTokens.HasValue && row.MaxOutputTokens != providerModel.MaxOutputTokens.Value)
        { row.MaxOutputTokens = providerModel.MaxOutputTokens.Value; changed = true; }

        if (providerModel.SupportsTools.HasValue && row.SupportsTools != providerModel.SupportsTools.Value)
        { row.SupportsTools = providerModel.SupportsTools.Value; changed = true; }

        if (providerModel.SupportsStreaming.HasValue && row.SupportsStreaming != providerModel.SupportsStreaming.Value)
        { row.SupportsStreaming = providerModel.SupportsStreaming.Value; changed = true; }

        if (providerModel.SupportsVision.HasValue && row.SupportsVision != providerModel.SupportsVision.Value)
        { row.SupportsVision = providerModel.SupportsVision.Value; changed = true; }

        if (providerModel.InputPricePer1M.HasValue && row.InputPricePer1M != providerModel.InputPricePer1M.Value)
        { row.InputPricePer1M = providerModel.InputPricePer1M.Value; changed = true; }

        if (providerModel.OutputPricePer1M.HasValue && row.OutputPricePer1M != providerModel.OutputPricePer1M.Value)
        { row.OutputPricePer1M = providerModel.OutputPricePer1M.Value; changed = true; }

        if (changed)
        {
            row.Tier = GuessTier(row.InputPricePer1M, row.OutputPricePer1M);
            row.UpdatedAt = now;
        }

        return changed;
    }

    /// <summary>Heuristic tier label from blended per-million token prices.</summary>
    private static string GuessTier(decimal? input, decimal? output)
    {
        var blended = (input ?? 0m) + (output ?? 0m);
        if (blended == 0m) return "balanced";
        if (blended <= 2m) return "cheap";
        if (blended <= 20m) return "balanced";
        return "smart";
    }
}
