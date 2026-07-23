using System.ComponentModel.DataAnnotations;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Safe-to-serialise view of a stored <c>UserApiKey</c>. Never carries the
/// plaintext key — only the masked hint. This DTO is what flows out to the
/// frontend, gets logged, ends up in error reports.
/// </summary>
public sealed record UserApiKeyView(
    Guid Id,
    string Provider,
    string KeyHint,
    string? Label,
    bool IsActive,
    DateTime? LastValidatedAt,
    DateTime? LastUsedAt,
    DateTime CreatedAt
);

/// <summary>
/// Request body for creating or rotating a user's provider API key.
/// </summary>
public sealed record UpsertUserApiKeyRequest
{
    /// <summary>Provider id matching <see cref="ILlmProvider.ProviderId"/>.</summary>
    [Required, MaxLength(32)]
    public string Provider { get; init; } = string.Empty;

    /// <summary>Plaintext API key validated against the provider before persistence.</summary>
    [Required, MaxLength(512)]
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Optional user-visible label for the key.</summary>
    [MaxLength(64)]
    public string? Label { get; init; }
}

/// <summary>
/// Outcome of <see cref="IUserApiKeyService.UpsertAsync"/>.
/// </summary>
public enum UpsertUserApiKeyStatus
{
    Created,
    Replaced,
    InvalidKey,
    UnsupportedProvider
}

/// <summary>
/// Result of upserting a user API key, including the masked view on success.
/// </summary>
/// <param name="Status">Whether the key was stored, replaced, or rejected.</param>
/// <param name="Key">Masked key view when successful.</param>
/// <param name="ErrorMessage">Provider validation or routing error message.</param>
public sealed record UpsertUserApiKeyResult(
    UpsertUserApiKeyStatus Status,
    UserApiKeyView? Key,
    string? ErrorMessage
);
