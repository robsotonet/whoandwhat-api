using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Represents a conflict between calendar events that requires resolution
/// </summary>
public class CalendarConflict : BaseEntity
{
    /// <summary>
    /// Maximum allowed description length
    /// </summary>
    public const int MaxDescriptionLength = 2000;

    /// <summary>
    /// Maximum allowed resolution notes length
    /// </summary>
    public const int MaxResolutionNotesLength = 1000;

    // Conflict Identification
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int ConflictType { get; set; } // Maps to ConflictType enum
    public int Severity { get; set; } = (int)ConflictSeverity.Medium; // Maps to ConflictSeverity enum
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;

    // Involved Events
    public Guid? InternalEventId { get; set; }
    public CalendarEvent? InternalEvent { get; set; }
    public string? ExternalEventId { get; set; }
    public string? ExternalCalendarId { get; set; }
    public int CalendarProvider { get; set; } = (int)CalendarProvider.None; // Maps to CalendarProvider enum

    // Conflict Details
    public string? ConflictingFields { get; set; } // JSON storage for list of conflicting fields
    public string? ConflictData { get; set; } // JSON storage for detailed conflict information
    public DateTime ConflictDetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConflictStartTime { get; set; } // When the conflicting time period starts
    public DateTime? ConflictEndTime { get; set; } // When the conflicting time period ends
    public int? OverlapMinutes { get; set; } // Duration of time overlap for time conflicts

    // Resolution Information
    public int ResolutionStatus { get; set; } = (int)ConflictResolutionStatus.Pending; // Maps to ConflictResolutionStatus enum
    public int? RecommendedAction { get; set; } // Maps to ConflictResolutionAction enum
    public string? ResolutionOptions { get; set; } // JSON storage for available resolution options
    public double? AutoResolutionConfidence { get; set; } // 0.0 to 1.0, confidence in auto-resolution

    // Resolution Execution
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public User? ResolvedByUser { get; set; }
    public int? ActualResolutionAction { get; set; } // Maps to ConflictResolutionAction enum
    public string? ResolutionNotes { get; set; }
    public string? ResolutionMetadata { get; set; } // JSON storage for resolution details

    // Analysis and Learning
    public double? SimilarityScore { get; set; } // For duplicate detection conflicts
    public string? AnalysisData { get; set; } // JSON storage for ML/AI analysis data
    public bool RequiresUserAction { get; set; } = true;
    public bool CanAutoResolve { get; set; } = false;
    public int Priority { get; set; } = (int)Priority.Medium;

    // Lifecycle Management
    public bool IsActive { get; set; } = true;
    public bool IsIgnored { get; set; } = false;
    public DateTime? IgnoredAt { get; set; }
    public string? IgnoreReason { get; set; }
    public DateTime? ExpiresAt { get; set; } // Conflicts can have expiration times

    // Related Conflicts
    public Guid? ParentConflictId { get; set; }
    public CalendarConflict? ParentConflict { get; set; }
    public ICollection<CalendarConflict> ChildConflicts { get; set; } = new List<CalendarConflict>();

    // Integration Reference
    public Guid? CalendarIntegrationId { get; set; }
    public CalendarIntegration? CalendarIntegration { get; set; }

    // Calculated Properties

    /// <summary>
    /// Gets whether the conflict is currently pending resolution
    /// </summary>
    public bool IsPending => ResolutionStatus == (int)ConflictResolutionStatus.Pending && IsActive && !IsIgnored;

    /// <summary>
    /// Gets whether the conflict has been resolved
    /// </summary>
    public bool IsResolved => ResolutionStatus == (int)ConflictResolutionStatus.Resolved && ResolvedAt.HasValue;

    /// <summary>
    /// Gets whether the conflict is high priority requiring immediate attention
    /// </summary>
    public bool IsHighPriority => Severity >= (int)ConflictSeverity.High || Priority >= (int)Priority.High;

    /// <summary>
    /// Gets whether the conflict is a time overlap conflict
    /// </summary>
    public bool IsTimeOverlapConflict => ConflictType == (int)ConflictType.TimeOverlap;

    /// <summary>
    /// Gets whether the conflict is a duplicate event conflict
    /// </summary>
    public bool IsDuplicateConflict => ConflictType == (int)ConflictType.DuplicateEvent;

    /// <summary>
    /// Gets whether the conflict involves external calendar data
    /// </summary>
    public bool InvolvesExternalCalendar => !string.IsNullOrEmpty(ExternalEventId) || CalendarProvider != (int)CalendarProvider.None;

    /// <summary>
    /// Gets whether the conflict has expired and should be automatically resolved
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Gets the age of the conflict
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - ConflictDetectedAt;

    /// <summary>
    /// Gets whether the conflict can be safely auto-resolved
    /// </summary>
    public bool IsSafeToAutoResolve => CanAutoResolve && 
        AutoResolutionConfidence.HasValue && 
        AutoResolutionConfidence.Value >= 0.8 &&
        Severity <= (int)ConflictSeverity.Low;

    // Domain Methods

    /// <summary>
    /// Marks the conflict as resolved
    /// </summary>
    public void Resolve(Guid resolvedByUserId, ConflictResolutionAction action, string? notes = null, string? metadata = null)
    {
        if (IsResolved)
        {
            return;
        }

        ResolutionStatus = (int)ConflictResolutionStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
        ResolvedByUserId = resolvedByUserId;
        ActualResolutionAction = (int)action;
        ResolutionNotes = notes;
        ResolutionMetadata = metadata;
        IsActive = false;

        MarkAsModified();
    }

    /// <summary>
    /// Marks the conflict as ignored
    /// </summary>
    public void Ignore(string reason, TimeSpan? ignoreDuration = null)
    {
        IsIgnored = true;
        IgnoredAt = DateTime.UtcNow;
        IgnoreReason = reason;
        
        if (ignoreDuration.HasValue)
        {
            ExpiresAt = DateTime.UtcNow.Add(ignoreDuration.Value);
        }

        IsActive = false;
        MarkAsModified();
    }

    /// <summary>
    /// Reactivates an ignored conflict
    /// </summary>
    public void Reactivate()
    {
        if (!IsIgnored && IsActive)
        {
            return;
        }

        IsIgnored = false;
        IgnoredAt = null;
        IgnoreReason = null;
        ExpiresAt = null;
        IsActive = true;
        ResolutionStatus = (int)ConflictResolutionStatus.Pending;

        MarkAsModified();
    }

    /// <summary>
    /// Updates the conflict's auto-resolution capability
    /// </summary>
    public void UpdateAutoResolutionCapability(bool canAutoResolve, double confidence, ConflictResolutionAction? recommendedAction = null)
    {
        CanAutoResolve = canAutoResolve;
        AutoResolutionConfidence = Math.Max(0.0, Math.Min(1.0, confidence));
        
        if (recommendedAction.HasValue)
        {
            RecommendedAction = (int)recommendedAction.Value;
        }

        RequiresUserAction = !canAutoResolve || confidence < 0.8;
        MarkAsModified();
    }

    /// <summary>
    /// Escalates the conflict severity
    /// </summary>
    public void Escalate(ConflictSeverity newSeverity, string? reason = null)
    {
        if ((int)newSeverity <= Severity)
        {
            return;
        }

        Severity = (int)newSeverity;
        
        if (!string.IsNullOrEmpty(reason))
        {
            Description = $"{Description} [ESCALATED: {reason}]";
        }

        // High severity conflicts require user action
        if (newSeverity >= ConflictSeverity.High)
        {
            RequiresUserAction = true;
            CanAutoResolve = false;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Gets the conflicting fields as a list
    /// </summary>
    public List<string> GetConflictingFields()
    {
        if (string.IsNullOrEmpty(ConflictingFields))
        {
            return new List<string>();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(ConflictingFields) 
                ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Sets the conflicting fields
    /// </summary>
    public void SetConflictingFields(List<string> fields)
    {
        ConflictingFields = System.Text.Json.JsonSerializer.Serialize(fields);
        MarkAsModified();
    }

    /// <summary>
    /// Gets the resolution options as a list
    /// </summary>
    public List<ConflictResolutionOption> GetResolutionOptions()
    {
        if (string.IsNullOrEmpty(ResolutionOptions))
        {
            return new List<ConflictResolutionOption>();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<ConflictResolutionOption>>(ResolutionOptions) 
                ?? new List<ConflictResolutionOption>();
        }
        catch
        {
            return new List<ConflictResolutionOption>();
        }
    }

    /// <summary>
    /// Sets the resolution options
    /// </summary>
    public void SetResolutionOptions(List<ConflictResolutionOption> options)
    {
        ResolutionOptions = System.Text.Json.JsonSerializer.Serialize(options);
        MarkAsModified();
    }

    /// <summary>
    /// Updates conflict analysis data
    /// </summary>
    public void UpdateAnalysis(object analysisData)
    {
        AnalysisData = System.Text.Json.JsonSerializer.Serialize(analysisData);
        MarkAsModified();
    }

    /// <summary>
    /// Adds a child conflict (for complex conflicts with multiple parts)
    /// </summary>
    public void AddChildConflict(CalendarConflict childConflict)
    {
        childConflict.ParentConflictId = Id;
        childConflict.ParentConflict = this;
        ChildConflicts.Add(childConflict);
        MarkAsModified();
    }

    /// <summary>
    /// Creates a time overlap conflict
    /// </summary>
    public static CalendarConflict CreateTimeOverlapConflict(Guid userId, CalendarEvent event1, CalendarEvent event2, int overlapMinutes)
    {
        return new CalendarConflict
        {
            UserId = userId,
            ConflictType = (int)ConflictType.TimeOverlap,
            Severity = overlapMinutes > 30 ? (int)ConflictSeverity.High : (int)ConflictSeverity.Medium,
            Title = "Time Overlap Conflict",
            Description = $"Events '{event1.Title}' and '{event2.Title}' have overlapping times",
            InternalEventId = event1.Id,
            InternalEvent = event1,
            ConflictStartTime = event1.StartTime > event2.StartTime ? event1.StartTime : event2.StartTime,
            ConflictEndTime = event1.EndTime < event2.EndTime ? event1.EndTime : event2.EndTime,
            OverlapMinutes = overlapMinutes,
            RequiresUserAction = overlapMinutes > 15,
            CanAutoResolve = overlapMinutes <= 5,
            Priority = overlapMinutes > 60 ? (int)Priority.High : (int)Priority.Medium
        };
    }

    /// <summary>
    /// Creates a duplicate event conflict
    /// </summary>
    public static CalendarConflict CreateDuplicateConflict(Guid userId, CalendarEvent internalEvent, 
        string externalEventId, double similarityScore, CalendarProvider provider)
    {
        return new CalendarConflict
        {
            UserId = userId,
            ConflictType = (int)ConflictType.DuplicateEvent,
            Severity = similarityScore > 0.9 ? (int)ConflictSeverity.High : (int)ConflictSeverity.Medium,
            Title = "Duplicate Event Detected",
            Description = $"Event '{internalEvent.Title}' appears to be duplicated in external calendar",
            InternalEventId = internalEvent.Id,
            InternalEvent = internalEvent,
            ExternalEventId = externalEventId,
            CalendarProvider = (int)provider,
            SimilarityScore = similarityScore,
            RequiresUserAction = similarityScore < 0.95,
            CanAutoResolve = similarityScore > 0.95,
            Priority = (int)Priority.Medium
        };
    }

    /// <summary>
    /// Creates a data inconsistency conflict
    /// </summary>
    public static CalendarConflict CreateDataInconsistencyConflict(Guid userId, CalendarEvent internalEvent, 
        string externalEventId, List<string> conflictingFields, CalendarProvider provider)
    {
        var conflict = new CalendarConflict
        {
            UserId = userId,
            ConflictType = (int)ConflictType.DataInconsistency,
            Severity = conflictingFields.Count > 3 ? (int)ConflictSeverity.High : (int)ConflictSeverity.Medium,
            Title = "Data Inconsistency Detected",
            Description = $"Event '{internalEvent.Title}' has inconsistent data between internal and external calendars",
            InternalEventId = internalEvent.Id,
            InternalEvent = internalEvent,
            ExternalEventId = externalEventId,
            CalendarProvider = (int)provider,
            RequiresUserAction = conflictingFields.Contains("StartTime") || conflictingFields.Contains("EndTime"),
            CanAutoResolve = !conflictingFields.Contains("StartTime") && !conflictingFields.Contains("EndTime"),
            Priority = conflictingFields.Contains("StartTime") ? (int)Priority.High : (int)Priority.Medium
        };

        conflict.SetConflictingFields(conflictingFields);
        return conflict;
    }
}

/// <summary>
/// Resolution option for conflicts
/// </summary>
public record ConflictResolutionOption(
    ConflictResolutionAction Action,
    string Description,
    double Confidence,
    bool IsRecommended,
    string? AdditionalInfo = null
);

/// <summary>
/// Conflict type enumeration
/// </summary>
public enum ConflictType
{
    TimeOverlap = 0,        // Events overlap in time
    DuplicateEvent = 1,     // Same event exists in multiple places
    DataInconsistency = 2,  // Same event has different data
    DeletedEvent = 3,       // Event deleted in one system but exists in another
    ModifiedEvent = 4,      // Event modified differently in different systems
    PermissionDenied = 5,   // Insufficient permissions to resolve
    SyncFailure = 6         // Technical sync failure
}

/// <summary>
/// Conflict severity enumeration
/// </summary>
public enum ConflictSeverity
{
    Low = 0,        // Minor issues, can be auto-resolved
    Medium = 1,     // Moderate issues, may need user input
    High = 2,       // Significant issues, requires user resolution
    Critical = 3    // Critical issues, immediate attention required
}

/// <summary>
/// Conflict resolution action enumeration
/// </summary>
public enum ConflictResolutionAction
{
    KeepInternal = 0,    // Keep the internal WhoAndWhat event
    KeepExternal = 1,    // Keep the external calendar event
    Merge = 2,           // Merge data from both events
    CreateBoth = 3,      // Keep both events as separate
    Skip = 4,            // Skip resolution, ignore the conflict
    UserDecision = 5,    // Defer to user for manual resolution
    DeleteBoth = 6,      // Delete both conflicting events
    Reschedule = 7       // Reschedule one of the events
}

/// <summary>
/// Conflict resolution status enumeration
/// </summary>
public enum ConflictResolutionStatus
{
    Pending = 0,      // Awaiting resolution
    InProgress = 1,   // Currently being resolved
    Resolved = 2,     // Successfully resolved
    Failed = 3,       // Resolution failed
    Expired = 4       // Conflict expired without resolution
}