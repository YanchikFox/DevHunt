using System.Security.Cryptography;
using OtpNet;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Service for TOTP-based two-factor authentication.
/// </summary>
public interface ITotpService
{
    /// <summary>Generates a new TOTP secret (Base32-encoded).</summary>
    string GenerateSecret();

    /// <summary>Creates an otpauth:// URI for QR code generation.</summary>
    string GenerateQrUri(string secret, string userEmail, string issuer = "DevHunt");

    /// <summary>Validates a 6-digit TOTP code against a secret.</summary>
    bool ValidateCode(string secret, string code);

    /// <summary>Generates a set of one-time recovery codes.</summary>
    List<string> GenerateRecoveryCodes(int count = 8);

    /// <summary>Hashes recovery codes for storage.</summary>
    string HashRecoveryCodes(List<string> codes);

    /// <summary>Validates a recovery code against stored hashes, removing it if valid.</summary>
    (bool IsValid, string? UpdatedHashes) ValidateRecoveryCode(string code, string storedHashes);
}

/// <summary>
/// Generates TOTP secrets, authenticator QR URIs, one-time recovery codes, and BCrypt-backed recovery-code hashes.
/// </summary>
public class TotpService : ITotpService
{
    private const int Step = 30; // 30-second window
    private const int Digits = 6;

    /// <summary>
    /// Generates a 160-bit random secret and returns it in Base32 format for authenticator apps.
    /// </summary>
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20); // 160-bit
        return Base32Encoding.ToString(key);
    }

    /// <summary>
    /// Builds an escaped otpauth URI using the configured issuer, user email, six digits, and a 30-second period.
    /// </summary>
    public string GenerateQrUri(string secret, string userEmail, string issuer = "DevHunt")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(userEmail);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&digits={Digits}&period={Step}";
    }

    /// <summary>
    /// Rejects blank or non-six-digit codes, then verifies the code against the Base32 secret with one previous and one future 30-second window allowed.
    /// </summary>
    public bool ValidateCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits)
            return false;

        var keyBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(keyBytes, step: Step, totpSize: Digits);

        // Allow ±1 window for clock skew
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }

    /// <summary>
    /// Generates the requested number of uppercase recovery codes as random `XXXX-XXXX` values.
    /// </summary>
    public List<string> GenerateRecoveryCodes(int count = 8)
    {
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            // Format: XXXX-XXXX (8 alphanumeric chars)
            var bytes = RandomNumberGenerator.GetBytes(5);
            var raw = Convert.ToHexString(bytes).ToUpperInvariant()[..8];
            codes.Add($"{raw[..4]}-{raw[4..]}");
        }
        return codes;
    }

    /// <summary>
    /// Removes dashes from recovery codes, hashes each normalized code with BCrypt, and joins the hashes for storage.
    /// </summary>
    public string HashRecoveryCodes(List<string> codes)
    {
        // Store as comma-separated BCrypt hashes
        var hashed = codes.Select(c => BCrypt.Net.BCrypt.HashPassword(c.Replace("-", ""), workFactor: 10));
        return string.Join(",", hashed);
    }

    /// <summary>
    /// Normalizes a submitted recovery code, checks it against stored BCrypt hashes, removes a matching hash for one-time use, and returns null updated hashes when none remain or validation fails.
    /// </summary>
    public (bool IsValid, string? UpdatedHashes) ValidateRecoveryCode(string code, string storedHashes)
    {
        if (string.IsNullOrWhiteSpace(storedHashes))
            return (false, null);

        var normalized = code.Replace("-", "").Trim().ToUpperInvariant();
        var hashes = storedHashes.Split(',').ToList();

        for (var i = 0; i < hashes.Count; i++)
        {
            if (BCrypt.Net.BCrypt.Verify(normalized, hashes[i]))
            {
                hashes.RemoveAt(i); // One-time use
                var updated = hashes.Count > 0 ? string.Join(",", hashes) : null;
                return (true, updated);
            }
        }

        return (false, null);
    }
}
