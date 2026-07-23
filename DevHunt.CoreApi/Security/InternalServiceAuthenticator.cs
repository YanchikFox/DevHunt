using System.Security.Cryptography;
using System.Text;

namespace DevHunt.CoreApi.Security;

/// <summary>
/// SECURITY: Validates internal service-to-service requests using HMAC-SHA256 signatures.
/// Prevents timing attacks (CVH-001) and header spoofing (CVH-002).
/// </summary>
public interface IInternalServiceAuthenticator
{
    /// <summary>
    /// Validates an internal service request from HTTP headers.
    /// </summary>
    bool ValidateRequest(HttpContext context, string resource);
}

public sealed class InternalServiceAuthenticator : IInternalServiceAuthenticator
{
    private static readonly string[] AllowedServices = { "integration-gateway", "notification-service" };
    private const int TimestampValidityMinutes = 5;
    private const int MinKeyLength = 32;

    private readonly ILogger<InternalServiceAuthenticator> _logger;
    private readonly string _environment;
    private readonly string _expectedKey;

    public InternalServiceAuthenticator(ILogger<InternalServiceAuthenticator> logger, IConfiguration configuration)
    {
        _logger = logger;
        _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        var key = Environment.GetEnvironmentVariable("INTERNAL_API_KEY");
        ValidateProductionKey(key);

        _expectedKey = key ?? "dev-internal-key-for-local-development-only";
    }

    private bool IsProduction => _environment.Equals("Production", StringComparison.OrdinalIgnoreCase);

    private void ValidateProductionKey(string? key)
    {
        if (!IsProduction) return;

        var isKeyMissing = string.IsNullOrEmpty(key);
        var isKeyTooShort = key?.Length < MinKeyLength;

        if (isKeyMissing || isKeyTooShort)
        {
            throw new InvalidOperationException(
                $"SECURITY: INTERNAL_API_KEY must be configured (min {MinKeyLength} chars) in production");
        }
    }

    public bool ValidateRequest(HttpContext context, string resource)
    {
        var headers = context.Request.Headers;
        var apiKey = headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        var serviceName = headers["X-Service-Name"].FirstOrDefault();
        var timestamp = headers["X-Request-Timestamp"].FirstOrDefault();
        var signature = headers["X-Request-Signature"].FirstOrDefault();

        // Basic validation
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(serviceName))
            return false;

        // Timing-safe API key comparison (CVH-001 fix)
        if (!TimingSafeEquals(apiKey, _expectedKey))
            return false;

        // Service whitelist check
        if (!AllowedServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase))
            return false;

        // Production requires HMAC signature
        if (IsProduction)
        {
            return ValidateHmacSignature(serviceName, timestamp, signature, resource);
        }

        return true;
    }

    private bool ValidateHmacSignature(string serviceName, string? timestamp, string? signature, string resource)
    {
        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Missing timestamp/signature from {Service}", serviceName);
            return false;
        }

        if (!long.TryParse(timestamp, out var unixTimestamp))
            return false;

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > TimestampValidityMinutes)
        {
            _logger.LogWarning("Expired timestamp from {Service}", serviceName);
            return false;
        }

        var expectedSignature = ComputeHmacSignature(serviceName, timestamp, resource);
        if (!TimingSafeEquals(signature, expectedSignature))
        {
            _logger.LogWarning("Invalid HMAC signature from {Service}", serviceName);
            return false;
        }

        return true;
    }

    private string ComputeHmacSignature(string serviceName, string timestamp, string resource)
    {
        var message = $"{serviceName}|{timestamp}|{resource}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_expectedKey));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));
    }

    private static bool TimingSafeEquals(string a, string b)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
    }
}
