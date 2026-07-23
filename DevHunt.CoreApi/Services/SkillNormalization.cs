using System.Text.RegularExpressions;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Normalizes raw skill tokens for consistent matching and alias resolution.
/// </summary>
public static class SkillNormalization
{
    /// <summary>
    /// Normalize a skill token by applying common aliases and stripping punctuation.
    /// </summary>
    /// <param name="value">Raw skill string.</param>
    /// <returns>Normalized token suitable for matching.</returns>
    public static string NormalizeSkillToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var s = value.Trim().ToLowerInvariant();

        // common ecosystem variants
        s = s.Replace("c#", "csharp");
        s = s.Replace("c++", "cplusplus");
        s = s.Replace("c plus plus", "cplusplus");
        s = s.Replace(".net", "dotnet");
        s = s.Replace("asp.net", "aspnet");
        s = s.Replace("node.js", "nodejs");
        s = s.Replace("next.js", "nextjs");
        s = s.Replace("react native", "reactnative");

        // keep only letters/digits; remove whitespace and punctuation
        s = Regex.Replace(s, "[^a-z0-9]+", "");
        return s;
    }
}
