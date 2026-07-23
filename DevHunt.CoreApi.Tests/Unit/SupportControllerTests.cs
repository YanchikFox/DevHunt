using Xunit;
using Microsoft.AspNetCore.Mvc;
using DevHunt.CoreApi.Controllers;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;

namespace DevHunt.CoreApi.Tests.Unit;

/// <summary>
/// Unit tests for SupportController (ARCH-008: Improve Test Coverage to 70%)
/// </summary>
public class SupportControllerTests : IDisposable
{
    private readonly DevHuntDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly SupportController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testAdminId;
    private readonly Guid _testProjectId;

    public SupportControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DevHuntDbContext(options);
        _auditServiceMock = new Mock<IAuditService>();
        _controller = new SupportController(_context, _auditServiceMock.Object);
        
        _testUserId = Guid.NewGuid();
        _testAdminId = Guid.NewGuid();
        _testProjectId = Guid.NewGuid();
        
        // Setup test users
        _context.Users.AddRange(new[]
        {
            new User
            {
                Id = _testUserId,
                Email = "user@example.com",
                PasswordHash = "hash",
                Role = "participant",
                IsActive = true,
                FullName = "Test User"
            },
            new User
            {
                Id = _testAdminId,
                Email = "admin@example.com",
                PasswordHash = "hash",
                Role = "admin",
                IsActive = true,
                FullName = "Admin User"
            }
        });
        
        // Setup test project
        _context.Projects.Add(new Project
        {
            Id = _testProjectId,
            Title = "Test Project",
            OwnerId = _testUserId,
            Status = "active"
        });
        
        _context.SaveChanges();
    }

    private void SetUserContext(Guid userId, string role = "participant")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task CreateTicket_ShouldCreateTicket_WhenValidRequest()
    {
        // Arrange
        SetUserContext(_testUserId);
        var request = new SupportController.CreateTicketRequest(
            "question",
            "Test Subject",
            "Test Description",
            null,
            null
        );

        // Act
        var result = await _controller.CreateTicket(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(createdResult.Value);
        
        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.UserId == _testUserId);
        Assert.NotNull(ticket);
        Assert.Equal("question", ticket.Category);
        Assert.Equal("Test Subject", ticket.Subject);
        Assert.Equal("open", ticket.Status);
    }

    [Fact]
    public async Task CreateTicket_ShouldReturnBadRequest_WhenInvalidCategory()
    {
        // Arrange
        SetUserContext(_testUserId);
        var request = new SupportController.CreateTicketRequest(
            "invalid_category",
            "Test Subject",
            "Test Description",
            null,
            null
        );

        // Act
        var result = await _controller.CreateTicket(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid category", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task CreateTicket_ShouldReturnUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };
        var request = new SupportController.CreateTicketRequest(
            "question",
            "Test Subject",
            "Test Description",
            null,
            null
        );

        // Act
        var result = await _controller.CreateTicket(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetMyTickets_ShouldReturnUserTickets()
    {
        // Arrange
        SetUserContext(_testUserId);
        
        _context.SupportTickets.AddRange(new[]
        {
            new SupportTicket
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                Category = "question",
                Subject = "Ticket 1",
                Description = "Desc 1",
                Status = "open",
                CreatedAt = DateTime.UtcNow
            },
            new SupportTicket
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                Category = "bug",
                Subject = "Ticket 2",
                Description = "Desc 2",
                Status = "resolved",
                CreatedAt = DateTime.UtcNow
            }
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMyTickets();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var tickets = okResult.Value as dynamic;
        Assert.NotNull(tickets);
    }

    [Fact]
    public async Task GetTicket_ShouldReturnTicket_WhenUserOwnsTicket()
    {
        // Arrange
        SetUserContext(_testUserId);
        var ticketId = Guid.NewGuid();
        _context.SupportTickets.Add(new SupportTicket
        {
            Id = ticketId,
            UserId = _testUserId,
            Category = "question",
            Subject = "Test Ticket",
            Description = "Test Description",
            Status = "open",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetTicket(ticketId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetTicket_ShouldReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        SetUserContext(_testUserId);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _controller.GetTicket(nonExistentId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AddMessage_ShouldAddMessage_WhenValidRequest()
    {
        // Arrange
        SetUserContext(_testUserId);
        var ticketId = Guid.NewGuid();
        _context.SupportTickets.Add(new SupportTicket
        {
            Id = ticketId,
            UserId = _testUserId,
            Category = "question",
            Subject = "Test Ticket",
            Description = "Test Description",
            Status = "open",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var request = new SupportController.AddMessageRequest("Test message content");

        // Act
        var result = await _controller.AddMessage(ticketId, request);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result);
        
        var message = await _context.TicketMessages.FirstOrDefaultAsync(m => m.TicketId == ticketId);
        Assert.NotNull(message);
        Assert.Equal("Test message content", message.Content);
        Assert.Equal(_testUserId, message.AuthorId);
    }

    [Fact]
    public async Task CloseTicket_ShouldCloseTicket_WhenUserOwnsTicket()
    {
        // Arrange
        SetUserContext(_testUserId);
        var ticketId = Guid.NewGuid();
        _context.SupportTickets.Add(new SupportTicket
        {
            Id = ticketId,
            UserId = _testUserId,
            Category = "question",
            Subject = "Test Ticket",
            Description = "Test Description",
            Status = "open",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.CloseTicket(ticketId);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        var ticket = await _context.SupportTickets.FindAsync(ticketId);
        Assert.NotNull(ticket);
        Assert.Equal("closed", ticket.Status);
        Assert.NotNull(ticket.ClosedAt);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

