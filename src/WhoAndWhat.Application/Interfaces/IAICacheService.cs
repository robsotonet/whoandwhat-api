using WhoAndWhat.Application.DTOs.AI;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Cache service interface for AI planning responses and data
/// </summary>
public interface IAICacheService
{
    /// <summary>
    /// Cache AI-generated day plan
    /// </summary>
    /// <param name="dayPlan">Day plan to cache</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheDayPlanAsync(AIGeneratedPlan dayPlan, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached day plan for user and date
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="planDate">Plan date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached day plan if available</returns>
    public Task<AIGeneratedPlan?> GetCachedDayPlanAsync(Guid userId, DateTime planDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache task priority suggestion
    /// </summary>
    /// <param name="prioritySuggestion">Priority suggestion to cache</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CachePrioritySuggestionAsync(TaskPrioritySuggestion prioritySuggestion, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached priority suggestion for task
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached priority suggestion if available</returns>
    public Task<TaskPrioritySuggestion?> GetCachedPrioritySuggestionAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache productivity insights
    /// </summary>
    /// <param name="insights">Productivity insights to cache</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheProductivityInsightsAsync(ProductivityInsights insights, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached productivity insights
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="analysisTimeframe">Analysis timeframe</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached insights if available</returns>
    public Task<ProductivityInsights?> GetCachedProductivityInsightsAsync(Guid userId, TimeframeAnalysis analysisTimeframe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache schedule optimization result
    /// </summary>
    /// <param name="optimizationResult">Optimization result to cache</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheScheduleOptimizationAsync(ScheduleOptimizationResult optimizationResult, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached schedule optimization result
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="optimizationDate">Optimization date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached optimization result if available</returns>
    public Task<ScheduleOptimizationResult?> GetCachedScheduleOptimizationAsync(Guid userId, DateTime optimizationDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache task categorization suggestions
    /// </summary>
    /// <param name="taskContent">Task content (used as key)</param>
    /// <param name="suggestions">Categorization suggestions to cache</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheCategorizationSuggestionsAsync(string taskContent, IEnumerable<CategorySuggestion> suggestions, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached categorization suggestions
    /// </summary>
    /// <param name="taskContent">Task content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached suggestions if available</returns>
    public Task<IEnumerable<CategorySuggestion>?> GetCachedCategorizationSuggestionsAsync(string taskContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache task time estimates
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="estimate">Time estimate to cache</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheTaskTimeEstimateAsync(Guid taskId, TaskTimeEstimate estimate, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached task time estimate
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached time estimate if available</returns>
    public Task<TaskTimeEstimate?> GetCachedTaskTimeEstimateAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all AI cache for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if invalidated successfully</returns>
    public Task<bool> InvalidateUserAICacheAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate specific AI cache types for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cacheTypes">Types of cache to invalidate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if invalidated successfully</returns>
    public Task<bool> InvalidateUserAICacheByTypeAsync(Guid userId, IEnumerable<AICacheType> cacheTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get AI cache metrics and statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache metrics</returns>
    public Task<AICacheMetrics> GetAICacheMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all AI cache data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cleared successfully</returns>
    public Task<bool> ClearAllAICacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Warm AI cache for frequently accessed user data
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of cache entries warmed</returns>
    public Task<int> WarmUserAICacheAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Types of AI cache for selective invalidation
/// </summary>
public enum AICacheType
{
    DayPlans,
    PrioritySuggestions,
    ProductivityInsights,
    ScheduleOptimizations,
    CategorizationSuggestions,
    TimeEstimates,
    All
}

/// <summary>
/// AI cache metrics and statistics
/// </summary>
public sealed record AICacheMetrics(
    int TotalEntries,
    long TotalSizeBytes,
    int HitCount,
    int MissCount,
    double HitRate,
    Dictionary<AICacheType, int> EntriesByType,
    Dictionary<AICacheType, double> HitRatesByType,
    DateTime LastResetTime,
    TimeSpan AverageResponseTime
);
