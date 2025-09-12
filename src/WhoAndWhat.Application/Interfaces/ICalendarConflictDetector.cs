using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service for detecting and resolving calendar synchronization conflicts
/// </summary>
public interface ICalendarConflictDetector
{
    /// <summary>
    /// Detect conflicts between internal events and external calendar events
    /// </summary>
    /// <param name="userId">User ID to check conflicts for</param>
    /// <param name="internalEvents">Internal WhoAndWhat events</param>
    /// <param name="externalEvents">External calendar events</param>
    /// <param name="detectionOptions">Options for conflict detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected conflicts with severity and type information</returns>
    Task<IEnumerable<DetectedConflict>> DetectConflictsAsync(Guid userId, IEnumerable<InternalCalendarEvent> internalEvents, IEnumerable<ExternalCalendarEvent> externalEvents, ConflictDetectionOptions detectionOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect time overlap conflicts for a specific time period
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="targetTimeRange">Time range to check for overlaps</param>
    /// <param name="existingEvents">Existing events to check against</param>
    /// <param name="conflictTolerance">Tolerance for time overlaps (buffer minutes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of time overlap conflicts</returns>
    Task<IEnumerable<TimeOverlapConflict>> DetectTimeOverlapAsync(Guid userId, TimeRange targetTimeRange, IEnumerable<InternalCalendarEvent> existingEvents, int conflictTolerance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect duplicate events across internal and external calendars
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="internalEvents">Internal events</param>
    /// <param name="externalEvents">External events</param>
    /// <param name="duplicateDetectionCriteria">Criteria for determining duplicates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of potential duplicate events</returns>
    Task<IEnumerable<DuplicateEventConflict>> DetectDuplicatesAsync(Guid userId, IEnumerable<InternalCalendarEvent> internalEvents, IEnumerable<ExternalCalendarEvent> externalEvents, DuplicateDetectionCriteria duplicateDetectionCriteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect data consistency conflicts (same event with different data)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="eventPairs">Paired internal and external events that represent the same event</param>
    /// <param name="consistencyOptions">Options for data consistency checking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of data consistency conflicts</returns>
    Task<IEnumerable<DataConsistencyConflict>> DetectDataInconsistencyAsync(Guid userId, IEnumerable<EventPair> eventPairs, DataConsistencyOptions consistencyOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze a specific conflict and provide resolution recommendations
    /// </summary>
    /// <param name="conflict">Conflict to analyze</param>
    /// <param name="userPreferences">User's conflict resolution preferences</param>
    /// <param name="historicalResolutions">Previous conflict resolution patterns</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conflict analysis with recommended resolution strategies</returns>
    Task<ConflictAnalysis> AnalyzeConflictAsync(DetectedConflict conflict, UserConflictPreferences userPreferences, IEnumerable<HistoricalResolution> historicalResolutions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-resolve conflicts based on configured resolution strategy
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="conflicts">Conflicts to resolve</param>
    /// <param name="resolutionStrategy">Strategy to use for resolution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results of automatic conflict resolution</returns>
    Task<IEnumerable<AutoResolutionResult>> AutoResolveConflictsAsync(Guid userId, IEnumerable<DetectedConflict> conflicts, ConflictResolutionStrategy resolutionStrategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply user-specified resolution to a conflict
    /// </summary>
    /// <param name="userId">User ID who is resolving the conflict</param>
    /// <param name="conflictId">ID of the conflict to resolve</param>
    /// <param name="resolution">User's chosen resolution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of manual conflict resolution</returns>
    Task<ManualResolutionResult> ApplyResolutionAsync(Guid userId, Guid conflictId, ConflictResolution resolution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate proposed conflict resolution to ensure it won't create new conflicts
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="conflictId">Conflict being resolved</param>
    /// <param name="proposedResolution">Proposed resolution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with potential issues</returns>
    Task<ResolutionValidationResult> ValidateResolutionAsync(Guid userId, Guid conflictId, ConflictResolution proposedResolution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get conflict resolution suggestions based on user patterns and preferences
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="conflict">Conflict requiring resolution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Intelligent suggestions for conflict resolution</returns>
    Task<IEnumerable<ResolutionSuggestion>> GetResolutionSuggestionsAsync(Guid userId, DetectedConflict conflict, CancellationToken cancellationToken = default);

    /// <summary>
    /// Predict potential conflicts before they occur during sync operations
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="plannedChanges">Changes planned to be made during sync</param>
    /// <param name="currentState">Current state of events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Predicted conflicts that may occur</returns>
    Task<IEnumerable<PredictedConflict>> PredictConflictsAsync(Guid userId, IEnumerable<PlannedSyncChange> plannedChanges, CalendarSyncState currentState, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get statistics about conflict patterns for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="timeRange">Time range for statistics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conflict statistics and patterns</returns>
    Task<ConflictStatistics> GetConflictStatisticsAsync(Guid userId, TimeRange timeRange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update user conflict resolution preferences based on their resolution patterns
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="recentResolutions">Recent conflict resolutions by the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated preferences based on user behavior</returns>
    Task<UserConflictPreferences> UpdateUserPreferencesAsync(Guid userId, IEnumerable<HistoricalResolution> recentResolutions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge conflicting events into a single consistent event
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="conflictingEvents">Events to merge</param>
    /// <param name="mergeStrategy">Strategy for merging data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Merged event result</returns>
    Task<EventMergeResult> MergeConflictingEventsAsync(Guid userId, IEnumerable<ConflictingEventData> conflictingEvents, EventMergeStrategy mergeStrategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a specific conflict has been resolved and is no longer active
    /// </summary>
    /// <param name="conflictId">Conflict ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if conflict is resolved</returns>
    Task<bool> IsConflictResolvedAsync(Guid conflictId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all pending conflicts for a user that require manual resolution
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="filterOptions">Options to filter conflicts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending conflicts</returns>
    Task<IEnumerable<DetectedConflict>> GetPendingConflictsAsync(Guid userId, ConflictFilterOptions filterOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a conflict as ignored (won't be shown to user again)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="conflictId">Conflict ID to ignore</param>
    /// <param name="ignoreReason">Reason for ignoring the conflict</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of ignore operation</returns>
    Task<ConflictIgnoreResult> IgnoreConflictAsync(Guid userId, Guid conflictId, string ignoreReason, CancellationToken cancellationToken = default);
}