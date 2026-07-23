using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevHunt.CoreApi.Tests.Services;

public class ProjectPermissionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DevHuntTestDbContext _db;
    private readonly ProjectPermissionService _permissions;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _outsiderId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public ProjectPermissionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new DevHuntTestDbContext(options);
        _db.Database.EnsureCreated();
        _permissions = new ProjectPermissionService(_db, new ProjectAuthorizationPolicy(_db));
        SeedPrivateDraftProject();
    }

    [Fact]
    public async Task GetPermissionsAsync_outsider_cannot_view_private_draft()
    {
        var perms = await _permissions.GetPermissionsAsync(_projectId, _outsiderId, isAdmin: false);

        perms.Should().NotBeNull();
        perms!.CanView.Should().BeFalse();
    }

    [Fact]
    public async Task GetPermissionsAsync_owner_can_view_private_draft()
    {
        var perms = await _permissions.GetPermissionsAsync(_projectId, _ownerId, isAdmin: false);

        perms.Should().NotBeNull();
        perms!.CanView.Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void SeedPrivateDraftProject()
    {
        _db.Users.AddRange(
            new User { Id = _ownerId, Email = "o@test.dev", PasswordHash = "h", Role = "participant" },
            new User { Id = _outsiderId, Email = "x@test.dev", PasswordHash = "h", Role = "participant" });

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Title = "Hidden",
            Description = "d",
            OwnerId = _ownerId,
            Status = ProjectStatus.Draft.Value,
            Visibility = ProjectVisibility.Private.Value,
            TechStack = [],
        });

        _db.SaveChanges();
    }
}
