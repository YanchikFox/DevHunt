namespace DevHunt.AuthService.Models;

/// <summary>User registration request.</summary>
/// <param name="Email">User's email address (will be normalized to lowercase).</param>
/// <param name="Password">Password (min 8 chars, must include upper, lower, number, special char).</param>
/// <param name="FullName">User's display name (min 2 chars).</param>
/// <param name="Username">Optional unique handle (3-50 chars, letters/digits/underscore/hyphen/dot).</param>
public record RegisterRequest(string Email, string Password, string FullName, string? Username = null);

/// <summary>Login request.</summary>
/// <param name="Email">User's email address.</param>
/// <param name="Password">User's password.</param>
public record LoginRequest(string Email, string Password);

/// <summary>Authentication response with tokens.</summary>
/// <param name="AccessToken">JWT access token (30 min expiry).</param>
/// <param name="RefreshToken">Refresh token for obtaining new access tokens (7 day expiry).</param>
/// <param name="Email">User's email address.</param>
/// <param name="UserId">User's unique identifier.</param>
public record AuthResponse(string AccessToken, string RefreshToken, string Email, Guid UserId);

/// <summary>Request to refresh access token.</summary>
/// <param name="RefreshToken">Current valid refresh token.</param>
public record RefreshTokenRequest(string RefreshToken);

/// <summary>One-time OAuth exchange code redemption request.</summary>
/// <param name="Code">Short-lived code from the OAuth callback redirect query string.</param>
public record OAuthExchangeRequest(string Code);

/// <summary>Email verification request.</summary>
/// <param name="UserId">User's ID.</param>
/// <param name="Code">6-digit verification code from email.</param>
public record VerifyEmailRequest(Guid UserId, string Code);

/// <summary>Request to resend verification email.</summary>
/// <param name="Email">User's email address.</param>
public record ResendVerificationRequest(string Email);

/// <summary>Forgot password request.</summary>
/// <param name="Email">User's email address.</param>
public record ForgotPasswordRequest(string Email);

/// <summary>Password reset request.</summary>
/// <param name="Token">Reset token from email.</param>
/// <param name="NewPassword">New password (must meet complexity requirements).</param>
public record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>OAuth user info from external provider.</summary>
/// <param name="Email">User's email from provider.</param>
/// <param name="ProviderUserId">User's ID at the provider.</param>
/// <param name="FullName">User's display name from provider.</param>
/// <param name="SuggestedUsername">Provider's username login (e.g. GitHub login) to pre-populate DevHunt username.</param>
public record OAuthUserInfo(string Email, string ProviderUserId, string? FullName, string? SuggestedUsername = null);

/// <summary>GitHub email object from API.</summary>
internal record GitHubEmail(string Email, bool Primary, bool Verified);

// ========================================================================
// Two-Factor Authentication DTOs
// ========================================================================

/// <summary>Login request with optional TOTP code for 2FA.</summary>
/// <param name="Email">User's email address.</param>
/// <param name="Password">User's password.</param>
/// <param name="TotpCode">6-digit TOTP code (required when 2FA is enabled).</param>
/// <param name="RecoveryCode">Recovery code (alternative to TOTP code).</param>
public record LoginWith2faRequest(string Email, string Password, string? TotpCode = null, string? RecoveryCode = null);

/// <summary>Response when login requires 2FA verification.</summary>
/// <param name="RequiresTwoFactor">Always true.</param>
/// <param name="UserId">User ID for the 2FA verification step.</param>
public record TwoFactorRequiredResponse(bool RequiresTwoFactor, Guid UserId);

/// <summary>TOTP setup initiation response.</summary>
/// <param name="Secret">Base32-encoded TOTP secret.</param>
/// <param name="QrUri">otpauth:// URI for QR code generation.</param>
public record TotpSetupResponse(string Secret, string QrUri);

/// <summary>Request to verify TOTP setup with a code from authenticator app.</summary>
/// <param name="Code">6-digit code from authenticator.</param>
/// <param name="Secret">Secret from setup step (to verify it matches).</param>
public record VerifyTotpSetupRequest(string Code, string Secret);

/// <summary>Response after successful TOTP setup with recovery codes.</summary>
/// <param name="RecoveryCodes">One-time recovery codes. Store safely!</param>
public record TotpSetupCompleteResponse(List<string> RecoveryCodes);

/// <summary>Request to disable 2FA.</summary>
/// <param name="Password">Current password for confirmation.</param>
/// <param name="TotpCode">Current TOTP code for confirmation.</param>
public record DisableTotpRequest(string Password, string? TotpCode = null);
