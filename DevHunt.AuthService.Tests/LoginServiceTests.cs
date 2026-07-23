using DevHunt.AuthService.Models;
using DevHunt.AuthService.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevHunt.AuthService.Tests;

[Collection("Database tests")]
public class LoginServiceTests : IDisposable
{
    private readonly TestDevHuntDbContext _dbContext;
    private readonly LoginService _service;

    public LoginServiceTests()
    {
        _dbContext = CreateContext();
        _service = new LoginService(_dbContext, new AuthValidationService());
    }

    [Fact]
    public async Task AuthenticateAsync_WhenUserNotFound_ShouldReturnInvalidCredentials()
    {
        var result = await _service.AuthenticateAsync(new LoginRequest("missing@example.com", "SomePass1!"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid email or password.");
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WhenPasswordIsWrong_ShouldReturnInvalidCredentials()
    {
        var user = await AddUserAsync("user@example.com", "RightPass1!", isEmailVerified: true);

        var result = await _service.AuthenticateAsync(new LoginRequest(user.Email, "WrongPass1!"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task AuthenticateAsync_WhenEmailNotVerified_ShouldRequireEmailVerification()
    {
        var user = await AddUserAsync("user@example.com", "RightPass1!", isEmailVerified: false);

        var result = await _service.AuthenticateAsync(new LoginRequest(user.Email, "RightPass1!"));

        result.Success.Should().BeFalse();
        result.RequiresEmailVerification.Should().BeTrue();
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenTotpEnabled_ShouldRequireTwoFactor()
    {
        var user = await AddUserAsync("user@example.com", "RightPass1!", isEmailVerified: true, isTotpEnabled: true);

        var result = await _service.AuthenticateAsync(new LoginRequest(user.Email, "RightPass1!"));

        result.Success.Should().BeFalse();
        result.RequiresTwoFactor.Should().BeTrue();
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenCredentialsAreValid_ShouldReturnSuccess()
    {
        var user = await AddUserAsync("user@example.com", "RightPass1!", isEmailVerified: true);

        var result = await _service.AuthenticateAsync(new LoginRequest(user.Email, "RightPass1!"));

        result.Success.Should().BeTrue();
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task FindUserByEmailAsync_ShouldMatchLegacyEmailFormatting()
    {
        var user = await AddUserAsync("  Legacy@Example.COM ", "RightPass1!", isEmailVerified: true);

        var found = await _service.FindUserByEmailAsync("legacy@example.com");

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldNormalizeLegacyEmailOnSuccessfulLogin()
    {
        var user = await AddUserAsync("  Legacy@Example.COM ", "RightPass1!", isEmailVerified: true);

        var result = await _service.AuthenticateAsync(new LoginRequest("legacy@example.com", "RightPass1!"));

        result.Success.Should().BeTrue();
        var updatedUser = await _dbContext.Users.SingleAsync(u => u.Id == user.Id);
        updatedUser.Email.Should().Be("legacy@example.com");
    }

    private static TestDevHuntDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestDevHuntDbContext(options);
    }

    private async Task<User> AddUserAsync(
        string email,
        string password,
        bool isEmailVerified,
        bool isTotpEnabled = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "participant",
            IsEmailVerified = isEmailVerified,
            IsTotpEnabled = isTotpEnabled
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
