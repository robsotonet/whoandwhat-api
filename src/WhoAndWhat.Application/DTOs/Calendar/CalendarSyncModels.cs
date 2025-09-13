
namespace WhoAndWhat.Application.DTOs.Calendar;

/// <summary>
/// Result of a calendar synchronization operation
/// </summary>
public sealed record CalendarSyncResult(
    Guid UserId,
    CalendarProvider Provider,
    bool Success,
    int EventsSynced,
    int EventsCreated,
    int EventsUpdated,
    int EventsDeleted,
    int ConflictsDetected,
    int ConflictsResolved,
    TimeSpan SyncDuration,
    string? NewSyncToken,
    List<string> Errors,
    List<string> Warnings,
    DateTime SyncStartTime,
    DateTime SyncEndTime,
    SyncDirection Direction,
    SyncStatistics Statistics
);

/// <summary>
/// Result of incremental calendar synchronization
/// </summary>
public sealed record IncrementalSyncResult(
    Guid UserId,
    CalendarProvider Provider,
    bool Success,
    string? PreviousSyncToken,
    string? NewSyncToken,
    List<EventChangeInfo> Changes,
    int TotalChanges,
    TimeSpan SyncDuration,
    List<string> Errors,
    DateTime SyncTime
);

/// <summary>
/// Result of individual event synchronization
/// </summary>
public sealed record EventSyncResult(
    Guid EventId,
    string? ExternalEventId,
    bool Success,
    SyncOperation Operation,
    string? ErrorMessage,
    DateTime ProcessedAt,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Result of task-to-event conversion and synchronization
/// </summary>
public sealed record TaskToEventSyncResult(
    Guid TaskId,
    Guid? CreatedEventId,
    string? ExternalEventId,
    bool Success,
    string? ErrorMessage,
    TaskToEventConversionResult ConversionResult,
    DateTime ProcessedAt
);

/// <summary>
/// Calendar synchronization status for a user and provider
/// </summary>
public sealed record CalendarSyncStatus(
    Guid UserId,
    CalendarProvider Provider,
    bool IsConfigured,
    bool IsConnected,
    DateTime? LastSyncTime,
    DateTime? NextScheduledSync,
    string? LastSyncToken,
    int TotalEventsSynced,
    int PendingConflicts,
    List<string> ConnectedCalendars,
    SyncHealthStatus HealthStatus,
    Dictionary<string, object> ProviderSpecificInfo
);

/// <summary>
/// Available calendar provider information
/// </summary>
public sealed record AvailableCalendarProvider(
    CalendarProvider Provider,
    string DisplayName,
    string Description,
    bool IsConfigured,
    bool IsEnabled,
    bool RequiresOAuth,
    List<string> RequiredScopes,
    List<string> SupportedFeatures,
    ProviderCapabilities Capabilities
);

/// <summary>
/// Calendar provider configuration request
/// </summary>
public sealed record CalendarProviderConfiguration(
    CalendarProvider Provider,
    string? ClientId,
    string? ClientSecret,
    List<string> Scopes,
    Dictionary<string, string> CustomSettings,
    bool EnableAutoSync,
    SyncMode SyncMode,
    ConflictResolutionStrategy ConflictStrategy
);

/// <summary>
/// Result of provider configuration
/// </summary>
public sealed record CalendarProviderConfigResult(
    bool Success,
    string? AuthorizationUrl,
    string? ErrorMessage,
    List<string> RequiredSteps,
    DateTime ConfiguredAt
);

/// <summary>
/// Result of provider disconnection
/// </summary>
public sealed record CalendarDisconnectResult(
    bool Success,
    int EventsDeleted,
    int ConflictsRemoved,
    string? ErrorMessage,
    DateTime DisconnectedAt
);

/// <summary>
/// Calendar synchronization conflict
/// </summary>
public sealed record CalendarSyncConflict(
    Guid ConflictId,
    Guid UserId,
    CalendarProvider Provider,
    ConflictType Type,
    ConflictSeverity Severity,
    string Title,
    string Description,
    InternalCalendarEvent? InternalEvent,
    ExternalCalendarEvent? ExternalEvent,
    List<ConflictResolutionOption> ResolutionOptions,
    DateTime DetectedAt,
    bool RequiresUserAction,
    Dictionary<string, object> ConflictMetadata
);

/// <summary>
/// Conflict resolution data
/// </summary>
public sealed record ConflictResolution(
    Guid ConflictId,
    ConflictResolutionAction Action,
    string? SelectedOption,
    Dictionary<string, object> ResolutionData,
    string? UserNotes,
    DateTime ResolvedAt
);

/// <summary>
/// Result of conflict resolution
/// </summary>
public sealed record ConflictResolutionResult(
    Guid ConflictId,
    bool Success,
    ConflictResolutionAction ActionTaken,
    string? ErrorMessage,
    List<EventSyncResult> AffectedEvents,
    DateTime ProcessedAt
);

/// <summary>
/// Calendar sync service health status
/// </summary>
public sealed record CalendarSyncHealthStatus(
    bool IsHealthy,
    TimeSpan ResponseTime,
    Dictionary<CalendarProvider, ProviderHealthInfo> ProviderHealth,
    int ActiveConnections,
    int PendingSyncs,
    List<string> Issues,
    DateTime CheckTimestamp
);

/// <summary>
/// Provider health information
/// </summary>
public sealed record ProviderHealthInfo(
    bool IsAvailable,
    TimeSpan ResponseTime,
    int RateLimitRemaining,
    DateTime? RateLimitReset,
    string? LastError,
    DateTime LastChecked
);

/// <summary>
/// Calendar backup operation result
/// </summary>
public sealed record CalendarBackupResult(
    bool Success,
    string? BackupId,
    int EventsBackedUp,
    long BackupSizeBytes,
    string? BackupLocation,
    string? ErrorMessage,
    DateTime BackupTime
);

/// <summary>
/// Calendar backup options
/// </summary>
public sealed record CalendarBackupOptions(
    bool IncludeEvents,
    bool IncludeSyncTokens,
    bool IncludeConflictHistory,
    bool CompressBackup,
    TimeRange? DateRange
);

/// <summary>
/// Calendar restore operation result
/// </summary>
public sealed record CalendarRestoreResult(
    bool Success,
    int EventsRestored,
    int ConflictsRestored,
    string? ErrorMessage,
    DateTime RestoreTime
);

/// <summary>
/// Calendar restore options
/// </summary>
public sealed record CalendarRestoreOptions(
    bool RestoreEvents,
    bool RestoreSyncTokens,
    bool RestoreConflictHistory,
    bool OverwriteExisting
);

// External Calendar Provider Models

/// <summary>
/// External calendar metadata
/// </summary>
public sealed record ExternalCalendar(
    string Id,
    string Name,
    string Description,
    bool IsPrimary,
    bool IsReadOnly,
    string TimeZone,
    string Color,
    CalendarProvider Provider,
    Dictionary<string, object> ProviderMetadata
);

/// <summary>
/// External calendar event
/// </summary>
public sealed record ExternalCalendarEvent(
    string Id,
    string CalendarId,
    string Title,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Location,
    EventStatus Status,
    EventVisibility Visibility,
    List<EventAttendee> Attendees,
    EventRecurrence? Recurrence,
    List<EventReminder> Reminders,
    string? CreatedBy,
    DateTime CreatedAt,
    DateTime ModifiedAt,
    string? ETag,
    Dictionary<string, object> ProviderSpecificData
);

/// <summary>
/// Internal WhoAndWhat calendar event
/// </summary>
public sealed record InternalCalendarEvent(
    Guid Id,
    Guid UserId,
    string Title,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Location,
    EventStatus Status,
    EventSource Source,
    Guid? RelatedTaskId,
    Guid? RelatedProjectId,
    List<Guid> RelatedContactIds,
    DateTime CreatedAt,
    DateTime ModifiedAt,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Event change information for incremental sync
/// </summary>
public sealed record EventChangeInfo(
    string EventId,
    ChangeType ChangeType,
    ExternalCalendarEvent? EventData,
    DateTime ChangeTime
);

/// <summary>
/// Event attendee information
/// </summary>
public sealed record EventAttendee(
    string Email,
    string? Name,
    AttendeeStatus Status,
    AttendeeType Type,
    bool IsOrganizer,
    bool IsResource
);

/// <summary>
/// Event recurrence pattern
/// </summary>
public sealed record EventRecurrence(
    RecurrenceFrequency Frequency,
    int Interval,
    List<DayOfWeek> DaysOfWeek,
    int? DayOfMonth,
    int? WeekOfMonth,
    DateTime? RecurrenceEndDate,
    int? OccurrenceCount,
    List<DateTime> ExceptionDates
);

/// <summary>
/// Event reminder
/// </summary>
public sealed record EventReminder(
    ReminderMethod Method,
    int MinutesBefore,
    bool IsDefault
);

/// <summary>
/// Request to create external event
/// </summary>
public sealed record ExternalEventCreateRequest(
    string Title,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Location,
    EventVisibility Visibility,
    List<EventAttendee> Attendees,
    EventRecurrence? Recurrence,
    List<EventReminder> Reminders,
    Dictionary<string, object> CustomProperties
);

/// <summary>
/// Request to update external event
/// </summary>
public sealed record ExternalEventUpdateRequest(
    string? Title,
    string? Description,
    DateTime? StartTime,
    DateTime? EndTime,
    bool? IsAllDay,
    string? Location,
    EventVisibility? Visibility,
    List<EventAttendee>? Attendees,
    EventRecurrence? Recurrence,
    List<EventReminder>? Reminders,
    Dictionary<string, object>? CustomProperties
);

/// <summary>
/// External event update with ID
/// </summary>
public sealed record ExternalEventUpdateWithId(
    string EventId,
    ExternalEventUpdateRequest UpdateRequest
);

/// <summary>
/// Result of external event operations
/// </summary>
public sealed record ExternalEventResult(
    string? EventId,
    bool Success,
    string? ErrorMessage,
    ExternalCalendarEvent? EventData,
    DateTime ProcessedAt
);

/// <summary>
/// Result of external event deletion
/// </summary>
public sealed record ExternalEventDeleteResult(
    bool Success,
    string? ErrorMessage,
    DateTime ProcessedAt
);

/// <summary>
/// Calendar authentication result
/// </summary>
public sealed record CalendarAuthResult(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    List<string> GrantedScopes,
    string? ErrorMessage,
    Dictionary<string, object> TokenMetadata
);

/// <summary>
/// Token refresh result
/// </summary>
public sealed record TokenRefreshResult(
    bool Success,
    string? NewAccessToken,
    string? NewRefreshToken,
    DateTime? ExpiresAt,
    string? ErrorMessage
);

/// <summary>
/// External calendar events result with sync information
/// </summary>
public sealed record ExternalCalendarEventsResult(
    List<ExternalCalendarEvent> Events,
    string? NextSyncToken,
    bool HasMorePages,
    string? NextPageToken,
    DateTime RetrievedAt
);

// Time and Scheduling Models

/// <summary>
/// Time range specification
/// </summary>
public sealed record TimeRange(
    DateTime StartTime,
    DateTime EndTime
)
{
    public TimeSpan Duration => EndTime - StartTime;
    public bool Contains(DateTime time) => time >= StartTime && time <= EndTime;
    public bool Overlaps(TimeRange other) => StartTime < other.EndTime && EndTime > other.StartTime;
}

/// <summary>
/// Free/busy information result
/// </summary>
public sealed record FreeBusyResult(
    Guid UserId,
    List<CalendarFreeBusy> CalendarFreeBusy,
    TimeRange RequestedTimeRange,
    DateTime GeneratedAt
);

/// <summary>
/// Free/busy information for a specific calendar
/// </summary>
public sealed record CalendarFreeBusy(
    string CalendarId,
    List<BusyTimeSlot> BusySlots,
    List<string> Errors
);

/// <summary>
/// Busy time slot
/// </summary>
public sealed record BusyTimeSlot(
    DateTime StartTime,
    DateTime EndTime,
    BusyStatus Status,
    string? Title
);

/// <summary>
/// Calendar watch result for webhooks
/// </summary>
public sealed record CalendarWatchResult(
    bool Success,
    string? WatchId,
    string? ResourceId,
    DateTime? ExpirationTime,
    string? ErrorMessage
);

/// <summary>
/// Calendar watch stop result
/// </summary>
public sealed record CalendarWatchStopResult(
    bool Success,
    string? ErrorMessage
);

/// <summary>
/// Webhook processing result
/// </summary>
public sealed record WebhookProcessResult(
    bool Success,
    List<EventChangeInfo> Changes,
    string? ErrorMessage,
    DateTime ProcessedAt
);

// Provider and Token Management Models

/// <summary>
/// Calendar access token information
/// </summary>
public sealed record CalendarAccessToken(
    string AccessToken,
    string? RefreshToken,
    DateTime ExpiresAt,
    List<string> Scopes,
    CalendarProvider Provider,
    DateTime ObtainedAt
);

/// <summary>
/// Provider rate limit status
/// </summary>
public sealed record ProviderRateLimitStatus(
    int RequestsRemaining,
    DateTime ResetTime,
    TimeSpan ResetTimeSpan,
    int RequestLimit,
    TimeSpan LimitWindow
);

/// <summary>
/// Token validation result
/// </summary>
public sealed record TokenValidationResult(
    bool IsValid,
    DateTime? ExpiresAt,
    List<string> ValidScopes,
    string? ErrorMessage
);

/// <summary>
/// Provider capabilities
/// </summary>
public sealed record ProviderCapabilities(
    bool SupportsBatchOperations,
    bool SupportsIncrementalSync,
    bool SupportsWebhooks,
    bool SupportsFreeBusy,
    bool SupportsRecurringEvents,
    bool SupportsAttendees,
    bool SupportsReminders,
    int MaxBatchSize,
    int MaxRecurringInstances,
    List<string> SupportedEventFields
);

// Conflict Detection and Resolution Models

/// <summary>
/// Detected conflict between events
/// </summary>
public sealed record DetectedConflict(
    Guid ConflictId,
    Guid UserId,
    ConflictType Type,
    ConflictSeverity Severity,
    string Description,
    InternalCalendarEvent? InternalEvent,
    ExternalCalendarEvent? ExternalEvent,
    List<string> ConflictFields,
    Dictionary<string, object> ConflictDetails,
    DateTime DetectedAt
);

/// <summary>
/// Time overlap conflict
/// </summary>
public sealed record TimeOverlapConflict(
    Guid ConflictId,
    List<InternalCalendarEvent> OverlappingEvents,
    TimeRange OverlapTimeRange,
    int OverlapMinutes,
    ConflictSeverity Severity
);

/// <summary>
/// Duplicate event conflict
/// </summary>
public sealed record DuplicateEventConflict(
    Guid ConflictId,
    InternalCalendarEvent InternalEvent,
    ExternalCalendarEvent ExternalEvent,
    double SimilarityScore,
    List<string> MatchingFields
);

/// <summary>
/// Data consistency conflict
/// </summary>
public sealed record DataConsistencyConflict(
    Guid ConflictId,
    EventPair EventPair,
    List<FieldInconsistency> Inconsistencies,
    DateTime LastSyncTime
);

/// <summary>
/// Pair of related internal and external events
/// </summary>
public sealed record EventPair(
    InternalCalendarEvent InternalEvent,
    ExternalCalendarEvent ExternalEvent,
    string MappingId
);

/// <summary>
/// Field inconsistency information
/// </summary>
public sealed record FieldInconsistency(
    string FieldName,
    object? InternalValue,
    object? ExternalValue,
    DateTime LastModifiedInternal,
    DateTime LastModifiedExternal
);

/// <summary>
/// Conflict analysis result
/// </summary>
public sealed record ConflictAnalysis(
    Guid ConflictId,
    ConflictComplexity Complexity,
    List<ResolutionSuggestion> Suggestions,
    double AutoResolutionConfidence,
    List<string> RequiredUserDecisions,
    Dictionary<string, object> AnalysisMetadata
);

/// <summary>
/// User conflict preferences
/// </summary>
public sealed record UserConflictPreferences(
    Guid UserId,
    ConflictResolutionStrategy DefaultStrategy,
    bool AutoResolveSimpleConflicts,
    Dictionary<ConflictType, ConflictResolutionStrategy> TypeSpecificStrategies,
    int ConflictToleranceMinutes,
    List<string> IgnoreFields,
    DateTime LastUpdated
);

/// <summary>
/// Historical conflict resolution
/// </summary>
public sealed record HistoricalResolution(
    Guid ConflictId,
    ConflictType Type,
    ConflictResolutionAction Action,
    bool WasSuccessful,
    DateTime ResolvedAt,
    Guid UserId
);

// Additional Supporting Models

/// <summary>
/// Task to event conversion options
/// </summary>
public sealed record TaskToEventConversionOptions(
    bool SetDefaultDuration,
    TimeSpan DefaultDuration,
    bool CreateReminder,
    int ReminderMinutes,
    bool InviteContacts,
    EventVisibility Visibility
);

/// <summary>
/// Task to event conversion result
/// </summary>
public sealed record TaskToEventConversionResult(
    bool Success,
    string? ErrorMessage,
    InternalCalendarEvent? CreatedEvent,
    List<string> Warnings
);

/// <summary>
/// Event conversion options
/// </summary>
public sealed record EventConversionOptions(
    bool PreserveIds,
    bool ConvertTimeZones,
    string? TargetTimeZone,
    bool IncludeAttendees,
    bool IncludeReminders
);

/// <summary>
/// Sync statistics
/// </summary>
public sealed record SyncStatistics(
    int ApiCallsMade,
    long DataTransferred,
    TimeSpan NetworkTime,
    TimeSpan ProcessingTime,
    int CacheHits,
    int CacheMisses
);

/// <summary>
/// Event ID mapping between internal and external systems
/// </summary>
public sealed record EventIdMapping(
    Guid InternalEventId,
    string ExternalEventId,
    CalendarProvider Provider,
    string CalendarId,
    DateTime CreatedAt,
    DateTime LastSyncTime
);

// Enums

public enum SyncDirection
{
    ToExternal,
    FromExternal,
    Bidirectional
}

public enum SyncOperation
{
    Create,
    Update,
    Delete,
    Skip
}

public enum ConflictType
{
    TimeOverlap,
    DuplicateEvent,
    DataInconsistency,
    DeletedEvent,
    ModifiedEvent,
    PermissionDenied
}

public enum ConflictSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum ConflictResolutionAction
{
    KeepInternal,
    KeepExternal,
    Merge,
    CreateBoth,
    Skip,
    UserDecision
}

public enum ConflictComplexity
{
    Simple,
    Moderate,
    Complex,
    RequiresUserInput
}

public enum EventStatus
{
    Confirmed,
    Tentative,
    Cancelled
}

public enum EventVisibility
{
    Public,
    Private,
    Confidential
}

public enum EventSource
{
    Internal,
    External,
    Converted
}

public enum AttendeeStatus
{
    NeedsAction,
    Accepted,
    Declined,
    Tentative
}

public enum AttendeeType
{
    Individual,
    Group,
    Resource,
    Room
}

public enum ReminderMethod
{
    Email,
    Popup,
    SMS
}

public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public enum ChangeType
{
    Created,
    Updated,
    Deleted
}

public enum BusyStatus
{
    Busy,
    Tentative,
    OutOfOffice
}

public enum SyncHealthStatus
{
    Healthy,
    Warning,
    Error,
    Disconnected
}

/// <summary>
/// Result of a calendar conflict resolution operation
/// </summary>
public sealed record CalendarConflictResult(
    Guid ConflictId,
    bool Success,
    ConflictResolutionAction ActionTaken,
    string? ErrorMessage,
    DateTime ResolvedAt,
    Guid ResolvedByUserId
);

/// <summary>
/// Result of manual conflict resolution
/// </summary>
public sealed record ManualResolutionResult(
    Guid ConflictId,
    bool Success,
    ConflictResolutionAction ActionTaken,
    string? UserNotes,
    DateTime ResolvedAt
);

/// <summary>
/// Result of resolution validation
/// </summary>
public sealed record ResolutionValidationResult(
    bool IsValid,
    List<string> ValidationErrors,
    List<string> Warnings,
    ConflictComplexity EstimatedComplexity
);

/// <summary>
/// Resolution suggestion for conflicts
/// </summary>
public sealed record ResolutionSuggestion(
    ConflictResolutionAction RecommendedAction,
    string Description,
    float ConfidenceScore,
    List<string> Reasoning,
    Dictionary<string, object> Parameters
);

/// <summary>
/// Planned synchronization change
/// </summary>
public sealed record PlannedSyncChange(
    string EventId,
    SyncOperation Operation,
    CalendarProvider SourceProvider,
    CalendarProvider? TargetProvider,
    Dictionary<string, object> Changes,
    DateTime PlannedAt
);

/// <summary>
/// Current state of calendar synchronization
/// </summary>
public sealed record CalendarSyncState(
    Guid UserId,
    CalendarProvider Provider,
    DateTime LastSyncTime,
    string? CurrentSyncToken,
    bool IsHealthy,
    int PendingChanges,
    DateTime? LastConflictTime
);

/// <summary>
/// Predicted conflict based on planned changes
/// </summary>
public sealed record PredictedConflict(
    string ConflictId,
    ConflictType Type,
    ConflictSeverity Severity,
    List<string> AffectedEventIds,
    string Description,
    float Probability
);

/// <summary>
/// Conflict detection and resolution statistics
/// </summary>
public sealed record ConflictStatistics(
    int TotalConflictsDetected,
    int ConflictsResolved,
    int ConflictsPending,
    Dictionary<ConflictType, int> ConflictsByType,
    Dictionary<ConflictSeverity, int> ConflictsBySeverity,
    TimeSpan AverageResolutionTime,
    float AutoResolutionRate
);

/// <summary>
/// Data for conflicting events
/// </summary>
public sealed record ConflictingEventData(
    ExternalCalendarEvent Event1,
    ExternalCalendarEvent Event2,
    List<string> ConflictingFields,
    Dictionary<string, (object Value1, object Value2)> FieldDifferences
);

/// <summary>
/// Strategy for merging conflicting events
/// </summary>
public enum EventMergeStrategy
{
    PreferInternal,
    PreferExternal,
    PreferMostRecent,
    PreferDetailed,
    Manual,
    FieldByField
}

/// <summary>
/// Result of event merge operation
/// </summary>
public sealed record EventMergeResult(
    bool Success,
    ExternalCalendarEvent? MergedEvent,
    List<string> AppliedChanges,
    List<string> SkippedChanges,
    string? ErrorMessage
);

/// <summary>
/// Options for filtering conflicts
/// </summary>
public sealed record ConflictFilterOptions(
    List<ConflictType>? IncludeTypes = null,
    List<ConflictSeverity>? IncludeSeverities = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    bool? OnlyUnresolved = null,
    CalendarProvider? Provider = null
);

/// <summary>
/// Result of ignoring a conflict
/// </summary>
public sealed record ConflictIgnoreResult(
    Guid ConflictId,
    bool Success,
    DateTime IgnoredAt,
    string? Reason,
    TimeSpan? IgnoreDuration
);

/// <summary>
/// Options available for conflict resolution
/// </summary>
public enum ConflictResolutionOption
{
    KeepInternal,
    KeepExternal,
    Merge,
    Ignore,
    Manual
}

/// <summary>
/// Supported calendar providers (Application layer copy for DTOs)
/// </summary>
public enum CalendarProvider
{
    Google,
    Outlook,
    ICloud,
    CalDAV,
    Exchange,
    Yahoo,
    Custom
}

/// <summary>
/// Conflict resolution strategies (Application layer copy for DTOs)
/// </summary>
public enum ConflictResolutionStrategy
{
    LastModifiedWins,
    ExternalWins,
    InternalWins,
    UserResolves,
    CreateDuplicates,
    SmartMerge
}

/// <summary>
/// Options for data consistency checking
/// </summary>
public sealed record DataConsistencyOptions(
    bool CheckMandatoryFields = true,
    bool CheckDataTypes = true,
    bool CheckDateRanges = true,
    bool CheckDuplicates = true,
    List<string>? IgnoreFields = null
);

/// <summary>
/// Result of automatic conflict resolution
/// </summary>
public sealed record AutoResolutionResult(
    bool WasResolved,
    ConflictResolutionAction ActionTaken,
    string? ResolutionReason,
    float ConfidenceScore,
    List<string> AppliedChanges
);

/// <summary>
/// Calendar synchronization modes
/// </summary>
public enum SyncMode
{
    OneWayToExternal,
    OneWayFromExternal,
    BiDirectional
}

/// <summary>
/// Options for conflict detection
/// </summary>
public sealed record ConflictDetectionOptions(
    bool EnableTimeOverlapDetection = true,
    bool EnableDuplicateDetection = true,
    bool EnableDataConsistencyCheck = true,
    TimeSpan OverlapTolerance = default,
    bool StrictModeEnabled = false
);

/// <summary>
/// Criteria for duplicate detection
/// </summary>
public sealed record DuplicateDetectionCriteria(
    bool MatchByTitle = true,
    bool MatchByTime = true,
    bool MatchByDescription = false,
    bool MatchByAttendees = false,
    double SimilarityThreshold = 0.85
);

/// <summary>
/// Conflict resolution type for internal processing
/// </summary>
public enum ConflictResolutionType
{
    Automatic,
    Manual,
    Deferred
}

/// <summary>
/// Internal reminder method for mapping
/// </summary>
public enum InternalReminderMethod
{
    Email,
    Push,
    SMS,
    Popup
}

/// <summary>
/// External reminder method for mapping
/// </summary>
public enum ExternalReminderMethod
{
    Email,
    Notification,
    Display,
    Sound
}

/// <summary>
/// Provider-specific configuration settings
/// </summary>
public record CalendarProviderSettings(
    string ProviderId,
    Dictionary<string, object> Settings,
    bool IsEnabled = true
);

/// <summary>
/// Google Calendar provider specific settings
/// </summary>
public sealed record GoogleCalendarProviderSettings(
    string ClientId,
    string ClientSecret,
    string ApiEndpoint = "https://www.googleapis.com/calendar/v3",
    string Scopes = "https://www.googleapis.com/auth/calendar",
    int MaxResults = 250
) : CalendarProviderSettings("google", new Dictionary<string, object>());

/// <summary>
/// Outlook Calendar provider specific settings
/// </summary>
public sealed record OutlookCalendarProviderSettings(
    string ApplicationId,
    string ApplicationSecret,
    string ApiEndpoint = "https://graph.microsoft.com/v1.0",
    string Scopes = "https://graph.microsoft.com/calendars.readwrite",
    string? TenantId = null
) : CalendarProviderSettings("outlook", new Dictionary<string, object>());

/// <summary>
/// iCloud Calendar provider specific settings
/// </summary>
public sealed record ICloudCalendarProviderSettings(
    string ServerUrl = "https://caldav.icloud.com",
    int Port = 443,
    bool UseSSL = true,
    string PrincipalUrlPattern = "/principal/{username}",
    string CalendarHomeUrlPattern = "/calendars/{username}"
) : CalendarProviderSettings("icloud", new Dictionary<string, object>());

/// <summary>
/// Potential conflict detection result
/// </summary>
public sealed record PotentialConflict(
    string Id,
    ConflictType Type,
    string Description,
    List<string> AffectedItems,
    float Probability
);
