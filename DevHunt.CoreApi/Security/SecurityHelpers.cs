using System.Security.Claims;
using Ganss.Xss;

namespace DevHunt.CoreApi.Security;

/// <summary>
/// Provides security-related utility methods for authentication, authorization,
/// input validation, and HTML sanitization.
/// </summary>
/// <remarks>
/// This class centralizes security operations to ensure consistent handling across controllers:
/// - User identity extraction from JWT claims
/// - Role-based authorization checks
/// - Input sanitization (XSS prevention)
/// - Email and URL validation
/// </remarks>
public static partial class SecurityHelpers
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    /// <summary>
    /// Extracts user ID from JWT claims.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal from HttpContext.User.</param>
    /// <returns>User's GUID if authenticated, null otherwise.</returns>
    public static Guid? GetUserId(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    /// <summary>
    /// Gets user's role from JWT claims.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal from HttpContext.User.</param>
    /// <returns>Role string (e.g., "admin", "curator", "participant") or null.</returns>
    public static string? GetUserRole(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Role);
    }

    /// <summary>
    /// Checks if user has admin role.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal from HttpContext.User.</param>
    /// <returns>True if user is admin or superadmin.</returns>
    public static bool IsAdmin(ClaimsPrincipal user)
    {
        var role = GetUserRole(user);
        return role is Models.UserRoles.Admin or Models.UserRoles.SuperAdmin;
    }

    /// <summary>
    /// Checks if user has admin or curator role (privileged access).
    /// </summary>
    /// <param name="user">The ClaimsPrincipal from HttpContext.User.</param>
    /// <returns>True if user is admin, curator, or superadmin.</returns>
    public static bool IsAdminOrCurator(ClaimsPrincipal user)
    {
        var role = GetUserRole(user);
        return role is Models.UserRoles.Admin or Models.UserRoles.Curator or Models.UserRoles.SuperAdmin;
    }

    /// <summary>
    /// Checks if user has superadmin role (highest privilege).
    /// </summary>
    /// <param name="user">The ClaimsPrincipal from HttpContext.User.</param>
    /// <returns>True if user is superadmin.</returns>
    public static bool IsSuperAdmin(ClaimsPrincipal user) => GetUserRole(user) == "superadmin";

    /// <summary>
    /// Sanitizes HTML content to prevent XSS attacks.
    /// Uses allowlist-based sanitization (HtmlSanitizer library).
    /// </summary>
    /// <param name="input">Raw HTML input from user.</param>
    /// <returns>Sanitized HTML with dangerous elements removed.</returns>
    /// <remarks>
    /// Allows basic formatting tags but removes:
    /// - Script tags and event handlers
    /// - Dangerous attributes (onclick, onerror, etc.)
    /// - Malicious URLs in href/src attributes
    /// </remarks>
    public static string SanitizeHtml(string? input) =>
        string.IsNullOrWhiteSpace(input) ? string.Empty : Sanitizer.Sanitize(input).Trim();

    /// <summary>
    /// Validates email format using .NET's MailAddress parser.
    /// </summary>
    /// <param name="email">Email address to validate.</param>
    /// <returns>True if email format is valid.</returns>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that input doesn't exceed maximum length.
    /// </summary>
    /// <param name="input">String to check.</param>
    /// <param name="maxLength">Maximum allowed length.</param>
    /// <returns>True if input is null/empty or within limit.</returns>
    public static bool IsValidLength(string? input, int maxLength) => string.IsNullOrEmpty(input) || input.Length <= maxLength;

    /// <summary>
    /// U-12: Checks if a filename contains a dangerous extension anywhere (double-extension attack).
    /// Example: "malware.exe.pdf" → true (contains .exe).
    /// </summary>
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".msi", ".scr", ".pif",
        ".ps1", ".vbs", ".js", ".wsf", ".wsh", ".hta",
        ".cpl", ".inf", ".reg", ".dll", ".sys"
    };

    public static bool HasDangerousExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        // Check all segments of the filename for dangerous extensions
        var parts = fileName.Split('.');
        for (var i = 1; i < parts.Length; i++)
        {
            if (DangerousExtensions.Contains($".{parts[i]}"))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Strips known prompt-injection markers from user input before sending to AI/ML services.
    /// This is a defense-in-depth measure — ML service should also have its own guardrails.
    /// </summary>
    public static string SanitizePromptInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        // Remove common prompt-injection delimiters and control sequences
        return PromptInjectionPattern().Replace(input, string.Empty).Trim();
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"<\|im_start\|>|<\|im_end\|>|<\|endoftext\|>|###\s*(system|instruction|assistant)|```system|<system>|</system>",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex PromptInjectionPattern();

    /// <summary>
    /// Creates configured HtmlSanitizer instance with safe defaults.
    /// </summary>
    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        return sanitizer;
    }
}

