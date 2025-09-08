using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Events;
using WhoAndWhat.Domain.ValueObjects;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service that manages category-specific workflows and state transitions
/// </summary>
public class CategoryWorkflowService
{
    private readonly CategoryBusinessRuleService _businessRuleService;

    public CategoryWorkflowService(CategoryBusinessRuleService businessRuleService)
    {
        _businessRuleService = businessRuleService ?? throw new ArgumentNullException(nameof(businessRuleService));
    }

    /// <summary>
    /// Processes a task through its category-specific workflow
    /// </summary>
    /// <param name="task">Task to process</param>
    /// <param name="action">Workflow action to perform</param>
    /// <returns>Workflow result</returns>
    public WorkflowResult ProcessAppTaskAction(DomainTask task, WorkflowAction action)
    {
        if (task == null)
            return WorkflowResult.Failed("Task cannot be null");

        var category = AppTaskCategory.FromValue(task.Category);
        
        return category.Name switch
        {
            "Appointment" => ProcessAppointmentWorkflow(task, action),
            "BillReminder" => ProcessBillReminderWorkflow(task, action),
            "Project" => ProcessProjectWorkflow(task, action),
            "Idea" => ProcessIdeaWorkflow(task, action),
            "ToDo" => ProcessToDoWorkflow(task, action),
            _ => ProcessGenericWorkflow(task, action)
        };
    }

    /// <summary>
    /// Gets the workflow state for a task based on its category
    /// </summary>
    /// <param name="task">Task to analyze</param>
    /// <returns>Current workflow state</returns>
    public WorkflowState GetWorkflowState(DomainTask task)
    {
        var category = AppTaskCategory.FromValue(task.Category);
        var status = (DomainAppTaskStatus)task.Status;

        return new WorkflowState
        {
            CurrentStep = GetCurrentWorkflowStep(task, category, status),
            AvailableActions = _businessRuleService.GetAvailableActions(task).ToList(),
            NextRecommendedStatus = _businessRuleService.GetRecommendedNextStatus(task),
            ProgressPercentage = CalculateProgressPercentage(task, category, status),
            EstimatedTimeRemaining = EstimateTimeRemaining(task, category, status),
            Blockers = IdentifyBlockers(task, category)
        };
    }

    /// <summary>
    /// Creates a recurring task based on category-specific rules
    /// </summary>
    /// <param name="originalTask">Original task to base recurrence on</param>
    /// <param name="recurrencePattern">Recurrence pattern</param>
    /// <returns>New recurring task</returns>
    public WorkflowResult CreateRecurringTask(DomainTask originalTask, RecurrencePattern recurrencePattern)
    {
        var category = AppTaskCategory.FromValue(originalTask.Category);

        // Only certain categories support recurrence
        if (!IsCategorySuitableForRecurrence(category))
        {
            return WorkflowResult.Failed($"{category.GetDisplayName()} tasks do not support recurrence");
        }

        try
        {
            var newTask = CloneTaskForRecurrence(originalTask, recurrencePattern);
            ApplyCategorySpecificRecurrenceRules(newTask, category, recurrencePattern);

            return WorkflowResult.Success("Recurring task created successfully", newTask);
        }
        catch (Exception ex)
        {
            return WorkflowResult.Failed($"Failed to create recurring task: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles category-specific task completion logic
    /// </summary>
    /// <param name="task">Task to complete</param>
    /// <returns>Completion result</returns>
    public WorkflowResult CompleteTask(DomainTask task)
    {
        var category = AppTaskCategory.FromValue(task.Category);
        var currentStatus = (DomainAppTaskStatus)task.Status;

        // Validate completion is allowed
        var validation = ValidateTaskCompletion(task, category);
        if (!validation.IsValid)
        {
            return WorkflowResult.Failed(validation.ErrorMessages.First());
        }

        // Apply category-specific completion logic
        var result = ApplyCategoryCompletionLogic(task, category);
        if (!result.IsSuccess)
        {
            return result;
        }

        // Mark task as completed
        task.Status = (int)DomainAppTaskStatus.Completed;
        task.UpdatedAt = DateTime.UtcNow;

        // Schedule auto-archiving if category supports it
        if (category.ShouldAutoArchive())
        {
            ScheduleAutoArchive(task, category);
        }

        // Raise domain event
        var completionEvent = new TaskCompletedEvent(task.Id, task.UserId, category.Name, DateTime.UtcNow);
        // Domain event would be added to task in a real implementation

        return WorkflowResult.Success($"{category.GetDisplayName()} completed successfully", task);
    }

    #region Private Workflow Methods

    private WorkflowResult ProcessAppointmentWorkflow(DomainTask task, WorkflowAction action)
    {
        var currentStatus = (DomainAppTaskStatus)task.Status;

        return action.Type switch
        {
            WorkflowActionType.Confirm => currentStatus == DomainAppTaskStatus.Pending 
                ? ConfirmAppointment(task)
                : WorkflowResult.Failed("Can only confirm pending appointments"),

            WorkflowActionType.Reschedule => currentStatus != DomainAppTaskStatus.Completed
                ? RescheduleAppointment(task, action.Parameters)
                : WorkflowResult.Failed("Cannot reschedule completed appointments"),

            WorkflowActionType.Cancel => currentStatus != DomainAppTaskStatus.Completed
                ? CancelAppointment(task)
                : WorkflowResult.Failed("Cannot cancel completed appointments"),

            WorkflowActionType.Complete => currentStatus == DomainAppTaskStatus.Confirmed || currentStatus == DomainAppTaskStatus.InProgress
                ? CompleteAppointment(task)
                : WorkflowResult.Failed("Appointments must be confirmed before completion"),

            _ => ProcessGenericWorkflow(task, action)
        };
    }

    private WorkflowResult ProcessBillReminderWorkflow(DomainTask task, WorkflowAction action)
    {
        return action.Type switch
        {
            WorkflowActionType.MarkPaid => MarkBillPaid(task),
            WorkflowActionType.SetRecurring => SetupRecurringBill(task, action.Parameters),
            WorkflowActionType.Snooze => SnoozeBillReminder(task, action.Parameters),
            _ => ProcessGenericWorkflow(task, action)
        };
    }

    private WorkflowResult ProcessProjectWorkflow(DomainTask task, WorkflowAction action)
    {
        return action.Type switch
        {
            WorkflowActionType.AddSubtask => AddProjectSubtask(task, action.Parameters),
            WorkflowActionType.UpdateProgress => UpdateProjectProgress(task, action.Parameters),
            WorkflowActionType.ReviewMilestones => ReviewProjectMilestones(task),
            _ => ProcessGenericWorkflow(task, action)
        };
    }

    private WorkflowResult ProcessIdeaWorkflow(DomainTask task, WorkflowAction action)
    {
        return action.Type switch
        {
            WorkflowActionType.ConvertToTodo => ConvertIdeaToTodo(task),
            WorkflowActionType.ConvertToProject => ConvertIdeaToProject(task),
            WorkflowActionType.Archive => ArchiveIdea(task),
            WorkflowActionType.Elaborate => ElaborateIdea(task, action.Parameters),
            _ => ProcessGenericWorkflow(task, action)
        };
    }

    private WorkflowResult ProcessToDoWorkflow(DomainTask task, WorkflowAction action)
    {
        return action.Type switch
        {
            WorkflowActionType.SetPriority => SetTaskPriority(task, action.Parameters),
            WorkflowActionType.AddSubtasks => ConvertToDoToProject(task),
            _ => ProcessGenericWorkflow(task, action)
        };
    }

    private WorkflowResult ProcessGenericWorkflow(DomainTask task, WorkflowAction action)
    {
        return action.Type switch
        {
            WorkflowActionType.Start => StartTask(task),
            WorkflowActionType.Complete => CompleteTask(task),
            WorkflowActionType.Pause => PauseTask(task),
            WorkflowActionType.Resume => ResumeTask(task),
            _ => WorkflowResult.Failed($"Action {action.Type} not supported for this task type")
        };
    }

    #endregion

    #region Appointment Workflow Methods

    private WorkflowResult ConfirmAppointment(DomainTask task)
    {
        if (task.DueDate.HasValue && task.DueDate.Value <= DateTime.UtcNow)
        {
            return WorkflowResult.Failed("Cannot confirm appointments in the past");
        }

        task.Status = (int)DomainAppTaskStatus.Confirmed;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Appointment confirmed", task);
    }

    private WorkflowResult RescheduleAppointment(DomainTask task, Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("newDateTime", out var newDateTimeObj) || 
            !DateTime.TryParse(newDateTimeObj.ToString(), out var newDateTime))
        {
            return WorkflowResult.Failed("Valid new date and time required for rescheduling");
        }

        if (newDateTime <= DateTime.UtcNow.AddMinutes(30))
        {
            return WorkflowResult.Failed("New appointment time must be at least 30 minutes in the future");
        }

        task.DueDate = newDateTime;
        task.Status = (int)DomainAppTaskStatus.Pending; // Reset to pending after reschedule
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Appointment rescheduled", task);
    }

    private WorkflowResult CancelAppointment(DomainTask task)
    {
        task.Status = (int)DomainAppTaskStatus.Cancelled;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Appointment cancelled", task);
    }

    private WorkflowResult CompleteAppointment(DomainTask task)
    {
        task.Status = (int)DomainAppTaskStatus.Completed;
        task.UpdatedAt = DateTime.UtcNow;

        // Auto-archive completed appointments
        ScheduleAutoArchive(task, AppTaskCategory.Appointment);

        return WorkflowResult.Success("Appointment completed", task);
    }

    #endregion

    #region Bill Reminder Workflow Methods

    private WorkflowResult MarkBillPaid(DomainTask task)
    {
        task.Status = (int)DomainAppTaskStatus.Completed;
        task.UpdatedAt = DateTime.UtcNow;

        // Add payment confirmation to description
        var paymentNote = $"\n[PAID: {DateTime.UtcNow:yyyy-MM-dd HH:mm}]";
        task.Description = (task.Description ?? "") + paymentNote;

        // Auto-archive paid bills
        ScheduleAutoArchive(task, AppTaskCategory.BillReminder);

        return WorkflowResult.Success("Bill marked as paid", task);
    }

    private WorkflowResult SetupRecurringBill(DomainTask task, Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("recurrencePattern", out var patternObj) ||
            patternObj is not RecurrencePattern pattern)
        {
            return WorkflowResult.Failed("Valid recurrence pattern required");
        }

        // Create recurring task logic would be implemented here
        var recurringResult = CreateRecurringTask(task, pattern);
        if (!recurringResult.IsSuccess)
        {
            return recurringResult;
        }

        // Mark original as completed
        task.Status = (int)DomainAppTaskStatus.Completed;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Recurring bill reminder set up", task);
    }

    private WorkflowResult SnoozeBillReminder(DomainTask task, Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("snoozeDays", out var daysObj) ||
            !int.TryParse(daysObj.ToString(), out var days) || days <= 0)
        {
            return WorkflowResult.Failed("Valid number of days required for snoozing");
        }

        task.DueDate = task.DueDate?.AddDays(days) ?? DateTime.UtcNow.AddDays(days);
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success($"Bill reminder snoozed for {days} days", task);
    }

    #endregion

    #region Project Workflow Methods

    private WorkflowResult AddProjectSubtask(DomainTask task, Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("subtaskTitle", out var titleObj) ||
            string.IsNullOrWhiteSpace(titleObj.ToString()))
        {
            return WorkflowResult.Failed("Subtask title is required");
        }

        // Subtask creation logic would be implemented here
        // This would typically involve creating a new task with the project as parent

        return WorkflowResult.Success("Subtask added to project", task);
    }

    private WorkflowResult UpdateProjectProgress(DomainTask task, Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("progressPercentage", out var progressObj) ||
            !int.TryParse(progressObj.ToString(), out var progress) ||
            progress < 0 || progress > 100)
        {
            return WorkflowResult.Failed("Valid progress percentage (0-100) required");
        }

        // Update project progress based on subtask completion
        // This would typically calculate progress from subtask completion rates

        if (progress >= 100)
        {
            task.Status = (int)DomainAppTaskStatus.Completed;
        }
        else if (progress > 0)
        {
            task.Status = (int)DomainAppTaskStatus.InProgress;
        }

        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success($"Project progress updated to {progress}%", task);
    }

    private WorkflowResult ReviewProjectMilestones(DomainTask task)
    {
        // Milestone review logic would analyze subtasks and deadlines
        return WorkflowResult.Success("Project milestones reviewed", task);
    }

    #endregion

    #region Idea Workflow Methods

    private WorkflowResult ConvertIdeaToTodo(DomainTask task)
    {
        if (!AppTaskCategory.Idea.CanConvertTo(AppTaskCategory.ToDo))
        {
            return WorkflowResult.Failed("Cannot convert idea to to-do");
        }

        task.Category = (int)AppTaskCategory.ToDo;
        task.Status = (int)DomainAppTaskStatus.Pending;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Idea converted to To-Do", task);
    }

    private WorkflowResult ConvertIdeaToProject(DomainTask task)
    {
        if (!AppTaskCategory.Idea.CanConvertTo(AppTaskCategory.Project))
        {
            return WorkflowResult.Failed("Cannot convert idea to project");
        }

        if (string.IsNullOrWhiteSpace(task.Description) || task.Description.Length < 20)
        {
            return WorkflowResult.Failed("Ideas must have detailed descriptions before converting to projects");
        }

        task.Category = (int)AppTaskCategory.Project;
        task.Status = (int)DomainAppTaskStatus.Pending;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Idea converted to Project", task);
    }

    private WorkflowResult ArchiveIdea(DomainTask task)
    {
        task.Status = (int)DomainAppTaskStatus.Archived;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Idea archived", task);
    }

    private WorkflowResult ElaborateIdea(DomainTask task, Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("additionalDetails", out var detailsObj) ||
            string.IsNullOrWhiteSpace(detailsObj.ToString()))
        {
            return WorkflowResult.Failed("Additional details required for idea elaboration");
        }

        var additionalDetails = detailsObj.ToString()!;
        task.Description = string.IsNullOrWhiteSpace(task.Description) 
            ? additionalDetails 
            : task.Description + "\n\n" + additionalDetails;

        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Idea elaborated with additional details", task);
    }

    #endregion

    #region Generic Workflow Methods

    private WorkflowResult StartTask(DomainTask task)
    {
        if (task.Status != (int)DomainAppTaskStatus.Pending)
        {
            return WorkflowResult.Failed("Only pending tasks can be started");
        }

        task.Status = (int)DomainAppTaskStatus.InProgress;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Task started", task);
    }

    private WorkflowResult PauseTask(DomainTask task)
    {
        if (task.Status != (int)DomainAppTaskStatus.InProgress)
        {
            return WorkflowResult.Failed("Only in-progress tasks can be paused");
        }

        task.Status = (int)DomainAppTaskStatus.Pending;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Task paused", task);
    }

    private WorkflowResult ResumeTask(DomainTask task)
    {
        if (task.Status != (int)DomainAppTaskStatus.Pending)
        {
            return WorkflowResult.Failed("Can only resume paused tasks");
        }

        task.Status = (int)DomainAppTaskStatus.InProgress;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("Task resumed", task);
    }

    private WorkflowResult SetTaskPriority(DomainTask task, Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("priority", out var priorityObj) ||
            !int.TryParse(priorityObj.ToString(), out var priority))
        {
            return WorkflowResult.Failed("Valid priority level required");
        }

        try
        {
            var priorityValue = Priority.FromValue(priority);
            task.Priority = priority;
            task.UpdatedAt = DateTime.UtcNow;

            return WorkflowResult.Success($"Task priority set to {priorityValue.GetDisplayName()}", task);
        }
        catch (ArgumentException)
        {
            return WorkflowResult.Failed("Invalid priority value");
        }
    }

    private WorkflowResult ConvertToDoToProject(DomainTask task)
    {
        task.Category = (int)AppTaskCategory.Project;
        task.UpdatedAt = DateTime.UtcNow;

        return WorkflowResult.Success("To-Do converted to Project", task);
    }

    #endregion

    #region Helper Methods

    private ValidationResult ValidateTaskCompletion(DomainTask task, AppTaskCategory category)
    {
        var errors = new List<string>();

        switch (category.Name)
        {
            case "Project":
                if (task.Subtasks?.Any(st => !st.IsDeleted && st.Status != (int)DomainAppTaskStatus.Completed) == true)
                {
                    errors.Add("Cannot complete project with incomplete subtasks");
                }
                break;

            case "Appointment":
                if (task.Status != (int)DomainAppTaskStatus.Confirmed && task.Status != (int)DomainAppTaskStatus.InProgress)
                {
                    errors.Add("Appointments must be confirmed before completion");
                }
                break;
        }

        return errors.Any() 
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    private WorkflowResult ApplyCategoryCompletionLogic(DomainTask task, AppTaskCategory category)
    {
        // Category-specific completion logic
        return category.Name switch
        {
            "BillReminder" => MarkBillPaid(task),
            "Appointment" => CompleteAppointment(task),
            _ => WorkflowResult.Success("Task ready for completion", task)
        };
    }

    private void ScheduleAutoArchive(DomainTask task, AppTaskCategory category)
    {
        // Auto-archiving logic would be implemented here
        // This could involve scheduling a background job or setting a flag
    }

    private bool IsCategorySuitableForRecurrence(AppTaskCategory category)
    {
        return category.Name is "BillReminder" or "Appointment";
    }

    private DomainTask CloneTaskForRecurrence(DomainTask original, RecurrencePattern pattern)
    {
        // Task cloning logic for recurrence
        return new DomainTask
        {
            Title = original.Title,
            Description = original.Description,
            Category = original.Category,
            Priority = original.Priority,
            UserId = original.UserId,
            DueDate = CalculateNextRecurrenceDate(original.DueDate, pattern),
            Status = (int)DomainAppTaskStatus.Pending
        };
    }

    private void ApplyCategorySpecificRecurrenceRules(DomainTask task, AppTaskCategory category, RecurrencePattern pattern)
    {
        switch (category.Name)
        {
            case "BillReminder":
                // Ensure bill reminders maintain payment details
                break;
            case "Appointment":
                // Reset appointment status for new occurrence
                task.Status = (int)DomainAppTaskStatus.Pending;
                break;
        }
    }

    private DateTime? CalculateNextRecurrenceDate(DateTime? baseDate, RecurrencePattern pattern)
    {
        if (!baseDate.HasValue) return DateTime.UtcNow.AddDays(30);

        return pattern.Type switch
        {
            RecurrenceType.Daily => baseDate.Value.AddDays(pattern.Interval),
            RecurrenceType.Weekly => baseDate.Value.AddDays(7 * pattern.Interval),
            RecurrenceType.Monthly => baseDate.Value.AddMonths(pattern.Interval),
            RecurrenceType.Yearly => baseDate.Value.AddYears(pattern.Interval),
            _ => baseDate.Value.AddDays(30)
        };
    }

    private string GetCurrentWorkflowStep(DomainTask task, AppTaskCategory category, DomainAppTaskStatus status)
    {
        if (category.Name == "Appointment")
        {
            if (status == DomainAppTaskStatus.Pending) return "Awaiting Confirmation";
            if (status == DomainAppTaskStatus.Confirmed) return "Confirmed";
            if (status == DomainAppTaskStatus.InProgress) return "In Progress";
            if (status == DomainAppTaskStatus.Completed) return "Completed";
        }
        else if (category.Name == "BillReminder")
        {
            if (status == DomainAppTaskStatus.Pending) return "Payment Due";
            if (status == DomainAppTaskStatus.InProgress) return "Processing Payment";
            if (status == DomainAppTaskStatus.Completed) return "Payment Completed";
        }
        else if (category.Name == "Project")
        {
            if (status == DomainAppTaskStatus.Pending) return "Planning Phase";
            if (status == DomainAppTaskStatus.InProgress) return "Execution Phase";
            if (status == DomainAppTaskStatus.Completed) return "Project Completed";
        }
        else if (category.Name == "Idea")
        {
            if (status == DomainAppTaskStatus.Pending) return "Idea Captured";
            if (status == DomainAppTaskStatus.InProgress) return "Under Development";
        }
        else if (category.Name == "ToDo")
        {
            if (status == DomainAppTaskStatus.Pending) return "Ready to Start";
            if (status == DomainAppTaskStatus.InProgress) return "In Progress";
        }

        return status.GetDisplayName();
    }

    private int CalculateProgressPercentage(DomainTask task, AppTaskCategory category, DomainAppTaskStatus status)
    {
        if (status == DomainAppTaskStatus.Pending) return 0;
        if (status == DomainAppTaskStatus.Confirmed) return 25;
        if (status == DomainAppTaskStatus.InProgress) return 50;
        if (status == DomainAppTaskStatus.Completed) return 100;
        return 0;
    }

    private TimeSpan? EstimateTimeRemaining(DomainTask task, AppTaskCategory category, DomainAppTaskStatus status)
    {
        if (status == DomainAppTaskStatus.Completed)
            return TimeSpan.Zero;

        var baseHours = category.GetEstimatedHours();
        var progressPercentage = CalculateProgressPercentage(task, category, status);
        var remainingPercentage = (100 - progressPercentage) / 100.0;

        return TimeSpan.FromHours(baseHours * remainingPercentage);
    }

    private List<string> IdentifyBlockers(DomainTask task, AppTaskCategory category)
    {
        var blockers = new List<string>();

        if (category.RequiresDueDate && !task.DueDate.HasValue)
        {
            blockers.Add("Missing required due date");
        }

        if (category.Name == "Project" && (task.Subtasks == null || !task.Subtasks.Any()))
        {
            blockers.Add("No subtasks defined for project");
        }

        if (task.DueDate.HasValue && task.DueDate.Value < DateTime.UtcNow)
        {
            blockers.Add("Task is overdue");
        }

        return blockers;
    }

    #endregion
}

/// <summary>
/// Represents a workflow action to be performed on a task
/// </summary>
public class WorkflowAction
{
    public WorkflowActionType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Types of workflow actions
/// </summary>
public enum WorkflowActionType
{
    Start,
    Complete,
    Pause,
    Resume,
    Confirm,
    Reschedule,
    Cancel,
    MarkPaid,
    SetRecurring,
    Snooze,
    AddSubtask,
    UpdateProgress,
    ReviewMilestones,
    ConvertToTodo,
    ConvertToProject,
    Archive,
    Elaborate,
    SetPriority,
    AddSubtasks
}

/// <summary>
/// Current state of a task's workflow
/// </summary>
public class WorkflowState
{
    public string CurrentStep { get; set; } = null!;
    public List<AppTaskAction> AvailableActions { get; set; } = new();
    public DomainAppTaskStatus? NextRecommendedStatus { get; set; }
    public int ProgressPercentage { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public List<string> Blockers { get; set; } = new();
}

/// <summary>
/// Result of a workflow operation
/// </summary>
public class WorkflowResult
{
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; }
    public DomainTask? UpdatedTask { get; private set; }
    public Exception? Exception { get; private set; }

    private WorkflowResult(bool isSuccess, string message, DomainTask? updatedTask = null, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        Message = message;
        UpdatedTask = updatedTask;
        Exception = exception;
    }

    public static WorkflowResult Success(string message, DomainTask? updatedTask = null)
    {
        return new WorkflowResult(true, message, updatedTask);
    }

    public static WorkflowResult Failed(string message, Exception? exception = null)
    {
        return new WorkflowResult(false, message, null, exception);
    }
}

/// <summary>
/// Recurrence pattern for recurring tasks
/// </summary>
public class RecurrencePattern
{
    public RecurrenceType Type { get; set; }
    public int Interval { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxOccurrences { get; set; }
}

/// <summary>
/// Types of recurrence patterns
/// </summary>
public enum RecurrenceType
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}