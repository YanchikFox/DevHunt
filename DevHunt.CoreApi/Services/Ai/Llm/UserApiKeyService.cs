using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Encrypts, validates, and resolves user-provided LLM API keys for BYOK chat.
/// Plaintext keys exist only transiently during provider calls.
/// </summary>
public sealed class UserApiKeyService : IUserApiKeyService
{
    private readonly DevHuntDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ILlmProviderRegistry _providers;
    private readonly ILogger<UserApiKeyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserApiKeyService"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="encryption">Encryption service for protected chat/key data.</param>
    /// <param name="providers">Registry of supported LLM providers.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public UserApiKeyService(
        DevHuntDbContext db,
        IEncryptionService encryption,
        ILlmProviderRegistry providers,
        ILogger<UserApiKeyService> logger)
    {
        _db = db;
        _encryption = encryption;
        _providers = providers;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserApiKeyView>> ListAsync(Guid userId, CancellationToken ct)
    {
        return await _db.UserApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderBy(k => k.Provider)
            .Select(k => new UserApiKeyView(
                k.Id,
                k.Provider,
                k.KeyHint,
                k.Label,
                k.IsActive,
                k.LastValidatedAt,
                k.LastUsedAt,
                k.CreatedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UpsertUserApiKeyResult> UpsertAsync(Guid userId, UpsertUserApiKeyRequest request, CancellationToken ct)
    {
        var providerId = request.Provider.Trim().ToLowerInvariant();
        var provider = _providers.Get(providerId);
        if (provider == null)
        {
            return new UpsertUserApiKeyResult(
                UpsertUserApiKeyStatus.UnsupportedProvider,
                null,
                $"Provider '{providerId}' is not supported.");
        }

        var rawKey = request.ApiKey.Trim();
        if (rawKey.Length == 0)
        {
            return new UpsertUserApiKeyResult(
                UpsertUserApiKeyStatus.InvalidKey,
                null,
                "API key is empty.");
        }

        // Validate against the live provider before we ever touch the DB. A
        // bad key never gets persisted, which avoids confusing "saved but
        // doesn't work" UX and prevents storing junk encrypted blobs.
        KeyValidationResult validation;
        try
        {
            validation = await provider.ValidateKeyAsync(rawKey, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Provider reachability problems aren't the user's fault — surface
            // as InvalidKey with a clear message but don't leak provider internals.
            _logger.LogWarning(ex, "Provider {Provider} validation threw during key upsert", providerId);
            return new UpsertUserApiKeyResult(
                UpsertUserApiKeyStatus.InvalidKey,
                null,
                "Could not reach provider to validate the key. Try again.");
        }

        if (!validation.IsValid)
        {
            return new UpsertUserApiKeyResult(
                UpsertUserApiKeyStatus.InvalidKey,
                null,
                validation.ErrorMessage ?? "Provider rejected this key.");
        }

        var encrypted = _encryption.Encrypt(rawKey);
        var hint = BuildKeyHint(rawKey);
        var now = DateTime.UtcNow;

        // B-08: single fetch + null check, no separate AnyAsync.
        var existing = await _db.UserApiKeys
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == providerId, ct);

        UpsertUserApiKeyStatus status;
        UserApiKey row;
        if (existing != null)
        {
            existing.EncryptedKey = encrypted;
            existing.KeyHint = hint;
            existing.Label = request.Label;
            existing.IsActive = true;
            existing.LastValidatedAt = now;
            existing.UpdatedAt = now;
            row = existing;
            status = UpsertUserApiKeyStatus.Replaced;
        }
        else
        {
            row = new UserApiKey
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Provider = providerId,
                EncryptedKey = encrypted,
                KeyHint = hint,
                Label = request.Label,
                IsActive = true,
                LastValidatedAt = now,
                CreatedAt = now
            };
            _db.UserApiKeys.Add(row);
            status = UpsertUserApiKeyStatus.Created;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (existing == null)
        {
            // Concurrent insert won the race against our null-check; the unique
            // index (UserId, Provider) caught it. Re-fetch and update instead.
            _logger.LogInformation(ex, "Concurrent UserApiKey insert collided on unique index; falling back to update");
            _db.Entry(row).State = EntityState.Detached;
            var winner = await _db.UserApiKeys
                .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == providerId, ct);
            if (winner == null)
            {
                throw;
            }
            winner.EncryptedKey = encrypted;
            winner.KeyHint = hint;
            winner.Label = request.Label;
            winner.IsActive = true;
            winner.LastValidatedAt = now;
            winner.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            row = winner;
            status = UpsertUserApiKeyStatus.Replaced;
        }

        return new UpsertUserApiKeyResult(status, ToView(row), null);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid userId, Guid keyId, CancellationToken ct)
    {
        // Authorization: scoping the WHERE by UserId prevents IDOR — even if a
        // user guesses someone else's keyId, the row simply isn't found.
        var row = await _db.UserApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct);
        if (row == null) return false;

        _db.UserApiKeys.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<string?> ResolveDecryptedKeyAsync(Guid userId, string providerId, CancellationToken ct)
    {
        var normalised = providerId.Trim().ToLowerInvariant();
        var row = await _db.UserApiKeys
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == normalised && k.IsActive, ct);
        if (row == null) return null;

        string decrypted;
        try
        {
            decrypted = _encryption.Decrypt(row.EncryptedKey);
        }
        catch (Exception ex)
        {
            // Tampering, key rotation, or corruption — treat as "no key" so the
            // caller can prompt the user to re-enter rather than crashing.
            _logger.LogError(ex, "Failed to decrypt UserApiKey {KeyId} for user {UserId}", row.Id, userId);
            return null;
        }

        row.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return decrypted;
    }

    /// <summary>Maps a persisted row to the masked API view.</summary>
    private static UserApiKeyView ToView(UserApiKey row) => new(
        row.Id,
        row.Provider,
        row.KeyHint,
        row.Label,
        row.IsActive,
        row.LastValidatedAt,
        row.LastUsedAt,
        row.CreatedAt);

    /// <summary>
    /// Builds a non-secret display hint from a raw key. We keep at most the
    /// final 4 characters and prefix with a generic "…" so the hint can be
    /// safely logged, shown in UI, or included in audit records.
    /// </summary>
    private static string BuildKeyHint(string rawKey)
    {
        if (string.IsNullOrEmpty(rawKey)) return string.Empty;
        var tail = rawKey.Length <= 4 ? rawKey : rawKey[^4..];
        return $"…{tail}";
    }
}
