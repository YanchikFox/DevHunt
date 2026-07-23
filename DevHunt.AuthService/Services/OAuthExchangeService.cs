using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace DevHunt.AuthService.Services;

/// <summary>
/// One-time OAuth exchange payload returned to the frontend after code redemption.
/// </summary>
public sealed record OAuthExchangePayload(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string Email);

/// <summary>
/// Stores short-lived OAuth exchange codes so tokens never appear in URL fragments.
/// </summary>
public interface IOAuthExchangeService
{
    /// <summary>Creates a single-use exchange code valid for a short TTL.</summary>
    Task<string> CreateExchangeCodeAsync(OAuthExchangePayload payload, CancellationToken ct = default);

    /// <summary>Redeems and invalidates an exchange code.</summary>
    Task<OAuthExchangePayload?> RedeemExchangeCodeAsync(string code, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class OAuthExchangeService : IOAuthExchangeService
{
    private const string CachePrefix = "oauth-exchange:";
    private static readonly TimeSpan ExchangeTtl = TimeSpan.FromMinutes(2);

    private readonly IDistributedCache _cache;
    private readonly ILogger<OAuthExchangeService> _logger;

    /// <summary>
    /// Initializes the service with a distributed cache backend (Redis or in-memory).
    /// </summary>
    public OAuthExchangeService(IDistributedCache cache, ILogger<OAuthExchangeService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> CreateExchangeCodeAsync(OAuthExchangePayload payload, CancellationToken ct = default)
    {
        var codeBytes = new byte[32];
        RandomNumberGenerator.Fill(codeBytes);
        var code = Convert.ToBase64String(codeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var json = JsonSerializer.Serialize(payload);
        await _cache.SetStringAsync(
            CachePrefix + code,
            json,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ExchangeTtl },
            ct);

        return code;
    }

    /// <inheritdoc />
    public async Task<OAuthExchangePayload?> RedeemExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 128)
            return null;

        var key = CachePrefix + code;
        var json = await _cache.GetStringAsync(key, ct);
        if (string.IsNullOrEmpty(json))
            return null;

        await _cache.RemoveAsync(key, ct);

        try
        {
            return JsonSerializer.Deserialize<OAuthExchangePayload>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid OAuth exchange payload");
            return null;
        }
    }
}
