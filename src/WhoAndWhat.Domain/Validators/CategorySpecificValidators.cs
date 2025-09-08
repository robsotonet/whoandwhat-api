using System.Text.RegularExpressions;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Validators;

/// <summary>
/// Contains category-specific validation logic for tasks
/// </summary>
public static class CategorySpecificValidators
{
    /// <summary>
    /// Validates appointment-specific requirements
    /// </summary>
    public static ValidationResult ValidateAppointment(DomainTask task)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Required due date validation
        if (!task.DueDate.HasValue)
        {
            errors.Add("Appointments must have a scheduled date and time");
        }
        else
        {
            // Future date validation
            if (task.DueDate.Value <= DateTime.UtcNow)
            {
                errors.Add("Appointment date must be in the future");
            }
            else if (task.DueDate.Value <= DateTime.UtcNow.AddMinutes(30))
            {
                warnings.Add("Appointment is scheduled very soon - consider buffer time");
            }

            // Business hours validation
            var appointmentHour = task.DueDate.Value.Hour;
            if (appointmentHour < 6 || appointmentHour > 22)
            {
                warnings.Add("Appointment scheduled outside typical business hours");
            }
        }

        // Location/details validation
        if (string.IsNullOrWhiteSpace(task.Description))
        {
            errors.Add("Appointments should include location or meeting details");
        }
        else
        {
            var hasLocationInfo = task.Description.Contains("@") ||
                                  ContainsLocationKeywords(task.Description) ||
                                  ContainsPhonePattern(task.Description) ||
                                  ContainsUrlPattern(task.Description);

            if (!hasLocationInfo)
            {
                warnings.Add("Consider adding location, phone number, or meeting link to appointment details");
            }
        }

        // Title validation
        if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length < 5)
        {
            errors.Add("Appointment title should be descriptive (at least 5 characters)");
        }

        // Priority validation
        if (task.Priority < (int)Priority.Medium)
        {
            warnings.Add("Appointments typically should have medium or higher priority");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            ErrorMessages = errors,
            WarningMessages = warnings
        };
    }

    /// <summary>
    /// Validates bill reminder-specific requirements
    /// </summary>
    public static ValidationResult ValidateBillReminder(DomainTask task)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Required due date validation
        if (!task.DueDate.HasValue)
        {
            errors.Add("Bill reminders must have a payment due date");
        }
        else
        {
            // Past due validation
            if (task.DueDate.Value.Date < DateTime.UtcNow.Date)
            {
                warnings.Add("Bill payment date is in the past - may incur late fees");
            }

            // Weekend validation
            if (task.DueDate.Value.DayOfWeek == DayOfWeek.Saturday ||
                task.DueDate.Value.DayOfWeek == DayOfWeek.Sunday)
            {
                warnings.Add("Bill due date falls on weekend - consider processing earlier");
            }
        }

        // Amount validation
        if (string.IsNullOrWhiteSpace(task.Description))
        {
            errors.Add("Bill reminders should include payment details (amount, account, etc.)");
        }
        else
        {
            var hasAmountInfo = ContainsCurrencyPattern(task.Description) ||
                               ContainsAmountKeywords(task.Description);

            if (!hasAmountInfo)
            {
                warnings.Add("Consider including payment amount in the description");
            }

            // Account/payee validation
            var hasPayeeInfo = ContainsPayeeKeywords(task.Description) ||
                              ContainsAccountPattern(task.Description);

            if (!hasPayeeInfo)
            {
                warnings.Add("Consider including payee or account information");
            }
        }

        // Title validation for bill type
        if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length < 3)
        {
            errors.Add("Bill reminder title should specify which bill (e.g., 'Electric Bill', 'Credit Card Payment')");
        }

        // Priority validation based on amount (if detectable)
        var detectedAmount = ExtractAmountFromDescription(task.Description);
        if (detectedAmount > 500 && task.Priority == (int)Priority.Low)
        {
            warnings.Add("High-value bills should typically have medium or higher priority");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            ErrorMessages = errors,
            WarningMessages = warnings
        };
    }

    /// <summary>
    /// Validates project-specific requirements
    /// </summary>
    public static ValidationResult ValidateProject(DomainTask task)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Description validation
        if (string.IsNullOrWhiteSpace(task.Description))
        {
            errors.Add("Projects must have a detailed description");
        }
        else if (task.Description.Length < 20)
        {
            warnings.Add("Project description is quite brief - consider adding more details about scope and deliverables");
        }
        else
        {
            // Check for project planning elements
            var hasObjectives = ContainsObjectiveKeywords(task.Description);
            var hasTimeline = ContainsTimelineKeywords(task.Description);
            var hasDeliverables = ContainsDeliverablesKeywords(task.Description);

            if (!hasObjectives)
            {
                warnings.Add("Consider adding project objectives or goals to the description");
            }

            if (!hasTimeline && !task.DueDate.HasValue)
            {
                warnings.Add("Projects should have either a due date or timeline information");
            }

            if (!hasDeliverables)
            {
                warnings.Add("Consider specifying expected deliverables or outcomes");
            }
        }

        // Title validation
        if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length < 5)
        {
            errors.Add("Project title should be descriptive and meaningful (at least 5 characters)");
        }

        // Status validation for new projects
        if (task.CreatedAt == task.UpdatedAt && task.Status != (int)DomainTaskStatus.Pending)
        {
            warnings.Add("New projects should typically start with 'Pending' status");
        }

        // Priority validation for complex projects
        if (task.Description?.Length > 200 && task.Priority == (int)Priority.Low)
        {
            warnings.Add("Complex projects should typically have medium or higher priority");
        }

        // Subtask guidance
        if (task.Subtasks == null || !task.Subtasks.Any())
        {
            warnings.Add("Projects benefit from being broken down into subtasks for better tracking");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            ErrorMessages = errors,
            WarningMessages = warnings
        };
    }

    /// <summary>
    /// Validates idea-specific requirements
    /// </summary>
    public static ValidationResult ValidateIdea(DomainTask task)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Title validation
        if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length < 3)
        {
            errors.Add("Ideas should have a meaningful title (at least 3 characters)");
        }

        // Priority validation - ideas shouldn't be urgent
        if (task.Priority >= (int)Priority.High)
        {
            warnings.Add("Ideas typically shouldn't have urgent priority - consider converting to a To-Do or Project if it's actionable");
        }

        // Due date validation - ideas are flexible
        if (task.DueDate.HasValue && task.DueDate.Value <= DateTime.UtcNow.AddDays(1))
        {
            warnings.Add("Ideas with immediate due dates might be better categorized as To-Dos");
        }

        // Elaboration suggestion
        if (string.IsNullOrWhiteSpace(task.Description))
        {
            warnings.Add("Consider adding details about why this idea interests you or potential next steps");
        }
        else if (task.Description.Length > 500)
        {
            warnings.Add("This detailed idea might be ready to convert to a Project");
        }

        // Status validation
        if (task.Status != (int)DomainTaskStatus.Pending && task.Status != (int)DomainTaskStatus.InProgress)
        {
            warnings.Add("Ideas are typically kept in Pending status until they're developed or converted");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            ErrorMessages = errors,
            WarningMessages = warnings
        };
    }

    /// <summary>
    /// Validates to-do specific requirements
    /// </summary>
    public static ValidationResult ValidateToDo(DomainTask task)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Title validation
        if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length < 3)
        {
            errors.Add("To-Do tasks should have meaningful titles (at least 3 characters)");
        }
        else
        {
            // Action-oriented title validation
            var startsWithActionWord = StartsWithActionWord(task.Title);
            if (!startsWithActionWord)
            {
                warnings.Add("To-Do titles are more effective when they start with action words (e.g., 'Call', 'Buy', 'Complete')");
            }
        }

        // Scope validation - suggest conversion if too complex
        if (!string.IsNullOrWhiteSpace(task.Description) && task.Description.Length > 300)
        {
            warnings.Add("This To-Do seems quite complex - consider converting to a Project for better tracking");
        }

        // Subtask validation
        if (task.Subtasks?.Any() == true)
        {
            warnings.Add("To-Dos with subtasks might be better managed as Projects");
        }

        // Priority and deadline consistency
        if (task.Priority >= (int)Priority.High && !task.DueDate.HasValue)
        {
            warnings.Add("High priority tasks should typically have due dates");
        }

        if (task.DueDate.HasValue && task.DueDate.Value < DateTime.UtcNow.AddHours(2) &&
            task.Priority < (int)Priority.High)
        {
            warnings.Add("Tasks due very soon should typically have high priority");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            ErrorMessages = errors,
            WarningMessages = warnings
        };
    }

    /// <summary>
    /// Validates category transition rules
    /// </summary>
    public static ValidationResult ValidateCategoryTransition(DomainTask task, AppTaskCategory fromCategory, AppTaskCategory toCategory)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Use the category's built-in conversion rules
        if (!fromCategory.CanConvertTo(toCategory))
        {
            errors.Add($"Cannot convert {fromCategory.GetDisplayName()} to {toCategory.GetDisplayName()}");
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessages = errors
            };
        }

        // Additional transition-specific validations
        switch ((fromCategory.Name, toCategory.Name))
        {
            case ("Idea", "ToDo"):
                if (string.IsNullOrWhiteSpace(task.Description))
                {
                    warnings.Add("Converting idea to To-Do: consider adding specific action steps");
                }
                break;

            case ("Idea", "Project"):
                if (string.IsNullOrWhiteSpace(task.Description) || task.Description.Length < 50)
                {
                    errors.Add("Converting idea to Project: detailed description is required");
                }
                break;

            case ("ToDo", "Project"):
                if (!task.Subtasks?.Any() == true)
                {
                    warnings.Add("Converting To-Do to Project: consider breaking it down into subtasks");
                }
                break;

            case ("Appointment", "ToDo"):
                if (task.DueDate.HasValue && task.DueDate.Value > DateTime.UtcNow)
                {
                    warnings.Add("Converting future appointment to To-Do: consider if this is intended");
                }
                break;

            case ("BillReminder", "ToDo"):
                if (task.DueDate.HasValue)
                {
                    warnings.Add("Converting bill reminder to To-Do: payment due date will remain as task due date");
                }
                break;
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            ErrorMessages = errors,
            WarningMessages = warnings
        };
    }

    #region Private Helper Methods

    private static bool ContainsLocationKeywords(string text)
    {
        var locationKeywords = new[] { "room", "building", "office", "address", "street", "location", "venue", "at ", "in " };
        return locationKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsPhonePattern(string text)
    {
        var phonePattern = @"(\+?1[-.\s]?)?\(?[0-9]{3}\)?[-.\s]?[0-9]{3}[-.\s]?[0-9]{4}";
        return Regex.IsMatch(text, phonePattern);
    }

    private static bool ContainsUrlPattern(string text)
    {
        var urlPattern = @"https?://[^\s]+|[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
        return Regex.IsMatch(text, urlPattern);
    }

    private static bool ContainsCurrencyPattern(string text)
    {
        var currencyPattern = @"\$\d+(?:\.\d{2})?|\d+(?:\.\d{2})?\s*(?:dollars?|USD|usd)";
        return Regex.IsMatch(text, currencyPattern, RegexOptions.IgnoreCase);
    }

    private static bool ContainsAmountKeywords(string text)
    {
        var amountKeywords = new[] { "amount", "total", "cost", "price", "fee", "charge", "bill", "payment" };
        return amountKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsPayeeKeywords(string text)
    {
        var payeeKeywords = new[] { "pay to", "payee", "company", "bank", "account", "vendor", "utility" };
        return payeeKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAccountPattern(string text)
    {
        var accountPattern = @"account\s*#?\s*\d+|acct\s*#?\s*\d+";
        return Regex.IsMatch(text, accountPattern, RegexOptions.IgnoreCase);
    }

    private static decimal ExtractAmountFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return 0;
        }

        var amountPattern = @"\$(\d+(?:\.\d{2})?)";
        var match = Regex.Match(description, amountPattern);

        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var amount))
        {
            return amount;
        }

        return 0;
    }

    private static bool ContainsObjectiveKeywords(string text)
    {
        var objectiveKeywords = new[] { "goal", "objective", "purpose", "aim", "target", "deliver", "achieve" };
        return objectiveKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTimelineKeywords(string text)
    {
        var timelineKeywords = new[] { "timeline", "schedule", "milestone", "phase", "deadline", "by ", "week", "month" };
        return timelineKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsDeliverablesKeywords(string text)
    {
        var deliverableKeywords = new[] { "deliverable", "outcome", "result", "output", "produce", "create", "complete" };
        return deliverableKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool StartsWithActionWord(string title)
    {
        var actionWords = new[]
        {
            "call", "buy", "complete", "finish", "start", "begin", "create", "make", "build",
            "write", "send", "email", "text", "contact", "meet", "schedule", "book",
            "review", "check", "verify", "confirm", "update", "fix", "repair",
            "clean", "organize", "plan", "prepare", "research", "study", "learn",
            "pay", "submit", "file", "sign", "order", "purchase", "get", "pick up"
        };

        var firstWord = title.Split(' ')[0].ToLowerInvariant().TrimEnd(',', ':', '-');
        return actionWords.Contains(firstWord);
    }

    #endregion
}

/// <summary>
/// Enhanced validation result with warnings
/// </summary>
public class ValidationResult : WhoAndWhat.Domain.Common.ValidationResult
{

    public string GetAllMessages()
    {
        var messages = new List<string>();

        if (ErrorMessages.Any())
        {
            messages.Add("Errors: " + string.Join(", ", ErrorMessages));
        }

        if (WarningMessages.Any())
        {
            messages.Add("Warnings: " + string.Join(", ", WarningMessages));
        }

        return string.Join(" | ", messages);
    }
}
