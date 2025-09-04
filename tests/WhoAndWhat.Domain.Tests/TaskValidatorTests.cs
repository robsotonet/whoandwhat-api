using FluentValidation.TestHelper;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;
using Task = WhoAndWhat.Domain.Entities.Task;

namespace WhoAndWhat.Domain.Tests;

public class TaskValidatorTests
{
    private readonly TaskValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Title_Is_Empty()
    {
        var task = new Task { Title = string.Empty };
        var result = _validator.TestValidate(task);
        result.ShouldHaveValidationErrorFor(t => t.Title);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Title_Is_Specified()
    {
        var task = new Task { Title = "Test Task" };
        var result = _validator.TestValidate(task);
        result.ShouldNotHaveValidationErrorFor(t => t.Title);
    }

    [Fact]
    public void Should_Have_Error_When_Title_Exceeds_Maximum_Length()
    {
        var longTitle = new string('a', 101); // 101 characters, exceeds 100 character limit
        var task = new Task { Title = longTitle };
        var result = _validator.TestValidate(task);
        result.ShouldHaveValidationErrorFor(t => t.Title);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Title_Is_At_Maximum_Length()
    {
        var maxLengthTitle = new string('a', 100); // Exactly 100 characters
        var task = new Task { Title = maxLengthTitle };
        var result = _validator.TestValidate(task);
        result.ShouldNotHaveValidationErrorFor(t => t.Title);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Empty()
    {
        var task = new Task { Title = "Test Task", Description = string.Empty };
        var result = _validator.TestValidate(task);
        result.ShouldNotHaveValidationErrorFor(t => t.Description);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Null()
    {
        var task = new Task { Title = "Test Task", Description = null };
        var result = _validator.TestValidate(task);
        result.ShouldNotHaveValidationErrorFor(t => t.Description);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Valid()
    {
        var task = new Task { Title = "Test Task", Description = "Valid description" };
        var result = _validator.TestValidate(task);
        result.ShouldNotHaveValidationErrorFor(t => t.Description);
    }

    [Fact]
    public void Should_Have_Error_When_Description_Exceeds_Maximum_Length()
    {
        var longDescription = new string('a', 501); // 501 characters, exceeds 500 character limit
        var task = new Task { Title = "Test Task", Description = longDescription };
        var result = _validator.TestValidate(task);
        result.ShouldHaveValidationErrorFor(t => t.Description);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_At_Maximum_Length()
    {
        var maxLengthDescription = new string('a', 500); // Exactly 500 characters
        var task = new Task { Title = "Test Task", Description = maxLengthDescription };
        var result = _validator.TestValidate(task);
        result.ShouldNotHaveValidationErrorFor(t => t.Description);
    }
}