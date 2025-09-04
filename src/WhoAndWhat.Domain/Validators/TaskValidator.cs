using FluentValidation;
using WhoAndWhat.Domain.Entities;
using Task = WhoAndWhat.Domain.Entities.Task;

namespace WhoAndWhat.Domain.Validators;

public class TaskValidator : AbstractValidator<Task>
{
    public TaskValidator()
    {
        RuleFor(task => task.Title).NotEmpty().MaximumLength(100);
        RuleFor(task => task.Description).MaximumLength(500);
    }
}