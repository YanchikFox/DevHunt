using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DevHunt.CoreApi.Services.Moderation;

/// <summary>Detects and censors profanity using an embedded word list with leetspeak normalization.</summary>
public sealed class ProfanityFilterService : IProfanityFilterService
{
    private readonly IReadOnlyList<string> _words;
    private readonly IReadOnlyList<Regex> _wordPatterns;
    private readonly ILogger<ProfanityFilterService> _logger;

    private static readonly (Regex Pattern, string Replacement)[] LeetMap =
    [
        (new Regex(@"@", RegexOptions.Compiled), "a"),
        (new Regex(@"3", RegexOptions.Compiled), "e"),
        (new Regex(@"1", RegexOptions.Compiled), "i"),
        (new Regex(@"0", RegexOptions.Compiled), "o"),
        (new Regex(@"\$", RegexOptions.Compiled), "s"),
        (new Regex(@"5", RegexOptions.Compiled), "s"),
        (new Regex(@"7", RegexOptions.Compiled), "t"),
        (new Regex(@"\+", RegexOptions.Compiled), "t"),
        (new Regex(@"!", RegexOptions.Compiled), "i"),
        (new Regex(@"\*", RegexOptions.Compiled), ""),
    ];

    private const string WordCharClass = @"\p{L}\p{N}_";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfanityFilterService"/> class.
    /// </summary>
    /// <param name="logger">Logger used when loading the embedded profanity dictionary.</param>
    public ProfanityFilterService(ILogger<ProfanityFilterService> logger)
    {
        _logger = logger;

        var assembly = typeof(ProfanityFilterService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("profanity_words.json"))
            ?? throw new InvalidOperationException(
                "Embedded resource profanity_words.json not found. Ensure it is included as EmbeddedResource in .csproj");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        _words = JsonSerializer.Deserialize<List<string>>(json)
            ?? throw new InvalidOperationException("Failed to deserialize profanity_words.json");

        _wordPatterns = _words.Select(BuildProfanityPattern).ToList();

        _logger.LogInformation("ProfanityFilterService loaded {Count} words", _words.Count);
    }

    /// <summary>Checks one string field and returns the first matched dictionary word.</summary>
    public ProfanityCheckResult? CheckField(string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = NormalizeLeet(value.ToLowerInvariant());

        for (var i = 0; i < _words.Count; i++)
        {
            if (_wordPatterns[i].IsMatch(normalized))
            {
                return new ProfanityCheckResult(fieldName, _words[i]);
            }
        }

        return null;
    }

    /// <summary>Scans public string properties and string collections on a DTO.</summary>
    public IReadOnlyList<ProfanityCheckResult> CheckDto(object dto)
    {
        var results = new List<ProfanityCheckResult>();

        var props = dto.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        foreach (var prop in props)
        {
            if (prop.PropertyType == typeof(string))
            {
                var val = (string?)prop.GetValue(dto);
                var result = CheckField(prop.Name, val);
                if (result is not null)
                    results.Add(result);
            }
            else if (typeof(IEnumerable<string>).IsAssignableFrom(prop.PropertyType))
            {
                var collection = prop.GetValue(dto) as IEnumerable<string>;
                if (collection is null) continue;
                foreach (var item in collection)
                {
                    var result = CheckField(prop.Name, item);
                    if (result is not null)
                    {
                        results.Add(result);
                        break; // One violation per field is enough
                    }
                }
            }
        }

        return results;
    }

    /// <summary>Replaces matched profanity tokens with <c>******</c>.</summary>
    public string CensorText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var result = text;
        foreach (var pattern in _wordPatterns)
        {
            result = pattern.Replace(result, "******");
        }

        return result;
    }

    /// <summary>Censors writable string properties and string lists in-place.</summary>
    public IReadOnlyList<string> CensorDto(object dto)
    {
        var censored = new List<string>();

        var props = dto.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        foreach (var prop in props)
        {
            if (prop.PropertyType == typeof(string) && prop.CanWrite)
            {
                var val = (string?)prop.GetValue(dto);
                if (string.IsNullOrWhiteSpace(val)) continue;

                var cleaned = CensorText(val);
                if (cleaned != val)
                {
                    prop.SetValue(dto, cleaned);
                    censored.Add(prop.Name);
                }
            }
            else if (prop.CanRead && prop.GetValue(dto) is IList<string> list)
            {
                var changed = false;
                for (var i = 0; i < list.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(list[i])) continue;
                    var cleaned = CensorText(list[i]);
                    if (cleaned != list[i])
                    {
                        list[i] = cleaned;
                        changed = true;
                    }
                }
                if (changed) censored.Add(prop.Name);
            }
        }

        return censored;
    }

    /// <summary>Builds a regex that matches spaced or punctuated variants without hitting substrings.</summary>
    private static Regex BuildProfanityPattern(string word)
    {
        // Match a profanity token with optional separators between chars, but not inside
        // normal words: "f.u.c.k" is blocked, while safe words such as "assist" and "class" are not.
        var separatorBetweenChars = string.Join(@"[.\-_\s]*", word.Select(c => Regex.Escape(c.ToString())));
        return new Regex(
            $@"(?<![{WordCharClass}]){separatorBetweenChars}(?![{WordCharClass}])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>Normalizes common character substitutions before matching.</summary>
    private static string NormalizeLeet(string input)
    {
        foreach (var (pattern, replacement) in LeetMap)
            input = pattern.Replace(input, replacement);

        return input;
    }
}
