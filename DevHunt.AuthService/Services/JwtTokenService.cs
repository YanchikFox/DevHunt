using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DevHunt.Infrastructure.Models;
using Microsoft.IdentityModel.Tokens;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Service for JWT token generation and validation.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Generates a JWT access token for the user.</summary>
    string GenerateAccessToken(User user);
}

/// <summary>
/// Implementation of JWT token service.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    /// <summary>
    /// Stores JWT configuration used for signing, issuer, and audience values.
    /// </summary>
    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    /// <inheritdoc />
    public string GenerateAccessToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Access token: short-lived (30 minutes)
        var accessTokenExpiry = DateTime.UtcNow.AddMinutes(30);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("role", user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: accessTokenExpiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
