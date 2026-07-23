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

public class ProjectAuthorizationPolicyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DevHuntTestDbContext _db;
    private readonly ProjectAuthorizationPolicy _policy;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _memberId = Guid.NewGuid();
    private readonly Guid _outsiderId = Guid.NewGuid();
    private readonly Guid _leftMemberId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public ProjectAuthorizationPolicyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new DevHuntTestDbContext(options);
        _db.Database.EnsureCreated();
        _policy = new ProjectAuthorizationPolicy(_db);
        SeedProjectGraph();
    }

    [Fact]
    public async Task GetActiveTeamMemberAsync_returns_null_for_left_member()
    {
        var member = await _policy.GetActiveTeamMemberAsync(_projectId, _leftMemberId);

        member.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveTeamMemberAsync_returns_row_for_active_member()
    {
        var member = await _policy.GetActiveTeamMemberAsync(_projectId, _memberId);

        member.Should().NotBeNull();
        member!.Status.Should().Be(TeamMemberStatus.Active.Value);
    }

    [Fact]
    public async Task IsOwnerOrActiveMemberAsync_denies_outsider()
    {
        var project = await _db.Projects.AsNoTracking().FirstAsync(p => p.Id == _projectId);

        var allowed = await _policy.IsOwnerOrActiveMemberAsync(project, _outsiderId);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void IsPublicNonDraft_false_for_private_project()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Private",
            Description = "d",
            OwnerId = _ownerId,
            Status = ProjectStatus.Active.Value,
            Visibility = ProjectVisibility.Private.Value,
            TechStack = [],
        };

        _policy.IsPublicNonDraft(project).Should().BeFalse();
    }

    [Fact]
    public void IsPublicNonDraft_false_for_public_draft()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Draft",
            Description = "d",
            OwnerId = _ownerId,
            Status = ProjectStatus.Draft.Value,
            Visibility = ProjectVisibility.Public.Value,
            TechStack = [],
        };

        _policy.IsPublicNonDraft(project).Should().BeFalse();
    }

    [Fact]
    public void IsPublicNonDraft_true_for_public_recruiting()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Open",
            Description = "d",
            OwnerId = _ownerId,
            Status = ProjectStatus.Recruiting.Value,
            Visibility = ProjectVisibility.Public.Value,
            TechStack = [],
        };

        _policy.IsPublicNonDraft(project).Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void SeedProjectGraph()
    {
        _db.Users.AddRange(
            new User { Id = _ownerId, Email = "owner@test.dev", PasswordHash = "h", Role = "participant" },
            new User { Id = _memberId, Email = "member@test.dev", PasswordHash = "h", Role = "participant" },
            new User { Id = _outsiderId, Email = "out@test.dev", PasswordHash = "h", Role = "participant" },
            new User { Id = _leftMemberId, Email = "left@test.dev", PasswordHash = "h", Role = "participant" });

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Title = "Auth test",
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
                UserId = _memberId,
                Role = "developer",
                Status = TeamMemberStatus.Active.Value,
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                ProjectId = _projectId,
                UserId = _leftMemberId,
                Role = "developer",
                Status = TeamMemberStatus.Left.Value,
                LeftAt = DateTime.UtcNow,
            });

        _db.SaveChanges();
    }
}
