using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Users;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;

namespace DevHunt.CoreApi.Tests.Controllers;

public class UsersSkillsFilterTests : IDisposable
{
    private readonly DevHuntDbContext _dbContext;
    private readonly UsersController _controller;

    public UsersSkillsFilterTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DevHuntTestDbContext(options);
        var userStatsService = new StubUserStatsService();
        var userSearchService = new UserSearchService(_dbContext);
        var notificationsMock = new Mock<INotificationHelperService>();
        var presenceMock = new Mock<IPresenceService>();
        var cacheMock = new Mock<ICacheService>();
        var userServices = new UserServicesFacade(
            userStatsService,
            new UserFollowService(_dbContext, notificationsMock.Object),
            new UserActivityService(_dbContext),
            new UserProfileService(_dbContext, userStatsService),
            userSearchService);
        _controller = new UsersController(
            _dbContext,
            new NoOpAuditService(),
            new StubActivityLogService(),
            userServices,
            presenceMock.Object,
            cacheMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }

    [Fact]
    public async Task GetUsers_ShouldFilterBySkills_UsingUserSkillEntries()
    {
        // Arrange
        var userWithSkill = new User
        {
            Id = Guid.NewGuid(),
            Email = "skill@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            IsEmailVerified = true,
            FullName = "Skilled User"
        };

        var userWithoutSkill = new User
        {
            Id = Guid.NewGuid(),
            Email = "noskill@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            IsEmailVerified = true,
            FullName = "Other User"
        };

        _dbContext.Users.AddRange(userWithSkill, userWithoutSkill);

        _dbContext.UserSkillEntries.Add(new UserSkillEntry
        {
            Id = Guid.NewGuid(),
            UserId = userWithSkill.Id,
            User = userWithSkill,
            Raw = "C#",
            RawNormalized = SkillNormalization.NormalizeSkillToken("C#"),
            SkillId = null,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetUsers(new UsersController.UserSearchQuery
        {
            Skills = "csharp",
            Page = 1,
            PageSize = 50,
            SortBy = "name",
            SortOrder = "asc",
        });

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);

        var ids = doc.RootElement.GetProperty("Data")
            .EnumerateArray()
            .Select(e => e.GetProperty("Id").GetGuid())
            .ToList();

        ids.Should().Contain(userWithSkill.Id);
        ids.Should().NotContain(userWithoutSkill.Id);
    }

    [Fact]
    public async Task GetUser_ShouldIncludeSkills_FromUserSkillEntries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "profile@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            IsEmailVerified = true,
            FullName = "Profile User"
        };
        _dbContext.Users.Add(user);

        _dbContext.UserSkillEntries.Add(new UserSkillEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = user,
            Raw = "C#",
            RawNormalized = SkillNormalization.NormalizeSkillToken("C#"),
            SkillId = null,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetUser(userId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var profile = okResult.Value.Should().BeOfType<UserProfileDto>().Subject;
        profile.Skills.Should().NotBeNull();
        profile.Skills!.Select(s => s.Name).Should().Contain("C#");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private class NoOpAuditService : IAuditService
    {
        public Task LogActionAsync(Guid? userId, string action, string entityType, Guid? entityId = null, string? details = null, string? ipAddress = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LogActionAsync(Guid? userId, string action, string entityType, Guid? entityId, string? details, string? ipAddress, string severity, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<List<AuditLog>> GetAuditLogsAsync(int page = 1, int pageSize = 50, string? action = null, Guid? userId = null, string? severity = null, string? ip = null, string? entityType = null, DateTime? dateFrom = null, DateTime? dateTo = null, bool adminOnly = false, CancellationToken ct = default)
            => Task.FromResult(new List<AuditLog>());

        public Task<int> GetAuditLogCountAsync(string? action = null, Guid? userId = null, string? severity = null, string? ip = null, string? entityType = null, DateTime? dateFrom = null, DateTime? dateTo = null, bool adminOnly = false, CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private class StubActivityLogService : IActivityLogService
    {
        public Task<ActivityRecord> LogAsync(Guid? projectId, Guid actorId, string eventType, string summary, string visibility = "public", string? eventGroup = null, Guid? targetUserId = null, object? payload = null, CancellationToken ct = default)
            => Task.FromResult(new ActivityRecord { Id = Guid.NewGuid(), ActorId = actorId, EventType = eventType, Visibility = visibility, Summary = summary });

        public Task<ActivityRecord> LogProjectEventAsync(Guid projectId, Guid actorId, string eventType, string summary, string visibility = "public", Guid? targetUserId = null, object? payload = null)
            => LogAsync(projectId, actorId, eventType, summary, visibility, null, targetUserId, payload);

        public Task<ActivityRecord> LogUserEventAsync(Guid actorId, string eventType, string summary, string visibility = "public", Guid? targetUserId = null, object? payload = null)
            => LogAsync(null, actorId, eventType, summary, visibility, null, targetUserId, payload);
    }

    private class StubUserStatsService : IUserStatsService
    {
        public Task<UserStatsDto> GetStatsAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(new UserStatsDto(0, 0, 0, 0, 0, 0, 0));
    }
}
