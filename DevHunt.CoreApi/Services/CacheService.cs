using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Distributed caching service with Redis backend via IDistributedCache abstraction.
/// Corresponds to devhunt_deployment.puml: Cache Service (Session storage, Cache layer, WebSocket Pub/Sub).
/// </summary>
/// <remarks>
/// <para><strong>Architecture</strong>:</para>
/// Uses IDistributedCache interface for implementation abstraction - can be swapped to Memcached, NCache, etc. without code changes.
/// Redis is preferred for production due to:
/// - High performance (in-memory storage)
/// - Advanced features (Pub/Sub, pattern-based deletion via SCAN)
/// - High availability (clustering support)
///
/// <para><strong>Use Cases</strong>:</para>
/// - Project and user profile caching (reduce DB load)
/// - User sessions (distributed session state)
/// - Distributed rate limiting (cross-instance counters)
/// - WebSocket Pub/Sub for real-time notifications (future enhancement)
///
/// <para><strong>Graceful Degradation</strong>:</para>
/// All operations are wrapped in try-catch blocks. If Redis is unavailable, the service:
/// - Returns null for Get operations (cache miss)
/// - Silently skips Set/Remove operations (logs warning)
/// - Does NOT throw exceptions (prevents cache failures from breaking API)
///
/// <para><strong>Default Expiration</strong>:</para>
/// 30 minutes for cached items (configurable per operation).
/// </remarks>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    /// <typeparam name="T">The cached reference type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or null when the key is missing or cache access fails.</returns>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Stores a value in the cache.
    /// </summary>
    /// <typeparam name="T">The cached reference type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiration">The optional absolute expiration relative to now.</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Removes a cached value by key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    Task RemoveAsync(string key);

    /// <summary>
    /// Removes cached values whose keys match the supplied Redis pattern when Redis is available.
    /// </summary>
    /// <param name="pattern">The Redis key pattern.</param>
    Task RemoveByPatternAsync(string pattern);
}

/// <summary>
/// Redis-backed implementation of <see cref="ICacheService"/>.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;
    private readonly IConnectionMultiplexer? _redis;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheService"/> class.
    /// </summary>
    /// <param name="cache">Distributed cache used for get/set/remove operations.</param>
    /// <param name="logger">Logger for cache failures during graceful degradation.</param>
    /// <param name="redis">Optional Redis multiplexer required for pattern-based deletion.</param>
    public CacheService(
        IDistributedCache cache,
        ILogger<CacheService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _cache = cache;
        _logger = logger;
        _redis = redis; // Optional: for pattern-based operations
    }

    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    /// <typeparam name="T">The cached reference type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or null when the key is missing or cache access fails.</returns>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var cached = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(cached))
                return null;

            return JsonSerializer.Deserialize<T>(cached);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get failed for key: {Key}", key);
            return null; // Graceful degradation
        }
    }

    /// <summary>
    /// Stores a value in the cache.
    /// </summary>
    /// <typeparam name="T">The cached reference type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiration">The optional absolute expiration relative to now.</param>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(30) // Default
            };

            await _cache.SetStringAsync(key, serialized, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache set failed for key: {Key}", key);
            // Graceful degradation - don't crash API if Cache Service is unavailable
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache remove failed for key: {Key}", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            // Use StackExchange.Redis for pattern-based deletion via SCAN
            if (_redis == null)
            {
                _logger.LogWarning("Redis connection not available for pattern deletion. Pattern: {Pattern}", pattern);
                return;
            }

            var database = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().FirstOrDefault()!);

            if (server == null)
            {
                _logger.LogWarning("Redis server not available for pattern deletion. Pattern: {Pattern}", pattern);
                return;
            }

            // Convert glob pattern to Redis pattern (e.g., "project:*" -> "project:*")
            var redisPattern = pattern;
            var keysDeleted = 0;

            // SCAN through keys matching the pattern
            await foreach (var key in server.KeysAsync(pattern: redisPattern))
            {
                try
                {
                    await database.KeyDeleteAsync(key);
                    keysDeleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete cache key: {Key}", key);
                }
            }

            _logger.LogInformation("Removed {Count} cache keys matching pattern: {Pattern}", keysDeleted, pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cache keys by pattern: {Pattern}", pattern);
            // Graceful degradation - don't throw, just log
        }
    }
}

