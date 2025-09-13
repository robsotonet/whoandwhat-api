using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Repository interface for SchedulingPattern entities
/// Provides data access methods for machine learning pattern detection and optimization
/// </summary>
public interface ISchedulingPatternRepository : IRepository<SchedulingPattern>
{
    /// <summary>
    /// Gets all active scheduling patterns for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Active scheduling patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetActivePatternsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reliable patterns for a user (high confidence and success rate)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reliable scheduling patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetReliablePatternsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns by type for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="patternType">The pattern type (e.g., "Productivity", "Energy", "Category")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Patterns of the specified type</returns>
    Task<IEnumerable<SchedulingPattern>> GetPatternsByTypeAsync(Guid userId, string patternType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns that apply to a specific time and context
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="time">The time to check</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="tags">Optional tags filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Applicable patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetApplicablePatternsAsync(Guid userId, DateTime time, string? category = null, List<string>? tags = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns with high productivity correlation
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="minCorrelation">Minimum productivity correlation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>High productivity patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetHighProductivityPatternsAsync(Guid userId, double minCorrelation = 0.7, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns that need more data for statistical significance
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="minReinforcementCount">Minimum reinforcement count for significance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Patterns needing more data</returns>
    Task<IEnumerable<SchedulingPattern>> GetPatternsNeedingMoreDataAsync(Guid userId, int minReinforcementCount = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns associated with specific categories
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="categories">Categories to match</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Category-associated patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetPatternsByCategoriesAsync(Guid userId, List<string> categories, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns for specific days of the week
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="daysOfWeek">Days of week to match</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Day-specific patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetPatternsByDaysOfWeekAsync(Guid userId, List<DayOfWeek> daysOfWeek, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns for specific time ranges
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startTime">Start time range</param>
    /// <param name="endTime">End time range</param>
    /// <param name="tolerance">Time tolerance in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time-specific patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetPatternsByTimeRangeAsync(Guid userId, TimeSpan startTime, TimeSpan endTime, int tolerance = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets similar patterns across different users for collaborative filtering
    /// </summary>
    /// <param name="referencePattern">The reference pattern to find similarities for</param>
    /// <param name="similarityThreshold">Minimum similarity score</param>
    /// <param name="maxResults">Maximum number of similar patterns</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Similar patterns from other users</returns>
    Task<IEnumerable<SchedulingPattern>> GetSimilarPatternsAsync(SchedulingPattern referencePattern, double similarityThreshold = 0.7, int maxResults = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns that should be applied to optimization
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimization-eligible patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetOptimizationEligiblePatternsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns with recent violations (may need adjustment)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="recentPeriod">Period to check for recent violations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recently violated patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetRecentlyViolatedPatternsAsync(Guid userId, TimeSpan recentPeriod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns with high success rates for specific contexts
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="minSuccessRate">Minimum success rate threshold</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>High success rate patterns</returns>
    Task<IEnumerable<SchedulingPattern>> GetHighSuccessRatePatternsAsync(Guid userId, double minSuccessRate = 0.8, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reinforces multiple patterns in batch
    /// </summary>
    /// <param name="reinforcements">Dictionary of pattern ID to productivity score</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of patterns reinforced</returns>
    Task<int> BulkReinforceAsync(Dictionary<Guid, double> reinforcements, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records violations for multiple patterns in batch
    /// </summary>
    /// <param name="patternIds">Pattern IDs that were violated</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of violations recorded</returns>
    Task<int> BulkRecordViolationsAsync(IEnumerable<Guid> patternIds, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates patterns with poor performance
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="maxSuccessRate">Maximum success rate threshold</param>
    /// <param name="minReinforcementCount">Minimum reinforcement count to consider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of patterns deactivated</returns>
    Task<int> DeactivatePoorPerformingPatternsAsync(Guid userId, double maxSuccessRate = 0.3, int minReinforcementCount = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pattern statistics for analytics and reporting
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="patternType">Optional pattern type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pattern statistics</returns>
    Task<Dictionary<string, object>> GetPatternStatisticsAsync(Guid userId, string? patternType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets productivity insights from pattern analysis
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="analysisPeriod">Period to analyze (default: 30 days)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Productivity insights</returns>
    Task<Dictionary<string, object>> GetProductivityInsightsAsync(Guid userId, TimeSpan? analysisPeriod = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pattern evolution data for trend analysis
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="patternId">Specific pattern to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pattern evolution data</returns>
    Task<Dictionary<string, object>> GetPatternEvolutionAsync(Guid userId, Guid patternId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates patterns based on new behavioral data
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="behaviorData">Behavioral data for pattern analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created or updated patterns</returns>
    Task<IEnumerable<SchedulingPattern>> CreateOrUpdatePatternsFromBehaviorAsync(Guid userId, Dictionary<string, object> behaviorData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recommendations for pattern improvements
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="patternId">Specific pattern to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pattern improvement recommendations</returns>
    Task<List<string>> GetPatternImprovementRecommendationsAsync(Guid userId, Guid patternId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a pattern belongs to the specified user
    /// </summary>
    /// <param name="patternId">The pattern ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the pattern belongs to the user</returns>
    Task<bool> PatternBelongsToUserAsync(Guid patternId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old or inactive patterns to maintain database health
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="inactiveThreshold">Threshold for considering patterns inactive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of patterns deleted</returns>
    Task<int> DeleteInactivePatternsAsync(Guid userId, TimeSpan inactiveThreshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets machine learning features for pattern analysis
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ML feature data</returns>
    Task<Dictionary<string, object>> GetMachineLearningFeaturesAsync(Guid userId, CancellationToken cancellationToken = default);
}