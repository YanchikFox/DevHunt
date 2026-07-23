using System.Data;
using System.Security.Cryptography;
using System.Text;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Service for secure work with refresh tokens
/// SECURITY FIX (R4): Storing HMAC hash instead of plain text tokens
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Create a new refresh token and return it to the client.
    /// </summary>
    Task<(string token, RefreshToken refreshTokenEntity)> CreateRefreshTokenAsync(
        Guid userId,
        TimeSpan lifetime,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically validates, revokes, and rotates a refresh token (one-time use).
    /// Returns null when invalid; revokes the entire token family on reuse detection.
    /// </summary>
    Task<RefreshTokenRotationResult?> RotateRefreshTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Revoke refresh token
    /// </summary>
    Task RevokeRefreshTokenAsync(string token, string reason, CancellationToken ct = default);

    /// <summary>
    /// Revoke all user tokens
    /// </summary>
    Task RevokeAllUserTokensAsync(Guid userId, string reason, CancellationToken ct = default);
}

/// <summary>
/// HMAC-based refresh token service with transactional rotation and family reuse detection.
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private readonly DevHuntDbContext _dbContext;
    private readonly ILogger<RefreshTokenService> _logger;
    private readonly string _hmacSecret;

    /// <summary>
    /// Loads the refresh-token HMAC secret from configuration, falling back to the JWT key and failing startup when neither is configured.
    /// </summary>
    public RefreshTokenService(
        DevHuntDbContext dbContext,
        ILogger<RefreshTokenService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;

        _hmacSecret = configuration["RefreshToken:HmacSecret"]
                      ?? configuration["Jwt:Key"]
                      ?? throw new InvalidOperationException("HMAC secret not configured");
    }

    /// <inheritdoc />
    public async Task<(string token, RefreshToken refreshTokenEntity)> CreateRefreshTokenAsync(
        Guid userId,
        TimeSpan lifetime,
        CancellationToken ct = default)
    {
        var (token, entity) = BuildRefreshTokenEntity(userId, lifetime, Guid.NewGuid());
        _dbContext.RefreshTokens.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created refresh token for user {UserId}, expires at {ExpiresAt}",
            userId,
            entity.ExpiresAt);

        return (token, entity);
    }

    /// <inheritdoc />
    public async Task<RefreshTokenRotationResult?> RotateRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        var tokenHash = ComputeTokenHash(token);
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

            try
            {
                await LockRefreshTokenRowAsync(tokenHash, ct);

                var refreshToken = await _dbContext.RefreshTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

                if (refreshToken?.User == null)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogWarning("Refresh token not found (invalid hash)");
                    return null;
                }

                if (refreshToken.ExpiresAt < DateTime.UtcNow)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogWarning(
                        "Refresh token {TokenId} expired at {ExpiresAt}",
                        refreshToken.Id,
                        refreshToken.ExpiresAt);
                    return null;
                }

                if (refreshToken.IsRevoked)
                {
                    _logger.LogError(
                        "SECURITY ALERT: Refresh token reuse detected for {TokenId}, family {FamilyId}, user {UserId}",
                        refreshToken.Id,
                        refreshToken.TokenFamilyId,
                        refreshToken.UserId);

                    await RevokeTokenFamilyAsync(refreshToken.TokenFamilyId, "reuse_detected", ct);
                    await _dbContext.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return null;
                }

                refreshToken.IsRevoked = true;
                refreshToken.RevokedAt = DateTime.UtcNow;
                refreshToken.RevocationReason = "rotated";
                refreshToken.LastUsedAt = DateTime.UtcNow;
                refreshToken.UsageCount++;

                var (newPlaintext, newEntity) = BuildRefreshTokenEntity(
                    refreshToken.UserId,
                    TimeSpan.FromDays(7),
                    refreshToken.TokenFamilyId);

                _dbContext.RefreshTokens.Add(newEntity);
                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Rotated refresh token {OldTokenId} -> {NewTokenId} for user {UserId}",
                    refreshToken.Id,
                    newEntity.Id,
                    refreshToken.UserId);

                return new RefreshTokenRotationResult(refreshToken.User, newPlaintext);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task RevokeRefreshTokenAsync(string token, string reason, CancellationToken ct = default)
    {
        var tokenHash = ComputeTokenHash(token);

        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (refreshToken != null && !refreshToken.IsRevoked)
        {
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevocationReason = reason;

            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Revoked refresh token {TokenId} for user {UserId}, reason: {Reason}",
                refreshToken.Id,
                refreshToken.UserId,
                reason);
        }
    }

    /// <inheritdoc />
    public async Task RevokeAllUserTokensAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        var count = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(rt => rt.IsRevoked, true)
                .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow)
                .SetProperty(rt => rt.RevocationReason, reason), ct);

        _logger.LogWarning(
            "Revoked {Count} refresh tokens for user {UserId}, reason: {Reason}",
            count,
            userId,
            reason);
    }

    private (string plaintext, RefreshToken entity) BuildRefreshTokenEntity(
        Guid userId,
        TimeSpan lifetime,
        Guid tokenFamilyId)
    {
        var tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);

        var token = Convert.ToBase64String(tokenBytes);
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenFamilyId = tokenFamilyId,
            TokenHash = ComputeTokenHash(token),
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            CreatedAt = DateTime.UtcNow,
            UsageCount = 0,
        };

        return (token, entity);
    }

    private async Task LockRefreshTokenRowAsync(string tokenHash, CancellationToken ct)
    {
        if (_dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT "Id" FROM "RefreshTokens" WHERE "TokenHash" = {tokenHash} FOR UPDATE""",
                ct);
        }
    }

    private async Task RevokeTokenFamilyAsync(Guid tokenFamilyId, string reason, CancellationToken ct)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.TokenFamilyId == tokenFamilyId && !rt.IsRevoked)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
            token.RevocationReason = reason;
        }
    }

    private string ComputeTokenHash(string token)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
