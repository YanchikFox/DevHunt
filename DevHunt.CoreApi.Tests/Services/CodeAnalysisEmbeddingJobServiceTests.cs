using DevHunt.CoreApi.Services.CodeAnalysis;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Constants;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevHunt.CoreApi.Tests.Services;

/// <summary>
/// Tests durable embedding job enqueue idempotency.
/// </summary>
public sealed class CodeAnalysisEmbeddingJobServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly DevHuntTestDbContext _db;
    private readonly CodeAnalysisEmbeddingJobService _service;

    public CodeAnalysisEmbeddingJobServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new DevHuntTestDbContext(options);
        _service = new CodeAnalysisEmbeddingJobService(_db, NullLogger<CodeAnalysisEmbeddingJobService>.Instance);
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _db.Database.EnsureCreatedAsync();
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var resultId = Guid.NewGuid();
        _db.Users.Add(new User { Id = ownerId, Email = "o@test.dev", PasswordHash = "h", Role = "participant" });
        _db.Projects.Add(new Project
        {
            Id = projectId,
            Title = "P",
            Description = "d",
            OwnerId = ownerId,
            Status = ProjectStatus.Draft,
            Visibility = ProjectVisibility.Private,
            TechStack = [],
        });
        _db.CodeAnalysisResults.Add(new CodeAnalysisResult
        {
            Id = resultId,
            ProjectId = projectId,
            Repository = "o/r",
            Status = "completed",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Enqueue is idempotent per analysis result id.
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_IsIdempotentPerAnalysisResult()
    {
        var analysisResultId = await _db.CodeAnalysisResults.Select(r => r.Id).FirstAsync();
        var projectId = await _db.CodeAnalysisResults.Select(r => r.ProjectId).FirstAsync();

        var first = await _service.EnqueueAsync(analysisResultId, projectId, CancellationToken.None);
        var second = await _service.EnqueueAsync(analysisResultId, projectId, CancellationToken.None);

        second.Should().Be(first);
        (await _db.CodeAnalysisEmbeddingJobs.CountAsync()).Should().Be(1);
        (await _db.CodeAnalysisEmbeddingJobs.Select(j => j.Status).FirstAsync())
            .Should().Be(CodeAnalysisEmbeddingJobStatus.Pending);
    }
}
