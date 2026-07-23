namespace DevHunt.AuthService.Models;

/// <summary>
/// Individual password requirement check.
/// </summary>
public record PasswordRequirement(string Name, bool IsMet, string Description);

/// <summary>
/// Password validation result with requirement breakdown.
/// </summary>
public class PasswordValidationResult
{
    /// <summary>Whether all requirements are met.</summary>
    public bool IsValid => Requirements.All(r => r.IsMet);
    
    /// <summary>Individual requirements.</summary>
    public IReadOnlyList<PasswordRequirement> Requirements { get; }

    public PasswordValidationResult(IEnumerable<PasswordRequirement> requirements)
    {
        Requirements = requirements.ToList().AsReadOnly();
    }

    // Convenience accessors
    public bool HasMinLength => GetRequirement("MinLength")?.IsMet ?? false;
    public bool HasMaxLength => GetRequirement("MaxLength")?.IsMet ?? false;
    public bool HasUppercase => GetRequirement("Uppercase")?.IsMet ?? false;
    public bool HasLowercase => GetRequirement("Lowercase")?.IsMet ?? false;
    public bool HasDigit => GetRequirement("Digit")?.IsMet ?? false;
    public bool HasSpecialChar => GetRequirement("SpecialChar")?.IsMet ?? false;

    private PasswordRequirement? GetRequirement(string name)
        => Requirements.FirstOrDefault(r => r.Name == name);

    /// <summary>Creates an invalid result with all requirements failed.</summary>
    public static PasswordValidationResult Invalid => new(new[]
    {
        new PasswordRequirement("MinLength", false, "At least 8 characters"),
        new PasswordRequirement("MaxLength", true, "At most 128 characters"),
        new PasswordRequirement("Uppercase", false, "Contains uppercase letter"),
        new PasswordRequirement("Lowercase", false, "Contains lowercase letter"),
        new PasswordRequirement("Digit", false, "Contains digit"),
        new PasswordRequirement("SpecialChar", false, "Contains special character")
    });
}
