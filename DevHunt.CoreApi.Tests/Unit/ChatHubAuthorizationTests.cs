using Xunit;
using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using DevHunt.CoreApi.Hubs;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevHunt.CoreApi.Tests.Unit;

/// <summary>
/// Authorization tests for the SignalR ChatHub.
/// Verify that users cannot join conversations or projects they are not authorized for.
/// </summary>
public class ChatHubAuthorizationTests : IDisposable
{
    private readonly Mock<ILogger<ChatHub>> _loggerMock;
    private readonly DevHuntDbContext _dbContext;
    private readonly ChatHub _chatHub;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly Mock<IGroupManager> _groupsMock;

    public ChatHubAuthorizationTests()
    {
        _loggerMock = new Mock<ILogger<ChatHub>>();
        
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new DevHuntDbContext(options);
        
        _chatHub = new ChatHub(_loggerMock.Object, _dbContext);
        
        // Setup SignalR mocks
        _clientProxyMock = new Mock<IClientProxy>();
        _clientsMock = new Mock<IHubCallerClients>();
        _contextMock = new Mock<HubCallerContext>();
        _groupsMock = new Mock<IGroupManager>();
        
        _clientsMock.Setup(c => c.Caller).Returns(_clientProxyMock.Object);
        
        // Use reflection to set private fields
        var hubContextType = typeof(ChatHub).BaseType!;
        var clientsField = hubContextType.GetField("_clients", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var contextField = hubContextType.GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var groupsField = hubContextType.GetField("_groups", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        clientsField?.SetValue(_chatHub, _clientsMock.Object);
        contextField?.SetValue(_chatHub, _contextMock.Object);
        groupsField?.SetValue(_chatHub, _groupsMock.Object);
    }

    [Fact]
    public async Task JoinConversation_WhenUserIsNotParticipant_ShouldReject()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        
        // Create conversation with different user
        var conversation = new Conversation
        {
            Id = conversationId,
            Type = ConversationType.Direct,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Conversations.Add(conversation);
        
        var participant = new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            UserId = otherUserId,
            JoinedAt = DateTime.UtcNow
        };
        _dbContext.ConversationParticipants.Add(participant);
        await _dbContext.SaveChangesAsync();
        
        // Setup user context
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _contextMock.Setup(c => c.User).Returns(principal);
        
        // Act
        await _chatHub.JoinConversation(conversationId);
        
        // Assert
        _clientProxyMock.Verify(
            c => c.SendCoreAsync("Error", 
                It.Is<object[]>(args => args[0].ToString()!.Contains("Forbidden")),
                default),
            Times.Once);
        _groupsMock.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task JoinConversation_WhenUserIsParticipant_ShouldAllow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        
        // Create conversation with user as participant
        var conversation = new Conversation
        {
            Id = conversationId,
            Type = ConversationType.Direct,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Conversations.Add(conversation);
        
        var participant = new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        };
        _dbContext.ConversationParticipants.Add(participant);
        await _dbContext.SaveChangesAsync();
        
        // Setup user context
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _contextMock.Setup(c => c.User).Returns(principal);
        _contextMock.Setup(c => c.ConnectionId).Returns("test-connection-id");
        
        // Act
        await _chatHub.JoinConversation(conversationId);
        
        // Assert
        _groupsMock.Verify(
            g => g.AddToGroupAsync("test-connection-id", $"conversation:{conversationId}", default),
            Times.Once);
        _clientProxyMock.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Never);
    }

    [Fact]
    public async Task JoinProject_WhenUserIsNotMember_ShouldReject()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        
        // Create project with different owner
        var project = new Project
        {
            Id = projectId,
            OwnerId = ownerId,
            Title = "Test Project",
            Description = "Test",
            Status = "active",
            Visibility = "public",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();
        
        // Setup user context
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _contextMock.Setup(c => c.User).Returns(principal);
        
        // Act
        await _chatHub.JoinProject(projectId);
        
        // Assert
        _clientProxyMock.Verify(
            c => c.SendCoreAsync("Error",
                It.Is<object[]>(args => args[0].ToString()!.Contains("Forbidden")),
                default),
            Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

