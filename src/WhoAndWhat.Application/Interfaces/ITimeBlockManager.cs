using WhoAndWhat.Application.DTOs.SmartScheduling;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service for managing and optimizing time blocks for enhanced productivity
/// </summary>
public interface ITimeBlockManager
{
    /// <summary>
    /// Generate time blocks based on scheduled items and user preferences
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="scheduledItems">Items already scheduled</param>
    /// <param name="preferences">User scheduling preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of suggested time blocks</returns>
    public Task<List<TimeBlockSuggestion>> GenerateTimeBlocksAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate time block recommendations for a specific date
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="date">Date to generate time blocks for</param>
    /// <param name="preferences">User preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of time block recommendations</returns>
    public Task<List<TimeBlockSuggestion>> GenerateTimeBlockRecommendationsAsync(
        Guid userId,
        DateTime date,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimize existing time blocks for better productivity
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="currentTimeBlocks">Current time blocks</param>
    /// <param name="preferences">User preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimized time blocks</returns>
    public Task<List<TimeBlockSuggestion>> OptimizeTimeBlocksAsync(
        Guid userId,
        List<TimeBlockSuggestion> currentTimeBlocks,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create time blocks for deep work sessions
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="availableTime">Available time slots</param>
    /// <param name="preferences">User preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deep work time blocks</returns>
    public Task<List<TimeBlockSuggestion>> CreateDeepWorkBlocksAsync(
        Guid userId,
        List<TimeSlot> availableTime,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create time blocks for administrative tasks
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="availableTime">Available time slots</param>
    /// <param name="preferences">User preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Administrative time blocks</returns>
    public Task<List<TimeBlockSuggestion>> CreateAdministrativeBlocksAsync(
        Guid userId,
        List<TimeSlot> availableTime,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create buffer time blocks between tasks
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="scheduledItems">Scheduled items to create buffers around</param>
    /// <param name="bufferDuration">Duration of buffer blocks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Buffer time blocks</returns>
    public Task<List<TimeBlockSuggestion>> CreateBufferBlocksAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        TimeSpan bufferDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze time block effectiveness based on user patterns
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="timeBlocks">Time blocks to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis of time block effectiveness</returns>
    public Task<TimeBlockAnalysis> AnalyzeTimeBlockEffectivenessAsync(
        Guid userId,
        List<TimeBlockSuggestion> timeBlocks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get optimal time block durations based on user patterns and task types
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="blockPurpose">Purpose of the time block</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recommended time block duration</returns>
    public Task<TimeBlockDurationRecommendation> GetOptimalBlockDurationAsync(
        Guid userId,
        TimeBlockPurpose blockPurpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if time block manager is available and properly configured
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if manager is ready for time block operations</returns>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Analysis of time block effectiveness
/// </summary>
public sealed record TimeBlockAnalysis(
    Guid UserId,
    DateTime AnalysisDate,
    double OverallEffectiveness,
    Dictionary<TimeBlockPurpose, double> EffectivenessByPurpose,
    List<TimeBlockInsight> Insights,
    List<string> Recommendations,
    TimeBlockMetrics Metrics
);

/// <summary>
/// Insight about time block usage
/// </summary>
public sealed record TimeBlockInsight(
    string InsightType,
    string Description,
    double ImpactScore,
    List<string> RecommendedActions,
    List<Guid> AffectedTimeBlocks
);

/// <summary>
/// Metrics about time block performance
/// </summary>
public sealed record TimeBlockMetrics(
    int TotalTimeBlocks,
    Dictionary<TimeBlockPurpose, int> BlocksByPurpose,
    TimeSpan AverageBlockDuration,
    TimeSpan TotalTimeBlocked,
    double UtilizationRate,
    int CompletedBlocks,
    int InterruptedBlocks
);

/// <summary>
/// Recommendation for optimal time block duration
/// </summary>
public sealed record TimeBlockDurationRecommendation(
    TimeBlockPurpose Purpose,
    TimeSpan RecommendedDuration,
    TimeSpan MinimumDuration,
    TimeSpan MaximumDuration,
    double ConfidenceScore,
    List<string> FactorsConsidered,
    string Reasoning
);
