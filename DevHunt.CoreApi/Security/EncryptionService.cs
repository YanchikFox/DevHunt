using System.Security.Cryptography;
using System.Text;

namespace DevHunt.CoreApi.Security;

/// <summary>
/// Defines the contract for IEncryptionService.
/// </summary>
public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

/// <summary>
/// SECURITY FIX (CVH-008): Uses AES-GCM (Galois/Counter Mode) for authenticated encryption.
///
/// Previous implementation used AES-CBC which is vulnerable to Padding Oracle attacks.
/// AES-GCM provides both confidentiality AND integrity, preventing:
/// - Padding oracle attacks (CVE-style vulnerabilities)
/// - Ciphertext modification attacks
/// - Bit-flipping attacks
///
/// Format: [Nonce (12 bytes)][Tag (16 bytes)][Ciphertext]
/// </summary>
public class EncryptionService : IEncryptionService
{
    // WARNING: This is a development-only fallback key. NEVER use in production!
    // In production, always configure Encryption:Key via environment variables or KeyVault
    private const string DevelopmentOnlyFallbackKey = "DefaultKeyForDevelopmentOnly32Bytes!";

    // AES-GCM constants
    private const int NonceSize = 12; // 96 bits recommended for GCM
    private const int TagSize = 16;   // 128 bits for maximum security

    private readonly byte[] _key;
    private readonly ILogger<EncryptionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionService"/> class.
    /// </summary>
    /// <param name="configuration">The configuration dependency.</param>
    /// <param name="logger">The logger dependency.</param>
    public EncryptionService(IConfiguration configuration, ILogger<EncryptionService> logger)
    {
        _logger = logger;

        // Get encryption key from configuration (should be 32 bytes for AES-256)
        var keyString = configuration["Encryption:Key"];
        if (string.IsNullOrEmpty(keyString))
        {
            // SECURITY: In production, encryption key MUST be configured
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SECURITY ERROR: Encryption:Key must be configured in production. " +
                    "Set the ENCRYPTION_KEY environment variable (exactly 32 characters).");
            }

            // Fallback: Use development key and log warning (ONLY for development)
            _logger.LogWarning("Encryption key not configured. Using development fallback key (NOT SECURE FOR PRODUCTION!)");
            keyString = DevelopmentOnlyFallbackKey;
        }

        var keyBytes = Encoding.UTF8.GetBytes(keyString);
        if (keyBytes.Length != 32)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            if (env.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SECURITY ERROR: Encryption:Key must be exactly 32 bytes for AES-256 (got {keyBytes.Length}). " +
                    "Set the ENCRYPTION_KEY environment variable to exactly 32 characters.");
            }

            _logger.LogWarning(
                "Encryption:Key is {ActualLength} bytes; AES-256 requires exactly 32. " +
                "Key will be {Action} — this WILL cause AuthenticationTagMismatchException if the effective key ever changes. " +
                "Set ENCRYPTION_KEY to exactly 32 characters.",
                keyBytes.Length,
                keyBytes.Length < 32 ? "padded with spaces" : "truncated");
        }

        _key = Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
    }

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM authenticated encryption.
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSize];

            using var aesGcm = new AesGcm(_key, TagSize);
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // Format: [Nonce (12 bytes)][Tag (16 bytes)][Ciphertext]
            var result = new byte[NonceSize + TagSize + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(cipherBytes, 0, result, NonceSize + TagSize, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption failed");
            throw;
        }
    }

    /// <summary>
    /// Decrypts ciphertext using AES-256-GCM authenticated encryption.
    /// Throws CryptographicException if authentication tag verification fails
    /// (indicating tampering or corruption).
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        try
        {
            var data = Convert.FromBase64String(cipherText);

            // Minimum length: Nonce + Tag + at least 1 byte of ciphertext
            var minLength = NonceSize + TagSize + 1;
            if (data.Length < minLength)
            {
                throw new ArgumentException($"Invalid ciphertext: too short (minimum {minLength} bytes required)");
            }

            // Extract components: [Nonce (12 bytes)][Tag (16 bytes)][Ciphertext]
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var cipherBytes = new byte[data.Length - NonceSize - TagSize];

            Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(data, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(data, NonceSize + TagSize, cipherBytes, 0, cipherBytes.Length);

            var plainBytes = new byte[cipherBytes.Length];

            using var aesGcm = new AesGcm(_key, TagSize);
            // This will throw CryptographicException if tag verification fails
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            // Authentication failed - ciphertext was tampered with or corrupted
            _logger.LogWarning(ex, "Decryption failed: authentication tag verification failed (possible tampering)");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed");
            throw;
        }
    }
}

