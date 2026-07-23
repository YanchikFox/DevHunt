using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using DevHunt.CoreApi.Services;
using System.Collections.Generic;

namespace DevHunt.CoreApi.Tests.Services;

/// <summary>
/// Unit tests for EventBusService
/// </summary>
public class EventBusServiceTests
{
    private readonly Mock<ILogger<RabbitMQEventBusService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;

    public EventBusServiceTests()
    {
        _loggerMock = new Mock<ILogger<RabbitMQEventBusService>>();
        _configurationMock = new Mock<IConfiguration>();
    }

    [Fact]
    public void PublishAsync_ShouldNotThrow_WhenRabbitMQNotConfigured()
    {
        // Arrange
        _configurationMock.Setup(x => x["RabbitMQ:ConnectionString"])
            .Returns((string?)null);

        var service = new RabbitMQEventBusService(_loggerMock.Object, _configurationMock.Object);
        var domainEvent = DomainEvents.ProjectCreated(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act = async () => await service.PublishAsync(domainEvent);
        act.Should().NotThrowAsync();
    }

    [Fact]
    public void DomainEvents_ProjectCreated_ShouldCreateCorrectEvent()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        // Act
        var domainEvent = DomainEvents.ProjectCreated(projectId, ownerId);

        // Assert
        domainEvent.Should().NotBeNull();
        domainEvent.EventType.Should().Be("project.created");
        domainEvent.EntityType.Should().Be("Project");
        domainEvent.EntityId.Should().Be(projectId);
    }

    [Fact]
    public void DomainEvents_ProjectUpdated_ShouldCreateCorrectEvent()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var domainEvent = DomainEvents.ProjectUpdated(projectId, userId);

        // Assert
        domainEvent.Should().NotBeNull();
        domainEvent.EventType.Should().Be("project.updated");
        domainEvent.EntityType.Should().Be("Project");
        domainEvent.EntityId.Should().Be(projectId);
    }

    [Fact]
    public void DomainEvents_ProfileUpdated_ShouldCreateCorrectEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var domainEvent = DomainEvents.ProfileUpdated(userId);

        // Assert
        domainEvent.Should().NotBeNull();
        domainEvent.EventType.Should().Be("profile.updated");
        domainEvent.EntityType.Should().Be("User");
        domainEvent.EntityId.Should().Be(userId);
    }

    [Fact]
    public void DomainEvents_TeamMemberJoined_ShouldCreateCorrectEvent()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var role = "Developer";

        // Act
        var domainEvent = DomainEvents.TeamMemberJoined(projectId, userId, role);

        // Assert
        domainEvent.Should().NotBeNull();
        domainEvent.EventType.Should().Be("team.member.joined");
        domainEvent.EntityType.Should().Be("TeamMember");
        domainEvent.EntityId.Should().Be(projectId);
    }
}

