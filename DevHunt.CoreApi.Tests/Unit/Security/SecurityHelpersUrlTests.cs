using DevHunt.CoreApi.Security;
using FluentAssertions;

namespace DevHunt.CoreApi.Tests.Unit.Security;

public class SecurityHelpersUrlTests
{
    [Theory]
    [InlineData("https://github.com/devhunt")]
    [InlineData("https://example.com/path?q=1")]
    [InlineData("https://avatars.githubusercontent.com/u/1")]
    public void IsValidUrl_accepts_public_https_urls(string url)
    {
        SecurityHelpers.IsValidUrl(url).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("//example.com")]
    [InlineData("http://github.com/user")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://localhost/")]
    [InlineData("https://10.0.0.1/internal")]
    [InlineData("https://192.168.0.1/")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://metadata.google.internal/")]
    [InlineData("ftp://example.com/file")]
    public void IsValidUrl_rejects_unsafe_or_non_https_urls(string? url)
    {
        SecurityHelpers.IsValidUrl(url).Should().BeFalse();
    }

    [Fact]
    public void IsValidUrl_rejects_private_ipv6_loopback()
    {
        SecurityHelpers.IsValidUrl("https://[::1]/").Should().BeFalse();
    }
}
