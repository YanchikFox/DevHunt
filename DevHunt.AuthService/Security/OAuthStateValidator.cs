using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace DevHunt.AuthService.Security;

/// <summary>
/// Signed OAuth state parameter validation with expiry.
/// </summary>
public static class OAuthStateValidator
{
    private static readonly TimeSpan MaxStateAge = TimeSpan.FromMinutes(10);
    private static readonly HashSet<string> WeakSecrets = new(StringComparer.Ordinal)
    {
        "devhunt-secret",
        "YOUR_SUPER_SECRET_KEY_THAT_IS_LONG_AND_COMPLEX",
    };

    /// <summary>
    /// Returns the configured OAuth state signing secret.
    /// </summary>
    public static string ResolveStateSecret(IConfiguration configuration)
    {
        var secret = configuration["OAuth:StateSecret"] ?? configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("OAuth state secret is not configured.");

        return secret;
    }

    /// <summary>
    /// Ensures production deployments do not use known weak OAuth state secrets.
    /// </summary>
    public static void EnsureProductionStateSecret(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        var secret = ResolveStateSecret(configuration);
        if (secret.Length < 32 || WeakSecrets.Contains(secret))
        {
            throw new InvalidOperationException(
                "SECURITY: OAuth:StateSecret or Jwt:Key must be a strong secret (32+ chars) in production.");
        }
    }

    /// <summary>
    /// Builds a signed OAuth state value containing redirect URI, nonce, and timestamp.
    /// </summary>
    public static string BuildState(string redirectUri, string secret)
    {
        var payload = $"{redirectUri}|{Guid.NewGuid()}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var signature = ComputeHmac(payload, secret);
        var combined = $"{payload}|{signature}";
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(combined));
    }

    /// <summary>
    /// Validates signature and expiry; returns redirect URI payload prefix on success.
    /// </summary>
    public static string? ValidateState(string state, string secret)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(state));
            var parts = decoded.Split('|');
            if (parts.Length != 4)
                return null;

            var payload = $"{parts[0]}|{parts[1]}|{parts[2]}";
            var signature = parts[3];
            var expectedSignature = ComputeHmac(payload, secret);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(signature),
                    Encoding.UTF8.GetBytes(expectedSignature)))
            {
                return null;
            }

            if (!long.TryParse(parts[2], out var unixTimestamp))
                return null;

            var createdAt = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
            var age = DateTimeOffset.UtcNow - createdAt;
            if (age > MaxStateAge || age < TimeSpan.Zero)
                return null;

            return payload;
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeHmac(string input, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }
}
