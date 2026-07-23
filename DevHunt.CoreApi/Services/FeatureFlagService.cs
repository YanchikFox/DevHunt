using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Reads feature flags from the database with in-memory caching.
/// Used by API handlers to gate optional behavior without redeploying.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>Returns true if the flag is enabled (defaults to true if flag doesn't exist in DB).</summary>
    Task<bool> IsEnabledAsync(string key, CancellationToken ct = default);

    /// <summary>Returns all flags as a key→enabled dictionary (cached).</summary>
    Task<Dictionary<string, bool>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Removes a cached flag so the next read hits the DB.</summary>
    void Invalidate(string key);
}

/// <summary>
/// <see cref="IFeatureFlagService"/> implementation that caches single flags and the full flag map
/// in <see cref="IMemoryCache"/> for 30 seconds. Unknown keys default to enabled.
/// </summary>
public sealed class CachedFeatureFlagService : IFeatureFlagService
{
    private const string AllFlagsCacheKey  = "feature_flags:all";
    private const string SingleFlagPrefix  = "feature_flag:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly DevHuntDbContext _db;
    private readonly IMemoryCache    _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedFeatureFlagService"/> class.
    /// </summary>
    /// <param name="db">Database context for reading <c>FeatureFlags</c>.</param>
    /// <param name="cache">In-memory cache for flag lookups.</param>
    public CachedFeatureFlagService(DevHuntDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = SingleFlagPrefix + key;
        if (_cache.TryGetValue(cacheKey, out bool enabled))
            return enabled;

        var flag = await _db.FeatureFlags.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == key, ct);

        enabled = flag?.Enabled ?? true; // unknown flag → allow by default
        _cache.Set(cacheKey, enabled, CacheDuration);
        return enabled;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, bool>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(AllFlagsCacheKey, out Dictionary<string, bool>? cached) && cached != null)
            return cached;

        var flags = await _db.FeatureFlags.AsNoTracking()
            .ToDictionaryAsync(f => f.Key, f => f.Enabled, ct);

        _cache.Set(AllFlagsCacheKey, flags, CacheDuration);
        return flags;
    }

    /// <inheritdoc />
    public void Invalidate(string key)
    {
        _cache.Remove(SingleFlagPrefix + key);
        _cache.Remove(AllFlagsCacheKey);
    }
}
