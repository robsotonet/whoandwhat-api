using FluentValidation;
using WhoAndWhat.Application.Features.Tasks.Commands.CreateTask;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Validators.Tasks;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Task title is required")
            .Length(3, 200)
            .WithMessage("Task title must be between 3 and 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("Task description cannot exceed 2000 characters");

        RuleFor(x => x.Category)
            .Must(BeValidCategory)
            .WithMessage("Invalid task category");

        RuleFor(x => x.Priority)
            .Must(BeValidPriority)
            .WithMessage("Invalid task priority");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.DueDate)
            .Must(BeValidDueDate)
            .WithMessage("Due date must be in the future")
            .When(x => x.DueDate.HasValue);

        RuleFor(x => x.ContactIds)
            .Must(x => x.Count <= 20)
            .WithMessage("Cannot assign more than 20 contacts to a task")
            .When(x => x.ContactIds.Any());

        // Category-specific validations
        RuleFor(x => x)
            .Must(HaveDueDateForAppointments)
            .WithMessage("Appointments must have a due date")
            .When(x => x.Category == (int)TaskCategory.Appointment);

        RuleFor(x => x)
            .Must(HaveDueDateForBillReminders)
            .WithMessage("Bill reminders must have a due date")
            .When(x => x.Category == (int)TaskCategory.BillReminder);
    }

    private static bool BeValidCategory(int category)
    {
        try
        {
            TaskCategory.FromValue(category);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidPriority(int priority)
    {
        try
        {
            Priority.FromValue(priority);
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

    private static bool HaveDueDateForAppointments(CreateTaskCommand command)
    {
        return command.Category != (int)TaskCategory.Appointment || command.DueDate.HasValue;
    }

    private static bool HaveDueDateForBillReminders(CreateTaskCommand command)
    {
        return command.Category != (int)TaskCategory.BillReminder || command.DueDate.HasValue;
    }
}