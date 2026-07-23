using System.Text.RegularExpressions;
using DevHunt.AuthService.Models;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Result of user registration attempt.
/// </summary>
public record RegistrationResult(
    bool Success,
    User? User = null,
    string? ErrorMessage = null,
    bool AutoVerified = false);

/// <summary>
/// Service for user registration logic.
/// </summary>
public interface IRegistrationService
{
    /// <summary>Validates registration request.</summary>
    Task<string?> ValidateRegistrationAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Creates a new user account.</summary>
    Task<User> CreateUserAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Checks if auto-verification should be applied.</summary>
    bool ShouldAutoVerify();

    /// <summary>Auto-verifies user in development mode.</summary>
    Task AutoVerifyUserAsync(User user, CancellationToken ct = default);
}

/// <summary>
/// Implementation of registration service.
/// </summary>
public class RegistrationService : IRegistrationService
{
    private readonly DevHuntDbContext _context;
    private readonly IAuthValidationService _validation;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    // REG-05: Role constant instead of hardcoded string
    private const string DefaultRole = "participant";

    /// <summary>
    /// Stores database, validation, configuration, and environment dependencies used for registration and dev-mode verification.
    /// </summary>
    public RegistrationService(
        DevHuntDbContext context,
        IAuthValidationService validation,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _context = context;
        _validation = validation;
        _config = config;
        _env = env;
    }

    /// <inheritdoc />
    public async Task<string?> ValidateRegistrationAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = _validation.NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(normalizedEmail) || !_validation.IsValidEmail(normalizedEmail))
            return "Invalid email format.";

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length < 2)
            return "Full name is required and must be at least 2 characters.";

        // REG-14: Max length validation for FullName
        if (request.FullName.Length > 100)
            return "Full name must not exceed 100 characters.";

        if (!_validation.IsValidPassword(request.Password))
            return "Password must be at least 8 characters, contain uppercase, lowercase, number, and special character.";

        // REG-04: Single query on normalized email (no double-query with Trim().ToLower())
        if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail && u.IsEmailVerified, ct))
            return "Email already exists.";

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            if (!Regex.IsMatch(request.Username, @"^[a-zA-Z0-9_\-\.]{3,50}$"))
                return "Username must be 3-50 characters: letters, digits, underscore, hyphen, dot.";
            if (await _context.Users.AnyAsync(u => u.Username == request.Username, ct))
                return "Username is already taken.";
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<User> CreateUserAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = _validation.NormalizeEmail(request.Email);

        // REG-01/REG-02: Wrap delete+insert in a single transaction with unique constraint catch
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellation);

            // Remove existing unverified accounts atomically
            await _context.Users
                .Where(u => u.Email == normalizedEmail && !u.IsEmailVerified)
                .ExecuteDeleteAsync(cancellation);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = request.FullName?.Trim(),
                Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = DefaultRole,
                IsEmailVerified = false,
                VerificationToken = GenerateVerificationCode(),
                VerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync(cancellation);
                await tx.CommitAsync(cancellation);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                throw new InvalidOperationException("Email already exists.");
            }

            return user;
        }, ct);
    }

    /// <inheritdoc />
    public bool ShouldAutoVerify()
    {
        var smtpHost = _config["Email:SmtpHost"];
        return _env.IsDevelopment() && string.IsNullOrEmpty(smtpHost);
    }

    /// <inheritdoc />
    public async Task AutoVerifyUserAsync(User user, CancellationToken ct = default)
    {
        user.IsEmailVerified = true;
        user.VerificationToken = null;
        user.VerificationTokenExpiresAt = null;
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Generates a random six-digit email verification code using <see cref="System.Security.Cryptography.RandomNumberGenerator"/>.
    /// </summary>
    private static string GenerateVerificationCode()
    {
        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999);
        return code.ToString();
    }

    /// <summary>
    /// Treats provider-specific duplicate-key messages, including PostgreSQL 23505, as unique constraint violations.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("23505") == true;
}
