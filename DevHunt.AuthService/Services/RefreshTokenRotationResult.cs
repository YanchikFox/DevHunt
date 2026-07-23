using DevHunt.Infrastructure.Models;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Result of a successful refresh-token rotation.
/// </summary>
/// <param name="User">The user that owns the rotated session.</param>
/// <param name="NewRefreshTokenPlaintext">New refresh token to return to the client.</param>
public sealed record RefreshTokenRotationResult(User User, string NewRefreshTokenPlaintext);
