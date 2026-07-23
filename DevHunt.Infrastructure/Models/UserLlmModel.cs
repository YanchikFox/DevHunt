using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Per-user model catalog entries discovered via BYOK sync.
/// These augment the shared <see cref="LlmModel"/> platform catalog for a single user's sessions only.
/// </summary>
public class UserLlmModel
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owner of the BYOK key that produced this entry.</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation property to the owning user.</summary>
    public User User { get; set; } = null!;

    /// <summary>Provider identifier — matches <c>ILlmProvider.ProviderId</c>.</summary>
    [Required, MaxLength(32)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>Wire-format model id sent to the provider.</summary>
    [Required, MaxLength(128)]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Human-readable model name shown in the DevHunt model picker.</summary>
    [Required, MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional model description.</summary>
    [MaxLength(512)]
    public string? Description { get; set; }

    /// <summary>Tier hint for the picker: "cheap", "balanced", "smart".</summary>
    [Required, MaxLength(16)]
    public string Tier { get; set; } = "balanced";

    /// <summary>Provider context window size.</summary>
    public int ContextWindow { get; set; }

    /// <summary>Provider output token limit, when known.</summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>Whether the model supports tool calling.</summary>
    public bool SupportsTools { get; set; }

    /// <summary>Whether the model supports streaming responses.</summary>
    public bool SupportsStreaming { get; set; } = true;

    /// <summary>Whether the model supports image inputs.</summary>
    public bool SupportsVision { get; set; }

    /// <summary>USD per 1,000,000 input tokens.</summary>
    public decimal? InputPricePer1M { get; set; }

    /// <summary>USD per 1,000,000 output tokens.</summary>
    public decimal? OutputPricePer1M { get; set; }

    /// <summary>UTC timestamp when this entry was first synced.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when this entry was last refreshed from the provider.</summary>
    public DateTime? UpdatedAt { get; set; }
}
