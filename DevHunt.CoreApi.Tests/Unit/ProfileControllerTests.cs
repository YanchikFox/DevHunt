using Xunit;
using Microsoft.AspNetCore.Mvc;
using DevHunt.CoreApi.Controllers;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Tests.Unit;

/// <summary>
/// Unit tests for ProfileController (ARCH-008: Improve Test Coverage to 70%)
/// </summary>
public class ProfileControllerTests : IDisposable
{
    private readonly DevHuntDbContext _context;
    private readonly ProfileController _controller;
    private readonly Guid _testUserId;

    public ProfileControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DevHuntDbContext(options);
        _controller = new ProfileController(_context);
        
        _testUserId = Guid.NewGuid();
        
        _context.Users.Add(new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            FullName = "Test User",
            Bio = "Test bio"
        });
        
        _context.SaveChanges();
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "participant")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    [Fact]
    public async Task GetMyProfile_ReturnsCurrentUserProfile()
    {
        // Act
        var result = await _controller.GetMyProfile();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<ProfileController.ProfileResponse>(okResult.Value);
        Assert.Equal(_testUserId, profile.Id);
        Assert.Equal("test@example.com", profile.Email);
    }

    [Fact]
    public async Task UpdateMyProfile_WithValidData_UpdatesProfile()
    {
        // Arrange
        var updateRequest = new ProfileController.UpdateProfileRequest(
            FullName: "Updated Name",
            Bio: "Updated bio",
            Timezone: "UTC",
            Skills: new[] { "C#", "React" },
            Experience: 5,
            Github: "github.com/test",
            Linkedin: "linkedin.com/in/test",
            Website: "test.com",
            AvatarUrl: null,
            Language: "en"
        );

        // Act
        var result = await _controller.UpdateMyProfile(updateRequest);

        // Assert
        Assert.IsType<OkResult>(result);
        
        var updatedUser = await _context.Users.FindAsync(_testUserId);
        Assert.Equal("Updated Name", updatedUser?.FullName);
        Assert.Equal("Updated bio", updatedUser?.Bio);
    }

    [Fact]
    public async Task GetMyProfile_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal();

        // Act
        var result = await _controller.GetMyProfile();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

