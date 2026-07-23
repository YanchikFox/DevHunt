using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using DevHunt.CoreApi;
using Microsoft.Extensions.DependencyInjection;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Tests.Integration;

/// <summary>
/// Integration tests for Projects API endpoints (ARCH-008: Improve Test Coverage to 70%)
/// </summary>
public class ProjectsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ProjectsIntegrationTests(WebApplicationFactory<Program> factory)
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
    public async Task GetProjects_WithoutAuth_ReturnsList()
    {
        // Act
        var response = await _client.GetAsync("/api/projects");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMetrics_ReturnsMetrics()
    {
        // Act
        var response = await _client.GetAsync("/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("# HELP", content); // Prometheus format
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

