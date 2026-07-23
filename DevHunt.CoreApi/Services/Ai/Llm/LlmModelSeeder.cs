using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Inserts a starter model catalog. This is intentionally insert-only:
/// prices/model availability move fast, so ops can tune rows in DB without
/// every app restart overwriting their edits.
/// </summary>
public sealed class LlmModelSeeder
{
    private readonly DevHuntDbContext _db;
    private readonly ILogger<LlmModelSeeder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmModelSeeder"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public LlmModelSeeder(DevHuntDbContext db, ILogger<LlmModelSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Inserts missing default model rows without overwriting operator edits to existing entries.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task SeedAsync(CancellationToken ct)
    {
        var existing = await _db.LlmModels
            .AsNoTracking()
            .Select(m => new { m.Provider, m.ModelId })
            .ToListAsync(ct);

        var existingKeys = existing
            .Select(m => Key(m.Provider, m.ModelId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = DefaultModels()
            .Where(m => !existingKeys.Contains(Key(m.Provider, m.ModelId)))
            .ToList();

        if (missing.Count == 0) return;

        await _db.LlmModels.AddRangeAsync(missing, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} LLM model registry rows", missing.Count);
    }

    /// <summary>Composite cache key for deduplicating seeded models.</summary>
    private static string Key(string provider, string modelId) => $"{provider}:{modelId}";

    /// <summary>Curated starter catalog inserted on first run.</summary>
    private static IReadOnlyList<LlmModel> DefaultModels()
    {
        var now = DateTime.UtcNow;
        return new[]
        {
            Model("openai", "gpt-5", "GPT-5", "smart", 400_000, true, true, true, 10, now),
            Model("openai", "gpt-5-mini", "GPT-5 Mini", "balanced", 400_000, true, true, true, 20, now),
            Model("openai", "gpt-4.1", "GPT-4.1", "balanced", 1_000_000, true, true, true, 30, now),

            Model("anthropic", "claude-sonnet-4-5", "Claude Sonnet 4.5", "smart", 200_000, true, true, true, 10, now),
            Model("anthropic", "claude-haiku-4-5", "Claude Haiku 4.5", "cheap", 200_000, true, true, true, 20, now),

            Model("gemini", "gemini-2.5-pro", "Gemini 2.5 Pro", "smart", 1_000_000, true, true, true, 10, now),
            Model("gemini", "gemini-2.5-flash", "Gemini 2.5 Flash", "cheap", 1_000_000, true, true, true, 20, now),

            Model("groq", "llama-3.3-70b-versatile", "Llama 3.3 70B Versatile", "balanced", 128_000, true, true, false, 10, now),
            Model("groq", "openai/gpt-oss-120b", "GPT-OSS 120B", "balanced", 128_000, true, true, false, 20, now),

            Model("deepseek", "deepseek-chat", "DeepSeek Chat", "cheap", 64_000, true, true, false, 10, now),
            Model("deepseek", "deepseek-reasoner", "DeepSeek Reasoner", "smart", 64_000, true, true, false, 20, now),

            Model("openrouter", "anthropic/claude-sonnet-4.5", "Claude Sonnet 4.5", "smart", 200_000, true, true, true, 10, now),
            Model("openrouter", "google/gemini-2.5-pro", "Gemini 2.5 Pro", "smart", 1_000_000, true, true, true, 20, now),
            Model("openrouter", "openai/gpt-5", "GPT-5", "smart", 400_000, true, true, true, 30, now),

            Model("xai", "grok-4", "Grok 4", "smart", 256_000, true, true, true, 10, now),
            Model("xai", "grok-3-mini", "Grok 3 Mini", "cheap", 128_000, true, true, false, 20, now),

            Model("mistral", "mistral-large-latest", "Mistral Large", "smart", 128_000, true, true, false, 10, now),
            Model("mistral", "codestral-latest", "Codestral", "balanced", 256_000, false, true, false, 20, now),

            Model("together", "meta-llama/Llama-3.3-70B-Instruct-Turbo", "Llama 3.3 70B Instruct Turbo", "balanced", 128_000, true, true, false, 10, now),
            Model("together", "Qwen/Qwen2.5-Coder-32B-Instruct", "Qwen 2.5 Coder 32B", "cheap", 32_000, true, true, false, 20, now),
        };
    }

    /// <summary>Factory for a single seeded <see cref="LlmModel"/> row.</summary>
    private static LlmModel Model(
        string provider,
        string modelId,
        string displayName,
        string tier,
        int contextWindow,
        bool tools,
        bool streaming,
        bool vision,
        int sortOrder,
        DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ModelId = modelId,
            DisplayName = displayName,
            Tier = tier,
            ContextWindow = contextWindow,
            SupportsTools = tools,
            SupportsStreaming = streaming,
            SupportsVision = vision,
            IsEnabled = true,
            SortOrder = sortOrder,
            CreatedAt = now,
        };
}
