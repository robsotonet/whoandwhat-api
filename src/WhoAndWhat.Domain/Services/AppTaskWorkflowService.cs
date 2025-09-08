using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service for managing complex task workflow operations and state transitions
/// </summary>
public class AppTaskWorkflowService
{
    /// <summary>
    /// Attempts to transition a task to a new status with full validation
    /// </summary>
    /// <param name="task">AppTask to transition</param>
    /// <param name="targetStatus">Target status</param>
    /// <param name="subtasks">Collection of subtasks for validation</param>
    /// <returns>Validation result indicating success or failure</returns>
    public ValidationResult TransitionAppTaskStatus(DomainAppTask task, DomainAppTaskStatus targetStatus, IEnumerable<DomainTask>? subtasks = null)
    {
        var currentStatus = DomainAppTaskStatus.FromValue(task.Status);
        var category = AppTaskCategory.FromValue(task.Category);
        
        // Check if basic transition is allowed
        var transitionValidation = currentStatus.ValidateTransition(
            targetStatus, 
            subtasks?.Any(s => DomainAppTaskStatus.FromValue(s.Status).IsActive()) ?? false,
            category);
            
        if (!transitionValidation.IsValid)
        {
            return transitionValidation;
        }

        // Additional business rule validations
        var errors = new List<string>();

        // Validate completion requirements
        if (targetStatus == DomainAppTaskStatus.Completed)
        {
            var completionValidation = ValidateTaskCompletion(task, category, subtasks);
            if (!completionValidation.IsValid)
            {
                errors.AddRange(completionValidation.Errors);
            }
        }

        // Validate archival requirements
        if (targetStatus == DomainAppTaskStatus.Archived)
        {
            var archivalValidation = ValidateTaskArchival(task, currentStatus);
            if (!archivalValidation.IsValid)
            {
                errors.AddRange(archivalValidation.Errors);
            }
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Validates if a task can be completed based on business rules
    /// </summary>
    private ValidationResult ValidateTaskCompletion(DomainAppTask task, AppTaskCategory category, IEnumerable<DomainTask>? subtasks = null)
    {
        var errors = new List<string>();

        // Appointment-specific completion rules
        if (category == AppTaskCategory.Appointment)
        {
            if (task.DueDate.HasValue && task.DueDate.Value > DateTime.UtcNow)
            {
                errors.Add("Cannot mark appointment as completed before its scheduled time");
            }
        }

        // Bill reminder completion rules
        if (category == AppTaskCategory.BillReminder)
        {
            if (task.DueDate.HasValue && DateTime.UtcNow < task.DueDate.Value.AddDays(-1))
            {
                errors.Add("Bill reminders should not be completed more than a day before due date");
            }
        }

        // Project completion rules
        if (category == AppTaskCategory.Project && subtasks != null)
        {
            var activeSubtasks = subtasks.Where(s => DomainAppTaskStatus.FromValue(s.Status).IsActive()).ToList();
            if (activeSubtasks.Any())
            {
                errors.Add($"Cannot complete project while {activeSubtasks.Count} subtasks are still active");
            }
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Validates if a task can be archived based on business rules
    /// </summary>
    private ValidationResult ValidateTaskArchival(DomainAppTask task, DomainAppTaskStatus currentStatus)
    {
        var errors = new List<string>();

        if (currentStatus != DomainAppTaskStatus.Completed)
        {
            // Allow archiving of very old pending tasks
            if (currentStatus == DomainAppTaskStatus.Pending && task.CreatedAt < DateTime.UtcNow.AddMonths(-6))
            {
                // This is acceptable - old pending tasks can be archived
            }
            else
            {
                errors.Add("Only completed tasks can be archived (except for very old pending tasks)");
            }
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Automatically manages task status based on due dates and business rules
    /// </summary>
    /// <param name="tasks">Collection of tasks to evaluate</param>
    /// <returns>Collection of tasks that should be updated with their new status</returns>
    public IEnumerable<(DomainAppTask Task, DomainAppTaskStatus NewStatus)> AutoManageAppTaskStatuses(IEnumerable<DomainTask> tasks)
    {
        var updates = new List<(DomainTask, DomainAppTaskStatus)>();

        foreach (var task in tasks)
        {
            var currentStatus = DomainAppTaskStatus.FromValue(task.Status);
            var category = AppTaskCategory.FromValue(task.Category);

            // Skip if already completed or archived
            if (!currentStatus.IsActive())
            {
                continue;
            }

            // Auto-complete appointments that have passed
            if (category == AppTaskCategory.Appointment && 
                task.DueDate.HasValue && 
                task.DueDate.Value.AddHours(2) < DateTime.UtcNow) // 2-hour grace period
            {
                updates.Add((task, DomainAppTaskStatus.Completed));
            }
            
            // Auto-archive old completed tasks based on category
            if (currentStatus == DomainAppTaskStatus.Completed && category.ShouldAutoArchive())
            {
                var daysSinceCompletion = (DateTime.UtcNow - task.UpdatedAt).TotalDays;
                var autoArchiveDays = GetAutoArchiveDays(category);
                
                if (daysSinceCompletion > autoArchiveDays)
                {
                    updates.Add((task, DomainAppTaskStatus.Archived));
                }
            }
        }

        return updates;
    }

    /// <summary>
    /// Gets the number of days after completion before auto-archiving
    /// </summary>
    private static int GetAutoArchiveDays(AppTaskCategory category) => category.Name switch
    {
        "BillReminder" => 30,      // Archive bill reminders after 30 days
        "Appointment" => 7,        // Archive appointments after 7 days
        _ => 90                    // Default: archive after 90 days
    };

    /// <summary>
    /// Escalates task priority based on due date proximity and current priority
    /// </summary>
    /// <param name="tasks">Tasks to evaluate for priority escalation</param>
    /// <returns>Collection of tasks with suggested priority escalations</returns>
    public IEnumerable<(DomainAppTask Task, Priority SuggestedPriority)> SuggestPriorityEscalations(IEnumerable<DomainTask> tasks)
    {
        var escalations = new List<(DomainTask, Priority)>();

        foreach (var task in tasks.Where(t => t.DueDate.HasValue))
        {
            var currentPriority = Priority.FromValue(task.Priority);
            var suggestedPriority = Priority.SuggestFromDueDate(task.DueDate);
            var category = AppTaskCategory.FromValue(task.Category);

            // Only suggest escalation if suggested priority is higher
            if (suggestedPriority.IsHigherThan(currentPriority))
            {
                // Additional context-based escalation rules
                if (category == AppTaskCategory.Appointment || category == AppTaskCategory.BillReminder)
                {
                    // These categories should always be at least High priority when due soon
                    if (task.DaysUntilDue <= 1 && currentPriority.IsLowerThan(Priority.High))
                    {
                        escalations.Add((task, Priority.High));
                    }
                }
                else
                {
                    escalations.Add((task, suggestedPriority));
                }
            }
        }

        return escalations;
    }

    /// <summary>
    /// Manages task dependencies and enforces ordering constraints
    /// </summary>
    /// <param name="parentTask">Parent task</param>
    /// <param name="subtasks">Collection of subtasks</param>
    /// <returns>Validation result for task dependencies</returns>
    public ValidationResult ValidateTaskDependencies(DomainAppTask parentTask, IEnumerable<DomainTask> subtasks)
    {
        var errors = new List<string>();
        var parentCategory = AppTaskCategory.FromValue(parentTask.Category);

        // Validate subtask relationships
        if (subtasks.Any() && !parentCategory.AllowsSubtasks)
        {
            errors.Add($"{parentCategory.GetDisplayName()} tasks cannot have subtasks");
        }

        // Validate due date consistency
        if (parentTask.DueDate.HasValue)
        {
            var overdueTasks = subtasks.Where(s => s.DueDate.HasValue && s.DueDate.Value > parentTask.DueDate.Value);
            if (overdueTasks.Any())
            {
                errors.Add($"Subtasks cannot have due dates later than parent task ({parentTask.DueDate:yyyy-MM-dd})");
            }
        }

        // Validate priority consistency
        var parentPriority = Priority.FromValue(parentTask.Priority);
        var higherPrioritySubtasks = subtasks.Where(s => Priority.FromValue(s.Priority).IsHigherThan(parentPriority));
        if (higherPrioritySubtasks.Any())
        {
            errors.Add("Subtasks should not have higher priority than their parent task");
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Calculates task completion score based on multiple factors
    /// </summary>
    /// <param name="task">AppTask to score</param>
    /// <param name="subtasks">Optional subtasks for calculation</param>
    /// <returns>Completion score between 0 and 100</returns>
    public double CalculateTaskCompletionScore(DomainAppTask task, IEnumerable<DomainTask>? subtasks = null)
    {
        var status = DomainAppTaskStatus.FromValue(task.Status);
        
        if (status == DomainAppTaskStatus.Completed)
        {
            return 100.0;
        }
            
        if (status == DomainAppTaskStatus.Archived)
        {
            return 100.0;
        }

        var score = 0.0;

        // Base score from status
        score += status == DomainAppTaskStatus.InProgress ? 50.0 : 0.0;

        // Score from subtask completion
        if (subtasks?.Any() == true)
        {
            var completionPercentage = task.CompletionPercentage;
            score = Math.Max(score, (double)completionPercentage);
        }

        // Adjust for overdue tasks (penalty)
        if (task.IsOverdue)
        {
            score *= 0.8; // 20% penalty for being overdue
        }

        // Adjust for task age (older in-progress tasks get higher scores)
        if (status == DomainAppTaskStatus.InProgress)
        {
            var daysInProgress = (DateTime.UtcNow - task.UpdatedAt).TotalDays;
            score += Math.Min(daysInProgress * 2, 20); // Up to 20 points for age
        }

        return Math.Min(score, 100.0);
    }
}