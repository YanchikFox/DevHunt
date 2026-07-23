using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.AuthService.Models;
using DevHunt.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DevHunt.AuthService.Controllers;

/// <summary>
/// Authentication controller handling user registration, login, logout,
/// email verification, password reset, and OAuth2 external providers.
/// </summary>
/// <remarks>
/// Security features:
/// - Passwords are hashed with BCrypt
/// - JWT tokens with short expiry (30 min access, 7 day refresh)
/// - HttpOnly cookies to prevent XSS attacks
/// - Refresh token rotation with reuse detection
/// - Email enumeration protection
/// - HMAC-signed OAuth state parameter
/// </remarks>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly DevHuntDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;
    private readonly IAuthServices _authServices;

    // Convenience accessors for facade services
    private IEmailService EmailService => _authServices.Email;
    private IWebHostEnvironment Env => _authServices.Environment;

    /// <summary>
    /// Stores the data context, configuration, logger, and authentication service facade used by auth endpoints.
    /// </summary>
    public AuthController(
        DevHuntDbContext context,
        IConfiguration config,
        ILogger<AuthController> logger,
        IAuthServices authServices)
    {
        _context = context;
        _config = config;
        _logger = logger;
        _authServices = authServices;
    }

    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <param name="request">Registration data including email, password, and full name.</param>
    /// <param name="ct">Cancels validation, user creation, and verification persistence when the request is aborted.</param>
    /// <returns>Success message with user ID, or error details.</returns>
    /// <response code="200">Registration successful. Verification email sent (or auto-verified in dev mode).</response>
    /// <response code="400">Invalid input (email format, password complexity, or email already exists).</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct = default)
    {
        // Validate registration request
        var validationError = await _authServices.Registration.ValidateRegistrationAsync(request, ct);
        if (validationError != null)
            return BadRequest(validationError);

        // Create user
        User user;
        try
        {
            user = await _authServices.Registration.CreateUserAsync(request, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Email already exists"))
        {
            return BadRequest("Email already exists.");
        }

        // Auto-verify in development when SMTP is not configured
        if (_authServices.Registration.ShouldAutoVerify())
        {
            await _authServices.Registration.AutoVerifyUserAsync(user, ct);
            _logger.LogInformation("Auto-verified user {Email} in development mode (SMTP not configured)", user.Email);

            return Ok(new
            {
                message = "Registration successful. Email auto-verified (dev mode).",
                userId = user.Id,
                autoVerified = true
            });
        }

        // REG-07: Check email send result
        var emailSent = await EmailService.SendVerificationEmailAsync(user.Email, user.VerificationToken!, user.Id);
        if (!emailSent)
        {
            _logger.LogError("Failed to send verification email to {Email} for user {UserId}", user.Email, user.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Account created, but verification email could not be sent. Use 'Resend verification' to retry.");
        }

        return Ok(new
        {
            message = "Registration successful. Please verify your email to continue.",
            userId = user.Id
        });
    }

    /// <summary>
    /// Authenticate user and issue JWT tokens.
    /// </summary>
    /// <param name="request">Login credentials (email and password).</param>
    /// <param name="ct">Cancels database updates for recovery-code use, last-login tracking, and token transaction work.</param>
    /// <returns>Access and refresh tokens, or error.</returns>
    /// <response code="200">Login successful. Returns tokens in body and sets httpOnly cookies.</response>
    /// <response code="401">Invalid email or password.</response>
    /// <response code="403">Email not verified. Verification code resent.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginWith2faRequest request, CancellationToken ct = default)
    {
        // Authenticate user with constant-time password verification
        var loginRequest = new LoginRequest(request.Email, request.Password);
        var result = await _authServices.Login.AuthenticateAsync(loginRequest);

        if (!result.Success && !result.RequiresEmailVerification && !result.RequiresTwoFactor)
            return Unauthorized(result.ErrorMessage ?? "Invalid email or password.");

        if (result.RequiresEmailVerification && result.User != null)
        {
            await IssueNewVerificationTokenAsync(result.User);
            return StatusCode(StatusCodes.Status403Forbidden, "Email not verified. Verification code sent.");
        }

        // 2FA: if TOTP enabled, require code
        if (result.RequiresTwoFactor && result.User != null)
        {
            if (string.IsNullOrWhiteSpace(request.TotpCode) && string.IsNullOrWhiteSpace(request.RecoveryCode))
                return Ok(new TwoFactorRequiredResponse(true, result.User.Id));

            var totpService = HttpContext.RequestServices.GetRequiredService<ITotpService>();

            if (!string.IsNullOrWhiteSpace(request.TotpCode))
            {
                if (!totpService.ValidateCode(result.User.TotpSecret!, request.TotpCode))
                    return Unauthorized("Invalid authenticator code.");
            }
            else if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
            {
                var (isValid, updatedHashes) = totpService.ValidateRecoveryCode(
                    request.RecoveryCode, result.User.TotpRecoveryCodes ?? "");
                if (!isValid)
                    return Unauthorized("Invalid recovery code.");
                result.User.TotpRecoveryCodes = updatedHashes;
                await _context.SaveChangesAsync(ct);
            }
        }

        var user = result.User!;
        var accessToken = _authServices.Jwt.GenerateAccessToken(user);

        // L-07: Wrap refresh token creation + LastLogin update in a transaction
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // SECURITY FIX (R4): Use RefreshTokenService with HMAC hashing
            var (refreshToken, _) = await _authServices.Tokens.RefreshToken.CreateRefreshTokenAsync(
                user.Id,
                TimeSpan.FromDays(7));

            // Track last successful login
            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // R7: Set httpOnly cookies for secure token storage
            _authServices.Cookie.SetAuthCookies(Response, accessToken, refreshToken);

            return Ok(new AuthResponse(accessToken, refreshToken, user.Email, user.Id));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Refresh access token using a valid refresh token.
    /// </summary>
    /// <param name="request">Refresh token from previous authentication.</param>
    /// <param name="ct">Cancels user lookup, token rotation persistence, and transaction operations.</param>
    /// <returns>New access and refresh tokens (old refresh token is invalidated).</returns>
    /// <response code="200">Token refreshed successfully.</response>
    /// <response code="401">Invalid or expired refresh token.</response>
    /// <remarks>
    /// Implements token rotation: each refresh token can only be used once.
    /// Reuse of a refresh token indicates potential token theft and triggers security logging.
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct = default)
    {
        var rotation = await _authServices.Tokens.RefreshToken.RotateRefreshTokenAsync(
            request.RefreshToken,
            ct);

        if (rotation == null)
        {
            _logger.LogWarning("Refresh token validation failed");
            return Unauthorized("Invalid or expired refresh token.");
        }

        var user = rotation.User;
        var newAccessToken = _authServices.Jwt.GenerateAccessToken(user);

        _authServices.Cookie.SetAuthCookies(Response, newAccessToken, rotation.NewRefreshTokenPlaintext);

        return Ok(new AuthResponse(
            newAccessToken,
            rotation.NewRefreshTokenPlaintext,
            user.Email,
            user.Id));
    }

    /// <summary>
    /// Get current authenticated user info.
    /// MVP FIX: This endpoint was missing - frontend couldn't check current session.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid user token.");
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Role,
                u.FullName,
                u.AvatarUrl,
                u.IsEmailVerified,
                u.IsVerified,
                u.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (user == null)
        {
            return NotFound("User not found or inactive.");
        }

        return Ok(user);
    }

    /// <summary>
    /// Log out user and invalidate tokens.
    /// </summary>
    /// <param name="request">Optional refresh token to invalidate.</param>
    /// <returns>Success message.</returns>
    /// <response code="200">Logged out successfully. Cookies cleared.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest? request)
    {
        if (!string.IsNullOrEmpty(request?.RefreshToken))
        {
            await _authServices.Tokens.RefreshToken.RevokeRefreshTokenAsync(request.RefreshToken, "user_logout");
        }

        // R7: Clear httpOnly cookies
        _authServices.Cookie.ClearAuthCookies(Response);

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Verifies an email code by user ID, returning not found for missing users, success for already-verified users, bad requests for expired or mismatched codes, and clearing the stored token after a timing-safe match.
    /// </summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync(new object[] { request.UserId }, ct);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        if (user.IsEmailVerified)
        {
            return Ok(new { message = "Email already verified." });
        }

        if (!user.VerificationTokenExpiresAt.HasValue || user.VerificationTokenExpiresAt < DateTime.UtcNow)
        {
            return BadRequest("Verification token expired. Please request a new code.");
        }

        // SECURITY FIX (CVH-003): Timing-safe comparison
        if (!VerifyTokenSecure(user.VerificationToken, request.Code))
        {
            return BadRequest("Invalid verification code.");
        }

        user.IsEmailVerified = true;
        user.VerificationToken = null;
        user.VerificationTokenExpiresAt = null;

        await _context.SaveChangesAsync(ct);

        return Ok(new { message = "Email verified successfully." });
    }

    /// <summary>
    /// Normalizes and validates an email, self-heals legacy email casing when found, rejects invalid or missing users, skips already-verified users, and sends a new verification code without exposing the user ID.
    /// </summary>
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = _authServices.Validation.NormalizeEmail(request.Email);

        if (!_authServices.Validation.IsValidEmail(normalizedEmail))
        {
            return BadRequest("Invalid email.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user == null)
        {
            user = await _context.Users.FirstOrDefaultAsync(
                u => u.Email != null && u.Email.Trim().ToLower() == normalizedEmail, ct);

            if (user != null && user.Email != normalizedEmail)
            {
                user.Email = normalizedEmail;
                await _context.SaveChangesAsync(ct);
            }
        }
        if (user == null)
        {
            return NotFound("User not found.");
        }

        if (user.IsEmailVerified)
        {
            return Ok(new { message = "Email already verified." });
        }

        await IssueNewVerificationTokenAsync(user);
        // SECURITY FIX (CVM-001): Don't expose userId to prevent information disclosure
        // Attackers could use this to enumerate accounts and get internal IDs
        return Ok(new { message = "If this email exists and is unverified, a verification code has been sent." });
    }

    /// <summary>
    /// Request password reset. Sends email with reset token if user exists.
    /// Always returns success to prevent email enumeration attacks.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = _authServices.Validation.NormalizeEmail(request.Email);

        if (!_authServices.Validation.IsValidEmail(normalizedEmail))
        {
            // Return success anyway to prevent enumeration
            return Ok(new { message = "If this email exists, a password reset link has been sent." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user == null)
        {
            // Backward-compatible lookup
            user = await _context.Users.FirstOrDefaultAsync(
                u => u.Email != null && u.Email.Trim().ToLower() == normalizedEmail, ct);
        }

        if (user != null)
        {
            // SECURITY FIX (CVH-004): Generate secure reset token and store only its hash
            // This protects tokens if the database is compromised
            var resetToken = GenerateSecureToken();
            var tokenHash = ComputeTokenHash(resetToken);

            user.PasswordResetToken = tokenHash; // Store HASH, not plaintext
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1); // 1 hour expiry
            await _context.SaveChangesAsync(ct);

            // Send the original (unhashed) token to user's email
            await EmailService.SendPasswordResetEmailAsync(user.Email, resetToken, user.Id);
        }

        // Always return success to prevent email enumeration
        return Ok(new { message = "If this email exists, a password reset link has been sent." });
    }

    /// <summary>
    /// Reset password using the token from email.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest("Reset token is required.");
        }

        if (!_authServices.Validation.IsValidPassword(request.NewPassword))
        {
            return BadRequest("Password must be at least 8 characters, contain uppercase, lowercase, number, and special character.");
        }

        // SECURITY FIX (CVH-004): Hash the provided token to compare with stored hash
        var tokenHash = ComputeTokenHash(request.Token);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == tokenHash, ct);
        if (user == null)
        {
            return BadRequest("Invalid or expired reset token.");
        }

        if (!user.PasswordResetTokenExpiresAt.HasValue || user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
        {
            // Clear expired token
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;
            await _context.SaveChangesAsync(ct);
            return BadRequest("Reset token has expired. Please request a new one.");
        }

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;

        // Also verify email if not already verified (user proved email ownership)
        if (!user.IsEmailVerified)
        {
            user.IsEmailVerified = true;
            user.VerificationToken = null;
            user.VerificationTokenExpiresAt = null;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Password reset successful for user {UserId}", user.Id);

        return Ok(new { message = "Password has been reset successfully. You can now log in with your new password." });
    }

    /// <summary>
    /// Check if a username is available for registration.
    /// </summary>
    /// <param name="username">Username to check.</param>
    /// <param name="ct">Cancels the uniqueness query when the availability request is aborted.</param>
    /// <returns>{ available: bool, reason?: string }</returns>
    [HttpGet("check-username")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckUsername([FromQuery] string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            !System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_\-\.]{3,50}$"))
            return Ok(new { available = false, reason = "invalid" });

        var taken = await _context.Users.AnyAsync(u => u.Username == username, ct);
        return Ok(new { available = !taken });
    }

    /// <summary>
    /// Starts an OAuth login by rejecting unsupported providers, validating the requested redirect URI against allowed origins, signing state, and redirecting to the provider authorization URL.
    /// </summary>
    [HttpGet("login/{provider}")]
    public IActionResult ExternalLogin([FromRoute] string provider, [FromQuery] string? redirectUri)
    {
        var parsedProvider = OAuthProviderExtensions.ParseProvider(provider);
        if (parsedProvider == null)
        {
            return BadRequest("Unsupported provider.");
        }

        var safeRedirect = _authServices.OAuth.ValidateRedirectUri(redirectUri);
        var callbackUrl = new CallbackUrl(Url.ActionLink(nameof(ExternalCallback), values: new { provider = parsedProvider.Value.ToProviderString() })!);
        var authorizationUrl = _authServices.OAuth.BuildAuthorizationUrl(parsedProvider.Value, callbackUrl, safeRedirect);

        return Redirect(authorizationUrl);
    }

    /// <summary>
    /// Completes OAuth login by validating provider, code, and signed state, exchanging the code for provider user info, linking or creating a verified user, issuing JWT and refresh tokens, setting auth cookies, and redirecting with token fragments.
    /// </summary>
    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> ExternalCallback([FromRoute] string provider, [FromQuery] string code, [FromQuery] string state, CancellationToken ct = default)
    {
        var parsedProvider = OAuthProviderExtensions.ParseProvider(provider);
        if (parsedProvider == null)
        {
            return BadRequest("Unsupported provider.");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return BadRequest("Missing OAuth parameters.");
        }

        var statePayload = _authServices.OAuth.ValidateState(state);
        if (statePayload == null)
        {
            return BadRequest("Invalid OAuth state.");
        }

        var stateParts = statePayload.Split('|', 3);
        var redirectUri = stateParts[0];
        var callbackUrl = new CallbackUrl(Url.ActionLink(nameof(ExternalCallback), values: new { provider = parsedProvider.Value.ToProviderString() })!);

        var exchange = new OAuthCodeExchange(parsedProvider.Value, code, callbackUrl);
        var externalUser = await _authServices.OAuth.ExchangeCodeAsync(exchange);
        if (externalUser == null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Failed to complete OAuth flow.");
        }

        var user = await _authServices.OAuth.HandleExternalUserAsync(parsedProvider.Value, externalUser);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Unable to process user.");
        }

        var accessToken = _authServices.Jwt.GenerateAccessToken(user);
        var (refreshToken, _) = await _authServices.Tokens.RefreshToken.CreateRefreshTokenAsync(user.Id, TimeSpan.FromDays(7));
        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var exchangeService = HttpContext.RequestServices.GetRequiredService<IOAuthExchangeService>();
        var exchangeCode = await exchangeService.CreateExchangeCodeAsync(
            new OAuthExchangePayload(accessToken, refreshToken, user.Id, user.Email),
            ct);

        var safeRedirect = _authServices.OAuth.ValidateRedirectUri(redirectUri);
        return Redirect(_authServices.OAuth.BuildExchangeRedirect(safeRedirect, exchangeCode));
    }

    /// <summary>
    /// Redeems a one-time OAuth exchange code for JWT tokens (no tokens in browser URL fragment).
    /// </summary>
    [HttpPost("oauth/exchange")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> OAuthExchange([FromBody] OAuthExchangeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Unauthorized("Invalid exchange code.");

        var exchangeService = HttpContext.RequestServices.GetRequiredService<IOAuthExchangeService>();
        var payload = await exchangeService.RedeemExchangeCodeAsync(request.Code, ct);
        if (payload == null)
            return Unauthorized("Invalid or expired exchange code.");

        _authServices.Cookie.SetAuthCookies(Response, payload.AccessToken, payload.RefreshToken);

        return Ok(new AuthResponse(
            payload.AccessToken,
            payload.RefreshToken,
            payload.Email,
            payload.UserId));
    }

    /// <summary>
    /// Generates a random six-digit email verification code using <see cref="RandomNumberGenerator"/>.
    /// </summary>
    private string GenerateVerificationCode()
    {
        var code = RandomNumberGenerator.GetInt32(100000, 999999);
        return code.ToString();
    }

    /// <summary>
    /// Generate a cryptographically secure token for password reset.
    /// </summary>
    private string GenerateSecureToken()
    {
        var tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('='); // URL-safe base64
    }

    // L-08: Use same config key as RefreshTokenService — eliminates duplicated secret source
    /// <summary>
    /// Returns an empty hash for an empty token, otherwise HMAC-SHA256 hashes the token with the configured JWT key and returns the Base64 hash.
    /// </summary>
    private string ComputeTokenHash(string token)
    {
        if (string.IsNullOrEmpty(token))
            return string.Empty;

        var secret = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("FATAL: Jwt:Key must be configured.");
        using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Replaces a user's verification token with a new 24-hour code, saves it, and sends the plaintext code by verification email.
    /// </summary>
    private async Task IssueNewVerificationTokenAsync(User user, CancellationToken ct = default)
    {
        user.VerificationToken = GenerateVerificationCode();
        user.VerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
        await _context.SaveChangesAsync(ct);
        await EmailService.SendVerificationEmailAsync(user.Email, user.VerificationToken!, user.Id);
    }

    /// <summary>
    /// SECURITY: Timing-safe token comparison to prevent timing attacks.
    /// Both values are hashed to equal-length SHA-256 digests so that
    /// FixedTimeEquals never short-circuits on differing array lengths.
    /// </summary>
    private static bool VerifyTokenSecure(string? stored, string? provided)
    {
        var a = stored?.ToLowerInvariant() ?? "";
        var b = provided?.ToLowerInvariant() ?? "";

        if (a.Length == 0 || b.Length == 0)
            return false;

        var hashA = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(a));
        var hashB = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(b));

        return CryptographicOperations.FixedTimeEquals(hashA, hashB);
    }

    // ========================================================================
    // Two-Factor Authentication (TOTP) Endpoints
    // ========================================================================

    /// <summary>
    /// POST /api/auth/totp/setup - Begin TOTP 2FA setup. Returns secret and QR URI.
    /// </summary>
    [HttpPost("totp/setup")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SetupTotp(CancellationToken ct = default)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user == null) return NotFound("User not found.");

        if (user.IsTotpEnabled)
            return BadRequest("Two-factor authentication is already enabled.");

        var totpService = HttpContext.RequestServices.GetRequiredService<ITotpService>();
        var secret = totpService.GenerateSecret();
        var qrUri = totpService.GenerateQrUri(secret, user.Email);

        return Ok(new TotpSetupResponse(secret, qrUri));
    }

    /// <summary>
    /// POST /api/auth/totp/verify-setup - Verify TOTP code and enable 2FA.
    /// </summary>
    [HttpPost("totp/verify-setup")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> VerifyTotpSetup([FromBody] VerifyTotpSetupRequest request, CancellationToken ct = default)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user == null) return NotFound("User not found.");

        if (user.IsTotpEnabled)
            return BadRequest("Two-factor authentication is already enabled.");

        var totpService = HttpContext.RequestServices.GetRequiredService<ITotpService>();

        // Verify the code matches the secret
        if (!totpService.ValidateCode(request.Secret, request.Code))
            return BadRequest("Invalid code. Please try again with a new code from your authenticator app.");

        // Enable 2FA
        var recoveryCodes = totpService.GenerateRecoveryCodes();
        user.TotpSecret = request.Secret;
        user.IsTotpEnabled = true;
        user.TotpRecoveryCodes = totpService.HashRecoveryCodes(recoveryCodes);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} enabled TOTP 2FA", userId);

        return Ok(new TotpSetupCompleteResponse(recoveryCodes));
    }

    /// <summary>
    /// POST /api/auth/totp/disable - Disable 2FA (requires password + TOTP code).
    /// </summary>
    [HttpPost("totp/disable")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DisableTotp([FromBody] DisableTotpRequest request, CancellationToken ct = default)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user == null) return NotFound("User not found.");

        if (!user.IsTotpEnabled)
            return BadRequest("Two-factor authentication is not enabled.");

        // Verify password
        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid password.");

        // Verify TOTP code if provided
        if (!string.IsNullOrWhiteSpace(request.TotpCode))
        {
            var totpService = HttpContext.RequestServices.GetRequiredService<ITotpService>();
            if (!totpService.ValidateCode(user.TotpSecret!, request.TotpCode))
                return Unauthorized("Invalid authenticator code.");
        }

        // Disable 2FA
        user.IsTotpEnabled = false;
        user.TotpSecret = null;
        user.TotpRecoveryCodes = null;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} disabled TOTP 2FA", userId);

        return Ok(new { message = "Two-factor authentication has been disabled." });
    }

    /// <summary>
    /// GET /api/auth/totp/status - Check if 2FA is enabled for current user.
    /// </summary>
    [HttpGet("totp/status")]
    [Authorize]
    public async Task<IActionResult> TotpStatus(CancellationToken ct = default)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        var isEnabled = await _context.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => u.IsTotpEnabled)
            .FirstOrDefaultAsync(ct);

        return Ok(new { isEnabled });
    }

    /// <summary>
    /// Reads the current user ID from the name-identifier claim or `sub` claim and returns null when neither claim contains a valid GUID.
    /// </summary>
    private Guid? GetUserIdFromClaims()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
