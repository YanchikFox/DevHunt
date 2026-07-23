using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Catalog of LLM models available across providers. Drives the model picker
/// UI, capability gating (hide tools toggle if model doesn't support them) and
/// per-request cost estimation. Updated by ops, not by users.
/// </summary>
public class LlmModel
{
    /// <summary>Primary key for a model catalog entry.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Provider identifier — matches <c>ILlmProvider.ProviderId</c>.
    /// </summary>
    [Required, MaxLength(32)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Wire-format model id sent to the provider — e.g. "gpt-5",
    /// "claude-opus-4-7", "gemini-2.5-pro", "llama-3.3-70b-versatile".
    /// </summary>
    [Required, MaxLength(128)]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Human-readable model name shown in the DevHunt model picker.</summary>
    [Required, MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional model description shown to operators or users selecting a model.</summary>
    [MaxLength(512)]
    public string? Description { get; set; }

    /// <summary>Tier hint for the picker: "cheap", "balanced", "smart".</summary>
    [Required, MaxLength(16)]
    public string Tier { get; set; } = "balanced";

    /// <summary>Provider context window size used for request planning and model selection.</summary>
    public int ContextWindow { get; set; }

    /// <summary>Provider output token limit used for request planning, when known.</summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>Indicates whether the model can call tools during AI workflows.</summary>
    public bool SupportsTools { get; set; }

    /// <summary>Indicates whether the model can stream generated responses.</summary>
    public bool SupportsStreaming { get; set; } = true;

    /// <summary>Indicates whether the model can process image inputs.</summary>
    public bool SupportsVision { get; set; }

    /// <summary>USD per 1,000,000 input tokens.</summary>
    public decimal? InputPricePer1M { get; set; }

    /// <summary>USD per 1,000,000 output tokens.</summary>
    public decimal? OutputPricePer1M { get; set; }

    /// <summary>Controls whether this model is available for selection in DevHunt.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Sort order within a provider's model list (lower = higher in dropdown).
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>UTC timestamp when the model catalog entry was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the model catalog entry was last changed.</summary>
    public DateTime? UpdatedAt { get; set; }
}
