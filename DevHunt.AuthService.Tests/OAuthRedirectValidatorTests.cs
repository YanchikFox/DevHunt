using DevHunt.AuthService.Security;
using FluentAssertions;
using Xunit;

namespace DevHunt.AuthService.Tests;

public class OAuthRedirectValidatorTests
{
    [Fact]
    public void IsAllowedRedirectUri_rejects_prefix_attack_host()
    {
        var redirect = new Uri("https://app.example.com.evil.tld/oauth-callback");
        var allowed = new[] { "https://app.example.com" };

        OAuthRedirectValidator.IsAllowedRedirectUri(redirect, allowed).Should().BeFalse();
    }

    [Fact]
    public void IsAllowedRedirectUri_accepts_same_origin_path()
    {
        var redirect = new Uri("https://app.example.com/en/oauth-callback");
        var allowed = new[] { "https://app.example.com" };

        OAuthRedirectValidator.IsAllowedRedirectUri(redirect, allowed).Should().BeTrue();
    }

    [Fact]
    public void IsAllowedRedirectUri_rejects_different_port()
    {
        var redirect = new Uri("http://localhost:3001/callback");
        var allowed = new[] { "http://localhost:3000" };

        OAuthRedirectValidator.IsAllowedRedirectUri(redirect, allowed).Should().BeFalse();
    }
}
