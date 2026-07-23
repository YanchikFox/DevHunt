using DevHunt.CoreApi.Services.Ai;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevHunt.CoreApi.Tests.Services;

public class AiPlanApplierTests
{
    [Fact]
    public async Task ApplyTwice_ShouldNotDuplicateTasks()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new DevHuntTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        db.Users.Add(new User
        {
            Id = userId,
            Email = "user@test.dev",
            Role = "participant"
        });

        db.Projects.Add(new Project
        {
            Id = projectId,
            Title = "Test Project",
            Description = "Test project",
            OwnerId = userId,
            Status = "draft",
            Visibility = "public",
            TechStack = new List<string>()
        });

        db.AiPlans.Add(new AiPlan
        {
            Id = planId,
            ProjectId = projectId,
            CreatedByUserId = userId,
            Idea = "Idea",
            TechStack = "Stack",
            Status = "draft",
            PlanVersion = "v1",
            PlanJson = "{}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var applier = new AiPlanApplier(
            db,
            Options.Create(new AiPlanningOptions()),
            NullLogger<AiPlanApplier>.Instance);

        var draft = new AiPlanDraft(
            "v1",
            "Idea",
            "Stack",
            new List<AiPlanPhase>
            {
                new AiPlanPhase(
                    "mvp",
                    "MVP",
                    "Phase",
                    new List<string>(),
                    new List<AiPlanTask>
                    {
                        new("task-a", "Task A", "Desc", new List<string>()),
                        new("task-b", "Task B", "Desc", new List<string> { "task-a" })
                    }
                )
            }
        );

        var first = await applier.ApplyAsync(projectId, planId, userId, draft, CancellationToken.None);
        var second = await applier.ApplyAsync(projectId, planId, userId, draft, CancellationToken.None);

        first.Status.Should().Be(AiPlanApplyStatus.Applied);
        second.Status.Should().Be(AiPlanApplyStatus.AlreadyApplied);

        var taskCount = await db.Tasks.CountAsync();
        var linkCount = await db.TaskLinks.CountAsync();

        taskCount.Should().Be(2);
        linkCount.Should().Be(1);
    }
}
