using System.Text.Json;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;

namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Static mapping and utility helpers for AI planning operations.
/// Extracted from AiPlanningService to reduce file size.
/// </summary>
internal static class AiPlanningMapper
{
    /// <summary>
    /// Camel-case JSON options used when serializing and deserializing plan payloads.
    /// </summary>
    internal static readonly JsonSerializerOptions PlanJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Maps an <see cref="AiTechStackDraft"/> to the API response DTO, copying token metadata.
    /// </summary>
    /// <param name="draft">Tech-stack draft from a planner strategy.</param>
    /// <returns>API-facing tech-stack response.</returns>
    internal static TechStackResponseDto MapTechStack(AiTechStackDraft draft)
    {
        return new TechStackResponseDto(
            draft.Version,
            draft.Options.Select(o => new TechStackOptionDto(o.Name, o.Description, o.Pros, o.Cons)).ToList(),
            draft.PromptVersion,
            draft.Provider,
            draft.Model,
            draft.PromptTokens,
            draft.CompletionTokens,
            draft.TotalTokens
        );
    }

    /// <summary>
    /// Combines a persisted <see cref="AiPlan"/> row with its deserialized draft for API responses.
    /// </summary>
    /// <param name="plan">Database plan entity.</param>
    /// <param name="draft">Parsed plan draft JSON.</param>
    /// <returns>API-facing plan response including draft content.</returns>
    internal static AiPlanResponseDto MapPlan(AiPlan plan, AiPlanDraft draft)
    {
        return new AiPlanResponseDto(
            plan.Id,
            plan.ProjectId,
            plan.Status,
            plan.Idea,
            plan.TechStack,
            plan.PlanVersion,
            plan.CreatedAt,
            plan.AppliedAt,
            draft
        );
    }

    /// <summary>
    /// Deserializes stored plan JSON into an <see cref="AiPlanDraft"/>.
    /// Returns <see langword="null"/> on malformed JSON rather than throwing.
    /// </summary>
    /// <param name="planJson">Raw JSON from <c>AiPlan.PlanJson</c>.</param>
    /// <returns>Parsed draft, or <see langword="null"/> when parsing fails.</returns>
    internal static AiPlanDraft? DeserializePlanDraft(string planJson)
    {
        try
        {
            return JsonSerializer.Deserialize<AiPlanDraft>(planJson, PlanJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Normalizes a locale tag to its primary language subtag, defaulting to <c>en</c>.
    /// </summary>
    /// <param name="locale">Locale from the request (for example <c>en-US</c>).</param>
    /// <returns>Lowercase language code.</returns>
    internal static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return "en";
        var parts = locale.Split('-');
        return parts[0].ToLowerInvariant();
    }

    /// <summary>
    /// Extracts token and provider fields from a plan draft for usage metering.
    /// </summary>
    /// <param name="draft">Completed plan draft from the ML service.</param>
    /// <returns>Metadata suitable for <see cref="AiUsageResult"/>.</returns>
    internal static AiUsageMetadata BuildUsageMetadata(AiPlanDraft draft)
    {
        return new AiUsageMetadata(
            draft.PromptVersion,
            draft.Provider,
            draft.Model,
            draft.PromptTokens,
            draft.CompletionTokens,
            draft.TotalTokens
        );
    }

    /// <summary>
    /// Truncates a string to a maximum length without throwing.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <param name="maxLength">Maximum allowed length.</param>
    /// <returns>Original value or a prefix of at most <paramref name="maxLength"/> characters.</returns>
    internal static string TrimToLength(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
