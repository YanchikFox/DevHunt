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
/// Unit tests for CommunityController (ARCH-008: Improve Test Coverage to 70%)
/// </summary>
public class CommunityControllerTests : IDisposable
{
    private readonly DevHuntDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly CommunityController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testAdminId;

    public CommunityControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DevHuntDbContext(options);
        _auditServiceMock = new Mock<IAuditService>();
        _controller = new CommunityController(_context, _auditServiceMock.Object);
        
        _testUserId = Guid.NewGuid();
        _testAdminId = Guid.NewGuid();
        
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

    private void SetAnonymousContext()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };
    }

    [Fact]
    public async Task CreateFeedback_ShouldCreateFeedback_WhenValidRequest()
    {
        // Arrange
        SetUserContext(_testUserId);
        var request = new CommunityController.CreateFeedbackRequest(
            "bug",
            "Test Bug",
            "Test Description",
            null
        );

        // Act
        var result = await _controller.CreateFeedback(request);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result);
        
        var feedback = await _context.FeedbackItems.FirstOrDefaultAsync(f => f.AuthorId == _testUserId);
        Assert.NotNull(feedback);
        Assert.Equal("bug", feedback.Type);
        Assert.Equal("Test Bug", feedback.Title);
        Assert.Equal("open", feedback.Status);
    }

    [Fact]
    public async Task CreateFeedback_ShouldReturnBadRequest_WhenInvalidType()
    {
        // Arrange
        SetUserContext(_testUserId);
        var request = new CommunityController.CreateFeedbackRequest(
            "invalid_type",
            "Test Title",
            "Test Description",
            null
        );

        // Act
        var result = await _controller.CreateFeedback(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid type", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task CreateFeedback_ShouldReturnUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        SetAnonymousContext();
        var request = new CommunityController.CreateFeedbackRequest(
            "bug",
            "Test Title",
            "Test Description",
            null
        );

        // Act
        var result = await _controller.CreateFeedback(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetFeedback_ShouldReturnFeedbackList_WhenAnonymous()
    {
        // Arrange
        SetAnonymousContext();
        
        _context.FeedbackItems.AddRange(new[]
        {
            new FeedbackItem
            {
                Id = Guid.NewGuid(),
                AuthorId = _testUserId,
                Type = "bug",
                Title = "Bug 1",
                Description = "Desc 1",
                Status = "open",
                VoteCount = 5,
                CreatedAt = DateTime.UtcNow
            },
            new FeedbackItem
            {
                Id = Guid.NewGuid(),
                AuthorId = _testUserId,
                Type = "suggestion",
                Title = "Suggestion 1",
                Description = "Desc 2",
                Status = "open",
                VoteCount = 3,
                CreatedAt = DateTime.UtcNow
            }
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetFeedback();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task VoteFeedback_ShouldAddVote_WhenValidRequest()
    {
        // Arrange
        SetUserContext(_testUserId);
        var feedbackId = Guid.NewGuid();
        _context.FeedbackItems.Add(new FeedbackItem
        {
            Id = feedbackId,
            AuthorId = _testUserId,
            Type = "bug",
            Title = "Test Bug",
            Description = "Test Description",
            Status = "open",
            VoteCount = 0,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.VoteFeedback(feedbackId, true);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        var feedback = await _context.FeedbackItems.FindAsync(feedbackId);
        Assert.NotNull(feedback);
        Assert.Equal(1, feedback.VoteCount);
        
        var vote = await _context.FeedbackVotes.FirstOrDefaultAsync(v => v.FeedbackId == feedbackId && v.UserId == _testUserId);
        Assert.NotNull(vote);
    }

    [Fact]
    public async Task VoteFeedback_ShouldRemoveVote_WhenDoubleClick()
    {
        // Arrange
        SetUserContext(_testUserId);
        var feedbackId = Guid.NewGuid();
        _context.FeedbackItems.Add(new FeedbackItem
        {
            Id = feedbackId,
            AuthorId = _testUserId,
            Type = "bug",
            Title = "Test Bug",
            Description = "Test Description",
            Status = "open",
            VoteCount = 1,
            CreatedAt = DateTime.UtcNow
        });
        _context.FeedbackVotes.Add(new FeedbackVote
        {
            Id = Guid.NewGuid(),
            FeedbackId = feedbackId,
            UserId = _testUserId,
            IsUpvote = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.VoteFeedback(feedbackId, true);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        var feedback = await _context.FeedbackItems.FindAsync(feedbackId);
        Assert.NotNull(feedback);
        Assert.Equal(0, feedback.VoteCount);
    }

    [Fact]
    public async Task AddComment_ShouldAddComment_WhenValidRequest()
    {
        // Arrange
        SetUserContext(_testUserId);
        var feedbackId = Guid.NewGuid();
        _context.FeedbackItems.Add(new FeedbackItem
        {
            Id = feedbackId,
            AuthorId = _testUserId,
            Type = "bug",
            Title = "Test Bug",
            Description = "Test Description",
            Status = "open",
            CommentCount = 0,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var request = new CommunityController.AddCommentRequest("Test comment");

        // Act
        var result = await _controller.AddComment(feedbackId, request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        var feedback = await _context.FeedbackItems.FindAsync(feedbackId);
        Assert.NotNull(feedback);
        Assert.Equal(1, feedback.CommentCount);
        
        var comment = await _context.FeedbackComments.FirstOrDefaultAsync(c => c.FeedbackId == feedbackId);
        Assert.NotNull(comment);
        Assert.Equal("Test comment", comment.Content);
    }

    [Fact]
    public async Task UpdateFeedbackStatus_ShouldUpdateStatus_WhenAdmin()
    {
        // Arrange
        SetUserContext(_testAdminId, "admin");
        var feedbackId = Guid.NewGuid();
        _context.FeedbackItems.Add(new FeedbackItem
        {
            Id = feedbackId,
            AuthorId = _testUserId,
            Type = "bug",
            Title = "Test Bug",
            Description = "Test Description",
            Status = "open",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var request = new CommunityController.UpdateFeedbackStatusRequest("planned", "high", null);

        // Act
        var result = await _controller.UpdateFeedbackStatus(feedbackId, request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        var feedback = await _context.FeedbackItems.FindAsync(feedbackId);
        Assert.NotNull(feedback);
        Assert.Equal("planned", feedback.Status);
        Assert.Equal("high", feedback.Priority);
    }

    [Fact]
    public async Task UpdateFeedbackStatus_ShouldReturnForbid_WhenNotAdmin()
    {
        // Arrange
        SetUserContext(_testUserId, "participant");
        var feedbackId = Guid.NewGuid();
        _context.FeedbackItems.Add(new FeedbackItem
        {
            Id = feedbackId,
            AuthorId = _testUserId,
            Type = "bug",
            Title = "Test Bug",
            Description = "Test Description",
            Status = "open",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var request = new CommunityController.UpdateFeedbackStatusRequest("planned", null, null);

        // Act
        var result = await _controller.UpdateFeedbackStatus(feedbackId, request);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

