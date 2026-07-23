using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.CoreApi.Services.Tasks;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevHunt.CoreApi.Tests.Services;

public class TaskAuthorizationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DevHuntTestDbContext _db;
    private readonly TaskAuthorizationService _service;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _activeMemberId = Guid.NewGuid();
    private readonly Guid _leftMemberId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public TaskAuthorizationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new DevHuntTestDbContext(options);
        _db.Database.EnsureCreated();
        var policy = new ProjectAuthorizationPolicy(_db);
        _service = new TaskAuthorizationService(_db, policy);
        SeedData();
    }

    [Fact]
    public async Task CanViewTasksAsync_denies_left_member()
    {
        var result = await _service.CanViewTasksAsync(_projectId, _leftMemberId);

        result.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task CanViewTasksAsync_allows_active_member()
    {
        var result = await _service.CanViewTasksAsync(_projectId, _activeMemberId);

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task IsValidAssigneeAsync_rejects_left_member()
    {
        var valid = await _service.IsValidAssigneeAsync(_projectId, _leftMemberId);

        valid.Should().BeFalse();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void SeedData()
    {
        _db.Users.AddRange(
            new User { Id = _ownerId, Email = "o@test.dev", PasswordHash = "h", Role = "participant" },
            new User { Id = _activeMemberId, Email = "a@test.dev", PasswordHash = "h", Role = "participant" },
            new User { Id = _leftMemberId, Email = "l@test.dev", PasswordHash = "h", Role = "participant" });

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Title = "Tasks",
            Description = "d",
            OwnerId = _ownerId,
            Status = ProjectStatus.Active.Value,
            Visibility = ProjectVisibility.Private.Value,
            TechStack = [],
        });

        _db.TeamMembers.AddRange(
            new TeamMember
            {
                Id = Guid.NewGuid(),
                ProjectId = _projectId,
                UserId = _activeMemberId,
                Role = "dev",
                Status = TeamMemberStatus.Active.Value,
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                ProjectId = _projectId,
                UserId = _leftMemberId,
                Role = "dev",
                Status = TeamMemberStatus.Left.Value,
                LeftAt = DateTime.UtcNow,
            });

        _db.SaveChanges();
    }
}
