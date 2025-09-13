using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Represents a scheduled item in a user's smart schedule
/// </summary>
public class ScheduledItem : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Task and Calendar Integration
    public Guid? TaskId { get; set; }
    public AppTask? Task { get; set; }

    public string? CalendarEventId { get; set; }
    public string? CalendarSource { get; set; } // "Google", "Outlook", "iCloud", etc.

    // Schedule Details
    public string Title { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan EstimatedDuration { get; set; }

    // Classification
    public int ItemType { get; set; } // Maps to ScheduledItemType enum
    public int Priority { get; set; } // Maps to Priority enum
    public string Category { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty; // Comma-separated

    // Scheduling Properties
    public bool IsFlexible { get; set; } = true;
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public bool IsRecurring { get; set; } = false;
    public string? RecurrencePattern { get; set; } // JSON for recurrence rules

    // Optimization Data
    public string SchedulingReasons { get; set; } = string.Empty; // JSON array of SchedulingReason
    public double ConfidenceScore { get; set; } = 0.0;
    public int OptimizationVersion { get; set; } = 1;

    // Relationships
    public Guid? TimeBlockId { get; set; }
    public TimeBlock? TimeBlock { get; set; }

    public Guid? SchedulingPreferenceId { get; set; }
    public UserSchedulingPreference? SchedulingPreference { get; set; }

    // Domain Properties

    /// <summary>
    /// Gets the actual duration of the scheduled item
    /// </summary>
    public TimeSpan ActualDuration => EndTime - StartTime;

    /// <summary>
    /// Gets whether the item is currently active (in progress)
    /// </summary>
    public bool IsActive => DateTime.UtcNow >= StartTime && DateTime.UtcNow <= EndTime && !IsCompleted;

    /// <summary>
    /// Gets whether the item is overdue (past end time and not completed)
    /// </summary>
    public bool IsOverdue => DateTime.UtcNow > EndTime && !IsCompleted;

    /// <summary>
    /// Gets whether the item is upcoming (start time is in the future)
    /// </summary>
    public bool IsUpcoming => StartTime > DateTime.UtcNow;

    /// <summary>
    /// Gets whether the item has a time conflict potential
    /// </summary>
    public bool HasPotentialConflict => !IsFlexible && IsUpcoming;

    // Domain Methods

    /// <summary>
    /// Gets the tags as a list of strings
    /// </summary>
    public List<string> GetTags()
    {
        if (string.IsNullOrEmpty(Tags))
            return new List<string>();

        return Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToList();
    }

    /// <summary>
    /// Sets the tags from a list of strings
    /// </summary>
    public void SetTags(List<string> tags)
    {
        Tags = string.Join(",", tags.Where(t => !string.IsNullOrWhiteSpace(t)));
        MarkAsModified();
    }

    /// <summary>
    /// Marks the item as completed
    /// </summary>
    public void MarkAsCompleted()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Reopens a completed item
    /// </summary>
    public void Reopen()
    {
        if (!IsCompleted)
            return;

        IsCompleted = false;
        CompletedAt = null;
        MarkAsModified();
    }

    /// <summary>
    /// Reschedules the item to a new time slot
    /// </summary>
    public void Reschedule(DateTime newStartTime, DateTime newEndTime, string reason = "")
    {
        if (newStartTime >= newEndTime)
            throw new ArgumentException("Start time must be before end time");

        var oldStartTime = StartTime;
        var oldEndTime = EndTime;

        StartTime = newStartTime;
        EndTime = newEndTime;
        EstimatedDuration = newEndTime - newStartTime;

        // Update scheduling reasons
        AddSchedulingReason("reschedule", string.IsNullOrEmpty(reason) ?
            $"Rescheduled from {oldStartTime:HH:mm} to {newStartTime:HH:mm}" : reason, 1.0);

        MarkAsModified();
    }

    /// <summary>
    /// Extends the duration of the item
    /// </summary>
    public void ExtendDuration(TimeSpan extension, string reason = "")
    {
        if (extension <= TimeSpan.Zero)
            throw new ArgumentException("Extension must be positive");

        EndTime = EndTime.Add(extension);
        EstimatedDuration = EstimatedDuration.Add(extension);

        AddSchedulingReason("extend", string.IsNullOrEmpty(reason) ?
            $"Extended duration by {extension.TotalMinutes} minutes" : reason, 0.8);

        MarkAsModified();
    }

    /// <summary>
    /// Sets the item as high priority and adjusts flexibility
    /// </summary>
    public void SetUrgent(string reason = "")
    {
        Priority = (int)Domain.ValueObjects.Priority.Urgent;
        IsFlexible = false;

        AddSchedulingReason("urgent", string.IsNullOrEmpty(reason) ?
            "Marked as urgent priority" : reason, 1.0);

        MarkAsModified();
    }

    /// <summary>
    /// Checks if this item conflicts with another scheduled item
    /// </summary>
    public bool ConflictsWith(ScheduledItem other)
    {
        if (other == null || Id == other.Id)
            return false;

        return StartTime < other.EndTime && other.StartTime < EndTime;
    }

    /// <summary>
    /// Gets the buffer time needed before this item
    /// </summary>
    public TimeSpan GetRequiredBufferTime()
    {
        // More buffer time for important, inflexible items
        if (!IsFlexible && Priority >= (int)Domain.ValueObjects.Priority.High)
            return TimeSpan.FromMinutes(20);

        if (ItemType == (int)ScheduledItemType.Meeting)
            return TimeSpan.FromMinutes(15);

        return TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Adds a scheduling reason to the item
    /// </summary>
    private void AddSchedulingReason(string reasonType, string description, double influenceWeight)
    {
        var reasons = GetSchedulingReasons();

        reasons.Add(new
        {
            ReasonType = reasonType,
            Description = description,
            InfluenceWeight = influenceWeight,
            Timestamp = DateTime.UtcNow
        });

        // Keep only the last 10 reasons to prevent data bloat
        if (reasons.Count > 10)
        {
            reasons.RemoveRange(0, reasons.Count - 10);
        }

        SchedulingReasons = System.Text.Json.JsonSerializer.Serialize(reasons);
    }

    /// <summary>
    /// Gets the scheduling reasons as a dynamic list
    /// </summary>
    private List<dynamic> GetSchedulingReasons()
    {
        if (string.IsNullOrEmpty(SchedulingReasons))
            return new List<dynamic>();

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<dynamic>>(SchedulingReasons) ?? new List<dynamic>();
        }
        catch
        {
            return new List<dynamic>();
        }
    }

    /// <summary>
    /// Updates the confidence score based on successful scheduling
    /// </summary>
    public void UpdateConfidenceScore(double score)
    {
        ConfidenceScore = Math.Max(0.0, Math.Min(1.0, score));
        MarkAsModified();
    }

    /// <summary>
    /// Creates a scheduled item from a task
    /// </summary>
    public static ScheduledItem FromTask(AppTask task, DateTime startTime, DateTime endTime)
    {
        return new ScheduledItem
        {
            UserId = task.UserId,
            TaskId = task.Id,
            Task = task,
            Title = task.Title,
            Description = task.Description ?? string.Empty,
            StartTime = startTime,
            EndTime = endTime,
            EstimatedDuration = endTime - startTime,
            ItemType = (int)ScheduledItemType.Task,
            Priority = task.Priority,
            Category = task.Category.ToString(),
            IsFlexible = task.Priority < (int)Domain.ValueObjects.Priority.High,
            ConfidenceScore = 0.8,
            OptimizationVersion = 1
        };
    }

    /// <summary>
    /// Creates a time block scheduled item
    /// </summary>
    public static ScheduledItem CreateTimeBlock(Guid userId, string title, DateTime startTime,
        DateTime endTime, ScheduledItemType itemType = ScheduledItemType.TimeBlock)
    {
        return new ScheduledItem
        {
            UserId = userId,
            Title = title,
            Description = $"Time block for {title.ToLower()}",
            StartTime = startTime,
            EndTime = endTime,
            EstimatedDuration = endTime - startTime,
            ItemType = (int)itemType,
            Priority = (int)Domain.ValueObjects.Priority.Medium,
            Category = "TimeBlock",
            IsFlexible = true,
            ConfidenceScore = 0.9,
            OptimizationVersion = 1
        };
    }
}

/// <summary>
/// Enum for scheduled item types to match the DTO definition
/// </summary>
public enum ScheduledItemType
{
    Task = 0,
    CalendarEvent = 1,
    Break = 2,
    TimeBlock = 3,
    Buffer = 4,
    Meeting = 5,
    FocusTime = 6
}
