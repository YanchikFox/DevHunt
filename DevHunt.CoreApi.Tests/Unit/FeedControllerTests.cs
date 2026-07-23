using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Tests.Unit;

public class FeedControllerTests : IDisposable
{
    private readonly DevHuntDbContext _context;
    private readonly FeedController _controller;
    private readonly Guid _viewerId = Guid.NewGuid();

    public FeedControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new DevHuntDbContext(options);
        _controller = new FeedController(
            _context,
            new NoOpAuditService(),
            new NoOpNotificationServiceClient(),
            new NoOpEventBusService(),
            new NoOpCacheService());

        var claimsPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _viewerId.ToString()) }, "test"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetFeed_OnlyIncludesAccessibleVisibilities()
    {
        var otherUserId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = otherUserId,
            Email = "other@example.com",
            PasswordHash = "hash",
            Role = "participant",
            IsActive = true,
            FullName = "Other User"
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerId = _viewerId,
            Title = "Visibility Project",
            Visibility = "public",
            Status = "active"
        };
        _context.Projects.Add(project);
        _context.TeamMembers.Add(new TeamMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = otherUserId,
            Role = "member",
            Status = "active",
            JoinedAt = DateTime.UtcNow
        });
        _context.ProjectSubscriptions.Add(new ProjectSubscription
        {
            ProjectId = project.Id,
            UserId = _viewerId
        });

        _context.UserFollows.Add(new UserFollow
        {
            FollowerId = _viewerId,
            FollowedId = otherUserId
        });

        var activities = new[]
        {
            new ActivityRecord
            {
                Id = Guid.NewGuid(),
                ActorId = otherUserId,
                EventType = "user.follow",
                Summary = "public",
                Visibility = "public",
                CreatedAt = DateTime.UtcNow
            },
            new ActivityRecord
            {
                Id = Guid.NewGuid(),
                ActorId = otherUserId,
                EventType = "user.follow",
                Summary = "followers",
                Visibility = "followers",
                CreatedAt = DateTime.UtcNow
            },
            new ActivityRecord
            {
                Id = Guid.NewGuid(),
                ActorId = otherUserId,
                ProjectId = project.Id,
                EventType = "project.news_published",
                Summary = "members",
                Visibility = "members",
                CreatedAt = DateTime.UtcNow
            },
            new ActivityRecord
            {
                Id = Guid.NewGuid(),
                ActorId = otherUserId,
                ProjectId = project.Id,
                EventType = "project.media_uploaded",
                Summary = "subscribers",
                Visibility = "subscribers",
                CreatedAt = DateTime.UtcNow
            },
            new ActivityRecord
            {
                Id = Guid.NewGuid(),
                ActorId = otherUserId,
                EventType = "user.follow",
                Summary = "private-target",
                Visibility = "private",
                TargetUserId = _viewerId,
                CreatedAt = DateTime.UtcNow
            },
            new ActivityRecord
            {
                Id = Guid.NewGuid(),
                ActorId = otherUserId,
                EventType = "user.follow",
                Summary = "private-other",
                Visibility = "private",
                TargetUserId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.ActivityRecords.AddRange(activities);
        await _context.SaveChangesAsync();

        var result = await _controller.GetFeed();
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = ok.Value!;
        var data = ExtractActivities(payload);

        Assert.DoesNotContain(data, a => a.Summary == "private-other");
        Assert.Equal(5, data.Count);
    }

    private static IReadOnlyCollection<FeedController.FeedActivityResponse> ExtractActivities(object payload)
    {
        var dataProperty = payload.GetType().GetProperty("Data");
        Assert.NotNull(dataProperty);
        var value = dataProperty!.GetValue(payload);
        return Assert.IsAssignableFrom<IReadOnlyCollection<FeedController.FeedActivityResponse>>(value);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private class NoOpAuditService : IAuditService
    {
        public Task LogActionAsync(Guid? userId, string action, string entityType, Guid? entityId = null, string? details = null, string? ipAddress = null)
            => Task.CompletedTask;
    }

    private class NoOpNotificationServiceClient : INotificationServiceClient
    {
        public Task MarkAllAsReadAsync(Guid userId) => Task.CompletedTask;
        public Task MarkNotificationAsReadAsync(Guid notificationId) => Task.CompletedTask;
        public Task SendBulkNotificationsAsync(List<Guid> userIds, string type, string title, string content, string priority = "medium") => Task.CompletedTask;
        public Task SendNotificationAsync(Guid userId, string type, string title, string content, string? relatedEntityType = null, Guid? relatedEntityId = null, string priority = "medium") => Task.CompletedTask;
    }

    private class NoOpEventBusService : IEventBusService
    {
        public Task PublishAsync(DomainEvent domainEvent) => Task.CompletedTask;
    }

    private class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key) where T : class => Task.FromResult<T?>(null);
        public Task RemoveAsync(string key) => Task.CompletedTask;
        public Task RemoveByPatternAsync(string pattern) => Task.CompletedTask;
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class => Task.CompletedTask;
    }
}
