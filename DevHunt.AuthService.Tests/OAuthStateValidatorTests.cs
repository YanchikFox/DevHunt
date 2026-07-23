using DevHunt.AuthService.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DevHunt.AuthService.Tests;

public class OAuthStateValidatorTests
{
    [Fact]
    public void ValidateState_rejects_expired_state()
    {
        var secret = "test_oauth_state_secret_min_32_chars_ok";
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-11).ToUnixTimeSeconds();
        var payload = $"http://localhost:3000/oauth|{Guid.NewGuid()}|{oldTimestamp}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));
        var combined = $"{payload}|{signature}";
        var state = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes(combined));

        OAuthStateValidator.ValidateState(state, secret).Should().BeNull();
    }

    [Fact]
    public void ValidateState_accepts_fresh_state()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "test_oauth_state_secret_min_32_chars_ok" })
            .Build();
        var secret = OAuthStateValidator.ResolveStateSecret(config);
        var state = OAuthStateValidator.BuildState("http://localhost:3000/oauth-callback", secret);

        var payload = OAuthStateValidator.ValidateState(state, secret);
        payload.Should().StartWith("http://localhost:3000/oauth-callback");
    }
}
