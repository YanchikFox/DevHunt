using DevHunt.AuthService.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevHunt.AuthService.Tests;

/// <summary>
/// Integration-style tests for refresh token rotation and reuse detection.
/// </summary>
public class RefreshTokenServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDevHuntDbContext _dbContext;
    private readonly RefreshTokenService _service;
    private readonly User _user;

    public RefreshTokenServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TestDevHuntDbContext(options);
        _dbContext.Database.EnsureCreated();

        _user = new User
        {
            Id = Guid.NewGuid(),
            Email = "rotate@test.dev",
            PasswordHash = "hash",
            Role = "participant",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Users.Add(_user);
        _dbContext.SaveChanges();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test_jwt_key_min_32_characters_long_for_testing",
            })
            .Build();

        _service = new RefreshTokenService(_dbContext, NullLogger<RefreshTokenService>.Instance, config);
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_allows_single_use_and_issues_new_token()
    {
        var (plaintext, _) = await _service.CreateRefreshTokenAsync(_user.Id, TimeSpan.FromDays(7));

        var rotation = await _service.RotateRefreshTokenAsync(plaintext);

        rotation.Should().NotBeNull();
        rotation!.User.Id.Should().Be(_user.Id);
        rotation.NewRefreshTokenPlaintext.Should().NotBe(plaintext);

        var revoked = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == _user.Id && rt.IsRevoked)
            .ToListAsync();
        revoked.Should().Contain(rt => rt.RevocationReason == "rotated");
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_rejects_reused_token_and_revokes_family()
    {
        var (plaintext, entity) = await _service.CreateRefreshTokenAsync(_user.Id, TimeSpan.FromDays(7));
        var first = await _service.RotateRefreshTokenAsync(plaintext);
        first.Should().NotBeNull();

        var reuse = await _service.RotateRefreshTokenAsync(plaintext);
        reuse.Should().BeNull();

        _dbContext.ChangeTracker.Clear();
        var familyTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.TokenFamilyId == entity.TokenFamilyId)
            .ToListAsync();

        familyTokens.Should().OnlyContain(rt => rt.IsRevoked);
        familyTokens.Should().Contain(rt => rt.RevocationReason == "reuse_detected" || rt.RevocationReason == "rotated");
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_parallel_requests_only_one_succeeds()
    {
        var (plaintext, _) = await _service.CreateRefreshTokenAsync(_user.Id, TimeSpan.FromDays(7));

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => _service.RotateRefreshTokenAsync(plaintext))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        results.Count(r => r != null).Should().Be(1);
        results.Count(r => r == null).Should().Be(7);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private string ComputeHash(string token)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes("test_jwt_key_min_32_characters_long_for_testing"));
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }
}
