using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace DevHunt.CoreApi.Tests.Controllers;

/// <summary>
/// Unit tests for InvitationsController
/// </summary>
public class InvitationsControllerTests : IDisposable
{
    private readonly DevHuntDbContext _dbContext;
    private readonly Mock<IEventBusService> _eventBusMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly InvitationsController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testProjectId = Guid.NewGuid();
    private readonly Guid _testInviteeId = Guid.NewGuid();

    public InvitationsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DevHuntTestDbContext(options);

        _eventBusMock = new Mock<IEventBusService>();
        _cacheMock = new Mock<ICacheService>();

        _eventBusMock
            .Setup(x => x.PublishAsync(It.IsAny<DomainEvent>()))
            .Returns(Task.CompletedTask);

        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _controller = new InvitationsController(
            _dbContext, 
            _eventBusMock.Object,
            _cacheMock.Object
        );

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
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };
    }

    [Fact]
    public async Task Send_ShouldCreateInvitation_WhenValidRequest()
    {
        // Arrange
        var project = new Project
        {
            Id = _testProjectId,
            Title = "Test Project",
            Description = "Description",
            OwnerId = _testUserId,
            Status = "recruiting",
            Visibility = "public",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);

        var invitee = new User
        {
            Id = _testInviteeId,
            Email = "invitee@example.com",
            PasswordHash = "hash",
            IsActive = true
        };
        _dbContext.Users.Add(invitee);
        await _dbContext.SaveChangesAsync();

        var request = new InvitationsController.InviteRequest(
            ProjectId: _testProjectId,
            UserId: _testInviteeId,
            Username: null,
            Role: "Developer",
            Type: "invite",
            Message: "Join our project!"
        );

        // Act
        var result = await _controller.Send(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        // Verify saving to DB
        var invitation = await _dbContext.Invitations
            .FirstOrDefaultAsync(i => i.ProjectId == _testProjectId && i.InviteeId == _testInviteeId);
        invitation.Should().NotBeNull();
        invitation!.Status.Should().Be("pending");
        invitation.Role.Should().Be("Developer");

        // Verify Event Bus was called
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<DomainEvent>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Respond_ShouldAcceptInvitation_WhenValidRequest()
    {
        // Arrange
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            InviterId = Guid.NewGuid(),
            InviteeId = _testUserId,
            Status = "pending",
            Role = "Developer",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        var request = new InvitationsController.RespondRequest(invitation.Id, "accept");

        // Act
        var result = await _controller.Respond(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        // Verify DB update
        var dbInvitation = await _dbContext.Invitations.FindAsync(invitation.Id);
        dbInvitation!.Status.Should().Be("accepted");
        dbInvitation.RespondedAt.Should().NotBeNull();

        // Verify Event Bus was called
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<DomainEvent>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Respond_ShouldDeclineInvitation_WhenValidRequest()
    {
        // Arrange
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            InviterId = Guid.NewGuid(),
            InviteeId = _testUserId,
            Status = "pending",
            Role = "Developer",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        var request = new InvitationsController.RespondRequest(invitation.Id, "decline");

        // Act
        var result = await _controller.Respond(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        // Verify DB update
        var dbInvitation = await _dbContext.Invitations.FindAsync(invitation.Id);
        dbInvitation!.Status.Should().Be("declined");
        dbInvitation.RespondedAt.Should().NotBeNull();

        // Verify Event Bus was called
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<DomainEvent>()),
            Times.Once
        );
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

