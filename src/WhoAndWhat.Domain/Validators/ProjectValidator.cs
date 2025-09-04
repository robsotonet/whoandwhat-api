using FluentValidation;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Validators;

public class ProjectValidator : AbstractValidator<Project>
{
    public ProjectValidator()
    {
        RuleFor(project => project.Name).NotEmpty().MaximumLength(100);
        RuleFor(project => project.Description).MaximumLength(1000);
    }
}
