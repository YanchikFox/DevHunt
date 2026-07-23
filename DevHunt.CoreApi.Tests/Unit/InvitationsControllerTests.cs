using Xunit;
using Microsoft.AspNetCore.Mvc;
using DevHunt.CoreApi.Controllers;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Services;
using Moq;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Tests.Unit;

/// <summary>
/// Unit tests for InvitationsController (ARCH-008: Improve Test Coverage to 70%)
/// </summary>
public class InvitationsControllerTests : IDisposable
{
    private readonly DevHuntDbContext _context;
    private readonly InvitationsController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testProjectId;

    public InvitationsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DevHuntDbContext(options);
        var eventBusMock = new Mock<IEventBusService>();
        _controller = new InvitationsController(
            _context,
            eventBusMock.Object);
        
        _testUserId = Guid.NewGuid();
        _testProjectId = Guid.NewGuid();
        
        _context.Users.Add(new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true
        });
        
        _context.Projects.Add(new Project
        {
            Id = _testProjectId,
            Title = "Test Project",
            OwnerId = _testUserId,
            Status = "active",
            Visibility = "public"
        });
        
        _context.SaveChanges();
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()),
            new Claim(ClaimTypes.Role, "participant")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    [Fact]
    public async Task GetInvitations_ReturnsListOfInvitations()
    {
        // Arrange
        var inviteeId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = inviteeId,
            Email = "invitee@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true
        });
        
        var invitationId = Guid.NewGuid();
        _context.Invitations.Add(new Invitation
        {
            Id = invitationId,
            ProjectId = _testProjectId,
            InviterId = _testUserId,
            InviteeId = inviteeId,
            Status = "pending",
            Role = "developer"
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetInvitations(_testProjectId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var invitations = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);
        Assert.NotEmpty(invitations);
    }

    [Fact]
    public async Task AcceptInvitation_WithValidId_UpdatesStatus()
    {
        // Arrange
        var inviteeId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = inviteeId,
            Email = "invitee@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true
        });
        
        var invitationId = Guid.NewGuid();
        _context.Invitations.Add(new Invitation
        {
            Id = invitationId,
            ProjectId = _testProjectId,
            InviterId = _testUserId,
            InviteeId = inviteeId,
            Status = "pending",
            Role = "developer"
        });
        await _context.SaveChangesAsync();

        // Act - Switch context to invitee
        var inviteeClaims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, inviteeId.ToString()),
        };
        var inviteeIdentity = new ClaimsIdentity(inviteeClaims, "Test");
        var inviteePrincipal = new ClaimsPrincipal(inviteeIdentity);
        _controller.ControllerContext.HttpContext.User = inviteePrincipal;

        var result = await _controller.AcceptInvitation(_testProjectId, invitationId);

        // Assert
        Assert.IsType<OkResult>(result);
        
        var invitation = await _context.Invitations.FindAsync(invitationId);
        Assert.Equal("accepted", invitation?.Status);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

