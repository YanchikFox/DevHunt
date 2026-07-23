using System.Net;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevHunt.CoreApi.Tests.Integration;

/// <summary>
/// Integration tests for ProjectsController.
/// Tests real HTTP requests to API with InMemory database.
/// </summary>
public class ProjectsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DevHuntDbContext _context;
    private readonly HttpClient _client;

    public ProjectsControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove ALL DbContext-related registrations
                var descriptorsToRemove = services
                    .Where(d => 
                        d.ServiceType == typeof(DbContextOptions<DevHuntDbContext>) ||
                        d.ServiceType == typeof(DevHuntDbContext) ||
                        (d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)) ||
                        d.ServiceType.Name.Contains("ReadWriteDbContextFactory"))
                    .ToList();
                
                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                // Register InMemory database for tests (replace PostgreSQL)
                services.AddDbContext<DevHuntDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                }, ServiceLifetime.Scoped);
            });
        });

        _client = _factory.CreateClient();
        
        // Get DbContext from the DI container
        using var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<DevHuntDbContext>();
    }

    [Fact]
    public async Task GetProjects_ShouldReturnUnauthorized_WhenNoAuth()
    {
        // Act
        var response = await _client.GetAsync("/api/projects");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Metrics_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Prometheus");
    }

    public void Dispose()
    {
        _context?.Dispose();
        _client?.Dispose();
    }
}

