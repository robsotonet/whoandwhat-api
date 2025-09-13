using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Represents a calendar event with rich domain behavior and external calendar integration
/// </summary>
public class CalendarEvent : BaseEntity
{
    /// <summary>
    /// Maximum allowed title length
    /// </summary>
    public const int MaxTitleLength = 500;

    /// <summary>
    /// Maximum allowed description length
    /// </summary>
    public const int MaxDescriptionLength = 10000;

    // Basic Event Properties
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Location { get; set; }
    public bool IsAllDay { get; set; } = false;

    // Event Classification
    public int EventType { get; set; } // Maps to EventType enum
    public int Status { get; set; } = (int)EventStatus.Confirmed; // Maps to EventStatus enum
    public int Visibility { get; set; } = (int)EventVisibility.Private; // Maps to EventVisibility enum
    public int Priority { get; set; } = 1; // Priority.Medium

    // User and Ownership
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // External Calendar Integration
    public string? ExternalEventId { get; set; }
    public string? ExternalCalendarId { get; set; }
    public int CalendarProvider { get; set; } = 0; // CalendarProvider.None
    public string? ProviderMetadata { get; set; } // JSON storage for provider-specific data
    public DateTime? LastSyncTime { get; set; }
    public string? SyncVersion { get; set; } // ETag or version from external provider

    // Recurrence and Repetition
    public bool IsRecurring { get; set; } = false;
    public string? RecurrenceRule { get; set; } // JSON storage for RecurrencePattern
    public Guid? RecurrenceGroupId { get; set; } // Groups related recurring events
    public bool IsRecurrenceException { get; set; } = false;
    public DateTime? OriginalStartTime { get; set; } // For exceptions, the original event start time

    // Attendees and Invitations
    public string? Attendees { get; set; } // JSON storage for EventAttendee list
    public string? Organizer { get; set; } // Organizer contact information
    public bool HasAttendees => !string.IsNullOrEmpty(Attendees);

    // Reminders and Notifications
    public string? Reminders { get; set; } // JSON storage for EventReminder list
    public bool HasReminders => !string.IsNullOrEmpty(Reminders);

    // Task and Project Integration
    public Guid? RelatedTaskId { get; set; }
    public AppTask? RelatedTask { get; set; }
    public Guid? RelatedProjectId { get; set; }
    public Project? RelatedProject { get; set; }
    public ICollection<AppTask> Tasks { get; set; } = new List<AppTask>();

    // Contact Integration
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();

    // Time Zone and Scheduling
    public string? TimeZone { get; set; }
    public bool IsFlexible { get; set; } = false;
    public TimeSpan? BufferTimeBefore { get; set; }
    public TimeSpan? BufferTimeAfter { get; set; }

    // Conflict and Sync Status
    public bool HasConflicts { get; set; } = false;
    public int ConflictCount { get; set; } = 0;
    public DateTime? LastConflictCheck { get; set; }

    // Analytics and Tracking
    public DateTime? CompletedAt { get; set; }
    public bool WasAttended { get; set; } = false;
    public string? AttendanceNotes { get; set; }
    public int? Duration { get; set; } // Actual duration in minutes if different from scheduled

    // Calculated Properties

    /// <summary>
    /// Gets the scheduled duration of the event
    /// </summary>
    public TimeSpan ScheduledDuration => EndTime - StartTime;

    /// <summary>
    /// Gets whether the event is currently active (in progress)
    /// </summary>
    public bool IsActive => DateTime.UtcNow >= StartTime && DateTime.UtcNow <= EndTime && !IsCompleted;

    /// <summary>
    /// Gets whether the event is completed
    /// </summary>
    public bool IsCompleted => CompletedAt.HasValue || Status == (int)EventStatus.Completed;

    /// <summary>
    /// Gets whether the event is upcoming (start time is in the future)
    /// </summary>
    public bool IsUpcoming => StartTime > DateTime.UtcNow;

    /// <summary>
    /// Gets whether the event is overdue (past end time and not completed)
    /// </summary>
    public bool IsOverdue => DateTime.UtcNow > EndTime && !IsCompleted;

    /// <summary>
    /// Gets whether the event is from an external calendar
    /// </summary>
    public bool IsExternal => !string.IsNullOrEmpty(ExternalEventId);

    /// <summary>
    /// Gets whether the event needs synchronization
    /// </summary>
    public bool NeedsSync => IsExternal && (!LastSyncTime.HasValue || UpdatedAt > LastSyncTime.Value);

    /// <summary>
    /// Gets whether the event is a master recurring event
    /// </summary>
    public bool IsMasterRecurringEvent => IsRecurring && !IsRecurrenceException;

    // Domain Methods

    /// <summary>
    /// Marks the event as completed
    /// </summary>
    public void MarkAsCompleted(bool wasAttended = true, string? notes = null)
    {
        if (IsCompleted)
        {
            return;
        }

        CompletedAt = DateTime.UtcNow;
        WasAttended = wasAttended;
        AttendanceNotes = notes;
        Status = (int)EventStatus.Completed;
        MarkAsModified();
    }

    /// <summary>
    /// Reopens a completed event
    /// </summary>
    public void Reopen()
    {
        if (!IsCompleted)
        {
            return;
        }

        CompletedAt = null;
        WasAttended = false;
        AttendanceNotes = null;
        Status = (int)EventStatus.Confirmed;
        MarkAsModified();
    }

    /// <summary>
    /// Reschedules the event to a new time slot
    /// </summary>
    public void Reschedule(DateTime newStartTime, DateTime newEndTime, string? reason = null)
    {
        if (newStartTime >= newEndTime)
        {
            throw new ArgumentException("Start time must be before end time");
        }

        if (IsRecurring && !IsRecurrenceException)
        {
            throw new InvalidOperationException("Cannot reschedule master recurring event. Create an exception instead.");
        }

        var oldStartTime = StartTime;
        var oldEndTime = EndTime;

        StartTime = newStartTime;
        EndTime = newEndTime;

        // Mark as needing sync if external
        if (IsExternal)
        {
            LastSyncTime = null;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Extends the duration of the event
    /// </summary>
    public void ExtendDuration(TimeSpan extension)
    {
        if (extension <= TimeSpan.Zero)
        {
            throw new ArgumentException("Extension must be positive");
        }

        EndTime = EndTime.Add(extension);

        if (IsExternal)
        {
            LastSyncTime = null;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Cancels the event
    /// </summary>
    public void Cancel(string? reason = null)
    {
        Status = (int)EventStatus.Cancelled;
        AttendanceNotes = reason;

        if (IsExternal)
        {
            LastSyncTime = null;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Checks if this event conflicts with another event
    /// </summary>
    public bool ConflictsWith(CalendarEvent other)
    {
        if (other == null || Id == other.Id)
        {
            return false;
        }

        // Don't check conflicts with cancelled events
        if (Status == (int)EventStatus.Cancelled || other.Status == (int)EventStatus.Cancelled)
        {
            return false;
        }

        return StartTime < other.EndTime && other.StartTime < EndTime;
    }

    /// <summary>
    /// Gets the overlap duration with another event
    /// </summary>
    public TimeSpan GetOverlapDuration(CalendarEvent other)
    {
        if (!ConflictsWith(other))
        {
            return TimeSpan.Zero;
        }

        var overlapStart = StartTime > other.StartTime ? StartTime : other.StartTime;
        var overlapEnd = EndTime < other.EndTime ? EndTime : other.EndTime;

        return overlapEnd - overlapStart;
    }

    /// <summary>
    /// Updates the last sync time when synchronized with external calendar
    /// </summary>
    public void MarkAsSynced(string? syncVersion = null)
    {
        LastSyncTime = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(syncVersion))
        {
            SyncVersion = syncVersion;
        }
        MarkAsModified();
    }

    /// <summary>
    /// Links the event to a task
    /// </summary>
    public void LinkToTask(Guid taskId)
    {
        RelatedTaskId = taskId;
        MarkAsModified();
    }

    /// <summary>
    /// Links the event to a project
    /// </summary>
    public void LinkToProject(Guid projectId)
    {
        RelatedProjectId = projectId;
        MarkAsModified();
    }

    /// <summary>
    /// Records a conflict detection result
    /// </summary>
    public void UpdateConflictStatus(bool hasConflicts, int conflictCount = 0)
    {
        HasConflicts = hasConflicts;
        ConflictCount = conflictCount;
        LastConflictCheck = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Creates a calendar event from a task
    /// </summary>
    public static CalendarEvent FromTask(AppTask task, DateTime startTime, DateTime endTime, bool isAllDay = false)
    {
        return new CalendarEvent
        {
            Title = task.Title,
            Description = task.Description,
            StartTime = startTime,
            EndTime = endTime,
            IsAllDay = isAllDay,
            UserId = task.UserId,
            RelatedTaskId = task.Id,
            RelatedTask = task,
            EventType = 0, // EventType.Task
            Priority = task.Priority,
            Status = 0, // EventStatus.Confirmed
            Visibility = 1, // EventVisibility.Private
            CalendarProvider = 0 // CalendarProvider.None
        };
    }

    /// <summary>
    /// Creates a calendar event for a project
    /// </summary>
    public static CalendarEvent FromProject(Project project, DateTime startTime, DateTime endTime, string title)
    {
        return new CalendarEvent
        {
            Title = title,
            Description = $"Project: {project.Name}",
            StartTime = startTime,
            EndTime = endTime,
            UserId = project.UserId,
            RelatedProjectId = project.Id,
            RelatedProject = project,
            EventType = 3, // EventType.Project
            Priority = 1, // Priority.Medium
            Status = 0, // EventStatus.Confirmed
            Visibility = 1, // EventVisibility.Private
            CalendarProvider = 0 // CalendarProvider.None
        };
    }

    /// <summary>
    /// Creates an external calendar event
    /// </summary>
    public static CalendarEvent FromExternalEvent(Guid userId, string externalEventId, string externalCalendarId, 
        CalendarProvider provider, string title, DateTime startTime, DateTime endTime)
    {
        return new CalendarEvent
        {
            Title = title,
            StartTime = startTime,
            EndTime = endTime,
            UserId = userId,
            ExternalEventId = externalEventId,
            ExternalCalendarId = externalCalendarId,
            CalendarProvider = (int)provider,
            EventType = 4, // EventType.External
            Status = 0, // EventStatus.Confirmed
            Visibility = 1, // EventVisibility.Private
            LastSyncTime = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Event types for calendar events
/// </summary>
public enum EventType
{
    Task = 0,
    Meeting = 1,
    Appointment = 2,
    Project = 3,
    External = 4,
    Personal = 5,
    Work = 6,
    Break = 7,
    Travel = 8
}

/// <summary>
/// Event status enumeration
/// </summary>
public enum EventStatus
{
    Confirmed = 0,
    Tentative = 1,
    Cancelled = 2,
    Completed = 3
}

/// <summary>
/// Event visibility enumeration
/// </summary>
public enum EventVisibility
{
    Public = 0,
    Private = 1,
    Confidential = 2
}

/// <summary>
/// Calendar provider enumeration
/// </summary>
public enum CalendarProvider
{
    None = 0,
    Google = 1,
    Outlook = 2,
    ICloud = 3,
    CalDAV = 4,
    Exchange = 5,
    Yahoo = 6,
    Custom = 7
}
