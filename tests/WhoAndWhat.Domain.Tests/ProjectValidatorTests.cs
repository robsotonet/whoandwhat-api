using FluentValidation.TestHelper;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;

namespace WhoAndWhat.Domain.Tests;

public class ProjectValidatorTests
{
    private readonly ProjectValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var project = new Project { Name = string.Empty };
        var result = _validator.TestValidate(project);
        result.ShouldHaveValidationErrorFor(p => p.Name);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Name_Is_Specified()
    {
        var project = new Project { Name = "Test Project" };
        var result = _validator.TestValidate(project);
        result.ShouldNotHaveValidationErrorFor(p => p.Name);
    }
}
