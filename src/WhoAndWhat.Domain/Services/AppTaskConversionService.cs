using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service for handling task conversions and transformations
/// </summary>
public class AppTaskConversionService
{
    /// <summary>
    /// Converts a task to a project with validation and business rules
    /// </summary>
    /// <param name="task">AppTask to convert</param>
    /// <param name="subtasks">Existing subtasks to associate with the new project</param>
    /// <returns>Validation result and converted project task</returns>
    public (ValidationResult ValidationResult, DomainTask? ConvertedProject) ConvertTaskToProject(DomainTask task, IEnumerable<DomainTask>? subtasks = null)
    {
        var validationResult = ValidateTaskToProjectConversion(task, subtasks);
        if (!validationResult.IsValid)
        {
            return (validationResult, null);
        }

        // Create the converted project task
        var convertedAppTask = new DomainTask
        {
            Id = task.Id,
            Title = task.Title,
            Description = EnhanceDescriptionForProject(task.Description, task),
            DueDate = task.DueDate,
            Priority = task.Priority,
            Category = AppTaskCategory.Project.Value,
            Status = task.Status,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = task.IsDeleted,
            UserId = task.UserId,
            ProjectId = null, // Projects cannot be part of other projects
            Subtasks = subtasks?.ToList() ?? new List<DomainTask>()
        };

        return (ValidationResult.Success(), convertedAppTask);
    }

    /// <summary>
    /// Validates if a task can be converted to a project
    /// </summary>
    private ValidationResult ValidateTaskToProjectConversion(DomainTask task, IEnumerable<DomainTask>? subtasks = null)
    {
        var errors = new List<string>();
        var currentStatus = DomainTaskStatus.FromValue(task.Status);
        var currentCategory = AppTaskCategory.FromValue(task.Category);

        // Use the task's own business logic
        if (!task.CanConvertToProject())
        {
            errors.Add("AppTask cannot be converted to project based on current state");
        }

        // Additional validation for specific scenarios
        if (currentCategory == AppTaskCategory.Appointment)
        {
            errors.Add("Appointments cannot be converted to projects");
        }

        if (currentCategory == AppTaskCategory.BillReminder)
        {
            errors.Add("Bill reminders cannot be converted to projects");
        }

        // Validate subtasks are eligible
        if (subtasks != null)
        {
            foreach (var subtask in subtasks)
            {
                var subtaskStatus = DomainTaskStatus.FromValue(subtask.Status);
                if (subtaskStatus == DomainTaskStatus.Archived)
                {
                    errors.Add($"Cannot include archived subtask '{subtask.Title}' in project conversion");
                }
            }
        }

        return errors.Any()
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Converts a task from one category to another with validation
    /// </summary>
    /// <param name="task">AppTask to convert</param>
    /// <param name="targetCategory">Target category</param>
    /// <returns>Validation result and converted task</returns>
    public (ValidationResult ValidationResult, DomainTask? ConvertedTask) ConvertAppTaskCategory(DomainTask task, AppTaskCategory targetCategory)
    {
        var currentCategory = AppTaskCategory.FromValue(task.Category);

        // Validate the conversion is allowed
        if (!currentCategory.CanConvertTo(targetCategory))
        {
            return (ValidationResult.Failure($"Cannot convert from {currentCategory.GetDisplayName()} to {targetCategory.GetDisplayName()}"), null);
        }

        // Additional validation for specific conversions
        var validationResult = ValidateCategoryConversion(task, currentCategory, targetCategory);
        if (!validationResult.IsValid)
        {
            return (validationResult, null);
        }

        // Create converted task
        var convertedAppTask = CreateConvertedTask(task, targetCategory);

        return (ValidationResult.Success(), convertedAppTask);
    }

    /// <summary>
    /// Validates specific category conversion rules
    /// </summary>
    private ValidationResult ValidateCategoryConversion(DomainTask task, AppTaskCategory fromCategory, AppTaskCategory toCategory)
    {
        var errors = new List<string>();

        // Validate the converted task would be valid for the new category
        var categoryValidation = toCategory.ValidateTaskData(
            task.Title,
            task.Description,
            task.DueDate,
            task.Subtasks?.Any() ?? false);

        if (!categoryValidation.IsValid)
        {
            errors.AddRange(categoryValidation.Errors);
        }

        // Specific conversion business rules
        if (fromCategory == AppTaskCategory.Idea && toCategory == AppTaskCategory.Appointment)
        {
            if (!task.DueDate.HasValue)
            {
                errors.Add("Ideas being converted to appointments must have a scheduled date");
            }
            else if (task.DueDate.Value <= DateTime.UtcNow)
            {
                errors.Add("Appointments cannot be scheduled in the past");
            }
        }

        if (fromCategory == AppTaskCategory.Idea && toCategory == AppTaskCategory.BillReminder)
        {
            if (!task.DueDate.HasValue)
            {
                errors.Add("Ideas being converted to bill reminders must have a due date");
            }

            if (string.IsNullOrWhiteSpace(task.Description))
            {
                errors.Add("Bill reminders should include payment details in the description");
            }
        }

        return errors.Any()
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Creates a converted task with appropriate adjustments for the new category
    /// </summary>
    private DomainTask CreateConvertedTask(DomainTask originalTask, AppTaskCategory newCategory)
    {
        var convertedAppTask = new DomainTask
        {
            Id = originalTask.Id,
            Title = originalTask.Title,
            Description = originalTask.Description,
            DueDate = originalTask.DueDate,
            Priority = AdjustPriorityForCategory(originalTask.Priority, newCategory),
            Category = newCategory.Value,
            Status = originalTask.Status,
            CreatedAt = originalTask.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = originalTask.IsDeleted,
            UserId = originalTask.UserId,
            ProjectId = originalTask.ProjectId,
            Subtasks = originalTask.Subtasks
        };

        // Category-specific adjustments
        if (newCategory == AppTaskCategory.Appointment)
        {
            // Appointments should have higher priority if due soon
            if (convertedAppTask.DueDate.HasValue && convertedAppTask.DueDate.Value <= DateTime.UtcNow.AddDays(1))
            {
                var priority = Priority.FromValue(convertedAppTask.Priority);
                if (priority.IsLowerThan(Priority.High))
                {
                    convertedAppTask.Priority = Priority.High.Value;
                }
            }
        }

        if (newCategory == AppTaskCategory.BillReminder)
        {
            // Bill reminders should have at least medium priority
            var priority = Priority.FromValue(convertedAppTask.Priority);
            if (priority.IsLowerThan(Priority.Medium))
            {
                convertedAppTask.Priority = Priority.Medium.Value;
            }
        }

        return convertedAppTask;
    }

    /// <summary>
    /// Adjusts task priority based on the new category's requirements
    /// </summary>
    private int AdjustPriorityForCategory(int currentPriorityValue, AppTaskCategory newCategory)
    {
        var currentPriority = Priority.FromValue(currentPriorityValue);
        var suggestedPriorities = newCategory.GetSuggestedPriorities().ToList();

        // If current priority is not suitable for new category, adjust to minimum suggested
        if (!suggestedPriorities.Contains(currentPriority))
        {
            var minSuggestedPriority = suggestedPriorities.OrderBy(p => p.SortOrder).First();
            return minSuggestedPriority.Value;
        }

        return currentPriorityValue;
    }

    /// <summary>
    /// Breaks down a project task into individual subtasks
    /// </summary>
    /// <param name="projectTask">Project to break down</param>
    /// <param name="subtaskTemplates">Templates for creating subtasks</param>
    /// <returns>Validation result and collection of created subtasks</returns>
    public (ValidationResult ValidationResult, IEnumerable<DomainTask>? Subtasks) BreakdownProject(
        DomainTask projectTask,
        IEnumerable<(string Title, string? Description, DateTime? DueDate, Priority Priority)> subtaskTemplates)
    {
        var category = AppTaskCategory.FromValue(projectTask.Category);

        if (category != AppTaskCategory.Project)
        {
            return (ValidationResult.Failure("Only project tasks can be broken down into subtasks"), null);
        }

        var errors = new List<string>();
        var subtasks = new List<DomainTask>();

        foreach (var template in subtaskTemplates)
        {
            // Validate subtask data
            if (string.IsNullOrWhiteSpace(template.Title))
            {
                errors.Add("Subtask title cannot be empty");
                continue;
            }

            if (template.DueDate.HasValue && projectTask.DueDate.HasValue &&
                template.DueDate.Value > projectTask.DueDate.Value)
            {
                errors.Add($"Subtask '{template.Title}' cannot have due date later than project due date");
                continue;
            }

            // Create subtask
            var subtask = new DomainTask
            {
                Id = Guid.NewGuid(),
                Title = template.Title,
                Description = template.Description,
                DueDate = template.DueDate,
                Priority = template.Priority.Value,
                Category = AppTaskCategory.ToDo.Value, // Subtasks are typically ToDo items
                Status = DomainTaskStatus.Pending.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                UserId = projectTask.UserId,
                ProjectId = projectTask.Id
            };

            subtasks.Add(subtask);
        }

        if (errors.Any())
        {
            return (ValidationResult.Failure(errors.ToArray()), null);
        }

        return (ValidationResult.Success(), subtasks);
    }

    /// <summary>
    /// Enhances task description when converting to project
    /// </summary>
    private string EnhanceDescriptionForProject(string? originalDescription, DomainTask originalTask)
    {
        var enhancedDescription = originalDescription ?? "";

        if (!enhancedDescription.Contains("Project converted from"))
        {
            var conversionNote = $"\n\n--- Project converted from {AppTaskCategory.FromValue(originalTask.Category).GetDisplayName()} on {DateTime.UtcNow:yyyy-MM-dd} ---";
            enhancedDescription += conversionNote;
        }

        if (string.IsNullOrWhiteSpace(enhancedDescription))
        {
            enhancedDescription = "This project was created by converting an existing task. Please add project details and breakdown into subtasks.";
        }

        return enhancedDescription;
    }

    /// <summary>
    /// Suggests task category based on content analysis
    /// </summary>
    /// <param name="title">AppTask title</param>
    /// <param name="description">AppTask description</param>
    /// <param name="dueDate">AppTask due date</param>
    /// <returns>Suggested category with confidence score</returns>
    public (AppTaskCategory SuggestedCategory, double ConfidenceScore) SuggestAppTaskCategory(string title, string? description, DateTime? dueDate)
    {
        var scores = new Dictionary<AppTaskCategory, double>();
        var content = $"{title} {description}".ToLowerInvariant();

        // Initialize all categories with base score
        foreach (var category in AppTaskCategory.GetAll())
        {
            scores[category] = 0.1;
        }

        // Keyword-based scoring
        var appointmentKeywords = new[] { "meeting", "appointment", "call", "visit", "interview", "conference" };
        var billKeywords = new[] { "bill", "payment", "invoice", "pay", "due", "utilities", "rent", "insurance" };
        var projectKeywords = new[] { "project", "plan", "develop", "build", "create", "design", "implement" };
        var ideaKeywords = new[] { "idea", "think", "consider", "maybe", "brainstorm", "concept", "inspiration" };

        scores[AppTaskCategory.Appointment] += CountKeywords(content, appointmentKeywords) * 0.3;
        scores[AppTaskCategory.BillReminder] += CountKeywords(content, billKeywords) * 0.3;
        scores[AppTaskCategory.Project] += CountKeywords(content, projectKeywords) * 0.3;
        scores[AppTaskCategory.Idea] += CountKeywords(content, ideaKeywords) * 0.3;

        // Due date based scoring
        if (dueDate.HasValue)
        {
            var daysUntilDue = (dueDate.Value - DateTime.UtcNow).TotalDays;

            if (daysUntilDue <= 1)
            {
                scores[AppTaskCategory.Appointment] += 0.4; // Likely an appointment
                scores[AppTaskCategory.BillReminder] += 0.3; // Could be a bill
            }
            else if (daysUntilDue > 30)
            {
                scores[AppTaskCategory.Project] += 0.2; // Long-term could be project
                scores[AppTaskCategory.Idea] += 0.1; // Or long-term idea
            }
        }
        else
        {
            scores[AppTaskCategory.Idea] += 0.2; // No due date suggests idea
        }

        // Length-based scoring
        var totalLength = content.Length;
        if (totalLength > 200)
        {
            scores[AppTaskCategory.Project] += 0.2; // Detailed descriptions suggest projects
        }
        else if (totalLength < 50)
        {
            scores[AppTaskCategory.ToDo] += 0.2; // Short descriptions suggest simple todos
            scores[AppTaskCategory.Idea] += 0.1;
        }

        var bestMatch = scores.OrderByDescending(kvp => kvp.Value).First();
        var confidence = Math.Min(bestMatch.Value, 1.0);

        return (bestMatch.Key, confidence);
    }

    /// <summary>
    /// Counts keyword occurrences in content
    /// </summary>
    private double CountKeywords(string content, string[] keywords)
    {
        return keywords.Count(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
