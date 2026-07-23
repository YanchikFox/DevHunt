using DevHunt.AuthService.Models;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Result of login attempt.
/// </summary>
public record LoginResult(
    bool Success,
    User? User = null,
    string? ErrorMessage = null,
    bool RequiresEmailVerification = false,
    bool RequiresTwoFactor = false);

/// <summary>
/// Service for user login logic.
/// </summary>
public interface ILoginService
{
    /// <summary>Attempts to authenticate a user.</summary>
    Task<LoginResult> AuthenticateAsync(LoginRequest request);

    /// <summary>Finds user by email with normalization.</summary>
    Task<User?> FindUserByEmailAsync(string email, CancellationToken ct = default);
}

/// <summary>
/// Implementation of login service.
/// </summary>
public class LoginService : ILoginService
{
    private readonly DevHuntDbContext _context;
    private readonly IAuthValidationService _validation;

    /// <summary>
    /// Stores the user context and normalization/validation helpers used during login.
    /// </summary>
    public LoginService(
        DevHuntDbContext context,
        IAuthValidationService validation)
    {
        _context = context;
        _validation = validation;
    }

    /// <inheritdoc />
    public async Task<LoginResult> AuthenticateAsync(LoginRequest request)
    {
        var user = await FindUserByEmailAsync(request.Email);

        // L-09: Self-heal legacy email casing during login (moved from FindUserByEmailAsync to keep it side-effect-free)
        if (user != null)
        {
            var normalizedEmail = _validation.NormalizeEmail(request.Email);
            if (user.Email != normalizedEmail)
            {
                user.Email = normalizedEmail;
                await _context.SaveChangesAsync();
            }
        }

        // Constant-time password verification to prevent timing attacks
        var isPasswordValid = VerifyPassword(user, request.Password);

        if (!isPasswordValid)
            return new LoginResult(false, ErrorMessage: "Invalid email or password.");

        if (user != null && !user.IsEmailVerified)
            return new LoginResult(false, User: user, RequiresEmailVerification: true);

        // Check if 2FA is enabled — caller must verify TOTP code
        if (user != null && user.IsTotpEnabled)
            return new LoginResult(false, User: user, RequiresTwoFactor: true);

        return new LoginResult(true, User: user);
    }

    /// <inheritdoc />
    public async Task<User?> FindUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = _validation.NormalizeEmail(email);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user == null)
        {
            // Backward-compatible: tolerate legacy rows with casing/whitespace
            user = await _context.Users.FirstOrDefaultAsync(
                u => u.Email != null && u.Email.Trim().ToLower() == normalizedEmail, ct);
        }

        return user;
    }

    // L-05: Pre-computed BCrypt hash for constant-time password verification
    // This ensures identical CPU profile regardless of whether user exists
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword("dummy-password-for-timing-safety", workFactor: 12);

    /// <summary>
    /// Verifies the supplied password against the user's BCrypt hash, or against a dummy hash when the user is missing, and only returns true when both the user exists and BCrypt succeeds.
    /// </summary>
    private static bool VerifyPassword(User? user, string password)
    {
        var hashToCheck = user?.PasswordHash ?? DummyPasswordHash;
        var result = BCrypt.Net.BCrypt.Verify(password, hashToCheck);
        return user != null && result;
    }
}
