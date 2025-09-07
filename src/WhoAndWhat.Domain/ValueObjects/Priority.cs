using WhoAndWhat.Domain.Common;

namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Rich value object representing task priority with ordering and comparison logic
/// </summary>
public record Priority : IComparable<Priority>
{
    public static readonly Priority Low = new("Low", 0, "Low priority - can be done when time allows", "#28a745", 7);
    public static readonly Priority Medium = new("Medium", 1, "Medium priority - should be done soon", "#ffc107", 3);
    public static readonly Priority High = new("High", 2, "High priority - should be done today", "#fd7e14", 1);
    public static readonly Priority Urgent = new("Urgent", 3, "Urgent priority - needs immediate attention", "#dc3545", 0);

    private static readonly IReadOnlyList<Priority> AllPriorities = new List<Priority>
    {
        Low, Medium, High, Urgent
    };

    private static readonly IReadOnlyDictionary<int, Priority> PriorityByValue = AllPriorities.ToDictionary(p => p.Value);
    private static readonly IReadOnlyDictionary<string, Priority> PriorityByName = AllPriorities.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public int Value { get; }
    public string Description { get; }
    public string ColorCode { get; }
    public int SortOrder { get; } // Lower numbers = higher priority for sorting

    private Priority(string name, int value, string description, string colorCode, int sortOrder)
    {
        Name = name;
        Value = value;
        Description = description;
        ColorCode = colorCode;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Gets all available priorities ordered by importance (highest first)
    /// </summary>
    /// <returns>Collection of all priorities</returns>
    public static IEnumerable<Priority> GetAll() => AllPriorities.OrderBy(p => p.SortOrder);

    /// <summary>
    /// Gets all priorities ordered by value (lowest first)
    /// </summary>
    /// <returns>Collection of priorities by value order</returns>
    public static IEnumerable<Priority> GetAllByValue() => AllPriorities.OrderBy(p => p.Value);

    /// <summary>
    /// Creates Priority from integer value
    /// </summary>
    /// <param name="value">Integer value</param>
    /// <returns>Priority instance</returns>
    /// <exception cref="ArgumentException">When value is invalid</exception>
    public static Priority FromValue(int value)
    {
        if (PriorityByValue.TryGetValue(value, out var priority))
        {
            return priority;
        }

        throw new ArgumentException($"Invalid priority value: {value}", nameof(value));
    }

    /// <summary>
    /// Creates Priority from name string
    /// </summary>
    /// <param name="name">Priority name</param>
    /// <returns>Priority instance</returns>
    /// <exception cref="ArgumentException">When name is invalid</exception>
    public static Priority FromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Priority name cannot be null or empty", nameof(name));
        }

        if (PriorityByName.TryGetValue(name.Trim(), out var priority))
        {
            return priority;
        }

        throw new ArgumentException($"Invalid priority name: {name}", nameof(name));
    }

    /// <summary>
    /// Tries to create Priority from integer value
    /// </summary>
    /// <param name="value">Integer value</param>
    /// <param name="priority">Output Priority if successful</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool TryFromValue(int value, out Priority? priority)
    {
        return PriorityByValue.TryGetValue(value, out priority);
    }

    /// <summary>
    /// Tries to create Priority from name string
    /// </summary>
    /// <param name="name">Priority name</param>
    /// <param name="priority">Output Priority if successful</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool TryFromName(string name, out Priority? priority)
    {
        priority = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return PriorityByName.TryGetValue(name.Trim(), out priority);
    }

    /// <summary>
    /// Gets priorities that are considered high importance (High and Urgent)
    /// </summary>
    /// <returns>Collection of high importance priorities</returns>
    public static IEnumerable<Priority> GetHighImportance()
    {
        return AllPriorities.Where(p => p == High || p == Urgent);
    }

    /// <summary>
    /// Gets priorities that are considered low importance (Low and Medium)
    /// </summary>
    /// <returns>Collection of low importance priorities</returns>
    public static IEnumerable<Priority> GetLowImportance()
    {
        return AllPriorities.Where(p => p == Low || p == Medium);
    }

    /// <summary>
    /// Determines if this priority can be escalated to a higher level
    /// </summary>
    /// <returns>True if escalation is possible</returns>
    public bool CanEscalate()
    {
        return this != Urgent;
    }

    /// <summary>
    /// Escalates the priority to the next higher level
    /// </summary>
    /// <returns>Higher priority level, or same if already at maximum</returns>
    public Priority Escalate()
    {
        return this switch
        {
            _ when this == Low => Medium,
            _ when this == Medium => High,
            _ when this == High => Urgent,
            _ when this == Urgent => Urgent, // Cannot escalate further
            _ => this
        };
    }

    /// <summary>
    /// Determines if this priority can be de-escalated to a lower level
    /// </summary>
    /// <returns>True if de-escalation is possible</returns>
    public bool CanDeEscalate()
    {
        return this != Low;
    }

    /// <summary>
    /// De-escalates the priority to the next lower level
    /// </summary>
    /// <returns>Lower priority level, or same if already at minimum</returns>
    public Priority DeEscalate()
    {
        return this switch
        {
            _ when this == Urgent => High,
            _ when this == High => Medium,
            _ when this == Medium => Low,
            _ when this == Low => Low, // Cannot de-escalate further
            _ => this
        };
    }

    /// <summary>
    /// Determines if this priority is higher than another priority
    /// </summary>
    /// <param name="other">Priority to compare against</param>
    /// <returns>True if this priority is higher</returns>
    public bool IsHigherThan(Priority other)
    {
        return SortOrder < other.SortOrder;
    }

    /// <summary>
    /// Determines if this priority is lower than another priority
    /// </summary>
    /// <param name="other">Priority to compare against</param>
    /// <returns>True if this priority is lower</returns>
    public bool IsLowerThan(Priority other)
    {
        return SortOrder > other.SortOrder;
    }

    /// <summary>
    /// Gets the recommended due date offset based on priority level
    /// </summary>
    /// <returns>Number of days from now for recommended due date</returns>
    public int GetRecommendedDueDateOffset()
    {
        return this switch
        {
            _ when this == Urgent => 0,    // Due today
            _ when this == High => 1,      // Due tomorrow
            _ when this == Medium => 7,    // Due in a week
            _ when this == Low => 30,      // Due in a month
            _ => 7
        };
    }

    /// <summary>
    /// Gets the notification lead time in hours before due date
    /// </summary>
    /// <returns>Hours before due date to send notification</returns>
    public int GetNotificationLeadHours()
    {
        return this switch
        {
            _ when this == Urgent => 1,    // 1 hour before
            _ when this == High => 4,      // 4 hours before
            _ when this == Medium => 24,   // 1 day before
            _ when this == Low => 168,     // 1 week before
            _ => 24
        };
    }

    /// <summary>
    /// Suggests a priority based on due date urgency
    /// </summary>
    /// <param name="dueDate">Task due date</param>
    /// <returns>Suggested priority level</returns>
    public static Priority SuggestFromDueDate(DateTime? dueDate)
    {
        if (!dueDate.HasValue)
        {
            return Medium;
        }

        var daysUntilDue = (dueDate.Value.Date - DateTime.UtcNow.Date).TotalDays;

        return daysUntilDue switch
        {
            <= 0 => Urgent,        // Overdue or due today
            <= 1 => High,          // Due tomorrow
            <= 7 => Medium,        // Due within a week
            _ => Low               // Due later
        };
    }

    /// <summary>
    /// Validates priority assignment based on task context
    /// </summary>
    /// <param name="taskCategory">Task category</param>
    /// <param name="dueDate">Task due date</param>
    /// <param name="hasSubtasks">Whether task has subtasks</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidatePriorityAssignment(TaskCategory? taskCategory, DateTime? dueDate, bool hasSubtasks = false)
    {
        var warnings = new List<string>();

        // Check if priority matches due date urgency
        if (dueDate.HasValue)
        {
            var suggestedPriority = SuggestFromDueDate(dueDate);
            if (IsLowerThan(suggestedPriority))
            {
                warnings.Add($"Priority might be too low for due date. Consider {suggestedPriority.GetDisplayName()} priority.");
            }
        }

        // Category-specific recommendations
        if (taskCategory != null)
        {
            switch (taskCategory.Name)
            {
                case "Appointment" when IsLowerThan(High):
                    warnings.Add("Appointments typically should be High or Urgent priority");
                    break;
                    
                case "BillReminder" when IsLowerThan(Medium):
                    warnings.Add("Bill reminders should typically be Medium priority or higher");
                    break;
                    
                case "Project" when this == Urgent:
                    warnings.Add("Projects are rarely urgent - consider breaking into smaller tasks");
                    break;
                    
                case "Idea" when IsHigherThan(Medium):
                    warnings.Add("Ideas are typically Low or Medium priority until developed");
                    break;
            }
        }

        // Subtask considerations
        if (hasSubtasks && this == Urgent)
        {
            warnings.Add("Tasks with subtasks are rarely urgent - consider prioritizing specific subtasks");
        }

        // Always return success for priorities - warnings don't invalidate
        var result = ValidationResult.Success();
        result.Errors.AddRange(warnings);
        return result;
    }

    /// <summary>
    /// Gets the display-friendly name of the priority
    /// </summary>
    /// <returns>Display name</returns>
    public string GetDisplayName() => Name;

    /// <summary>
    /// Gets CSS class name for UI styling
    /// </summary>
    /// <returns>CSS class name</returns>
    public string GetCssClass() => Name.ToLowerInvariant() switch
    {
        "low" => "priority-low",
        "medium" => "priority-medium",
        "high" => "priority-high",
        "urgent" => "priority-urgent",
        _ => "priority-unknown"
    };

    /// <summary>
    /// Gets icon name for UI display
    /// </summary>
    /// <returns>Icon name</returns>
    public string GetIconName() => this switch
    {
        _ when this == Low => "arrow-down",
        _ when this == Medium => "minus",
        _ when this == High => "arrow-up",
        _ when this == Urgent => "exclamation-triangle",
        _ => "question"
    };

    /// <summary>
    /// Gets priority weight for scoring algorithms
    /// </summary>
    /// <returns>Numeric weight (higher = more important)</returns>
    public double GetWeight()
    {
        return this switch
        {
            _ when this == Low => 1.0,
            _ when this == Medium => 2.0,
            _ when this == High => 4.0,
            _ when this == Urgent => 8.0,
            _ => 1.0
        };
    }

    /// <summary>
    /// Gets urgency multiplier for scheduling algorithms
    /// </summary>
    /// <returns>Numeric multiplier for urgency calculation</returns>
    public double GetUrgencyMultiplier()
    {
        return this switch
        {
            _ when this == Low => 0.5,
            _ when this == Medium => 1.0,
            _ when this == High => 2.0,
            _ when this == Urgent => 4.0,
            _ => 1.0
        };
    }

    // Implicit conversion from Priority to int for database storage
    public static implicit operator int(Priority priority) => priority.Value;

    // Explicit conversion from int to Priority
    public static explicit operator Priority(int value) => FromValue(value);

    // Comparison operators
    public static bool operator >(Priority left, Priority right) => left.IsHigherThan(right);
    public static bool operator <(Priority left, Priority right) => left.IsLowerThan(right);
    public static bool operator >=(Priority left, Priority right) => left.IsHigherThan(right) || left == right;
    public static bool operator <=(Priority left, Priority right) => left.IsLowerThan(right) || left == right;

    public int CompareTo(Priority? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }
        if (ReferenceEquals(null, other))
        {
            return 1;
        }
        return SortOrder.CompareTo(other.SortOrder);
    }

    public override string ToString() => Name;
}