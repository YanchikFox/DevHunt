using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Projects;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using DomainEvent = DevHunt.CoreApi.Services.DomainEvent;
using System.Text.Json;

namespace DevHunt.CoreApi.Tests.Controllers;

/// <summary>
/// Unit тесты для ProjectsController
/// </summary>
public class ProjectsControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DevHuntDbContext _dbContext;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<IEventBusService> _eventBusMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IActivityLogService> _activityLogMock;
    private readonly IProjectFilterService _filterService;
    private readonly Mock<IProjectServices> _projectServicesMock;
    private readonly ProjectsController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public ProjectsControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new DevHuntTestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _dbContext.Users.Add(new User
        {
            Id = _testUserId,
            Email = "owner@test.dev",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
        });
        _dbContext.SaveChanges();

        // Моки для зависимостей
        _auditServiceMock = new Mock<IAuditService>();
        _eventBusMock = new Mock<IEventBusService>();
        _cacheMock = new Mock<ICacheService>();
        _activityLogMock = new Mock<IActivityLogService>();

        // Use real services for filtering (integration-style tests)
        var techStackMatcher = new TechStackMatcher(_dbContext);
        _filterService = new ProjectFilterService(_dbContext, techStackMatcher);
        var permissionService = new ProjectPermissionService(_dbContext, new ProjectAuthorizationPolicy(_dbContext));

        // Setup project services facade mock
        _projectServicesMock = new Mock<IProjectServices>();
        _projectServicesMock.Setup(x => x.Filter).Returns(_filterService);
        _projectServicesMock.Setup(x => x.Permissions).Returns(permissionService);
        _projectServicesMock.Setup(x => x.ActivityLog).Returns(_activityLogMock.Object);

        _eventBusMock
            .Setup(x => x.PublishAsync(It.IsAny<DomainEvent>()))
            .Returns(Task.CompletedTask);

        _cacheMock
            .Setup(x => x.GetAsync<ProjectsController.ProjectDetailsDto>(It.IsAny<string>()))
            .ReturnsAsync((ProjectsController.ProjectDetailsDto?)null);
        _cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ProjectsController.ProjectDetailsDto>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _cacheMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>()))
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
                EventType = "test.event",
                Visibility = "public",
                EventGroup = "user"
            });

        _activityLogMock
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
                ActorId = _testUserId,
                EventType = "test.event",
                Visibility = "public",
                EventGroup = "project"
            });

        // Настройка контроллера
        _controller = new ProjectsController(
            _dbContext,
            _auditServiceMock.Object,
            _eventBusMock.Object,
            _cacheMock.Object,
            _projectServicesMock.Object
        );

        // Настройка User из Claims
        SetupUserClaims(_testUserId);
    }

    private void SetupUserClaims(Guid userId, bool isAdmin = false)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = ControllerTestHttpContext.Create(claimsPrincipal),
        };
    }

    private void SetupAnonymousUser()
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    #region CreateProject Tests

    [Fact]
    public async Task CreateProject_ShouldReturnCreatedProject_WhenValidRequest()
    {
        // Arrange
        var request = new ProjectsController.CreateProjectRequest(
            Title: "Test Project",
            ShortDescription: "Short desc",
            Description: "Full description",
            TechStack: new List<string> { "C#", ".NET" },
            Status: null,
            Visibility: "public",
            DefaultNewsVisibility: null,
            DefaultFilesVisibility: null,
            DifficultyLevel: "intermediate",
            ExpectedDurationDays: 30,
            ShowcasePublished: false,
            Featured: false,
            MaxTeamSize: 5,
            RequiredRoles: new List<string> { "Developer" },
            StartDate: null,
            EndDate: null,
            OpenRoles: null
        );

        // Act
        var result = await _controller.CreateProject(request, default);

        // Assert
        result.Should().NotBeNull();
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var project = createdResult.Value.Should().BeOfType<ProjectsController.ProjectDetailsDto>().Subject;
        
        project.Title.Should().Be("Test Project");
        project.OwnerId.Should().Be(_testUserId);
        project.Status.Should().Be("draft");

        // Проверка сохранения в БД
        var dbProject = await _dbContext.Projects.FindAsync(project.Id);
        dbProject.Should().NotBeNull();
        dbProject!.Title.Should().Be("Test Project");

        // Проверка вызова Event Bus
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<DomainEvent>()),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateProject_ShouldReturnValidationProblem_WhenTitleIsWhitespace()
    {
        // Arrange
        var request = new ProjectsController.CreateProjectRequest(
            Title: "   ", // Whitespace only
            ShortDescription: null,
            Description: "Description",
            TechStack: null,
            Status: null,
            Visibility: "public",
            DefaultNewsVisibility: null,
            DefaultFilesVisibility: null,
            DifficultyLevel: null,
            ExpectedDurationDays: null,
            ShowcasePublished: false,
            Featured: false,
            MaxTeamSize: null,
            RequiredRoles: null,
            StartDate: null,
            EndDate: null,
            OpenRoles: null
        );

        // Act
        var result = await _controller.CreateProject(request, default);

        // Assert - Whitespace-only title triggers ValidationProblem
        // In unit tests without full MVC pipeline, ValidationProblem() returns UnprocessableEntityObjectResult
        var actionResult = result.Result;
        actionResult.Should().NotBeNull();
        actionResult.Should().BeAssignableTo<ObjectResult>();
        
        // Get the value and verify it contains validation errors
        var objectResult = (ObjectResult)actionResult!;
        var validationProblem = objectResult.Value as ValidationProblemDetails;
        validationProblem.Should().NotBeNull();
        validationProblem!.Errors.Should().ContainKey("Title");
    }

    #endregion

    #region GetProject Tests

    [Fact]
    public async Task GetProject_ShouldReturnProject_WhenExists()
    {
        // Arrange - Use 'active' status so project is publicly accessible, owner can view
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Test Project",
            Description = "Description",
            OwnerId = _testUserId,
            Status = "active", // Changed from 'draft' so it's publicly viewable
            Visibility = "public",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TeamMembers = new List<TeamMember>(),
            Tasks = new List<TaskItem>(),
            Invitations = new List<Invitation>()
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetProject(project.Id);

        // Assert - For public active projects, the DTO is returned directly via Value property
        result.Should().NotBeNull();
        var dto = result.Value.Should().BeOfType<ProjectsController.ProjectDetailsDto>().Subject;
        dto.Title.Should().Be("Test Project");
    }

    [Fact]
    public async Task GetProject_ShouldReturnNotFound_WhenNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _controller.GetProject(nonExistentId);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetProject_ShouldReturnNotFound_ForPrivateProjectByNonOwner()
    {
        // Arrange - API returns NotFound to hide existence of private projects (security best practice)
        var otherUserId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Private Project",
            Description = "Description",
            OwnerId = otherUserId,
            Status = "draft",
            Visibility = "private",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetProject(project.Id);

        // Assert - API hides existence of private projects by returning NotFound
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region UpdateProject Tests

    [Fact]
    public async Task UpdateProject_ShouldReturnNoContent_WhenValidRequest()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Original Title",
            Description = "Original Description",
            OwnerId = _testUserId,
            Status = "draft",
            Visibility = "public",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        var updateRequest = new ProjectsController.UpdateProjectRequest(
            Title: "Updated Title",
            ShortDescription: null,
            Description: "Updated Description",
            TechStack: null,
            Status: null,
            Visibility: null,
            DefaultNewsVisibility: null,
            DefaultFilesVisibility: null,
            DifficultyLevel: null,
            ExpectedDurationDays: null,
            ShowcasePublished: false,
            Featured: false,
            MaxTeamSize: null,
            RequiredRoles: null,
            StartDate: null,
            EndDate: null,
            OpenRoles: null
        );

        // Act
        var result = await _controller.UpdateProject(project.Id, updateRequest, default);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NoContentResult>();

        // Проверка сохранения в БД
        var dbProject = await _dbContext.Projects.FindAsync(project.Id);
        dbProject!.Title.Should().Be("Updated Title");

        // Проверка вызова Event Bus
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<DomainEvent>()),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateProject_ShouldReturnForbid_WhenUserIsNotOwner()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Test Project",
            Description = "Description",
            OwnerId = otherUserId, // Другой владелец
            Status = "draft",
            Visibility = "public",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        var updateRequest = new ProjectsController.UpdateProjectRequest(
            Title: "Updated Title",
            ShortDescription: null,
            Description: "Updated Description",
            TechStack: null,
            Status: null,
            Visibility: null,
            DefaultNewsVisibility: null,
            DefaultFilesVisibility: null,
            DifficultyLevel: null,
            ExpectedDurationDays: null,
            ShowcasePublished: false,
            Featured: false,
            MaxTeamSize: null,
            RequiredRoles: null,
            StartDate: null,
            EndDate: null,
            OpenRoles: null
        );

        // Act
        var result = await _controller.UpdateProject(project.Id, updateRequest, default);

        // Assert
        result.Should().NotBeNull();
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UpdateProject_ShouldReturnNotFound_WhenProjectNotExists()
    {
        // Arrange
        var updateRequest = new ProjectsController.UpdateProjectRequest(
            Title: "Updated Title",
            ShortDescription: null,
            Description: null,
            TechStack: null,
            Status: null,
            Visibility: null,
            DefaultNewsVisibility: null,
            DefaultFilesVisibility: null,
            DifficultyLevel: null,
            ExpectedDurationDays: null,
            ShowcasePublished: false,
            Featured: false,
            MaxTeamSize: null,
            RequiredRoles: null,
            StartDate: null,
            EndDate: null,
            OpenRoles: null
        );

        // Act
        var result = await _controller.UpdateProject(Guid.NewGuid(), updateRequest, default);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetProjects (List) Tests

    [Fact]
    public async Task GetProjects_ShouldReturnPaginatedList_WhenValidRequest()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            _dbContext.Projects.Add(new Project
            {
                Id = Guid.NewGuid(),
                Title = $"Project {i}",
                Description = "Description",
                OwnerId = _testUserId,
                Status = "recruiting", // Not draft - visible in catalog
                Visibility = "public",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetProjects(new ProjectQueryParams
        {
            Page = 1,
            PageSize = 10
        });

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        
        doc.RootElement.GetProperty("Data").GetArrayLength().Should().Be(10);
        doc.RootElement.GetProperty("Pagination").GetProperty("Total").GetInt32().Should().Be(15);
        doc.RootElement.GetProperty("Pagination").GetProperty("TotalPages").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("Pagination").GetProperty("HasNext").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetProjects_ShouldHideDrafts_ForAnonymousCatalog()
    {
        // Arrange
        SetupAnonymousUser();

        _dbContext.Projects.AddRange(
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Public Draft",
                Description = "Description",
                OwnerId = Guid.NewGuid(),
                Status = "draft",
                Visibility = "public",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Public Recruiting",
                Description = "Description",
                OwnerId = Guid.NewGuid(),
                Status = "recruiting",
                Visibility = "public",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetProjects(new ProjectQueryParams
        {
            Page = 1,
            PageSize = 50
        });

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);

        var titles = doc.RootElement.GetProperty("Data")
            .EnumerateArray()
            .Select(e => e.GetProperty("Title").GetString())
            .ToList();

        titles.Should().Contain("Public Recruiting");
        titles.Should().NotContain("Public Draft");
    }

    [Fact]
    public async Task GetProjects_ShouldShowDrafts_ForOwner()
    {
        // Arrange - user is authenticated as _testUserId
        _dbContext.Projects.AddRange(
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "My Draft",
                Description = "Description",
                OwnerId = _testUserId, // Same as current user
                Status = "draft",
                Visibility = "public",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Other Draft",
                Description = "Description",
                OwnerId = Guid.NewGuid(), // Different user
                Status = "draft",
                Visibility = "public",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await _dbContext.SaveChangesAsync();

        // Act - request myProjects=true
        var result = await _controller.GetProjects(new ProjectQueryParams
        {
            MyProjects = true,
            Page = 1,
            PageSize = 50
        });

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);

        var titles = doc.RootElement.GetProperty("Data")
            .EnumerateArray()
            .Select(e => e.GetProperty("Title").GetString())
            .ToList();

        titles.Should().Contain("My Draft");
        titles.Should().NotContain("Other Draft");
    }

    [Fact]
    public async Task GetProjects_ShouldSupportCommaSeparatedTechFilter()
    {
        // Arrange
        SetupAnonymousUser();

        _dbContext.Projects.AddRange(
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "React project",
                Description = "Description",
                OwnerId = Guid.NewGuid(),
                Status = "recruiting",
                Visibility = "public",
                TechStack = new List<string> { "React", "TypeScript" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Go project",
                Description = "Description",
                OwnerId = Guid.NewGuid(),
                Status = "recruiting",
                Visibility = "public",
                TechStack = new List<string> { "Go" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Rust project",
                Description = "Description",
                OwnerId = Guid.NewGuid(),
                Status = "recruiting",
                Visibility = "public",
                TechStack = new List<string> { "Rust" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await _dbContext.SaveChangesAsync();

        // Act - filter by "React,Go" should match React and Go projects
        var result = await _controller.GetProjects(new ProjectQueryParams
        {
            Tech = "React,Go",
            Page = 1,
            PageSize = 50
        });

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);

        var titles = doc.RootElement.GetProperty("Data")
            .EnumerateArray()
            .Select(e => e.GetProperty("Title").GetString())
            .ToList();

        titles.Should().Contain("React project");
        titles.Should().Contain("Go project");
        titles.Should().NotContain("Rust project");
    }

    [Fact]
    public async Task GetProjects_ShouldMatchTechFilterViaSkillAliases()
    {
        // Arrange
        SetupAnonymousUser();

        var csharp = new Skill
        {
            Id = Guid.NewGuid(),
            Name = "C#",
            Category = "Backend",
            Description = "C# language",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Skills.Add(csharp);
        _dbContext.SkillAliases.Add(new SkillAlias
        {
            Id = Guid.NewGuid(),
            SkillId = csharp.Id,
            Alias = "csharp",
            AliasNormalized = SkillNormalization.NormalizeSkillToken("csharp"),
            CreatedAt = DateTime.UtcNow
        });

        _dbContext.Projects.AddRange(
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "C# project",
                Description = "Description",
                OwnerId = Guid.NewGuid(),
                Status = "recruiting",
                Visibility = "public",
                TechStack = new List<string> { "C#" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Rust project",
                Description = "Description",
                OwnerId = Guid.NewGuid(),
                Status = "recruiting",
                Visibility = "public",
                TechStack = new List<string> { "Rust" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await _dbContext.SaveChangesAsync();

        // Act - search by alias "csharp" should find "C#" project
        var result = await _controller.GetProjects(new ProjectQueryParams
        {
            Tech = "csharp",
            Page = 1,
            PageSize = 50
        });

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);

        var titles = doc.RootElement.GetProperty("Data")
            .EnumerateArray()
            .Select(e => e.GetProperty("Title").GetString())
            .ToList();

        titles.Should().Contain("C# project");
        titles.Should().NotContain("Rust project");
    }

    [Fact]
    public async Task GetProjects_ShouldFilterByStatus()
    {
        // Arrange
        _dbContext.Projects.AddRange(
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Recruiting Project",
                Description = "Description",
                OwnerId = _testUserId,
                Status = "recruiting",
                Visibility = "public",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Active Project",
                Description = "Description",
                OwnerId = _testUserId,
                Status = "active",
                Visibility = "public",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetProjects(new ProjectQueryParams
        {
            Status = "recruiting",
            Page = 1,
            PageSize = 50
        });

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);

        var titles = doc.RootElement.GetProperty("Data")
            .EnumerateArray()
            .Select(e => e.GetProperty("Title").GetString())
            .ToList();

        titles.Should().Contain("Recruiting Project");
        titles.Should().NotContain("Active Project");
    }

    [Fact]
    public async Task GetProjects_ShouldFilterByTextQuery()
    {
        // Arrange
        _dbContext.Projects.AddRange(
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "DevHunt Platform",
                Description = "A platform for developers",
                OwnerId = _testUserId,
                Status = "recruiting",
                Visibility = "public",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Weather App",
                Description = "Mobile weather application",
                OwnerId = _testUserId,
                Status = "recruiting",
                Visibility = "public",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetProjects(new ProjectQueryParams
        {
            Query = "DevHunt",
            Page = 1,
            PageSize = 50
        });

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);

        var titles = doc.RootElement.GetProperty("Data")
            .EnumerateArray()
            .Select(e => e.GetProperty("Title").GetString())
            .ToList();

        titles.Should().Contain("DevHunt Platform");
        titles.Should().NotContain("Weather App");
    }

    #endregion

    #region DeleteProject Tests

    [Fact]
    public async Task DeleteProject_ShouldPermanentlyDelete_WhenOwner()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Project to Delete",
            Description = "Description",
            OwnerId = _testUserId,
            Status = "draft",
            Visibility = "public",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteProject(project.Id, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        // Project should be permanently deleted.
        // ExecuteDeleteAsync bypasses the change tracker, so FindAsync would return the
        // stale tracked instance from Arrange instead of re-querying — use AsNoTracking
        // to force a real read of the store.
        var deletedProject = await _dbContext.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == project.Id);
        deletedProject.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProject_ShouldReturnForbid_WhenNotOwner()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Other's Project",
            Description = "Description",
            OwnerId = Guid.NewGuid(), // Different user
            Status = "draft",
            Visibility = "public",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteProject(project.Id, default);

        // Assert - Controller returns ForbidResult for non-owner
        result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}

