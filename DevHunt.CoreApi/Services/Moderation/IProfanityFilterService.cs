namespace DevHunt.CoreApi.Services.Moderation;

/// <summary>Profanity detection and censorship helpers for user-generated content.</summary>
public interface IProfanityFilterService
{
    /// <summary>Detect profanity in a single field. Returns null if clean.</summary>
    ProfanityCheckResult? CheckField(string fieldName, string? value);

    /// <summary>Detect profanity in all string properties of a DTO.</summary>
    IReadOnlyList<ProfanityCheckResult> CheckDto(object dto);

    /// <summary>Replace profanity words with ****** in the given text.</summary>
    string CensorText(string text);

    /// <summary>Censor all string properties of a DTO in-place. Returns list of censored fields.</summary>
    IReadOnlyList<string> CensorDto(object dto);
}

/// <summary>Result of a profanity scan on one field.</summary>
/// <param name="FieldName">Property or field that matched.</param>
/// <param name="MatchedWord">Dictionary word that triggered the match.</param>
public record ProfanityCheckResult(string FieldName, string MatchedWord);
