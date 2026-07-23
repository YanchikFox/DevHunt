namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// CRUD for user-provided LLM API keys. The plaintext key only ever lives
/// inside this service: callers that need to make a provider request use
/// <see cref="ResolveDecryptedKeyAsync"/>, everything else operates on
/// <see cref="UserApiKeyView"/> which is masked.
/// </summary>
public interface IUserApiKeyService
{
    /// <summary>Lists masked keys for the user; never returns plaintext secrets.</summary>
    Task<IReadOnlyList<UserApiKeyView>> ListAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Creates or replaces the user's key for the given provider. Validates the
    /// key against the provider before persisting; an invalid key never lands
    /// in the database. Replaces atomically so the user can rotate without an
    /// intermediate "no key" state.
    /// </summary>
    Task<UpsertUserApiKeyResult> UpsertAsync(Guid userId, UpsertUserApiKeyRequest request, CancellationToken ct);

    /// <summary>Deletes a key row when it belongs to the user.</summary>
    /// <returns><see langword="true"/> when a row was removed.</returns>
    Task<bool> DeleteAsync(Guid userId, Guid keyId, CancellationToken ct);

    /// <summary>
    /// Returns the decrypted key string for the given provider, intended to
    /// be passed straight to <c>ILlmProvider</c>. Bumps <c>LastUsedAt</c>.
    /// Returns null if no active key exists for that provider.
    ///
    /// CRITICAL: never log the returned string, never echo it in DTOs, never
    /// hold it longer than the single outbound provider call.
    /// </summary>
    Task<string?> ResolveDecryptedKeyAsync(Guid userId, string providerId, CancellationToken ct);
}
