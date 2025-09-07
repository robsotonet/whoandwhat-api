using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.Task;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.TaskStatus;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service for managing complex task workflow operations and state transitions
/// </summary>
public class TaskWorkflowService
{
    /// <summary>
    /// Attempts to transition a task to a new status with full validation
    /// </summary>
    /// <param name="task">Task to transition</param>
    /// <param name="targetStatus">Target status</param>
    /// <param name="subtasks">Collection of subtasks for validation</param>
    /// <returns>Validation result indicating success or failure</returns>
    public ValidationResult TransitionTaskStatus(DomainTask task, DomainTaskStatus targetStatus, IEnumerable<DomainTask>? subtasks = null)
    {
        var currentStatus = DomainTaskStatus.FromValue(task.Status);
        var category = TaskCategory.FromValue(task.Category);
        
        // Check if basic transition is allowed
        var transitionValidation = currentStatus.ValidateTransition(
            targetStatus, 
            subtasks?.Any(s => DomainTaskStatus.FromValue(s.Status).IsActive()) ?? false,
            category);
            
        if (!transitionValidation.IsValid)
        {
            return transitionValidation;
        }

        // Additional business rule validations
        var errors = new List<string>();

        // Validate completion requirements
        if (targetStatus == DomainTaskStatus.Completed)
        {
            var completionValidation = ValidateTaskCompletion(task, category, subtasks);
            if (!completionValidation.IsValid)
            {
                errors.AddRange(completionValidation.Errors);
            }
        }

        // Validate archival requirements
        if (targetStatus == DomainTaskStatus.Archived)
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
    private ValidationResult ValidateTaskCompletion(DomainTask task, TaskCategory category, IEnumerable<DomainTask>? subtasks = null)
    {
        var errors = new List<string>();

        // Appointment-specific completion rules
        if (category == TaskCategory.Appointment)
        {
            if (task.DueDate.HasValue && task.DueDate.Value > DateTime.UtcNow)
            {
                errors.Add("Cannot mark appointment as completed before its scheduled time");
            }
        }

        // Bill reminder completion rules
        if (category == TaskCategory.BillReminder)
        {
            if (task.DueDate.HasValue && DateTime.UtcNow < task.DueDate.Value.AddDays(-1))
            {
                errors.Add("Bill reminders should not be completed more than a day before due date");
            }
        }

        // Project completion rules
        if (category == TaskCategory.Project && subtasks != null)
        {
            var activeSubtasks = subtasks.Where(s => DomainTaskStatus.FromValue(s.Status).IsActive()).ToList();
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
    private ValidationResult ValidateTaskArchival(DomainTask task, DomainTaskStatus currentStatus)
    {
        var errors = new List<string>();

        if (currentStatus != DomainTaskStatus.Completed)
        {
            // Allow archiving of very old pending tasks
            if (currentStatus == DomainTaskStatus.Pending && task.CreatedAt < DateTime.UtcNow.AddMonths(-6))
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
    public IEnumerable<(DomainTask Task, DomainTaskStatus NewStatus)> AutoManageTaskStatuses(IEnumerable<DomainTask> tasks)
    {
        var updates = new List<(DomainTask, DomainTaskStatus)>();

        foreach (var task in tasks)
        {
            var currentStatus = DomainTaskStatus.FromValue(task.Status);
            var category = TaskCategory.FromValue(task.Category);

            // Skip if already completed or archived
            if (!currentStatus.IsActive())
            {
                continue;
            }

            // Auto-complete appointments that have passed
            if (category == TaskCategory.Appointment && 
                task.DueDate.HasValue && 
                task.DueDate.Value.AddHours(2) < DateTime.UtcNow) // 2-hour grace period
            {
                updates.Add((task, DomainTaskStatus.Completed));
            }
            
            // Auto-archive old completed tasks based on category
            if (currentStatus == DomainTaskStatus.Completed && category.ShouldAutoArchive())
            {
                var daysSinceCompletion = (DateTime.UtcNow - task.UpdatedAt).TotalDays;
                var autoArchiveDays = GetAutoArchiveDays(category);
                
                if (daysSinceCompletion > autoArchiveDays)
                {
                    updates.Add((task, DomainTaskStatus.Archived));
                }
            }
        }

        return updates;
    }

    /// <summary>
    /// Gets the number of days after completion before auto-archiving
    /// </summary>
    private static int GetAutoArchiveDays(TaskCategory category) => category.Name switch
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
    public IEnumerable<(DomainTask Task, Priority SuggestedPriority)> SuggestPriorityEscalations(IEnumerable<DomainTask> tasks)
    {
        var escalations = new List<(DomainTask, Priority)>();

        foreach (var task in tasks.Where(t => t.DueDate.HasValue))
        {
            var currentPriority = Priority.FromValue(task.Priority);
            var suggestedPriority = Priority.SuggestFromDueDate(task.DueDate);
            var category = TaskCategory.FromValue(task.Category);

            // Only suggest escalation if suggested priority is higher
            if (suggestedPriority.IsHigherThan(currentPriority))
            {
                // Additional context-based escalation rules
                if (category == TaskCategory.Appointment || category == TaskCategory.BillReminder)
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
    public ValidationResult ValidateTaskDependencies(DomainTask parentTask, IEnumerable<DomainTask> subtasks)
    {
        var errors = new List<string>();
        var parentCategory = TaskCategory.FromValue(parentTask.Category);

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
    /// <param name="task">Task to score</param>
    /// <param name="subtasks">Optional subtasks for calculation</param>
    /// <returns>Completion score between 0 and 100</returns>
    public double CalculateTaskCompletionScore(DomainTask task, IEnumerable<DomainTask>? subtasks = null)
    {
        var status = DomainTaskStatus.FromValue(task.Status);
        
        if (status == DomainTaskStatus.Completed)
        {
            return 100.0;
        }
            
        if (status == DomainTaskStatus.Archived)
        {
            return 100.0;
        }

        var score = 0.0;

        // Base score from status
        score += status == DomainTaskStatus.InProgress ? 50.0 : 0.0;

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
        if (status == DomainTaskStatus.InProgress)
        {
            var daysInProgress = (DateTime.UtcNow - task.UpdatedAt).TotalDays;
            score += Math.Min(daysInProgress * 2, 20); // Up to 20 points for age
        }

        return Math.Min(score, 100.0);
    }
}