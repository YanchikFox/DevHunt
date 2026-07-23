using Microsoft.Extensions.Caching.Distributed;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Tracks user online presence via distributed cache keys with TTL.
/// </summary>
public interface IPresenceService
{
    /// <summary>Marks a user online for a specific SignalR hub source with a sliding two-minute TTL.</summary>
    /// <param name="userId">User whose presence is updated.</param>
    /// <param name="source">Identifies the caller (e.g. <c>chat</c>, <c>notify</c>) to avoid cross-hub conflicts.</param>
    Task SetOnlineAsync(Guid userId, string source);
    /// <summary>Removes the presence key for a hub source when the user disconnects.</summary>
    /// <param name="userId">User whose presence is cleared.</param>
    /// <param name="source">Must match the source used in <see cref="SetOnlineAsync"/>.</param>
    Task SetOfflineAsync(Guid userId, string source);
    /// <summary>Returns whether any known hub source key exists for the user.</summary>
    /// <param name="userId">User to check.</param>
    /// <returns><see langword="true"/> when at least one presence key is present.</returns>
    Task<bool> IsOnlineAsync(Guid userId);
    /// <summary>Batch version of <see cref="IsOnlineAsync"/> for many user IDs.</summary>
    /// <param name="userIds">Users to inspect.</param>
    /// <returns>Map of user ID to online flag; failures default to offline.</returns>
    Task<Dictionary<Guid, bool>> GetOnlineStatusAsync(IEnumerable<Guid> userIds);
}

/// <summary>
/// Implementation of <see cref="IPresenceService"/> backed by <see cref="IDistributedCache"/>.
/// Works with both Redis (production) and in-memory cache (development).
///
/// Each SignalR hub writes its own key: <c>presence:{userId}:{source}</c>.
/// A user is considered online if ANY source key exists.
/// This avoids the read-modify-write race condition that a single counter key had.
/// </summary>
public class PresenceService : IPresenceService
{
    /// <summary>All known hub sources. Used when checking if a user is online.</summary>
    private static readonly string[] Sources = ["chat", "notify"];

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(2)
    };

    private readonly IDistributedCache _cache;
    private readonly ILogger<PresenceService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PresenceService"/> class.
    /// </summary>
    /// <param name="cache">Distributed cache backing presence keys.</param>
    /// <param name="logger">Logger for cache read/write failures.</param>
    public PresenceService(IDistributedCache cache, ILogger<PresenceService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Builds the distributed cache key for a user's presence in a specific hub source.
    /// </summary>
    private static string Key(Guid userId, string source) => $"presence:{userId}:{source}";

    /// <inheritdoc />
    public async Task SetOnlineAsync(Guid userId, string source)
    {
        try
        {
            await _cache.SetStringAsync(Key(userId, source), "1", CacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set online status for user {UserId} source {Source}", userId, source);
        }
    }

    /// <inheritdoc />
    public async Task SetOfflineAsync(Guid userId, string source)
    {
        try
        {
            await _cache.RemoveAsync(Key(userId, source));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set offline status for user {UserId} source {Source}", userId, source);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsOnlineAsync(Guid userId)
    {
        try
        {
            return await AnySourceOnlineAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check online status for user {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, bool>> GetOnlineStatusAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.ToList();
        var result = new Dictionary<Guid, bool>(ids.Count);

        foreach (Guid id in ids)
        {
            try
            {
                result[id] = await AnySourceOnlineAsync(id);
            }
            catch
            {
                result[id] = false;
            }
        }

        return result;
    }

    /// <summary>Checks the configured hub sources for an existing presence key.</summary>
    private async Task<bool> AnySourceOnlineAsync(Guid userId)
    {
        foreach (string source in Sources)
        {
            string? val = await _cache.GetStringAsync(Key(userId, source));
            if (val is not null) return true;
        }
        return false;
    }
}
