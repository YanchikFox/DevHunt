using Xunit;
using Microsoft.AspNetCore.Mvc;
using Moq;
using DevHunt.CoreApi.Controllers;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Services;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Tests.Unit;

/// <summary>
/// Unit tests for ProjectTeamController read endpoints.
/// </summary>
public class ProjectTeamControllerReadTests : IDisposable
{
    private readonly DevHuntDbContext _context;
    private readonly ProjectTeamController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testProjectId;

    public ProjectTeamControllerReadTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DevHuntDbContext(options);

        var eventBus = new Mock<IEventBusService>();
        var activityLog = new Mock<IActivityLogService>();
        activityLog
            .Setup(x => x.LogProjectEventAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>()))
            .ReturnsAsync(new ActivityRecord
            {
                Id = Guid.NewGuid(),
                ActorId = Guid.NewGuid(),
                EventType = "test.event",
                Visibility = "public",
                EventGroup = "test"
            });

        _controller = new ProjectTeamController(_context, eventBus.Object, activityLog.Object);
        
        _testUserId = Guid.NewGuid();
        _testProjectId = Guid.NewGuid();
        
        // Setup test user
        _context.Users.Add(new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true
        });
        
        // Setup test project
        _context.Projects.Add(new Project
        {
            Id = _testProjectId,
            Title = "Test Project",
            OwnerId = _testUserId,
            Status = "active",
            Visibility = "public"
        });
        
        _context.SaveChanges();
        
    }

    [Fact]
    public async Task GetMembers_ReturnsActiveAndLeftMembers()
    {
        // Arrange
        var user2Id = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = user2Id,
            Email = "user2@example.com",
            PasswordHash = "hash",
            Role = "participant",
            FullName = "User Two",
            IsActive = true
        });

        _context.TeamMembers.AddRange(
            new TeamMember
            {
                Id = Guid.NewGuid(),
                ProjectId = _testProjectId,
                UserId = _testUserId,
                Role = "Developer",
                Status = "active",
                JoinedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                ProjectId = _testProjectId,
                UserId = user2Id,
                Role = "Designer",
                Status = "left",
                JoinedAt = DateTime.UtcNow.AddDays(-2),
                LeftAt = DateTime.UtcNow.AddDays(-1)
            });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMembers(_testProjectId, includePermissions: false);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var members = Assert.IsAssignableFrom<IEnumerable<ProjectTeamController.PublicTeamMemberDto>>(okResult.Value);
        members.Should().HaveCount(2);
        members.Select(x => x.Status).Should().Contain(new[] { "active", "left" });
    }

    [Fact]
    public async Task GetRoles_ReturnsVacanciesBasedOnRequiredRoles()
    {
        // Arrange
        var project = await _context.Projects.FirstAsync(p => p.Id == _testProjectId);
        project.RequiredRoles = new List<string> { "Developer", "Developer", "Designer" };

        _context.TeamMembers.Add(new TeamMember
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = "Developer",
            Status = "active",
            JoinedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRoles(_testProjectId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var vacancies = Assert.IsAssignableFrom<IEnumerable<ProjectTeamController.VacancyDto>>(okResult.Value);
        vacancies.Should().Contain(v => v.Role == "Developer" && v.TotalNeeded == 2 && v.CurrentFilled == 1);
        vacancies.Should().Contain(v => v.Role == "Designer" && v.TotalNeeded == 1 && v.CurrentFilled == 0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

