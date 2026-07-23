using System.Security.Claims;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Hubs;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using static DevHunt.CoreApi.Tests.Unit.ChatControllerTests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DevHunt.CoreApi.Tests.Unit;

public class ChatControllerTests : IDisposable
{
    private readonly DevHuntDbContext _context;
    private readonly ChatController _controller;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly Mock<IHubContext<ChatHub>> _chatHubMock;
    private readonly Mock<IEventBusService> _eventBusMock;

    public ChatControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DevHuntDbContext(options);
        _auditServiceMock = new Mock<IAuditService>();
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _chatHubMock = new Mock<IHubContext<ChatHub>>();
        _eventBusMock = new Mock<IEventBusService>();

        _encryptionServiceMock.Setup(e => e.Encrypt(It.IsAny<string>()))
            .Returns<string>(s => $"encrypted_{s}");
        _encryptionServiceMock.Setup(e => e.Decrypt(It.IsAny<string>()))
            .Returns<string>(s => s.Replace("encrypted_", ""));

        // Setup SignalR Hub mocks for SendMessage
        var clientsProxyMock = new Mock<IHubClients>();
        _groupProxyMock = new Mock<IClientProxy>();
        var groupsMock = new Mock<IGroupManager>();
        
        // Setup chain: Clients -> Group() -> SendAsync()
        clientsProxyMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupProxyMock.Object);
        _groupProxyMock.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
            .Returns(Task.CompletedTask);
        
        _chatHubMock.Setup(h => h.Clients).Returns(clientsProxyMock.Object);
        _chatHubMock.Setup(h => h.Groups).Returns(groupsMock.Object);

        _controller = new ChatController(
            _context,
            _auditServiceMock.Object,
            _encryptionServiceMock.Object,
            _chatHubMock.Object,
            _eventBusMock.Object);

        // Setup user claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    [Fact]
    public async Task CreateDirectConversation_ShouldReturnConversationId()
    {
        // Arrange
        var userId = Guid.Parse(_controller.ControllerContext.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var otherUserId = Guid.NewGuid();

        var user1 = new User { Id = userId, Email = "user1@test.com", PasswordHash = "hash", Role = "participant" };
        var user2 = new User { Id = otherUserId, Email = "user2@test.com", PasswordHash = "hash", Role = "participant" };
        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        // Act - use existing method GetOrCreateDirectConversation
        var result = await _controller.GetOrCreateDirectConversation(otherUserId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();

        var conversation = await _context.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync();
        conversation.Should().NotBeNull();
        conversation!.Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendMessage_ShouldEncryptContent()
    {
        // Arrange
        var userId = Guid.Parse(_controller.ControllerContext.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var otherUserId = Guid.NewGuid();

        var user1 = new User { Id = userId, Email = "user1@test.com", PasswordHash = "hash", Role = "participant" };
        var user2 = new User { Id = otherUserId, Email = "user2@test.com", PasswordHash = "hash", Role = "participant" };
        _context.Users.AddRange(user1, user2);

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Conversations.Add(conversation);

        _context.ConversationParticipants.AddRange(
            new ConversationParticipant { Id = Guid.NewGuid(), ConversationId = conversation.Id, UserId = userId, JoinedAt = DateTime.UtcNow },
            new ConversationParticipant { Id = Guid.NewGuid(), ConversationId = conversation.Id, UserId = otherUserId, JoinedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        var messageContent = "Test message";
        // Use the correct DTO from ChatController
        var dto = new DevHunt.CoreApi.Controllers.SendMessageDto { Content = messageContent };

        // Act
        var result = await _controller.SendMessage(conversation.Id, dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var message = await _context.Messages.FirstOrDefaultAsync();
        message.Should().NotBeNull();
        message!.Content.Should().Contain("encrypted_"); // Verify that content was encrypted
        _encryptionServiceMock.Verify(e => e.Encrypt(messageContent), Times.Once);
        
        // Verify that SignalR was called
        groupProxyMock.Verify(
            p => p.SendAsync("MessageReceived", It.IsAny<object[]>(), default), 
            Times.Once);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}


