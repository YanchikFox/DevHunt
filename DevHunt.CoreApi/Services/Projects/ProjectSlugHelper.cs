using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Helpers for generating, validating, and ensuring uniqueness of project slugs
/// (URL-safe handles used as pretty alternatives to UUIDs).
/// </summary>
public static class ProjectSlugHelper
{
    /// <summary>Minimum allowed slug length after normalization.</summary>
    public const int MinLength = 2;
    /// <summary>Maximum allowed slug length before numeric suffix trimming.</summary>
    public const int MaxLength = 48;

    private static readonly Regex SlugPattern = new(
        @"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Slugs that would collide with existing or anticipated route segments.
    /// Any attempt to use these must be rejected.
    /// </summary>
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "new", "create", "edit", "delete", "settings", "admin",
        "all", "my", "mine", "search", "explore", "trending",
        "public", "private", "drafts", "archived", "showcase",
        "api", "dashboard", "login", "logout", "signup", "signin",
        "profile", "profiles", "projects", "project", "team", "teams",
        "help", "support", "about", "terms", "privacy", "docs",
    };

    /// <summary>Returns true when <paramref name="value"/> satisfies the slug format.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length < MinLength || value.Length > MaxLength) return false;
        if (value.Contains("--", StringComparison.Ordinal)) return false;
        return SlugPattern.IsMatch(value);
    }

    /// <summary>Returns true when the slug name is reserved and cannot be assigned.</summary>
    public static bool IsReserved(string value) => ReservedSlugs.Contains(value);

    /// <summary>
    /// Convert arbitrary text (usually a project title) into a candidate slug.
    /// Returns <c>null</c> when the input cannot yield any valid characters.
    /// </summary>
    public static string? Slugify(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;

        // Normalize and strip diacritics before slug generation.
        var normalized = source.Trim().Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is ' ' or '-' or '_' or '.' or '/' or '\t' or '\n')
            {
                if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
            }
        }

        // Trim trailing hyphens/underscores.
        while (sb.Length > 0 && sb[^1] == '-') sb.Length--;

        // Drop any non-ASCII that slipped through (e.g. Cyrillic that couldn't be decomposed).
        var ascii = new StringBuilder(sb.Length);
        foreach (var ch in sb.ToString())
        {
            if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
                ascii.Append(ch);
        }

        if (ascii.Length == 0) return null;
        if (ascii.Length < MinLength) return null;
        if (ascii.Length > MaxLength) ascii.Length = MaxLength;
        while (ascii.Length > 0 && ascii[^1] == '-') ascii.Length--;
        // Trim leading hyphens that appear when a title begins with non-ASCII (e.g. Cyrillic) chars:
        // the inter-word separators survive the ASCII filter but the Cyrillic chars don't,
        // leaving orphan leading hyphens (e.g. "Основная идея TimeSwap" → "--timeswap").
        var leadStart = 0;
        while (leadStart < ascii.Length && ascii[leadStart] == '-') leadStart++;
        if (leadStart > 0) ascii.Remove(0, leadStart);
        return ascii.Length >= MinLength ? ascii.ToString() : null;
    }

    /// <summary>
    /// Given a base slug, return one that is unique in the database (appending -2, -3, ...)
    /// and not reserved. Returns <c>null</c> if the input doesn't yield any valid slug.
    /// </summary>
    public static async Task<string?> EnsureUniqueAsync(
        DevHuntDbContext db,
        string baseSlug,
        Guid? excludeProjectId,
        CancellationToken ct = default)
    {
        // Reject structurally invalid slugs (e.g. leading hyphens) before hitting the DB.
        if (!IsValid(baseSlug)) return null;

        var candidate = baseSlug;
        var suffix = 1;

        while (IsReserved(candidate) || await SlugExistsAsync(db, candidate, excludeProjectId, ct))
        {
            suffix++;
            var suffixStr = "-" + suffix.ToString(CultureInfo.InvariantCulture);
            var maxBaseLength = MaxLength - suffixStr.Length;
            var trimmedBase = baseSlug.Length > maxBaseLength ? baseSlug[..maxBaseLength] : baseSlug;
            candidate = trimmedBase + suffixStr;

            // Safety: bail out on pathological loops (shouldn't happen in practice).
            if (suffix > 9999) return null;
        }

        return candidate;
    }

    /// <summary>True when another project already uses the slug (optionally excluding one project id).</summary>
    private static Task<bool> SlugExistsAsync(
        DevHuntDbContext db,
        string slug,
        Guid? excludeProjectId,
        CancellationToken ct)
    {
        var query = db.Projects.AsNoTracking().Where(p => p.Slug == slug);
        if (excludeProjectId.HasValue)
            query = query.Where(p => p.Id != excludeProjectId.Value);
        return query.AnyAsync(ct);
    }
}
