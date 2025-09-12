using FluentValidation;
using WhoAndWhat.Domain.Entities;
using AppTask = WhoAndWhat.Domain.Entities.AppTask;

namespace WhoAndWhat.Domain.Validators;

public class AppTaskValidator : AbstractValidator<AppTask>
{
    public AppTaskValidator()
    {
        RuleFor(task => task.Title).NotEmpty().MaximumLength(100);
        RuleFor(task => task.Description).MaximumLength(500);
    }
}
