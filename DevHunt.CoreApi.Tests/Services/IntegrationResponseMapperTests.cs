using DevHunt.CoreApi.Services.Integrations;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace DevHunt.CoreApi.Tests.Services;

/// <summary>
/// Unit tests for client-safe integration API projections.
/// </summary>
public class IntegrationResponseMapperTests
{
    /// <summary>
    /// Ensures webhook secrets and encrypted token fields never appear in API DTOs.
    /// </summary>
    [Fact]
    public void ToDto_RedactsConfigSecrets_AndOmitsEncryptedToken()
    {
        var integration = new Integration
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ServiceType = "github",
            AccessTokenEncrypted = "encrypted-blob",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Config = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["repository"] = "owner/repo",
                ["webhook_secret"] = "super-secret",
                ["access_token"] = "plain-token",
            },
        };

        var dto = IntegrationResponseMapper.ToDto(integration);

        dto.HasAccessToken.Should().BeTrue();
        dto.Config.Should().NotBeNull();
        dto.Config!["repository"].Should().Be("owner/repo");
        dto.Config!["webhook_secret"].Should().Be("***");
        dto.Config!["access_token"].Should().Be("***");

        var json = System.Text.Json.JsonSerializer.Serialize(dto);
        json.Should().NotContain("encrypted-blob");
        json.Should().NotContain("super-secret");
        json.Should().NotContain("plain-token");
        json.Should().NotContain("AccessTokenEncrypted");
    }
}
