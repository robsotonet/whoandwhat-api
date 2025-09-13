using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Events;

// Calendar Event Domain Events

/// <summary>
/// Raised when a new calendar event is created
/// </summary>
public sealed record CalendarEventCreatedEvent : IDomainEvent
{
    public CalendarEventCreatedEvent(Guid eventId, Guid userId, string title, DateTime startTime, 
        DateTime endTime, EventType eventType, CalendarProvider provider, bool isFromSync = false)
    {
        EventId = eventId;
        UserId = userId;
        Title = title;
        StartTime = startTime;
        EndTime = endTime;
        EventType = eventType;
        Provider = provider;
        IsFromSync = isFromSync;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid EventId { get; }
    public Guid UserId { get; }
    public string Title { get; }
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }
    public EventType EventType { get; }
    public CalendarProvider Provider { get; }
    public bool IsFromSync { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar event is updated
/// </summary>
public sealed record CalendarEventUpdatedEvent : IDomainEvent
{
    public CalendarEventUpdatedEvent(Guid eventId, Guid userId, List<string> changedFields, 
        Dictionary<string, object>? previousValues = null, bool isFromSync = false)
    {
        EventId = eventId;
        UserId = userId;
        ChangedFields = changedFields;
        PreviousValues = previousValues ?? new Dictionary<string, object>();
        IsFromSync = isFromSync;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid EventId { get; }
    public Guid UserId { get; }
    public List<string> ChangedFields { get; }
    public Dictionary<string, object> PreviousValues { get; }
    public bool IsFromSync { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar event is deleted
/// </summary>
public sealed record CalendarEventDeletedEvent : IDomainEvent
{
    public CalendarEventDeletedEvent(Guid eventId, Guid userId, string title, DateTime startTime, 
        CalendarProvider provider, bool isFromSync = false, string? reason = null)
    {
        EventId = eventId;
        UserId = userId;
        Title = title;
        StartTime = startTime;
        Provider = provider;
        IsFromSync = isFromSync;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid EventId { get; }
    public Guid UserId { get; }
    public string Title { get; }
    public DateTime StartTime { get; }
    public CalendarProvider Provider { get; }
    public bool IsFromSync { get; }
    public string? Reason { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar event is completed
/// </summary>
public sealed record CalendarEventCompletedEvent : IDomainEvent
{
    public CalendarEventCompletedEvent(Guid eventId, Guid userId, string title, DateTime startTime, 
        DateTime completedAt, bool wasAttended, string? notes = null)
    {
        EventId = eventId;
        UserId = userId;
        Title = title;
        StartTime = startTime;
        CompletedAt = completedAt;
        WasAttended = wasAttended;
        Notes = notes;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid EventId { get; }
    public Guid UserId { get; }
    public string Title { get; }
    public DateTime StartTime { get; }
    public DateTime CompletedAt { get; }
    public bool WasAttended { get; }
    public string? Notes { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar event is rescheduled
/// </summary>
public sealed record CalendarEventRescheduledEvent : IDomainEvent
{
    public CalendarEventRescheduledEvent(Guid eventId, Guid userId, string title, 
        DateTime previousStartTime, DateTime previousEndTime, 
        DateTime newStartTime, DateTime newEndTime, string? reason = null)
    {
        EventId = eventId;
        UserId = userId;
        Title = title;
        PreviousStartTime = previousStartTime;
        PreviousEndTime = previousEndTime;
        NewStartTime = newStartTime;
        NewEndTime = newEndTime;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid EventId { get; }
    public Guid UserId { get; }
    public string Title { get; }
    public DateTime PreviousStartTime { get; }
    public DateTime PreviousEndTime { get; }
    public DateTime NewStartTime { get; }
    public DateTime NewEndTime { get; }
    public string? Reason { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar event is cancelled
/// </summary>
public sealed record CalendarEventCancelledEvent : IDomainEvent
{
    public CalendarEventCancelledEvent(Guid eventId, Guid userId, string title, DateTime startTime, 
        string? reason = null, bool notifyAttendees = false)
    {
        EventId = eventId;
        UserId = userId;
        Title = title;
        StartTime = startTime;
        Reason = reason;
        NotifyAttendees = notifyAttendees;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid EventId { get; }
    public Guid UserId { get; }
    public string Title { get; }
    public DateTime StartTime { get; }
    public string? Reason { get; }
    public bool NotifyAttendees { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

// Calendar Integration Domain Events

/// <summary>
/// Raised when a new calendar integration is created
/// </summary>
public sealed record CalendarIntegrationCreatedEvent : IDomainEvent
{
    public CalendarIntegrationCreatedEvent(Guid integrationId, Guid userId, CalendarProvider provider, 
        string providerName, bool autoSyncEnabled)
    {
        IntegrationId = integrationId;
        UserId = userId;
        Provider = provider;
        ProviderName = providerName;
        AutoSyncEnabled = autoSyncEnabled;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid IntegrationId { get; }
    public Guid UserId { get; }
    public CalendarProvider Provider { get; }
    public string ProviderName { get; }
    public bool AutoSyncEnabled { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar integration is enabled or disabled
/// </summary>
public sealed record CalendarIntegrationStatusChangedEvent : IDomainEvent
{
    public CalendarIntegrationStatusChangedEvent(Guid integrationId, Guid userId, CalendarProvider provider, 
        bool previousEnabled, bool currentEnabled, string? reason = null)
    {
        IntegrationId = integrationId;
        UserId = userId;
        Provider = provider;
        PreviousEnabled = previousEnabled;
        CurrentEnabled = currentEnabled;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid IntegrationId { get; }
    public Guid UserId { get; }
    public CalendarProvider Provider { get; }
    public bool PreviousEnabled { get; }
    public bool CurrentEnabled { get; }
    public string? Reason { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar integration's health status changes
/// </summary>
public sealed record CalendarIntegrationHealthChangedEvent : IDomainEvent
{
    public CalendarIntegrationHealthChangedEvent(Guid integrationId, Guid userId, CalendarProvider provider,
        IntegrationHealthStatus previousHealth, IntegrationHealthStatus currentHealth, string? reason = null)
    {
        IntegrationId = integrationId;
        UserId = userId;
        Provider = provider;
        PreviousHealth = previousHealth;
        CurrentHealth = currentHealth;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid IntegrationId { get; }
    public Guid UserId { get; }
    public CalendarProvider Provider { get; }
    public IntegrationHealthStatus PreviousHealth { get; }
    public IntegrationHealthStatus CurrentHealth { get; }
    public string? Reason { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar integration is deleted/disconnected
/// </summary>
public sealed record CalendarIntegrationDeletedEvent : IDomainEvent
{
    public CalendarIntegrationDeletedEvent(Guid integrationId, Guid userId, CalendarProvider provider,
        int totalEventsSynced, string? reason = null)
    {
        IntegrationId = integrationId;
        UserId = userId;
        Provider = provider;
        TotalEventsSynced = totalEventsSynced;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid IntegrationId { get; }
    public Guid UserId { get; }
    public CalendarProvider Provider { get; }
    public int TotalEventsSynced { get; }
    public string? Reason { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

// Calendar Sync Domain Events

/// <summary>
/// Raised when a calendar sync operation starts
/// </summary>
public sealed record CalendarSyncStartedEvent : IDomainEvent
{
    public CalendarSyncStartedEvent(Guid integrationId, Guid userId, CalendarProvider provider, 
        SyncStrategy strategy, SyncDirection direction)
    {
        IntegrationId = integrationId;
        UserId = userId;
        Provider = provider;
        Strategy = strategy;
        Direction = direction;
        SyncId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }

    public Guid SyncId { get; }
    public Guid IntegrationId { get; }
    public Guid UserId { get; }
    public CalendarProvider Provider { get; }
    public SyncStrategy Strategy { get; }
    public SyncDirection Direction { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar sync operation completes successfully
/// </summary>
public sealed record CalendarSyncCompletedEvent : IDomainEvent
{
    public CalendarSyncCompletedEvent(Guid syncId, Guid integrationId, Guid userId, CalendarProvider provider,
        int eventsCreated, int eventsUpdated, int eventsDeleted, TimeSpan duration, string? syncToken = null)
    {
        SyncId = syncId;
        IntegrationId = integrationId;
        UserId = userId;
        Provider = provider;
        EventsCreated = eventsCreated;
        EventsUpdated = eventsUpdated;
        EventsDeleted = eventsDeleted;
        Duration = duration;
        SyncToken = syncToken;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid SyncId { get; }
    public Guid IntegrationId { get; }
    public Guid UserId { get; }
    public CalendarProvider Provider { get; }
    public int EventsCreated { get; }
    public int EventsUpdated { get; }
    public int EventsDeleted { get; }
    public TimeSpan Duration { get; }
    public string? SyncToken { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar sync operation fails
/// </summary>
public sealed record CalendarSyncFailedEvent : IDomainEvent
{
    public CalendarSyncFailedEvent(Guid syncId, Guid integrationId, Guid userId, CalendarProvider provider,
        string errorMessage, Exception? exception = null, int attemptNumber = 1)
    {
        SyncId = syncId;
        IntegrationId = integrationId;
        UserId = userId;
        Provider = provider;
        ErrorMessage = errorMessage;
        Exception = exception;
        AttemptNumber = attemptNumber;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid SyncId { get; }
    public Guid IntegrationId { get; }
    public Guid UserId { get; }
    public CalendarProvider Provider { get; }
    public string ErrorMessage { get; }
    public Exception? Exception { get; }
    public int AttemptNumber { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

// Calendar Conflict Domain Events

/// <summary>
/// Raised when a calendar conflict is detected
/// </summary>
public sealed record CalendarConflictDetectedEvent : IDomainEvent
{
    public CalendarConflictDetectedEvent(Guid conflictId, Guid userId, ConflictType conflictType, 
        ConflictSeverity severity, Guid? internalEventId = null, string? externalEventId = null,
        CalendarProvider? provider = null)
    {
        ConflictId = conflictId;
        UserId = userId;
        ConflictType = conflictType;
        Severity = severity;
        InternalEventId = internalEventId;
        ExternalEventId = externalEventId;
        Provider = provider;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid ConflictId { get; }
    public Guid UserId { get; }
    public ConflictType ConflictType { get; }
    public ConflictSeverity Severity { get; }
    public Guid? InternalEventId { get; }
    public string? ExternalEventId { get; }
    public CalendarProvider? Provider { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar conflict is resolved
/// </summary>
public sealed record CalendarConflictResolvedEvent : IDomainEvent
{
    public CalendarConflictResolvedEvent(Guid conflictId, Guid userId, ConflictResolutionAction action,
        Guid resolvedByUserId, bool wasAutoResolved = false, string? notes = null)
    {
        ConflictId = conflictId;
        UserId = userId;
        Action = action;
        ResolvedByUserId = resolvedByUserId;
        WasAutoResolved = wasAutoResolved;
        Notes = notes;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid ConflictId { get; }
    public Guid UserId { get; }
    public ConflictResolutionAction Action { get; }
    public Guid ResolvedByUserId { get; }
    public bool WasAutoResolved { get; }
    public string? Notes { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when a calendar conflict is escalated
/// </summary>
public sealed record CalendarConflictEscalatedEvent : IDomainEvent
{
    public CalendarConflictEscalatedEvent(Guid conflictId, Guid userId, ConflictSeverity previousSeverity,
        ConflictSeverity newSeverity, string? reason = null)
    {
        ConflictId = conflictId;
        UserId = userId;
        PreviousSeverity = previousSeverity;
        NewSeverity = newSeverity;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid ConflictId { get; }
    public Guid UserId { get; }
    public ConflictSeverity PreviousSeverity { get; }
    public ConflictSeverity NewSeverity { get; }
    public string? Reason { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

/// <summary>
/// Raised when multiple conflicts are resolved in a batch operation
/// </summary>
public sealed record CalendarConflictBatchResolvedEvent : IDomainEvent
{
    public CalendarConflictBatchResolvedEvent(Guid userId, List<Guid> conflictIds, 
        ConflictResolutionAction action, int totalResolved, int totalFailed)
    {
        UserId = userId;
        ConflictIds = conflictIds;
        Action = action;
        TotalResolved = totalResolved;
        TotalFailed = totalFailed;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid UserId { get; }
    public List<Guid> ConflictIds { get; }
    public ConflictResolutionAction Action { get; }
    public int TotalResolved { get; }
    public int TotalFailed { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

// Calendar Reminder Domain Events

/// <summary>
/// Raised when a calendar event reminder is triggered
/// </summary>
public sealed record CalendarEventReminderTriggeredEvent : IDomainEvent
{
    public CalendarEventReminderTriggeredEvent(Guid eventId, Guid userId, string eventTitle, 
        DateTime eventStartTime, ReminderMethod method, int minutesBefore, string? customMessage = null)
    {
        EventId = eventId;
        UserId = userId;
        EventTitle = eventTitle;
        EventStartTime = eventStartTime;
        Method = method;
        MinutesBefore = minutesBefore;
        CustomMessage = customMessage;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid EventId { get; }
    public Guid UserId { get; }
    public string EventTitle { get; }
    public DateTime EventStartTime { get; }
    public ReminderMethod Method { get; }
    public int MinutesBefore { get; }
    public string? CustomMessage { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

// Calendar Analytics Domain Events

/// <summary>
/// Raised when significant calendar analytics are calculated
/// </summary>
public sealed record CalendarAnalyticsUpdatedEvent : IDomainEvent
{
    public CalendarAnalyticsUpdatedEvent(Guid userId, Dictionary<string, object> metrics, 
        DateTime periodStart, DateTime periodEnd)
    {
        UserId = userId;
        Metrics = metrics;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        OccurredOn = DateTime.UtcNow;
    }

    public Guid UserId { get; }
    public Dictionary<string, object> Metrics { get; }
    public DateTime PeriodStart { get; }
    public DateTime PeriodEnd { get; }
    public DateTime OccurredOn { get; }
    public DateTime DateOccurred => OccurredOn;
}

// Required enums and supporting types from other parts of the domain

public enum SyncStrategy
{
    Full,
    Incremental
}

public enum ConflictResolutionAction
{
    KeepInternal,
    KeepExternal,
    Merge,
    CreateBoth,
    Skip,
    UserDecision,
    DeleteBoth,
    Reschedule
}