using Xunit;
using Microsoft.AspNetCore.Mvc;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Tests.Unit;

/// <summary>
/// Unit tests for UsersController (ARCH-008: Improve Test Coverage to 70%)
/// </summary>
public class UsersControllerTests : IDisposable
{
    private readonly DevHuntDbContext _context;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DevHuntDbContext(options);
        _controller = new UsersController(
            _context,
            new NoOpAuditService(),
            new StubActivityLogService(),
            new StubUserStatsService());
    }

    [Fact]
    public async Task GetUsers_ReturnsListOfActiveUsers()
    {
        // Arrange
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "user1@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            FullName = "Test User 1"
        });
        
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "user2@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = false, // Should be filtered out
            FullName = "Test User 2"
        });
        
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetUsers(null, null, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UsersController.UserSummaryDto>>(okResult.Value);
        Assert.Single(users); // Only active user
        Assert.All(users, u => Assert.True(u.Id != Guid.Empty));
    }

    [Fact]
    public async Task GetUsers_WithQuery_FiltersByQuery()
    {
        // Arrange
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "user1@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            FullName = "John Doe",
            Bio = "Developer"
        });
        
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "user2@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            FullName = "Jane Smith",
            Bio = "Designer"
        });
        
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetUsers("John", null, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UsersController.UserSummaryDto>>(okResult.Value);
        Assert.Single(users);
        Assert.Contains("John", users.First().FullName ?? "");
    }

    [Fact]
    public async Task GetUsers_WithRole_FiltersByRole()
    {
        // Arrange
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            PasswordHash = "hash",
            Role = "admin",
            IsActive = true
        });
        
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true
        });
        
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetUsers(null, "admin", null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UsersController.UserSummaryDto>>(okResult.Value);
        Assert.All(users, u => Assert.Equal("admin", u.Role));
    }

    [Fact]
    public async Task GetUsers_WithInvalidRole_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetUsers(null, "invalid-role", null);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetUser_WithValidId_ReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            FullName = "Test User"
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetUser(userId);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetUser_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetUser(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private class NoOpAuditService : IAuditService
    {
        public Task LogActionAsync(Guid? userId, string action, string entityType, Guid? entityId = null, string? details = null, string? ipAddress = null)
            => Task.CompletedTask;
    }

    private class StubActivityLogService : IActivityLogService
    {
        public Task<ActivityRecord> LogAsync(Guid? projectId, Guid actorId, string eventType, string summary, string visibility = "public", string? eventGroup = null, Guid? targetUserId = null, object? payload = null)
            => Task.FromResult(new ActivityRecord { Id = Guid.NewGuid(), ActorId = actorId, EventType = eventType, Visibility = visibility, Summary = summary });

        public Task<ActivityRecord> LogProjectEventAsync(Guid projectId, Guid actorId, string eventType, string summary, string visibility = "public", Guid? targetUserId = null, object? payload = null)
            => LogAsync(projectId, actorId, eventType, summary, visibility, null, targetUserId, payload);

        public Task<ActivityRecord> LogUserEventAsync(Guid actorId, string eventType, string summary, string visibility = "public", Guid? targetUserId = null, object? payload = null)
            => LogAsync(null, actorId, eventType, summary, visibility, null, targetUserId, payload);
    }

    private class StubUserStatsService : IUserStatsService
    {
        public Task<UserStatsDto> GetStatsAsync(Guid userId) => Task.FromResult(new UserStatsDto(0, 0, 0, 0, 0, 0, 0));
    }
}

