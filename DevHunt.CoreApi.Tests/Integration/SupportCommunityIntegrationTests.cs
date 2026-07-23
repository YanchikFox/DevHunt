using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using DevHunt.CoreApi;
using Microsoft.Extensions.DependencyInjection;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Tests.Integration;

/// <summary>
/// Integration tests for Support and Community API endpoints (ARCH-008: Improve Test Coverage to 70%)
/// </summary>
public class SupportCommunityIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SupportCommunityIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace database with in-memory for testing
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<DevHuntDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<DevHuntDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });
            });
        });
        
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetFeedback_WithoutAuth_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/community/feedback");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task CreateFeedback_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new
        {
            type = "bug",
            title = "Test Bug",
            description = "Test Description"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/community/feedback", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSupportTickets_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/support/tickets");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfilePrivacy_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/profile/privacy");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

