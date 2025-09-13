using WhoAndWhat.Application.DTOs.SmartScheduling;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service for managing user scheduling preferences and learning from user patterns
/// </summary>
public interface IUserSchedulingPreferenceService
{
    /// <summary>
    /// Get user's current scheduling preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current user scheduling preferences</returns>
    public Task<SmartSchedulingPreferences> GetUserPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update user's scheduling preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="preferences">New preferences to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated preferences</returns>
    public Task<SmartSchedulingPreferences> UpdatePreferencesAsync(Guid userId, SmartSchedulingPreferences preferences, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze user's scheduling patterns from historical data
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="startDate">Start date for pattern analysis</param>
    /// <param name="endDate">End date for pattern analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected scheduling patterns and insights</returns>
    public Task<UserSchedulingPatternsResponse> AnalyzeSchedulingPatternsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user's scheduling patterns (cached or recently analyzed)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User's scheduling patterns</returns>
    public Task<UserSchedulingPatternsResponse> GetUserSchedulingPatternsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record scheduling activity to learn from user behavior
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="scheduledItems">Items that were scheduled</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public Task RecordSchedulingActivityAsync(Guid userId, List<SmartScheduledItem> scheduledItems, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record user feedback on schedule quality
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="scheduleId">Schedule that received feedback</param>
    /// <param name="feedback">User feedback on the schedule</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public Task RecordScheduleFeedbackAsync(Guid userId, Guid scheduleId, ScheduleFeedback feedback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Learn and update preferences based on user behavior
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated preferences based on learned patterns</returns>
    public Task<SmartSchedulingPreferences> LearnAndUpdatePreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get productivity insights based on user's scheduling history
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="timeframe">Timeframe for analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Productivity insights and recommendations</returns>
    public Task<ProductivityInsightsResponse> GetProductivityInsightsAsync(Guid userId, AnalysisTimeframe timeframe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Predict optimal times for specific task types based on user patterns
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="taskCategory">Category of task to predict optimal time for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Predicted optimal time slots</returns>
    public Task<List<OptimalTimeSlot>> PredictOptimalTimesAsync(Guid userId, string taskCategory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user's energy level predictions for different times of day
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="date">Date to predict energy levels for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Energy level predictions throughout the day</returns>
    public Task<List<EnergyLevelPrediction>> GetEnergyLevelPredictionsAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialize default preferences for a new user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="timezone">User's timezone</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Default preferences for the user</returns>
    public Task<SmartSchedulingPreferences> InitializeDefaultPreferencesAsync(Guid userId, string timezone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the user preference service is available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is available</returns>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// User feedback on schedule quality
/// </summary>
public sealed record ScheduleFeedback(
    double OverallRating,
    double ProductivityRating,
    double BalanceRating,
    double FlexibilityRating,
    List<string> PositiveAspects,
    List<string> ImprovementAreas,
    string FreeformComments,
    DateTime ProvidedAt
);

/// <summary>
/// Analysis timeframe for productivity insights
/// </summary>
public sealed record AnalysisTimeframe(
    DateTime StartDate,
    DateTime EndDate,
    TimeframePeriod Period
);

/// <summary>
/// Productivity insights response
/// </summary>
public sealed record ProductivityInsightsResponse(
    Guid UserId,
    AnalysisTimeframe Timeframe,
    double AverageProductivityScore,
    Dictionary<string, double> ProductivityByTimeOfDay,
    Dictionary<string, double> ProductivityByTaskCategory,
    List<ProductivityTrend> Trends,
    List<string> Insights,
    List<string> Recommendations,
    DateTime GeneratedAt
);

/// <summary>
/// Productivity trend over time
/// </summary>
public sealed record ProductivityTrend(
    string TrendName,
    TrendDirection Direction,
    double ChangePercentage,
    TimeframePeriod Period,
    List<TrendDataPoint> DataPoints,
    string Description
);

/// <summary>
/// Optimal time slot for a task type
/// </summary>
public sealed record OptimalTimeSlot(
    TimeSpan StartTime,
    TimeSpan EndTime,
    double OptimalityScore,
    string Reasoning,
    List<string> SupportingFactors
);

/// <summary>
/// Energy level prediction for a time period
/// </summary>
public sealed record EnergyLevelPrediction(
    TimeSpan Time,
    double EnergyLevel,
    EnergyLevelType LevelType,
    double Confidence,
    List<string> InfluencingFactors
);

// Enums

public enum TimeframePeriod
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

public enum TrendDirection
{
    Improving,
    Declining,
    Stable,
    Volatile
}

public enum EnergyLevelType
{
    VeryLow,
    Low,
    Moderate,
    High,
    VeryHigh,
    Peak
}
