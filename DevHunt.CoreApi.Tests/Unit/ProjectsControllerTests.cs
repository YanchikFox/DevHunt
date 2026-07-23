using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevHunt.CoreApi.Controllers;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace DevHunt.CoreApi.Tests.Unit;

/// <summary>
/// Tests for ProjectsController.
/// Cover core CRUD operations and project management business logic.
/// </summary>
public class ProjectsControllerTests : IDisposable
{
    private readonly DevHuntDbContext _dbContext;
    private readonly ProjectsController _controller;
    private readonly Guid _testUserId;

    public ProjectsControllerTests()
    {
        // Use in-memory database
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new DevHuntDbContext(options);
        _controller = new ProjectsController(_dbContext);
        
        _testUserId = Guid.NewGuid();
        
        // Setup user context
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            },
            RouteData = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor()
        };
        
        // Seed test user
        var user = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hashed",
            Role = "participant"
        };
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetProjects_ShouldReturnListOfProjects()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Test Project",
            Description = "Test Description",
            Status = "active",
            Visibility = "public",
            OwnerId = _testUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetProjects(null, null, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var projects = okResult!.Value as IEnumerable<ProjectsController.ProjectSummaryDto>;
        projects.Should().NotBeNull();
        projects!.Should().HaveCount(1);
        projects.First().Title.Should().Be("Test Project");
    }

    [Fact]
    public async Task GetProject_WhenExists_ShouldReturnProjectDetails()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Title = "Test Project",
            Description = "Test Description",
            Status = "active",
            Visibility = "public",
            OwnerId = _testUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetProject(projectId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var projectDto = okResult!.Value as ProjectsController.ProjectDetailsDto;
        projectDto.Should().NotBeNull();
        projectDto!.Title.Should().Be("Test Project");
    }

    [Fact]
    public async Task GetProject_WhenNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _controller.GetProject(nonExistentId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateProject_ShouldCreateAndReturnProject()
    {
        // Arrange
        var request = new ProjectsController.CreateProjectRequest(
            Title: "New Project",
            Description: "New Description",
            ShortDescription: "Short",
            TechStack: new[] { "React", "TypeScript" },
            Status: "draft",
            Visibility: "public",
            DifficultyLevel: "intermediate",
            ExpectedDurationDays: 30,
            MaxTeamSize: 5
        );

        // Act
        var result = await _controller.CreateProject(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        
        // Verify project was saved
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Title == "New Project");
        project.Should().NotBeNull();
        project!.OwnerId.Should().Be(_testUserId);
    }

    [Fact]
    public async Task CreateProject_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new ProjectsController.CreateProjectRequest(
            Title: "", // Invalid: empty title
            Description: "Description",
            ShortDescription: null,
            TechStack: Array.Empty<string>(),
            Status: "draft",
            Visibility: "public",
            DifficultyLevel: null,
            ExpectedDurationDays: null,
            MaxTeamSize: null
        );

        // Act
        var result = await _controller.CreateProject(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateProject_WhenUserIsOwner_ShouldUpdateProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Title = "Original Title",
            Description = "Original Description",
            Status = "draft",
            Visibility = "public",
            OwnerId = _testUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        var request = new ProjectsController.UpdateProjectRequest(
            Title: "Updated Title",
            Description: "Updated Description",
            ShortDescription: null,
            TechStack: new[] { "React" },
            Status: "active",
            Visibility: "public",
            DifficultyLevel: "intermediate",
            ExpectedDurationDays: 30,
            MaxTeamSize: 5
        );

        // Act
        var result = await _controller.UpdateProject(projectId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        // Verify project was updated
        var updatedProject = await _dbContext.Projects.FindAsync(projectId);
        updatedProject.Should().NotBeNull();
        updatedProject!.Title.Should().Be("Updated Title");
        updatedProject.Status.Should().Be("active");
    }

    [Fact]
    public async Task UpdateProject_WhenUserIsNotOwner_ShouldReturnForbidden()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var otherUser = new User
        {
            Id = otherUserId,
            Email = "other@example.com",
            PasswordHash = "hashed",
            Role = "participant"
        };
        _dbContext.Users.Add(otherUser);
        
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Title = "Other User Project",
            Description = "Description",
            Status = "active",
            Visibility = "public",
            OwnerId = otherUserId, // Different owner
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        var request = new ProjectsController.UpdateProjectRequest(
            Title: "Hacked Title",
            Description: "Hacked",
            ShortDescription: null,
            TechStack: Array.Empty<string>(),
            Status: "active",
            Visibility: "public",
            DifficultyLevel: null,
            ExpectedDurationDays: null,
            MaxTeamSize: null
        );

        // Act
        var result = await _controller.UpdateProject(projectId, request);

        // Assert
        result.Should().BeOfType<ForbidResult>();
        
        // Verify project was NOT updated
        var unchangedProject = await _dbContext.Projects.FindAsync(projectId);
        unchangedProject!.Title.Should().Be("Other User Project");
    }

    [Fact]
    public async Task DeleteProject_WhenUserIsOwner_ShouldDeleteProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Title = "To Delete",
            Description = "Will be deleted",
            Status = "active",
            Visibility = "public",
            OwnerId = _testUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteProject(projectId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        // Verify project was deleted
        var deletedProject = await _dbContext.Projects.FindAsync(projectId);
        deletedProject.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProject_WhenNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _controller.DeleteProject(nonExistentId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

