using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Profile;
using DevHunt.CoreApi.Services.Users;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace DevHunt.CoreApi.Tests.Controllers;

/// <summary>
/// Unit tests for ProfileController
/// </summary>
public class ProfileControllerTests : IDisposable
{
    private readonly DevHuntDbContext _dbContext;
    private readonly Mock<IProfileServices> _profileServicesMock;
    private readonly Mock<IObjectStorageService> _objectStorageMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IEventBusService> _eventBusMock;
    private readonly Mock<IActivityLogService> _activityLogMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly Mock<ILogger<ProfileController>> _loggerMock;
    private readonly ProfileController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public ProfileControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DevHuntTestDbContext(options);

        // Setup individual service mocks
        _objectStorageMock = new Mock<IObjectStorageService>();
        _cacheMock = new Mock<ICacheService>();
        _eventBusMock = new Mock<IEventBusService>();
        _activityLogMock = new Mock<IActivityLogService>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _loggerMock = new Mock<ILogger<ProfileController>>();

        // Setup cache mock
        _cacheMock
            .Setup(x => x.GetAsync<ProfileController.ProfileResponse>(It.IsAny<string>()))
            .ReturnsAsync((ProfileController.ProfileResponse?)null);
        _cacheMock
            .Setup(x => x.GetAsync<ProfileController.ProfilePublicResponse>(It.IsAny<string>()))
            .ReturnsAsync((ProfileController.ProfilePublicResponse?)null);
        _cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ProfileController.ProfileResponse>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ProfileController.ProfilePublicResponse>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _eventBusMock
            .Setup(x => x.PublishAsync(It.IsAny<DomainEvent>()))
            .Returns(Task.CompletedTask);

        _activityLogMock
            .Setup(x => x.LogUserEventAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>()))
            .ReturnsAsync(new ActivityRecord
            {
                Id = Guid.NewGuid(),
                ActorId = _testUserId,
                EventType = "user.updated_profile",
                Visibility = "public",
                EventGroup = "user"
            });

        // Setup IProfileServices facade mock
        _profileServicesMock = new Mock<IProfileServices>();
        _profileServicesMock.Setup(x => x.ObjectStorage).Returns(_objectStorageMock.Object);
        _profileServicesMock.Setup(x => x.Cache).Returns(_cacheMock.Object);
        _profileServicesMock.Setup(x => x.EventBus).Returns(_eventBusMock.Object);
        _profileServicesMock.Setup(x => x.ActivityLog).Returns(_activityLogMock.Object);
        _profileServicesMock.Setup(x => x.Environment).Returns(_environmentMock.Object);

        var userStatsService = new UserStatsService(_dbContext);
        var userProfileService = new UserProfileService(_dbContext, userStatsService);

        _controller = new ProfileController(
            _dbContext,
            _profileServicesMock.Object,
            _loggerMock.Object,
            userProfileService,
            userStatsService);

        // Setup User from Claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = ControllerTestHttpContext.Create(claimsPrincipal),
        };
    }

    [Fact]
    public async Task GetMyProfile_ShouldReturnProfile_WhenUserExists()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            FullName = "Test User",
            Bio = "Test bio",
            IsActive = true
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetMyProfile();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateMyProfile_ShouldReturnUpdatedProfile_WhenValidRequest()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            FullName = "Original Name",
            Bio = "Original bio",
            IsActive = true
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var updateRequest = new ProfileController.UpdateProfileRequest(
            FullName: "Updated Name",
            Bio: "Updated bio",
            Timezone: "UTC",
            Skills: null,
            Experience: null,
            Github: null,
            GithubUsername: null,
            Linkedin: null,
            Website: null,
            AvatarUrl: null,
            Language: null
        );

        // Act
        var result = await _controller.UpdateMyProfile(updateRequest, default);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        // Verify saving to DB
        var dbUser = await _dbContext.Users.FindAsync(_testUserId);
        dbUser!.FullName.Should().Be("Updated Name");
        dbUser.Bio.Should().Be("Updated bio");

        // Verify Event Bus was called
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<DomainEvent>()),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateMyProfile_WithSkills_ShouldPersistUserSkillEntries_WithCanonicalAndRaw()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            FullName = "Original Name",
            Bio = "Original bio",
            IsActive = true
        };
        _dbContext.Users.Add(user);

        var csharp = new Skill
        {
            Id = Guid.NewGuid(),
            Name = "C#",
            Category = "Programming Languages",
            Description = "",
            IconUrl = ""
        };
        _dbContext.Skills.Add(csharp);
        await _dbContext.SaveChangesAsync();

        var updateRequest = new ProfileController.UpdateProfileRequest(
            FullName: null,
            Bio: null,
            Timezone: null,
            Skills: new() { "C#", "SomeMadeUpSkill" },
            Experience: null,
            Github: null,
            GithubUsername: null,
            Linkedin: null,
            Website: null,
            AvatarUrl: null,
            Language: null
        );

        // Act
        var result = await _controller.UpdateMyProfile(updateRequest, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var entries = await _dbContext.UserSkillEntries
            .Where(x => x.UserId == _testUserId)
            .ToListAsync();

        entries.Should().HaveCount(2);
        entries.Should().ContainSingle(x => x.Raw == "C#" && x.SkillId == csharp.Id);
        entries.Should().ContainSingle(x => x.Raw == "SomeMadeUpSkill" && x.SkillId == null);
    }

    [Fact]
    public async Task GetPublicProfile_ShouldReturnProfile_WhenUserExists()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "public@example.com",
            PasswordHash = "hash",
            FullName = "Public User",
            Bio = "Public bio",
            IsActive = true
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetPublicProfile(user.Id, default);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPublicProfile_ShouldReturnNotFound_WhenUserNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _controller.GetPublicProfile(nonExistentId, default);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundResult>();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
