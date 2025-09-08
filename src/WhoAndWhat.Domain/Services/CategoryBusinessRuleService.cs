using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service that enforces category-specific business rules and behaviors
/// </summary>
public class CategoryBusinessRuleService
{
    /// <summary>
    /// Validates task creation according to category-specific business rules
    /// </summary>
    /// <param name="task">Task to validate</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidateTaskCreation(DomainTask task)
    {
        if (task == null)
            return ValidationResult.Failure("Task cannot be null");

        var category = AppTaskCategory.FromValue(task.Category);
        var errors = new List<string>();

        // Basic category validation using value object
        var categoryValidation = category.ValidateTaskData(
            task.Title, 
            task.Description, 
            task.DueDate, 
            task.Subtasks?.Any() == true);

        if (!categoryValidation.IsValid)
        {
            errors.AddRange(categoryValidation.ErrorMessages);
        }

        // Additional business rule validations
        errors.AddRange(ValidateAppointmentRules(task, category));
        errors.AddRange(ValidateBillReminderRules(task, category));
        errors.AddRange(ValidateProjectRules(task, category));
        errors.AddRange(ValidateIdeaRules(task, category));
        errors.AddRange(ValidateToDoRules(task, category));

        return errors.Any() 
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    /// <summary>
    /// Validates task updates according to category-specific business rules
    /// </summary>
    /// <param name="existingTask">Current task state</param>
    /// <param name="updates">Proposed updates</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidateTaskUpdate(DomainTask existingTask, AppTaskUpdateRequest updates)
    {
        if (existingTask == null)
            return ValidationResult.Failure("Existing task cannot be null");

        if (updates == null)
            return ValidationResult.Failure("Updates cannot be null");

        var errors = new List<string>();
        var currentCategory = AppTaskCategory.FromValue(existingTask.Category);
        var newCategory = updates.Category.HasValue ? AppTaskCategory.FromValue(updates.Category.Value) : currentCategory;

        // Validate category changes
        if (updates.Category.HasValue && existingTask.Category != updates.Category.Value)
        {
            var categoryChangeValidation = ValidateCategoryChange(existingTask, currentCategory, newCategory);
            if (!categoryChangeValidation.IsValid)
            {
                errors.AddRange(categoryChangeValidation.ErrorMessages);
            }
        }

        // Validate status changes with category context
        if (updates.Status != null && existingTask.Status != (int)updates.Status)
        {
            var statusChangeValidation = ValidateStatusChangeForCategory(existingTask, updates.Status, newCategory);
            if (!statusChangeValidation.IsValid)
            {
                errors.AddRange(statusChangeValidation.ErrorMessages);
            }
        }

        // Validate due date changes
        if (updates.DueDate != existingTask.DueDate)
        {
            var dueDateValidation = ValidateDueDateChangeForCategory(existingTask, updates.DueDate, newCategory);
            if (!dueDateValidation.IsValid)
            {
                errors.AddRange(dueDateValidation.ErrorMessages);
            }
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    /// <summary>
    /// Determines the recommended next status for a task based on its category
    /// </summary>
    /// <param name="task">Task to analyze</param>
    /// <returns>Recommended next status</returns>
    public DomainAppTaskStatus GetRecommendedNextStatus(DomainTask task)
    {
        var category = AppTaskCategory.FromValue(task.Category);
        var currentStatus = DomainAppTaskStatus.FromValue(task.Status);

        // Use if-else chain instead of switch expression to avoid constant value issues
        if (category.Name == "Appointment")
        {
            if (currentStatus == DomainAppTaskStatus.Pending) return DomainAppTaskStatus.Confirmed;
            if (currentStatus == DomainAppTaskStatus.Confirmed) return DomainAppTaskStatus.InProgress;
            if (currentStatus == DomainAppTaskStatus.InProgress) return DomainAppTaskStatus.Completed;
        }
        else if (category.Name == "BillReminder")
        {
            if (currentStatus == DomainAppTaskStatus.Pending) return DomainAppTaskStatus.InProgress;
            if (currentStatus == DomainAppTaskStatus.InProgress) return DomainAppTaskStatus.Completed;
        }
        else if (category.Name == "Project")
        {
            if (currentStatus == DomainAppTaskStatus.Pending) return DomainAppTaskStatus.InProgress;
            if (currentStatus == DomainAppTaskStatus.InProgress) 
                return HasInProgressSubtasks(task) ? DomainAppTaskStatus.InProgress : DomainAppTaskStatus.Completed;
        }
        else
        {
            // Standard workflow for ToDo and Idea
            if (currentStatus == DomainAppTaskStatus.Pending) return DomainAppTaskStatus.InProgress;
            if (currentStatus == DomainAppTaskStatus.InProgress) return DomainAppTaskStatus.Completed;
            if (currentStatus == DomainAppTaskStatus.Confirmed) return DomainAppTaskStatus.InProgress;
        }

        return currentStatus;
    }

    /// <summary>
    /// Gets category-specific actions available for a task
    /// </summary>
    /// <param name="task">Task to analyze</param>
    /// <returns>List of available actions</returns>
    public IEnumerable<AppTaskAction> GetAvailableActions(DomainTask task)
    {
        var category = AppTaskCategory.FromValue(task.Category);
        var status = (DomainAppTaskStatus)task.Status;
        var actions = new List<AppTaskAction>();

        // Common actions
        if (status != DomainAppTaskStatus.Completed)
        {
            actions.Add(new AppTaskAction("Complete", "Mark as completed", "check"));
        }

        if (status == DomainAppTaskStatus.Completed)
        {
            actions.Add(new AppTaskAction("Reopen", "Reopen task", "undo"));
        }

        // Category-specific actions
        switch (category.Name)
        {
            case "Appointment":
                if (status == DomainAppTaskStatus.Pending)
                {
                    actions.Add(new AppTaskAction("Confirm", "Confirm appointment", "calendar-check"));
                    actions.Add(new AppTaskAction("Reschedule", "Reschedule appointment", "calendar-edit"));
                }
                if (status != DomainAppTaskStatus.Completed)
                {
                    actions.Add(new AppTaskAction("Cancel", "Cancel appointment", "calendar-x"));
                }
                break;

            case "BillReminder":
                if (status != DomainAppTaskStatus.Completed)
                {
                    actions.Add(new AppTaskAction("MarkPaid", "Mark as paid", "credit-card"));
                    actions.Add(new AppTaskAction("SetRecurring", "Set as recurring", "repeat"));
                }
                break;

            case "Project":
                if (category.AllowsSubtasks)
                {
                    actions.Add(new AppTaskAction("AddSubtask", "Add subtask", "plus"));
                }
                if (status != DomainAppTaskStatus.Completed)
                {
                    actions.Add(new AppTaskAction("ViewProgress", "View progress", "bar-chart"));
                }
                break;

            case "Idea":
                if (status == DomainAppTaskStatus.Pending)
                {
                    actions.Add(new AppTaskAction("ConvertToTodo", "Convert to To-Do", "arrow-right"));
                    actions.Add(new AppTaskAction("ConvertToProject", "Convert to Project", "folder"));
                }
                actions.Add(new AppTaskAction("Archive", "Archive idea", "archive"));
                break;

            case "ToDo":
                if (status == DomainAppTaskStatus.Pending)
                {
                    actions.Add(new AppTaskAction("SetPriority", "Set priority", "flag"));
                }
                break;
        }

        return actions;
    }

    /// <summary>
    /// Calculates category-specific completion metrics
    /// </summary>
    /// <param name="tasks">Tasks to analyze</param>
    /// <returns>Category metrics</returns>
    public CategoryMetrics CalculateCategoryMetrics(IEnumerable<DomainTask> tasks)
    {
        var taskList = tasks.ToList();
        var metrics = new CategoryMetrics();

        foreach (var categoryGroup in taskList.GroupBy(t => t.Category))
        {
            var category = AppTaskCategory.FromValue(categoryGroup.Key);
            var categoryTasks = categoryGroup.ToList();
            
            var completedCount = categoryTasks.Count(t => t.Status == (int)DomainAppTaskStatus.Completed);
            var overdueCount = categoryTasks.Count(t => t.IsOverdue);
            var totalCount = categoryTasks.Count;

            var categoryMetric = new CategoryMetric
            {
                Category = category,
                TotalTasks = totalCount,
                CompletedTasks = completedCount,
                OverdueTasks = overdueCount,
                CompletionPercentage = totalCount > 0 ? (decimal)completedCount / totalCount * 100 : 0,
                AverageCompletionTime = CalculateAverageCompletionTime(categoryTasks),
                EfficiencyScore = CalculateEfficiencyScore(categoryTasks, category)
            };

            metrics.Metrics.Add(categoryMetric);
        }

        return metrics;
    }

    /// <summary>
    /// Suggests optimal scheduling for category-specific tasks
    /// </summary>
    /// <param name="tasks">Tasks to schedule</param>
    /// <returns>Scheduling suggestions</returns>
    public SchedulingSuggestions GetSchedulingSuggestions(IEnumerable<DomainTask> tasks)
    {
        var suggestions = new SchedulingSuggestions();
        var taskList = tasks.Where(t => t.Status != (int)DomainAppTaskStatus.Completed).ToList();

        foreach (var task in taskList)
        {
            var category = AppTaskCategory.FromValue(task.Category);
            var priority = Priority.FromValue(task.Priority);
            
            var suggestion = new SchedulingSuggestion
            {
                Task = task,
                RecommendedDate = CalculateRecommendedDate(task, category, priority),
                EstimatedDuration = TimeSpan.FromHours(category.GetEstimatedHours()),
                OptimalTimeOfDay = GetOptimalTimeOfDay(category),
                BufferTime = GetRecommendedBufferTime(category),
                Reasoning = GetSchedulingReasoning(task, category, priority)
            };

            suggestions.Suggestions.Add(suggestion);
        }

        // Sort by priority and due date
        suggestions.Suggestions = suggestions.Suggestions
            .OrderBy(s => s.Task.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(s => s.Task.Priority)
            .ToList();

        return suggestions;
    }

    #region Private Helper Methods

    private List<string> ValidateAppointmentRules(DomainTask task, AppTaskCategory category)
    {
        var errors = new List<string>();
        
        if (category.Name != "Appointment") return errors;

        // Appointments must have future due dates
        if (task.DueDate.HasValue && task.DueDate.Value <= DateTime.UtcNow.AddMinutes(30))
        {
            errors.Add("Appointments must be scheduled at least 30 minutes in the future");
        }

        // Appointments should have location or meeting details
        if (string.IsNullOrWhiteSpace(task.Description) || 
            (!task.Description.Contains("@") && !task.Description.Contains("location", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("Appointments should include location or meeting details");
        }

        // High priority is recommended for appointments
        if (task.Priority < (int)Priority.Medium)
        {
            errors.Add("Appointments should typically have medium or higher priority");
        }

        return errors;
    }

    private List<string> ValidateBillReminderRules(DomainTask task, AppTaskCategory category)
    {
        var errors = new List<string>();
        
        if (category.Name != "BillReminder") return errors;

        // Bill reminders must have due dates
        if (!task.DueDate.HasValue)
        {
            errors.Add("Bill reminders must have a payment due date");
        }

        // Description should include amount or payee
        if (string.IsNullOrWhiteSpace(task.Description) || 
            (!task.Description.Contains("$") && !task.Description.Contains("amount", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("Bill reminders should include payment amount or payee information");
        }

        // Title should indicate what bill it is
        if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length < 5)
        {
            errors.Add("Bill reminders should have descriptive titles indicating which bill");
        }

        return errors;
    }

    private List<string> ValidateProjectRules(DomainTask task, AppTaskCategory category)
    {
        var errors = new List<string>();
        
        if (category.Name != "Project") return errors;

        // Projects should have detailed descriptions
        if (string.IsNullOrWhiteSpace(task.Description) || task.Description.Length < 20)
        {
            errors.Add("Projects should have detailed descriptions of at least 20 characters");
        }

        // Projects should start as pending, not other statuses
        if (task.Status != (int)DomainAppTaskStatus.Pending && task.CreatedAt == task.UpdatedAt)
        {
            errors.Add("New projects should start with 'Pending' status");
        }

        // Complex projects should have medium or higher priority
        if (task.Priority == (int)Priority.Low && !string.IsNullOrEmpty(task.Description) && task.Description.Length > 100)
        {
            errors.Add("Complex projects should have medium or higher priority");
        }

        return errors;
    }

    private List<string> ValidateIdeaRules(DomainTask task, AppTaskCategory category)
    {
        var errors = new List<string>();
        
        if (category.Name != "Idea") return errors;

        // Ideas can be very flexible, but provide guidance
        if (string.IsNullOrWhiteSpace(task.Description))
        {
            // This is a warning, not an error - ideas can be brief
            // Consider adding to a warnings collection if needed
        }

        // Ideas typically shouldn't have urgent priority
        if (task.Priority >= (int)Priority.High)
        {
            errors.Add("Ideas typically should not have urgent priority - consider converting to a To-Do or Project first");
        }

        return errors;
    }

    private List<string> ValidateToDoRules(DomainTask task, AppTaskCategory category)
    {
        var errors = new List<string>();
        
        if (category.Name != "ToDo") return errors;

        // Basic validation - To-Dos are most flexible
        if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length < 3)
        {
            errors.Add("To-Do tasks should have meaningful titles of at least 3 characters");
        }

        return errors;
    }

    private ValidationResult ValidateCategoryChange(DomainTask task, AppTaskCategory from, AppTaskCategory to)
    {
        if (!from.CanConvertTo(to))
        {
            return ValidationResult.Failure($"Cannot convert {from.GetDisplayName()} to {to.GetDisplayName()}");
        }

        var errors = new List<string>();

        // Additional validation for specific conversions
        if (to.Name == "Project" && (task.Subtasks == null || !task.Subtasks.Any()))
        {
            errors.Add("Converting to Project category requires adding subtasks to justify the complexity");
        }

        if (to.Name == "Appointment" && !task.DueDate.HasValue)
        {
            errors.Add("Converting to Appointment requires setting a future date and time");
        }

        if (to.Name == "BillReminder" && string.IsNullOrWhiteSpace(task.Description))
        {
            errors.Add("Converting to Bill Reminder requires adding payment details in description");
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    private ValidationResult ValidateStatusChangeForCategory(DomainTask task, DomainAppTaskStatus newStatus, AppTaskCategory category)
    {
        var errors = new List<string>();
        var currentStatus = (DomainAppTaskStatus)task.Status;

        // Category-specific status change rules
        switch (category.Name)
        {
            case "Appointment":
                if (currentStatus == DomainAppTaskStatus.Pending && newStatus == DomainAppTaskStatus.Completed)
                {
                    errors.Add("Appointments should be confirmed before marking as completed");
                }
                if (newStatus == DomainAppTaskStatus.Confirmed && task.DueDate < DateTime.UtcNow)
                {
                    errors.Add("Cannot confirm past appointments");
                }
                break;

            case "BillReminder":
                if (newStatus == DomainAppTaskStatus.Completed && task.DueDate > DateTime.UtcNow.AddDays(7))
                {
                    errors.Add("Bill payments should not be marked complete too far in advance");
                }
                break;

            case "Project":
                if (newStatus == DomainAppTaskStatus.Completed && HasInProgressSubtasks(task))
                {
                    errors.Add("Cannot complete project while subtasks are still in progress");
                }
                break;
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    private ValidationResult ValidateDueDateChangeForCategory(DomainTask task, DateTime? newDueDate, AppTaskCategory category)
    {
        var errors = new List<string>();

        switch (category.Name)
        {
            case "Appointment":
                if (newDueDate.HasValue && newDueDate.Value <= DateTime.UtcNow.AddMinutes(30))
                {
                    errors.Add("Appointment dates must be at least 30 minutes in the future");
                }
                break;

            case "BillReminder":
                if (newDueDate.HasValue && newDueDate.Value < DateTime.UtcNow.Date)
                {
                    errors.Add("Bill payment dates should not be in the past");
                }
                break;
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    private bool HasInProgressSubtasks(DomainTask task)
    {
        return task.Subtasks?.Any(st => !st.IsDeleted && st.Status == (int)DomainAppTaskStatus.InProgress) == true;
    }

    private double CalculateAverageCompletionTime(List<DomainTask> tasks)
    {
        var completedTasks = tasks.Where(t => t.Status == (int)DomainAppTaskStatus.Completed).ToList();
        if (!completedTasks.Any()) return 0;

        var totalHours = completedTasks.Sum(t => (t.UpdatedAt - t.CreatedAt).TotalHours);
        return totalHours / completedTasks.Count;
    }

    private double CalculateEfficiencyScore(List<DomainTask> tasks, AppTaskCategory category)
    {
        var completedOnTime = tasks.Count(t => 
            t.Status == (int)DomainAppTaskStatus.Completed && 
            (!t.DueDate.HasValue || t.UpdatedAt <= t.DueDate.Value));
        
        var totalTasks = tasks.Count;
        return totalTasks > 0 ? (double)completedOnTime / totalTasks * 100 : 0;
    }

    private DateTime CalculateRecommendedDate(DomainTask task, AppTaskCategory category, Priority priority)
    {
        var baseDate = DateTime.UtcNow;

        // Factor in category urgency
        var categoryDays = category.Name switch
        {
            "Appointment" => 0, // Appointments have fixed dates
            "BillReminder" => task.DueDate?.Subtract(baseDate).Days ?? 7,
            "Project" => 14, // Projects need planning time
            "ToDo" => priority.GetUrgencyMultiplier() * 3,
            "Idea" => 30, // Ideas can be scheduled later
            _ => 7
        };

        return baseDate.AddDays(Math.Max(0, categoryDays));
    }

    private TimeSpan GetOptimalTimeOfDay(AppTaskCategory category)
    {
        return category.Name switch
        {
            "Appointment" => TimeSpan.FromHours(10), // Mid-morning
            "BillReminder" => TimeSpan.FromHours(9),  // Early morning
            "Project" => TimeSpan.FromHours(9),       // When energy is high
            "ToDo" => TimeSpan.FromHours(14),         // Afternoon
            "Idea" => TimeSpan.FromHours(16),         // Late afternoon for creativity
            _ => TimeSpan.FromHours(10)
        };
    }

    private TimeSpan GetRecommendedBufferTime(AppTaskCategory category)
    {
        return category.Name switch
        {
            "Appointment" => TimeSpan.FromMinutes(15), // Travel/setup time
            "BillReminder" => TimeSpan.FromMinutes(5),  // Quick task
            "Project" => TimeSpan.FromHours(1),         // Complexity buffer
            "ToDo" => TimeSpan.FromMinutes(30),         // Standard buffer
            "Idea" => TimeSpan.FromMinutes(15),         // Minimal buffer
            _ => TimeSpan.FromMinutes(30)
        };
    }

    private string GetSchedulingReasoning(DomainTask task, AppTaskCategory category, Priority priority)
    {
        return category.Name switch
        {
            "Appointment" => "Scheduled based on appointment time with buffer for preparation",
            "BillReminder" => $"Scheduled to avoid late fees, considering {priority.GetDisplayName()} priority",
            "Project" => "Scheduled with adequate planning time for complex deliverables",
            "ToDo" => $"Scheduled based on {priority.GetDisplayName()} priority level",
            "Idea" => "Flexible scheduling to allow for inspiration and creativity",
            _ => "Standard scheduling based on task priority and category"
        };
    }

    #endregion
}

/// <summary>
/// Represents a request to update task properties
/// </summary>
public class AppTaskUpdateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int? Category { get; set; }
    public int? Priority { get; set; }
    public DomainAppTaskStatus? Status { get; set; }
}

/// <summary>
/// Represents an available action for a task
/// </summary>
public class AppTaskAction
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Icon { get; }

    public AppTaskAction(string id, string displayName, string icon)
    {
        Id = id;
        DisplayName = displayName;
        Icon = icon;
    }
}

/// <summary>
/// Contains metrics for all task categories
/// </summary>
public class CategoryMetrics
{
    public List<CategoryMetric> Metrics { get; } = new();
}

/// <summary>
/// Metrics for a specific task category
/// </summary>
public class CategoryMetric
{
    public AppTaskCategory Category { get; set; } = null!;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public decimal CompletionPercentage { get; set; }
    public double AverageCompletionTime { get; set; }
    public double EfficiencyScore { get; set; }
}

/// <summary>
/// Contains scheduling suggestions for tasks
/// </summary>
public class SchedulingSuggestions
{
    public List<SchedulingSuggestion> Suggestions { get; set; } = new();
}

/// <summary>
/// Scheduling suggestion for a specific task
/// </summary>
public class SchedulingSuggestion
{
    public DomainTask Task { get; set; } = null!;
    public DateTime RecommendedDate { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public TimeSpan OptimalTimeOfDay { get; set; }
    public TimeSpan BufferTime { get; set; }
    public string Reasoning { get; set; } = null!;
}