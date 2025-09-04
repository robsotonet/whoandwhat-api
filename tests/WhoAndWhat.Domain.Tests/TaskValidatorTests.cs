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
}