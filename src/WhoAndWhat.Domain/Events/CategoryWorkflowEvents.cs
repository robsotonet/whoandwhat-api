using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Events;

/// <summary>
/// Domain event raised when a task category is changed
/// </summary>
public class TaskCategoryChangedEvent : IDomainEvent
{
    public Guid TaskId { get; }
    public Guid UserId { get; }
    public string FromCategory { get; }
    public string ToCategory { get; }
    public string? Reason { get; }
    public DateTime DateOccurred { get; }

    public TaskCategoryChangedEvent(Guid taskId, Guid userId, string fromCategory, string toCategory, string? reason = null)
    {
        TaskId = taskId;
        UserId = userId;
        FromCategory = fromCategory;
        ToCategory = toCategory;
        Reason = reason;
        DateOccurred = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain event raised when a task is completed
/// </summary>
public class TaskCompletedEvent : IDomainEvent
{
    public Guid TaskId { get; }
    public Guid UserId { get; }
    public string Category { get; }
    public TimeSpan CompletionTime { get; }
    public bool CompletedOnTime { get; }
    public DateTime DateOccurred { get; }

    public TaskCompletedEvent(Guid taskId, Guid userId, string category, DateTime createdAt, DateTime? dueDate = null)
    {
        TaskId = taskId;
        UserId = userId;
        Category = category;
        DateOccurred = DateTime.UtcNow;
        CompletionTime = DateOccurred - createdAt;
        CompletedOnTime = !dueDate.HasValue || DateOccurred <= dueDate.Value;
    }
}

/// <summary>
/// Domain event raised when an appointment is confirmed
/// </summary>
public class AppointmentConfirmedEvent : IDomainEvent
{
    public Guid TaskId { get; }
    public Guid UserId { get; }
    public DateTime AppointmentDate { get; }
    public string? Location { get; }
    public DateTime DateOccurred { get; }

    public AppointmentConfirmedEvent(Guid taskId, Guid userId, DateTime appointmentDate, string? location = null)
    {
        TaskId = taskId;
        UserId = userId;
        AppointmentDate = appointmentDate;
        Location = location;
        DateOccurred = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain event raised when an appointment is rescheduled
/// </summary>
public class AppointmentRescheduledEvent : IDomainEvent
{
    public Guid TaskId { get; }
    public Guid UserId { get; }
    public DateTime OriginalDate { get; }
    public DateTime NewDate { get; }
    public string? Reason { get; }
    public DateTime DateOccurred { get; }

    public AppointmentRescheduledEvent(Guid taskId, Guid userId, DateTime originalDate, DateTime newDate, string? reason = null)
    {
        TaskId = taskId;
        UserId = userId;
        OriginalDate = originalDate;
        NewDate = newDate;
        Reason = reason;
        DateOccurred = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain event raised when a bill is marked as paid
/// </summary>
public class BillPaidEvent : IDomainEvent
{
    public Guid TaskId { get; }
    public Guid UserId { get; }
    public decimal? Amount { get; }
    public string? PaymentMethod { get; }
    public DateTime DueDate { get; }
    public bool PaidOnTime { get; }
    public DateTime DateOccurred { get; }

    public BillPaidEvent(Guid taskId, Guid userId, DateTime dueDate, decimal? amount = null, string? paymentMethod = null)
    {
        TaskId = taskId;
        UserId = userId;
        Amount = amount;
        PaymentMethod = paymentMethod;
        DueDate = dueDate;
        DateOccurred = DateTime.UtcNow;
        PaidOnTime = DateOccurred <= dueDate;
    }
}

/// <summary>
/// Domain event raised when a recurring bill reminder is set up
/// </summary>
public class RecurringBillSetupEvent : IDomainEvent
{
    public Guid OriginalTaskId { get; }
    public Guid UserId { get; }
    public string RecurrencePattern { get; }
    public DateTime NextDueDate { get; }
    public DateTime DateOccurred { get; }

    public RecurringBillSetupEvent(Guid originalTaskId, Guid userId, string recurrencePattern, DateTime nextDueDate)
    {
        OriginalTaskId = originalTaskId;
        UserId = userId;
        RecurrencePattern = recurrencePattern;
        NextDueDate = nextDueDate;
        DateOccurred = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain event raised when a project milestone is reached
/// </summary>
public class ProjectMilestoneReachedEvent : IDomainEvent
{
    public Guid ProjectId { get; }
    public Guid UserId { get; }
    public string MilestoneName { get; }
    public int ProgressPercentage { get; }
    public int CompletedSubtasks { get; }
    public int TotalSubtasks { get; }
    public DateTime DateOccurred { get; }

    public ProjectMilestoneReachedEvent(Guid projectId, Guid userId, string milestoneName, int progressPercentage, int completedSubtasks, int totalSubtasks)
    {
        ProjectId = projectId;
        UserId = userId;
        MilestoneName = milestoneName;
        ProgressPercentage = progressPercentage;
        CompletedSubtasks = completedSubtasks;
        TotalSubtasks = totalSubtasks;
        DateOccurred = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain event raised when an idea is converted to another category
/// </summary>
public class IdeaConvertedEvent : IDomainEvent
{
    public Guid TaskId { get; }
    public Guid UserId { get; }
    public string ToCategory { get; }
    public string? ConversionReason { get; }
    public DateTime DateOccurred { get; }

    public IdeaConvertedEvent(Guid taskId, Guid userId, string toCategory, string? conversionReason = null)
    {
        TaskId = taskId;
        UserId = userId;
        ToCategory = toCategory;
        ConversionReason = conversionReason;
        DateOccurred = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain event raised when a task workflow is blocked
/// </summary>
public class TaskWorkflowBlockedEvent : IDomainEvent
{
    public Guid TaskId { get; }
    public Guid UserId { get; }
    public string Category { get; }
    public string CurrentStatus { get; }
    public List<string> Blockers { get; }
    public DateTime DateOccurred { get; }

    public TaskWorkflowBlockedEvent(Guid taskId, Guid userId, string category, string currentStatus, List<string> blockers)
    {
        TaskId = taskId;
        UserId = userId;
        Category = category;
        CurrentStatus = currentStatus;
        Blockers = blockers;
        DateOccurred = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain event raised when category-specific automation is triggered
/// </summary>
public class CategoryAutomationTriggeredEvent : IDomainEvent
{
    public Guid TaskId { get; }
    public Guid UserId { get; }
    public string Category { get; }
    public string AutomationType { get; }
    public Dictionary<string, object> Parameters { get; }
    public DateTime DateOccurred { get; }

    public CategoryAutomationTriggeredEvent(Guid taskId, Guid userId, string category, string automationType, Dictionary<string, object> parameters)
    {
        TaskId = taskId;
        UserId = userId;
        Category = category;
        AutomationType = automationType;
        Parameters = parameters;
        DateOccurred = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain event raised when task priority is automatically adjusted based on category rules
/// </summary>
public class TaskPriorityAutoAdjustedEvent : IDomainEvent
{
    public Guid TaskId { get; }
    public Guid UserId { get; }
    public string Category { get; }
    public string FromPriority { get; }
    public string ToPriority { get; }
    public string Reason { get; }
    public DateTime DateOccurred { get; }

    public TaskPriorityAutoAdjustedEvent(Guid taskId, Guid userId, string category, string fromPriority, string toPriority, string reason)
    {
        TaskId = taskId;
        UserId = userId;
        Category = category;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        Reason = reason;
        DateOccurred = DateTime.UtcNow;
    }
}
