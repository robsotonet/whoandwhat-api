using FluentValidation.TestHelper;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;

namespace WhoAndWhat.Domain.Tests;

public class ContactValidatorTests
{
    private readonly ContactValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var contact = new Contact { Name = string.Empty };
        var result = _validator.TestValidate(contact);
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Name_Is_Specified()
    {
        var contact = new Contact { Name = "Test Contact" };
        var result = _validator.TestValidate(contact);
        result.ShouldNotHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Invalid()
    {
        var contact = new Contact { Name = "Test Contact", Email = "invalid-email" };
        var result = _validator.TestValidate(contact);
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Email_Is_Valid()
    {
        var contact = new Contact { Name = "Test Contact", Email = "valid@email.com" };
        var result = _validator.TestValidate(contact);
        result.ShouldNotHaveValidationErrorFor(c => c.Email);
    }
}
