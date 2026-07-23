using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DevHunt.Infrastructure.Security;

/// <summary>
/// Authorizes access to Prometheus <c>/metrics</c> endpoints (loopback or <c>X-Metrics-Token</c>).
/// </summary>
public static class MetricsAuthorization
{
    /// <summary>
    /// Returns true when the caller may read service metrics.
    /// </summary>
    /// <param name="remoteIp">Client IP from the connection.</param>
    /// <param name="providedMetricsToken">Value of the <c>X-Metrics-Token</c> header, if any.</param>
    public static bool IsAuthorized(IPAddress? remoteIp, string? providedMetricsToken)
    {
        if (remoteIp is not null && IPAddress.IsLoopback(remoteIp))
            return true;

        var expectedToken = Environment.GetEnvironmentVariable("METRICS_TOKEN");
        if (string.IsNullOrEmpty(expectedToken) || string.IsNullOrEmpty(providedMetricsToken))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedMetricsToken),
            Encoding.UTF8.GetBytes(expectedToken));
    }
}
