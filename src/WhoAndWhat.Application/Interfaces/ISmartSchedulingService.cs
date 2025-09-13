using WhoAndWhat.Application.DTOs.SmartScheduling;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service for intelligent task scheduling and optimization using AI and calendar integration
/// </summary>
public interface ISmartSchedulingService
{
    /// <summary>
    /// Generate an optimized smart schedule for a user based on tasks, calendar events, and preferences
    /// </summary>
    /// <param name="request">Schedule generation request with user preferences and constraints</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Smart schedule with optimized task placement and time blocks</returns>
    Task<SmartScheduleResponse> GenerateSmartScheduleAsync(GenerateSmartScheduleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimize an existing schedule to improve productivity and efficiency
    /// </summary>
    /// <param name="request">Schedule optimization request with current schedule and goals</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimized schedule with changes and improvements</returns>
    Task<ScheduleOptimizationResponse> OptimizeScheduleAsync(OptimizeScheduleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get intelligent scheduling suggestions for specific tasks
    /// </summary>
    /// <param name="request">Scheduling suggestions request for tasks and date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of scheduling suggestions with reasoning</returns>
    Task<SchedulingSuggestionsResponse> GetSchedulingSuggestionsAsync(GetSchedulingSuggestionsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update user's smart scheduling preferences
    /// </summary>
    /// <param name="request">Preference update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated preferences confirmation</returns>
    Task<UpdateSchedulingPreferencesResponse> UpdateUserSchedulingPreferencesAsync(UpdateSchedulingPreferencesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze user's scheduling patterns to improve future scheduling
    /// </summary>
    /// <param name="request">Pattern analysis request with date range</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected patterns and insights about user's scheduling behavior</returns>
    Task<UserSchedulingPatternsResponse> AnalyzeUserSchedulingPatternsAsync(GetUserSchedulingPatternsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user's current scheduling preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current user scheduling preferences</returns>
    Task<SmartSchedulingPreferences> GetUserSchedulingPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate schedule conflicts and dependencies
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="scheduledItems">Items to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation results with conflicts and warnings</returns>
    Task<ScheduleValidationResult> ValidateScheduleAsync(Guid userId, List<SmartScheduledItem> scheduledItems, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply real-time adjustments to a schedule based on changes
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="scheduleId">Schedule to adjust</param>
    /// <param name="changes">Changes that occurred</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Adjusted schedule with minimal disruption</returns>
    Task<SmartScheduleResponse> ApplyRealTimeScheduleAdjustmentAsync(Guid userId, Guid scheduleId, List<ScheduleChange> changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get time block recommendations for better productivity
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="date">Date to generate time blocks for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recommended time blocks for optimal productivity</returns>
    Task<List<TimeBlockSuggestion>> GetTimeBlockRecommendationsAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if smart scheduling service is available and properly configured
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is ready for scheduling operations</returns>
    Task<bool> IsSmartSchedulingAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Schedule validation result containing conflicts and warnings
/// </summary>
public sealed record ScheduleValidationResult(
    bool IsValid,
    List<ScheduleConflict> Conflicts,
    List<ScheduleWarning> Warnings,
    List<string> Recommendations
);

/// <summary>
/// A conflict detected in the schedule
/// </summary>
public sealed record ScheduleConflict(
    string ConflictType,
    List<Guid> ConflictingItemIds,
    string Description,
    string Severity,
    List<string> ResolutionSuggestions
);

/// <summary>
/// A warning about the schedule
/// </summary>
public sealed record ScheduleWarning(
    string WarningType,
    List<Guid> AffectedItemIds,
    string Description,
    string Impact
);