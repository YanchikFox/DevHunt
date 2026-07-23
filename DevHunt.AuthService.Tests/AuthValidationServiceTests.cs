using DevHunt.AuthService.Services;
using FluentAssertions;
using Xunit;

namespace DevHunt.AuthService.Tests;

public class AuthValidationServiceTests
{
    private readonly AuthValidationService _service = new();

    [Fact]
    public void NormalizeEmail_ShouldTrimAndLowercase()
    {
        var normalized = _service.NormalizeEmail("  User@Example.COM  ");
        normalized.Should().Be("user@example.com");
    }

    [Fact]
    public void NormalizeEmail_WithNull_ShouldReturnEmptyString()
    {
        var normalized = _service.NormalizeEmail(null);
        normalized.Should().Be(string.Empty);
    }

    [Fact]
    public void IsValidEmail_ShouldValidateExpectedFormats()
    {
        _service.IsValidEmail("valid.user@example.com").Should().BeTrue();
        _service.IsValidEmail("").Should().BeFalse();
        _service.IsValidEmail("not-an-email").Should().BeFalse();
    }

    [Fact]
    public void ValidatePasswordWithDetails_WithStrongPassword_ShouldBeValid()
    {
        var result = _service.ValidatePasswordWithDetails("StrongPass1!");

        result.IsValid.Should().BeTrue();
        result.HasMinLength.Should().BeTrue();
        result.HasMaxLength.Should().BeTrue();
        result.HasUppercase.Should().BeTrue();
        result.HasLowercase.Should().BeTrue();
        result.HasDigit.Should().BeTrue();
        result.HasSpecialChar.Should().BeTrue();
    }

    [Fact]
    public void ValidatePasswordWithDetails_WithWeakPassword_ShouldReturnRequirementBreakdown()
    {
        var result = _service.ValidatePasswordWithDetails("short");

        result.IsValid.Should().BeFalse();
        result.HasMinLength.Should().BeFalse();
        result.HasUppercase.Should().BeFalse();
        result.HasDigit.Should().BeFalse();
        result.HasSpecialChar.Should().BeFalse();
        result.HasLowercase.Should().BeTrue();
    }

    [Fact]
    public void IsValidPassword_WithWhitespace_ShouldBeFalse()
    {
        _service.IsValidPassword("   ").Should().BeFalse();
    }
}
