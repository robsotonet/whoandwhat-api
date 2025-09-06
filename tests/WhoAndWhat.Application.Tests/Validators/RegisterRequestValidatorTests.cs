using FluentAssertions;
using FluentValidation.TestHelper;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Validators;
using Xunit;

namespace WhoAndWhat.Application.Tests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator;

    public RegisterRequestValidatorTests()
    {
        _validator = new RegisterRequestValidator();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Request_Is_Valid()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_Have_Error_When_Email_Is_Empty_Or_Null(string? email)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = email,
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test.example.com")]
    public void Should_Have_Error_When_Email_Format_Is_Invalid(string email)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = email,
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Valid email address is required");
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Too_Long()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@example.com"; // 263 characters total
        var request = new RegisterRequest
        {
            Email = longEmail,
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must not exceed 254 characters");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_Have_Error_When_Username_Is_Empty_Or_Null(string? username)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = username,
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage("Username is required");
    }

    [Fact]
    public void Should_Have_Error_When_Username_Is_Too_Short()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "ab", // Only 2 characters
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage("Username must be at least 3 characters long");
    }

    [Fact]
    public void Should_Have_Error_When_Username_Is_Too_Long()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = new string('a', 51), // 51 characters
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage("Username must not exceed 50 characters");
    }

    [Theory]
    [InlineData("user name")] // Space
    [InlineData("user@name")] // @
    [InlineData("user#name")] // #
    [InlineData("user$name")] // $
    [InlineData("user%name")] // %
    public void Should_Have_Error_When_Username_Contains_Invalid_Characters(string username)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = username,
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage("Username can only contain letters, numbers, dots, hyphens, and underscores");
    }

    [Theory]
    [InlineData("user_name")] // Underscore
    [InlineData("user-name")] // Hyphen
    [InlineData("user.name")] // Dot
    [InlineData("username123")] // Numbers
    [InlineData("USERNAME")] // Uppercase
    public void Should_Not_Have_Error_When_Username_Contains_Valid_Characters(string username)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = username,
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_Have_Error_When_Password_Is_Empty_Or_Null(string? password)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = password,
            ConfirmPassword = password,
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required");
    }

    [Fact]
    public void Should_Have_Error_When_Password_Is_Too_Short()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "Short1!", // Only 7 characters
            ConfirmPassword = "Short1!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters long");
    }

    [Fact]
    public void Should_Have_Error_When_Password_Is_Too_Long()
    {
        // Arrange
        var longPassword = new string('a', 101); // 101 characters
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = longPassword,
            ConfirmPassword = longPassword,
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must not exceed 100 characters");
    }

    [Theory]
    [InlineData("testpassword123")] // No uppercase
    [InlineData("TESTPASSWORD123")] // No lowercase
    [InlineData("TestPassword")] // No digit
    public void Should_Have_Error_When_Password_Does_Not_Meet_Complexity_Requirements(string password)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = password,
            ConfirmPassword = password,
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter, one uppercase letter, and one digit");
    }

    [Fact]
    public void Should_Have_Error_When_Passwords_Do_Not_Match()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "DifferentPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage("Password confirmation does not match password");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_Have_Error_When_PreferredLanguage_Is_Empty_Or_Null(string? language)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = language,
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PreferredLanguage)
            .WithErrorMessage("Preferred language is required");
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("invalid")]
    [InlineData("123")]
    public void Should_Have_Error_When_PreferredLanguage_Is_Invalid(string language)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = language,
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PreferredLanguage)
            .WithErrorMessage("Invalid language. Supported languages are: en, es");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("EN")]
    [InlineData("ES")]
    public void Should_Not_Have_Error_When_PreferredLanguage_Is_Valid(string language)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = language,
            AcceptTerms = true
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.PreferredLanguage);
    }

    [Fact]
    public void Should_Have_Error_When_AcceptTerms_Is_False()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
            PreferredLanguage = "en",
            AcceptTerms = false
        };

        // Act & Assert
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.AcceptTerms)
            .WithErrorMessage("You must accept the terms and conditions to register");
    }

    [Fact]
    public void Should_Have_Multiple_Errors_When_Multiple_Fields_Are_Invalid()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "invalid-email",
            Username = "a", // Too short
            Password = "weak", // Too short, no uppercase, no digit
            ConfirmPassword = "different",
            PreferredLanguage = "invalid",
            AcceptTerms = false
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Username);
        result.ShouldHaveValidationErrorFor(x => x.Password);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
        result.ShouldHaveValidationErrorFor(x => x.PreferredLanguage);
        result.ShouldHaveValidationErrorFor(x => x.AcceptTerms);
    }
}