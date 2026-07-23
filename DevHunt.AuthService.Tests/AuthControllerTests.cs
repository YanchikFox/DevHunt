using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevHunt.AuthService.Controllers;
using DevHunt.AuthService.Models;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;
using Moq;
using Microsoft.Extensions.Logging;
using DevHunt.AuthService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DevHunt.AuthService.Tests;

/// <summary>
/// Tests for AuthController, including refresh tokens functionality.
/// </summary>
[Collection("Database tests")]
public class AuthControllerTests : IDisposable
{
    private readonly TestDevHuntDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly AuthController _controller;
    private readonly Mock<IAuthServices> _mockAuthServices;
    private readonly Mock<IRefreshTokenService> _mockRefreshTokenService;
    private readonly Mock<IRegistrationService> _mockRegistrationService;
    private readonly Mock<ILoginService> _mockLoginService;

    public AuthControllerTests()
    {
        _dbContext = CreateInMemoryDbContext();
        _configuration = CreateConfiguration();
        
        _mockRefreshTokenService = CreateRefreshTokenServiceMock();
        _mockRegistrationService = CreateRegistrationServiceMock();
        _mockLoginService = CreateLoginServiceMock();
        _mockAuthServices = CreateAuthServicesFacadeMock();

        _controller = CreateController();
    }

    #region Setup Helper Methods

    private static TestDevHuntDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDevHuntDbContext(options);
    }

    private static IConfiguration CreateConfiguration()
    {
        var configDict = new Dictionary<string, string?>
        {
            { "Jwt:Key", "test_jwt_key_min_32_characters_long_for_testing" },
            { "Jwt:Issuer", "DevHunt.AuthService.Test" },
            { "Jwt:Audience", "DevHunt.CoreApi.Test" }
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    private Mock<IRefreshTokenService> CreateRefreshTokenServiceMock()
    {
        var mock = new Mock<IRefreshTokenService>();
        // Default setup - individual tests may override
        return mock;
    }

    private Mock<IRegistrationService> CreateRegistrationServiceMock()
    {
        var mock = new Mock<IRegistrationService>();
        mock.Setup(r => r.ValidateRegistrationAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        mock.Setup(r => r.CreateUserAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                FullName = "Test User",
                PasswordHash = "hash",
                Role = "participant",
                IsEmailVerified = false,
                VerificationToken = "123456"
            });
        mock.Setup(r => r.ShouldAutoVerify()).Returns(true);
        mock.Setup(r => r.AutoVerifyUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback((User u, CancellationToken _) => u.IsEmailVerified = true)
            .Returns(Task.CompletedTask);
        return mock;
    }

    private Mock<ILoginService> CreateLoginServiceMock()
    {
        var mock = new Mock<ILoginService>();
        mock.Setup(l => l.AuthenticateAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync((LoginRequest req) =>
            {
                var user = _dbContext.Users.FirstOrDefault(u => u.Email == req.Email.Trim().ToLowerInvariant());
                if (user == null)
                    return new LoginResult(false, ErrorMessage: "Invalid email or password.");
                if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                    return new LoginResult(false, ErrorMessage: "Invalid email or password.");
                if (!user.IsEmailVerified)
                    return new LoginResult(false, User: user, RequiresEmailVerification: true);
                return new LoginResult(true, User: user);
            });
        return mock;
    }

    private Mock<IAuthServices> CreateAuthServicesFacadeMock()
    {
        var mockValidation = new Mock<IAuthValidationService>();
        mockValidation.Setup(v => v.NormalizeEmail(It.IsAny<string>()))
            .Returns((string email) => (email ?? "").Trim().ToLowerInvariant());
        mockValidation.Setup(v => v.IsValidEmail(It.IsAny<string>()))
            .Returns((string email) => !string.IsNullOrWhiteSpace(email) && email.Contains("@"));
        mockValidation.Setup(v => v.IsValidPassword(It.IsAny<string>()))
            .Returns((string pwd) => !string.IsNullOrWhiteSpace(pwd) && pwd.Length >= 8);

        var mockJwt = new Mock<IJwtTokenService>();
        mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<User>())).Returns("test_access_token");

        var mockCookie = new Mock<IAuthCookieService>();
        var mockOAuth = new Mock<IOAuthService>();

        // Create token services sub-facade
        var mockTokenServices = new Mock<ITokenServices>();
        mockTokenServices.Setup(t => t.Jwt).Returns(mockJwt.Object);
        mockTokenServices.Setup(t => t.RefreshToken).Returns(_mockRefreshTokenService.Object);
        mockTokenServices.Setup(t => t.Cookie).Returns(mockCookie.Object);

        // Create user services sub-facade
        var mockUserServices = new Mock<IUserAuthServices>();
        mockUserServices.Setup(u => u.Registration).Returns(_mockRegistrationService.Object);
        mockUserServices.Setup(u => u.Login).Returns(_mockLoginService.Object);
        mockUserServices.Setup(u => u.Validation).Returns(mockValidation.Object);

        var mock = new Mock<IAuthServices>();
        mock.Setup(s => s.Tokens).Returns(mockTokenServices.Object);
        mock.Setup(s => s.User).Returns(mockUserServices.Object);
        mock.Setup(s => s.OAuth).Returns(mockOAuth.Object);
        // Convenience accessors
        mock.Setup(s => s.Validation).Returns(mockValidation.Object);
        mock.Setup(s => s.Jwt).Returns(mockJwt.Object);
        mock.Setup(s => s.Cookie).Returns(mockCookie.Object);
        mock.Setup(s => s.Registration).Returns(_mockRegistrationService.Object);
        mock.Setup(s => s.Login).Returns(_mockLoginService.Object);

        return mock;
    }

    private AuthController CreateController()
    {
        var mockLogger = new Mock<ILogger<AuthController>>();

        var controller = new AuthController(
            _dbContext,
            _configuration,
            mockLogger.Object,
            _mockAuthServices.Object
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    #endregion

    [Fact]
    public async Task Register_ShouldCreateUserAndSendVerificationEmail()
    {
        // Arrange
        var request = new RegisterRequest("test@example.com", "TestPassword123!", "Test User");

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        
        var json = System.Text.Json.JsonSerializer.Serialize(okResult!.Value);
        json.Should().Contain("Registration successful");
        json.Should().Contain("userId");
        json.Should().Contain("autoVerified");
        
        _mockRegistrationService.Verify(r => r.ValidateRegistrationAsync(It.IsAny<RegisterRequest>()), Times.Once);
        _mockRegistrationService.Verify(r => r.CreateUserAsync(It.IsAny<RegisterRequest>()), Times.Once);
    }

    [Fact]
    public async Task Login_ShouldReturnAccessAndRefreshTokens()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
            Role = "participant",
            IsEmailVerified = true
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _mockRefreshTokenService.Setup(x => x.CreateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(("new_refresh_token", refreshTokenEntity));

        var request = new LoginWith2faRequest("test@example.com", "TestPassword123!");

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value as AuthResponse;
        
        response.Should().NotBeNull();
        response!.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().Be("new_refresh_token");
    }

    [Fact]
    public async Task Refresh_WithValidToken_ShouldReturnNewTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "participant"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new RefreshTokenRequest("valid_refresh_token");
        var newRefreshToken = "new_refresh_token";

        _mockRefreshTokenService.Setup(s => s.RotateRefreshTokenAsync(request.RefreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenRotationResult(user, newRefreshToken));

        // Act
        var result = await _controller.Refresh(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value as AuthResponse;
        
        response.Should().NotBeNull();
        response!.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().Be(newRefreshToken);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new RefreshTokenRequest("invalid_token");
        _mockRefreshTokenService.Setup(s => s.RotateRefreshTokenAsync(request.RefreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenRotationResult?)null);

        // Act
        var result = await _controller.Refresh(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Logout_ShouldRevokeRefreshToken()
    {
        // Arrange
        var request = new RefreshTokenRequest("token_to_revoke");
        _mockRefreshTokenService.Setup(s => s.RevokeRefreshTokenAsync(request.RefreshToken, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Logout(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRefreshTokenService.Verify(s => s.RevokeRefreshTokenAsync(request.RefreshToken, It.IsAny<string>()), Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
