using System.ComponentModel.DataAnnotations;

namespace DevHunt.CoreApi.Security;

/// <summary>
/// Validation attribute for sanitizing HTML content (SEC-007).
/// NOTE: SQL injection protection removed - EF Core uses parameterized queries by default.
/// This attribute only sanitizes dangerous HTML/JavaScript.
/// </summary>
public class SafeHtmlAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            var sanitized = SecurityHelpers.SanitizeHtml(str);
            if (sanitized != str)
                return new ValidationResult("Zawiera niedozwoloną treść.");
        }
        return ValidationResult.Success;
    }
}

public class MaxLengthSafeAttribute : ValidationAttribute
{
    private readonly int _maxLength;

    public MaxLengthSafeAttribute(int maxLength) => _maxLength = maxLength;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string str && str.Length > _maxLength)
        {
            return new ValidationResult($"Długość nie może przekraczać {_maxLength} znaków.");
        }

        return ValidationResult.Success;
    }
}

public class ValidUrlAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string url && !string.IsNullOrWhiteSpace(url) && !SecurityHelpers.IsValidUrl(url))
        {
            return new ValidationResult("Nieprawidłowy URL.");
        }

        return ValidationResult.Success;
    }
}
