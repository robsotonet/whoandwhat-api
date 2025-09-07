using WhoAndWhat.Domain.Common;

namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Rich value object representing task status with business rules and validation
/// </summary>
public record TaskStatus
{
    public static readonly TaskStatus Pending = new("Pending", 0, "Task is pending and not yet started");
    public static readonly TaskStatus InProgress = new("InProgress", 1, "Task is currently being worked on");
    public static readonly TaskStatus Completed = new("Completed", 2, "Task has been completed successfully");
    public static readonly TaskStatus Archived = new("Archived", 3, "Task has been archived for record-keeping");

    private static readonly IReadOnlyList<TaskStatus> AllStatuses = new List<TaskStatus>
    {
        Pending, InProgress, Completed, Archived
    };

    private static readonly IReadOnlyDictionary<int, TaskStatus> StatusByValue = AllStatuses.ToDictionary(s => s.Value);
    private static readonly IReadOnlyDictionary<string, TaskStatus> StatusByName = AllStatuses.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public int Value { get; }
    public string Description { get; }

    private TaskStatus(string name, int value, string description)
    {
        Name = name;
        Value = value;
        Description = description;
    }

    /// <summary>
    /// Gets all available task statuses
    /// </summary>
    /// <returns>Collection of all task statuses</returns>
    public static IEnumerable<TaskStatus> GetAll() => AllStatuses;

    /// <summary>
    /// Creates TaskStatus from integer value
    /// </summary>
    /// <param name="value">Integer value</param>
    /// <returns>TaskStatus instance</returns>
    /// <exception cref="ArgumentException">When value is invalid</exception>
    public static TaskStatus FromValue(int value)
    {
        if (StatusByValue.TryGetValue(value, out var status))
        {
            return status;
        }

        throw new ArgumentException($"Invalid task status value: {value}", nameof(value));
    }

    /// <summary>
    /// Creates TaskStatus from name string
    /// </summary>
    /// <param name="name">Status name</param>
    /// <returns>TaskStatus instance</returns>
    /// <exception cref="ArgumentException">When name is invalid</exception>
    public static TaskStatus FromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task status name cannot be null or empty", nameof(name));
        }

        if (StatusByName.TryGetValue(name.Trim(), out var status))
        {
            return status;
        }

        throw new ArgumentException($"Invalid task status name: {name}", nameof(name));
    }

    /// <summary>
    /// Tries to create TaskStatus from integer value
    /// </summary>
    /// <param name="value">Integer value</param>
    /// <param name="status">Output TaskStatus if successful</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool TryFromValue(int value, out TaskStatus? status)
    {
        return StatusByValue.TryGetValue(value, out status);
    }

    /// <summary>
    /// Tries to create TaskStatus from name string
    /// </summary>
    /// <param name="name">Status name</param>
    /// <param name="status">Output TaskStatus if successful</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool TryFromName(string name, out TaskStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return StatusByName.TryGetValue(name.Trim(), out status);
    }

    /// <summary>
    /// Determines if a transition from this status to the target status is valid
    /// </summary>
    /// <param name="targetStatus">Target status to transition to</param>
    /// <returns>True if the transition is valid</returns>
    public bool CanTransitionTo(TaskStatus targetStatus)
    {
        // Cannot transition to the same status
        if (this == targetStatus)
        {
            return false;
        }

        return this switch
        {
            _ when this == Pending => targetStatus == InProgress || targetStatus == Completed,
            _ when this == InProgress => targetStatus == Completed || targetStatus == Pending,
            _ when this == Completed => targetStatus == Archived,
            _ when this == Archived => false, // Cannot transition from archived
            _ => false
        };
    }

    /// <summary>
    /// Gets all valid transition statuses from the current status
    /// </summary>
    /// <returns>Collection of valid target statuses</returns>
    public IEnumerable<TaskStatus> GetValidTransitions()
    {
        return AllStatuses.Where(CanTransitionTo);
    }

    /// <summary>
    /// Determines if this is a terminal status (no further transitions possible)
    /// </summary>
    /// <returns>True if this is a terminal status</returns>
    public bool IsTerminal() => this == Archived;

    /// <summary>
    /// Determines if this status represents an active task
    /// </summary>
    /// <returns>True if task is active</returns>
    public bool IsActive() => this == Pending || this == InProgress;

    /// <summary>
    /// Determines if this status represents a completed task
    /// </summary>
    /// <returns>True if task is completed</returns>
    public bool IsCompleted() => this == Completed;

    /// <summary>
    /// Gets the display-friendly name of the status
    /// </summary>
    /// <returns>Display name</returns>
    public string GetDisplayName() => Name switch
    {
        "Pending" => "Pending",
        "InProgress" => "In Progress", 
        "Completed" => "Completed",
        "Archived" => "Archived",
        _ => Name
    };

    /// <summary>
    /// Gets CSS class name for UI styling
    /// </summary>
    /// <returns>CSS class name</returns>
    public string GetCssClass() => Name switch
    {
        "Pending" => "status-pending",
        "InProgress" => "status-in-progress",
        "Completed" => "status-completed", 
        "Archived" => "status-archived",
        _ => "status-unknown"
    };

    /// <summary>
    /// Gets color code associated with the status
    /// </summary>
    /// <returns>Hex color code</returns>
    public string GetColorCode() => Name switch
    {
        "Pending" => "#6c757d",     // Gray
        "InProgress" => "#007bff",  // Blue
        "Completed" => "#28a745",   // Green
        "Archived" => "#17a2b8",    // Teal
        _ => "#000000"
    };

    /// <summary>
    /// Validates transition rules for business logic
    /// </summary>
    /// <param name="targetStatus">Target status</param>
    /// <param name="hasActiveSubtasks">Whether the task has active subtasks</param>
    /// <param name="taskCategory">Task category</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidateTransition(TaskStatus targetStatus, bool hasActiveSubtasks = false, TaskCategory? taskCategory = null)
    {
        var errors = new List<string>();

        if (!CanTransitionTo(targetStatus))
        {
            errors.Add($"Cannot transition from {GetDisplayName()} to {targetStatus.GetDisplayName()}");
        }

        // Specific business rules
        if (targetStatus == Completed && hasActiveSubtasks)
        {
            if (taskCategory?.Name != "Idea" && taskCategory?.Name != "Project")
            {
                errors.Add("Cannot complete task while it has active subtasks");
            }
        }

        if (targetStatus == Archived && this != Completed)
        {
            errors.Add("Only completed tasks can be archived");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }

    // Implicit conversion from TaskStatus to int for database storage
    public static implicit operator int(TaskStatus status) => status.Value;

    // Explicit conversion from int to TaskStatus
    public static explicit operator TaskStatus(int value) => FromValue(value);

    public override string ToString() => Name;

}