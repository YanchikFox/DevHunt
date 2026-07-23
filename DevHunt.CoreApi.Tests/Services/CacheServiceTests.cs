using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using DevHunt.CoreApi.Services;
using System.Text.Json;
using System.Text;

namespace DevHunt.CoreApi.Tests.Services;

/// <summary>
/// Unit tests for CacheService
/// </summary>
public class CacheServiceTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<CacheService>> _loggerMock;
    private readonly CacheService _cacheService;

    public CacheServiceTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<CacheService>>();
        _cacheService = new CacheService(_cacheMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnValue_WhenKeyExistsAsync()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var jsonValue = JsonSerializer.Serialize(value);
        var bytes = Encoding.UTF8.GetBytes(jsonValue);

        _cacheMock.Setup(x => x.GetAsync(key, default))
            .ReturnsAsync(bytes);

        // Act
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyNotExistsAsync()
    {
        // Arrange
        var key = "non-existent-key";
        _cacheMock.Setup(x => x.GetAsync(key, default))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ShouldSetValue_WhenValidRequestAsync()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";

        _cacheMock.Setup(x => x.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(10));

        // Assert
        _cacheMock.Verify(
            x => x.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.Once
        );
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveValue_WhenKeyExistsAsync()
    {
        // Arrange
        var key = "test-key";
        _cacheMock.Setup(x => x.RemoveAsync(key, default))
            .Returns(Task.CompletedTask);

        // Act
        await _cacheService.RemoveAsync(key);

        // Assert
        _cacheMock.Verify(
            x => x.RemoveAsync(key, default),
            Times.Once
        );
    }
}

