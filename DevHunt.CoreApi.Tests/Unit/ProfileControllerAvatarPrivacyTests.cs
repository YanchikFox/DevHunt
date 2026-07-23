using Xunit;
using Microsoft.AspNetCore.Mvc;
using DevHunt.CoreApi.Controllers;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;

namespace DevHunt.CoreApi.Tests.Unit;

/// <summary>
/// Unit tests for ProfileController (Avatar and Privacy features) (ARCH-008: Improve Test Coverage to 70%)
/// </summary>
public class ProfileControllerAvatarPrivacyTests : IDisposable
{
    private readonly DevHuntDbContext _context;
    private readonly Mock<IObjectStorageService> _objectStorageMock;
    private readonly ProfileController _controller;
    private readonly Guid _testUserId;

    public ProfileControllerAvatarPrivacyTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DevHuntDbContext(options);
        _objectStorageMock = new Mock<IObjectStorageService>();
        var cacheMock = new Mock<ICacheService>();
        var eventBusMock = new Mock<IEventBusService>();
        _controller = new ProfileController(
            _context,
            _objectStorageMock.Object,
            cacheMock.Object,
            eventBusMock.Object);
        
        _testUserId = Guid.NewGuid();
        
        // Setup test user
        _context.Users.Add(new User
        {
            Id = _testUserId,
            Email = "user@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            FullName = "Test User"
        });
        
        _context.SaveChanges();
    }

    private void SetUserContext(Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetPrivacySettings_ShouldReturnDefaultSettings_WhenNoneExist()
    {
        // Arrange
        SetUserContext(_testUserId);

        // Act
        var result = await _controller.GetPrivacySettings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Verify the settings were created
        var settings = await _context.UserPrivacySettings.FirstOrDefaultAsync(s => s.UserId == _testUserId);
        Assert.NotNull(settings);
    }

    [Fact]
    public async Task GetPrivacySettings_ShouldReturnExistingSettings_WhenTheyExist()
    {
        // Arrange
        SetUserContext(_testUserId);
        _context.UserPrivacySettings.Add(new UserPrivacySettings
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            ProfileVisibility = "private",
            ShowEmail = false,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPrivacySettings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task UpdatePrivacySettings_ShouldUpdateSettings_WhenValidRequest()
    {
        // Arrange
        SetUserContext(_testUserId);
        var request = new ProfileController.UpdatePrivacySettingsRequest(
            "private",
            false,
            true,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );

        // Act
        var result = await _controller.UpdatePrivacySettings(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        var settings = await _context.UserPrivacySettings.FirstOrDefaultAsync(s => s.UserId == _testUserId);
        Assert.NotNull(settings);
        Assert.Equal("private", settings.ProfileVisibility);
        Assert.False(settings.ShowEmail);
        Assert.True(settings.ShowSkills);
    }

    [Fact]
    public async Task UpdatePrivacySettings_ShouldReturnBadRequest_WhenInvalidVisibility()
    {
        // Arrange
        SetUserContext(_testUserId);
        var request = new ProfileController.UpdatePrivacySettingsRequest(
            "invalid_visibility",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );

        // Act
        var result = await _controller.UpdatePrivacySettings(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid ProfileVisibility", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task DeleteAvatar_ShouldDeleteAvatar_WhenAvatarExists()
    {
        // Arrange
        SetUserContext(_testUserId);
        var user = await _context.Users.FindAsync(_testUserId);
        user!.AvatarUrl = "avatars/test.jpg";
        await _context.SaveChangesAsync();

        _objectStorageMock.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteAvatar();

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        user = await _context.Users.FindAsync(_testUserId);
        Assert.Null(user!.AvatarUrl);
    }

    [Fact]
    public async Task DeleteAvatar_ShouldReturnBadRequest_WhenNoAvatar()
    {
        // Arrange
        SetUserContext(_testUserId);

        // Act
        var result = await _controller.DeleteAvatar();

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("No avatar", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task UploadAvatar_ShouldReturnBadRequest_WhenNoFile()
    {
        // Arrange
        SetUserContext(_testUserId);
        IFormFile? file = null;

        // Act
        var result = await _controller.UploadAvatar(file!);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("No file", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task UploadAvatar_ShouldReturnBadRequest_WhenFileTooLarge()
    {
        // Arrange
        SetUserContext(_testUserId);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(6 * 1024 * 1024); // 6 MB
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");

        // Act
        var result = await _controller.UploadAvatar(fileMock.Object);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("exceeds 5 MB", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task UploadAvatar_ShouldReturnBadRequest_WhenInvalidFileType()
    {
        // Arrange
        SetUserContext(_testUserId);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");
        fileMock.Setup(f => f.FileName).Returns("test.pdf");

        // Act
        var result = await _controller.UploadAvatar(fileMock.Object);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid file type", badRequest.Value?.ToString() ?? "");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

