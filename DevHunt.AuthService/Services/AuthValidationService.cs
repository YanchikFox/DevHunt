using DevHunt.AuthService.Models;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Service for authentication-related validations.
/// </summary>
public interface IAuthValidationService
{
    /// <summary>Validates email format.</summary>
    bool IsValidEmail(string email);

    /// <summary>Validates password complexity.</summary>
    bool IsValidPassword(string password);

    /// <summary>Normalizes email to lowercase trimmed format.</summary>
    string NormalizeEmail(string? email);

    /// <summary>Validates password with detailed results.</summary>
    PasswordValidationResult ValidatePasswordWithDetails(string password);
}

/// <summary>
/// Implementation of authentication validation service.
/// </summary>
public class AuthValidationService : IAuthValidationService
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;
    private const string SpecialCharacters = "@$!%*?&";

    /// <inheritdoc />
    public string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <inheritdoc />
    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsValidPassword(string password)
    {
        var result = ValidatePasswordWithDetails(password);
        return result.IsValid;
    }

    /// <inheritdoc />
    public PasswordValidationResult ValidatePasswordWithDetails(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return PasswordValidationResult.Invalid;

        var requirements = new[]
        {
            new PasswordRequirement("MinLength", password.Length >= MinPasswordLength, "At least 8 characters"),
            new PasswordRequirement("MaxLength", password.Length <= MaxPasswordLength, "At most 128 characters"),
            new PasswordRequirement("Uppercase", password.Any(char.IsUpper), "Contains uppercase letter"),
            new PasswordRequirement("Lowercase", password.Any(char.IsLower), "Contains lowercase letter"),
            new PasswordRequirement("Digit", password.Any(char.IsDigit), "Contains digit"),
            new PasswordRequirement("SpecialChar", password.Any(c => SpecialCharacters.Contains(c)), "Contains special character")
        };

        return new PasswordValidationResult(requirements);
    }
}
