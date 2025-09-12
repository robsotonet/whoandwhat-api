using FluentValidation.TestHelper;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Tests;

public class UserValidatorTests
{
    private readonly UserValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Username_Is_Empty()
    {
        var user = new User("test@test.com", string.Empty, Language.en);
        var result = _validator.TestValidate(user);
        result.ShouldHaveValidationErrorFor(u => u.Username);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Username_Is_Specified()
    {
        var user = new User("test@test.com", "testuser", Language.en);
        var result = _validator.TestValidate(user);
        result.ShouldNotHaveValidationErrorFor(u => u.Username);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        var user = new User(string.Empty, "testuser", Language.en);
        var result = _validator.TestValidate(user);
        result.ShouldHaveValidationErrorFor(u => u.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Invalid()
    {
        var user = new User("invalid-email", "testuser", Language.en);
        var result = _validator.TestValidate(user);
        result.ShouldHaveValidationErrorFor(u => u.Email);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Email_Is_Valid()
    {
        var user = new User("valid@email.com", "testuser", Language.en);
        var result = _validator.TestValidate(user);
        result.ShouldNotHaveValidationErrorFor(u => u.Email);
    }
}
