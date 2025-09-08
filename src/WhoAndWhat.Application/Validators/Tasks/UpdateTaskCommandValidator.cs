using FluentValidation;
using WhoAndWhat.Application.Features.Tasks.Commands.UpdateTask;
using WhoAndWhat.Domain.ValueObjects;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Application.Validators.Tasks;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty()
            .WithMessage("Task ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.Title)
            .Length(3, 200)
            .WithMessage("Task title must be between 3 and 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("Task description cannot exceed 2000 characters")
            .When(x => x.Description != null);

        RuleFor(x => x.Category)
            .Must(BeValidCategory)
            .WithMessage("Invalid task category")
            .When(x => x.Category.HasValue);

        RuleFor(x => x.Status)
            .Must(BeValidStatus)
            .WithMessage("Invalid task status")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.Priority)
            .Must(BeValidPriority)
            .WithMessage("Invalid task priority")
            .When(x => x.Priority.HasValue);

        RuleFor(x => x.DueDate)
            .Must(BeValidDueDate)
            .WithMessage("Due date must be in the future")
            .When(x => x.DueDate.HasValue);

        RuleFor(x => x.ContactIds)
            .Must(x => x!.Count <= 20)
            .WithMessage("Cannot assign more than 20 contacts to a task")
            .When(x => x.ContactIds?.Any() == true);
    }

    private static bool BeValidCategory(int? category)
    {
        if (!category.HasValue) return true;
        
        try
        {
            TaskCategory.FromValue(category.Value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidStatus(int? status)
    {
        if (!status.HasValue) return true;
        
        try
        {
            DomainTaskStatus.FromValue(status.Value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidPriority(int? priority)
    {
        if (!priority.HasValue) return true;
        
        try
        {
            Priority.FromValue(priority.Value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidDueDate(DateTime? dueDate)
    {
        return !dueDate.HasValue || dueDate.Value > DateTime.UtcNow;
    }
}