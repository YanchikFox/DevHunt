using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// User-provided LLM API key (BYOK). The raw key is never stored — only the
/// AES-256-GCM encrypted blob plus a non-sensitive masked hint for UI display.
/// One row per (UserId, Provider): adding a new key for the same provider
/// replaces the old one.
/// </summary>
public class UserApiKey
{
    /// <summary>Primary key for a user-owned LLM API key record.</summary>
    public Guid Id { get; set; }

    /// <summary>User who owns the encrypted provider API key.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Stable provider identifier — e.g. "openai", "anthropic", "gemini",
    /// "groq", "openrouter". Matches <c>ILlmProvider.ProviderId</c>.
    /// </summary>
    [Required, MaxLength(32)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// AES-256-GCM ciphertext (base64). Decrypt only at the moment of use,
    /// never store decrypted, never log.
    /// </summary>
    [Required]
    public string EncryptedKey { get; set; } = string.Empty;

    /// <summary>
    /// Last 4 characters of the original key, prefixed with provider-typical
    /// prefix. Safe to show in UI ("sk-…abc1"). Never includes secret material.
    /// </summary>
    [Required, MaxLength(32)]
    public string KeyHint { get; set; } = string.Empty;

    /// <summary>User-defined label that helps distinguish provider keys in the account UI.</summary>
    [MaxLength(64)]
    public string? Label { get; set; }

    /// <summary>Indicates whether this provider key can be used for BYOK AI requests.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the key was last validated against its provider.</summary>
    public DateTime? LastValidatedAt { get; set; }

    /// <summary>UTC timestamp when the key last served a DevHunt AI request.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>UTC timestamp when the encrypted API key record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the encrypted API key record was last changed.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>User who owns this encrypted provider API key.</summary>
    public User? User { get; set; }
}
