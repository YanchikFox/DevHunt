using Microsoft.AspNetCore.Hosting;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Token-related services grouped together.
/// </summary>
public interface ITokenServices
{
    /// <summary>JWT token service.</summary>
    IJwtTokenService Jwt { get; }

    /// <summary>Refresh token service.</summary>
    IRefreshTokenService RefreshToken { get; }

    /// <summary>Cookie service for token storage.</summary>
    IAuthCookieService Cookie { get; }
}

/// <summary>
/// User authentication services grouped together.
/// </summary>
public interface IUserAuthServices
{
    /// <summary>Registration service.</summary>
    IRegistrationService Registration { get; }

    /// <summary>Login service.</summary>
    ILoginService Login { get; }

    /// <summary>Validation service.</summary>
    IAuthValidationService Validation { get; }
}

/// <summary>
/// Facade that groups authentication-related services to reduce constructor over-injection.
/// Uses sub-groups to keep individual parameter counts low.
/// </summary>
public interface IAuthServices
{
    /// <summary>OAuth provider service.</summary>
    IOAuthService OAuth { get; }

    /// <summary>Token-related services.</summary>
    ITokenServices Tokens { get; }

    /// <summary>User authentication services.</summary>
    IUserAuthServices User { get; }

    /// <summary>Email service for sending verification/reset emails.</summary>
    IEmailService Email { get; }

    /// <summary>Environment info for dev/prod checks.</summary>
    IWebHostEnvironment Environment { get; }
    
    // Convenience accessors for backward compatibility
    /// <summary>JWT token service (shortcut).</summary>
    IJwtTokenService Jwt => Tokens.Jwt;
    
    /// <summary>Cookie service (shortcut).</summary>
    IAuthCookieService Cookie => Tokens.Cookie;
    
    /// <summary>Validation service (shortcut).</summary>
    IAuthValidationService Validation => User.Validation;
    
    /// <summary>Registration service (shortcut).</summary>
    IRegistrationService Registration => User.Registration;
    
    /// <summary>Login service (shortcut).</summary>
    ILoginService Login => User.Login;
}

/// <summary>
/// Implementation of ITokenServices.
/// </summary>
public class TokenServices : ITokenServices
{
    /// <summary>
    /// JWT access-token generator.
    /// </summary>
    public IJwtTokenService Jwt { get; }
    /// <summary>
    /// Refresh-token creation, validation, and revocation service.
    /// </summary>
    public IRefreshTokenService RefreshToken { get; }
    /// <summary>
    /// HttpOnly authentication-cookie writer.
    /// </summary>
    public IAuthCookieService Cookie { get; }

    /// <summary>
    /// Groups token services for injection into authentication endpoints.
    /// </summary>
    public TokenServices(
        IJwtTokenService jwt,
        IRefreshTokenService refreshToken,
        IAuthCookieService cookie)
    {
        Jwt = jwt;
        RefreshToken = refreshToken;
        Cookie = cookie;
    }
}

/// <summary>
/// Implementation of IUserAuthServices.
/// </summary>
public class UserAuthServices : IUserAuthServices
{
    /// <summary>
    /// Registration validation and account creation service.
    /// </summary>
    public IRegistrationService Registration { get; }
    /// <summary>
    /// Password-login authentication service.
    /// </summary>
    public ILoginService Login { get; }
    /// <summary>
    /// Shared email and password validation service.
    /// </summary>
    public IAuthValidationService Validation { get; }

    /// <summary>
    /// Groups user registration, login, and validation services for facade injection.
    /// </summary>
    public UserAuthServices(
        IRegistrationService registration,
        ILoginService login,
        IAuthValidationService validation)
    {
        Registration = registration;
        Login = login;
        Validation = validation;
    }
}

/// <summary>
/// Implementation of IAuthServices facade with sub-groups.
/// </summary>
public class AuthServicesFacade : IAuthServices
{
    /// <summary>
    /// OAuth provider flow service.
    /// </summary>
    public IOAuthService OAuth { get; }
    /// <summary>
    /// Token and cookie services used by authentication flows.
    /// </summary>
    public ITokenServices Tokens { get; }
    /// <summary>
    /// Registration, login, and validation services used by user-authentication flows.
    /// </summary>
    public IUserAuthServices User { get; }
    /// <summary>
    /// Email sender for verification and password-reset messages.
    /// </summary>
    public IEmailService Email { get; }
    /// <summary>
    /// Hosting environment used for development and production auth behavior.
    /// </summary>
    public IWebHostEnvironment Environment { get; }

    /// <summary>
    /// Creates a facade over OAuth, token, user-authentication, email, and environment services.
    /// </summary>
    public AuthServicesFacade(
        IOAuthService oAuth,
        ITokenServices tokens,
        IUserAuthServices user,
        IEmailService email,
        IWebHostEnvironment environment)
    {
        OAuth = oAuth;
        Tokens = tokens;
        User = user;
        Email = email;
        Environment = environment;
    }
}
