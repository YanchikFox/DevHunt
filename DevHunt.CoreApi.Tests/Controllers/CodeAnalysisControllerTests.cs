using System.Security.Claims;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DevHunt.CoreApi.Tests.Controllers;

/// <summary>
/// Unit tests for public code-analysis endpoints and project membership authorization.
/// </summary>
public class CodeAnalysisControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly DevHuntDbContext _dbContext;
    private readonly CodeAnalysisController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public CodeAnalysisControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new DevHuntTestDbContext(options);

        var cacheMock = new Mock<ICacheService>();
        var mlServiceMock = new Mock<IMLServiceClient>();
        var loggerMock = new Mock<ILogger<CodeAnalysisController>>();

        _controller = new CodeAnalysisController(
            _dbContext,
            cacheMock.Object,
            mlServiceMock.Object,
            loggerMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, _userId.ToString())],
                    "Test")),
            },
        };
    }

    /// <inheritdoc />
    public async Task InitializeAsync() => await _dbContext.Database.EnsureCreatedAsync();

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Former project members with status left must not read latest analysis results.
    /// </summary>
    [Fact]
    public async Task GetLatest_WhenMemberStatusIsLeft_ReturnsForbid()
    {
        await SeedLeftMemberAsync();
        await SeedCompletedAnalysisAsync();

        var result = await _controller.GetLatest(_projectId);

        result.Should().BeOfType<ForbidResult>();
    }

    /// <summary>
    /// Former project members with status left must not read analysis history.
    /// </summary>
    [Fact]
    public async Task GetHistory_WhenMemberStatusIsLeft_ReturnsForbid()
    {
        await SeedLeftMemberAsync();
        await SeedCompletedAnalysisAsync();

        var result = await _controller.GetHistory(_projectId);

        result.Should().BeOfType<ForbidResult>();
    }

    /// <summary>
    /// Active project members can read latest completed analysis results.
    /// </summary>
    [Fact]
    public async Task GetLatest_WhenMemberStatusIsActive_ReturnsOk()
    {
        await SeedActiveMemberAsync();
        await SeedCompletedAnalysisAsync();

        var result = await _controller.GetLatest(_projectId);

        result.Should().BeOfType<OkObjectResult>();
    }

    private async Task SeedLeftMemberAsync()
    {
        await SeedProjectGraphAsync();
        _dbContext.TeamMembers.Add(new TeamMember
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            UserId = _userId,
            Role = "developer",
            Status = TeamMemberStatus.Left.Value,
            LeftAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedActiveMemberAsync()
    {
        await SeedProjectGraphAsync();
        _dbContext.TeamMembers.Add(new TeamMember
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            UserId = _userId,
            Role = "developer",
            Status = TeamMemberStatus.Active.Value,
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedCompletedAnalysisAsync()
    {
        await SeedProjectGraphAsync();
        _dbContext.CodeAnalysisResults.Add(new CodeAnalysisResult
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Repository = "owner/repo",
            Status = "completed",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedProjectGraphAsync()
    {
        if (await _dbContext.Projects.AnyAsync(p => p.Id == _projectId))
            return;

        _dbContext.Users.Add(new User
        {
            Id = _userId,
            Email = "member@test.dev",
            PasswordHash = "hash",
            Role = "participant",
        });

        _dbContext.Projects.Add(new Project
        {
            Id = _projectId,
            Title = "Test Project",
            Description = "Test",
            OwnerId = _userId,
            Status = "active",
            Visibility = "public",
            TechStack = [],
        });

        await _dbContext.SaveChangesAsync();
    }
}
