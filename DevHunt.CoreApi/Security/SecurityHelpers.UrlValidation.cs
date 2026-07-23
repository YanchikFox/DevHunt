using System.Net;
using System.Net.Sockets;

namespace DevHunt.CoreApi.Security;

public static partial class SecurityHelpers
{
    private static readonly HashSet<string> BlockedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "metadata.google.internal",
        "metadata.google",
    };

    /// <summary>
    /// Validates HTTPS URLs safe for user-provided links and integration callbacks (SSRF-resistant).
    /// </summary>
    /// <param name="url">Absolute URL to validate.</param>
    /// <returns>True when the URL is HTTPS and does not target private or metadata endpoints.</returns>
    public static bool IsValidUrl(string? url)
    {
        if (!TryParseHttpsUrl(url, out var uri))
            return false;

        if (IsBlockedHostname(uri.Host))
            return false;

        if (IPAddress.TryParse(uri.Host, out var literalIp))
            return IsPublicIp(literalIp);

        try
        {
            var addresses = Dns.GetHostAddresses(uri.Host);
            return addresses.Length > 0 && addresses.All(IsPublicIp);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseHttpsUrl(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed))
            return false;

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrEmpty(parsed.Host))
            return false;

        uri = parsed;
        return true;
    }

    private static bool IsBlockedHostname(string host)
    {
        if (BlockedHosts.Contains(host))
            return true;

        return host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 0 || bytes[0] == 10)
                return false;
            if (bytes[0] == 127)
                return false;
            if (bytes[0] == 169 && bytes[1] == 254)
                return false;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return false;
            if (bytes[0] == 192 && bytes[1] == 168)
                return false;
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
                return false;

            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0
                && bytes[4] == 0 && bytes[5] == 0 && bytes[6] == 0 && bytes[7] == 1)
                return false;

            return true;
        }

        return false;
    }
}
