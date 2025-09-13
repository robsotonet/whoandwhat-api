using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Repository interface for TimeBlock entities
/// Provides data access methods for time block management and productivity optimization
/// </summary>
public interface ITimeBlockRepository : IRepository<TimeBlock>
{
    /// <summary>
    /// Gets time blocks for a user within a date range
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time blocks in date range</returns>
    public Task<IEnumerable<TimeBlock>> GetByUserAndDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets time blocks for a specific date
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="date">The specific date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time blocks for the date</returns>
    public Task<IEnumerable<TimeBlock>> GetByUserAndDateAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active time blocks for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Currently active time blocks</returns>
    public Task<IEnumerable<TimeBlock>> GetActiveTimeBlocksAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upcoming time blocks for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="hoursAhead">Hours to look ahead (default: 24)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upcoming time blocks</returns>
    public Task<IEnumerable<TimeBlock>> GetUpcomingTimeBlocksAsync(Guid userId, int hoursAhead = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets time blocks by purpose/type
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="purpose">The time block purpose</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time blocks with the specified purpose</returns>
    public Task<IEnumerable<TimeBlock>> GetByPurposeAsync(Guid userId, int purpose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets completed time blocks for productivity analysis
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startDate">Start date for analysis</param>
    /// <param name="endDate">End date for analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Completed time blocks in date range</returns>
    public Task<IEnumerable<TimeBlock>> GetCompletedTimeBlocksAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recurring time blocks for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recurring time blocks</returns>
    public Task<IEnumerable<TimeBlock>> GetRecurringTimeBlocksAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets flexible time blocks that can be rescheduled
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startDate">Start date for search range</param>
    /// <param name="endDate">End date for search range</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Flexible time blocks</returns>
    public Task<IEnumerable<TimeBlock>> GetFlexibleTimeBlocksAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets deep work time blocks for productivity optimization
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deep work time blocks</returns>
    public Task<IEnumerable<TimeBlock>> GetDeepWorkTimeBlocksAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets time blocks with low productivity scores for optimization
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="maxProductivityScore">Maximum productivity score threshold</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Low productivity time blocks</returns>
    public Task<IEnumerable<TimeBlock>> GetLowProductivityTimeBlocksAsync(Guid userId, double maxProductivityScore = 0.5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets time blocks that need analysis (haven't been analyzed recently)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="analysisThreshold">Time threshold since last analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time blocks needing analysis</returns>
    public Task<IEnumerable<TimeBlock>> GetTimeBlocksNeedingAnalysisAsync(Guid userId, TimeSpan analysisThreshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for time conflicts with existing time blocks
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startTime">Start time of the potential time block</param>
    /// <param name="endTime">End time of the potential time block</param>
    /// <param name="excludeTimeBlockId">Time block ID to exclude from conflict check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conflicting time blocks</returns>
    public Task<IEnumerable<TimeBlock>> GetConflictingTimeBlocksAsync(Guid userId, DateTime startTime, DateTime endTime, Guid? excludeTimeBlockId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets optimal time slots for a specific purpose based on historical performance
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="purpose">The time block purpose</param>
    /// <param name="duration">Required duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimal time slots with productivity scores</returns>
    public Task<IEnumerable<(TimeSpan StartTime, TimeSpan EndTime, double ProductivityScore)>> GetOptimalTimeSlotsForPurposeAsync(Guid userId, int purpose, TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets productivity patterns by time of day
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="purpose">Optional purpose filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Productivity patterns by hour of day</returns>
    public Task<Dictionary<int, double>> GetProductivityPatternsByTimeAsync(Guid userId, int? purpose = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets productivity patterns by day of week
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="purpose">Optional purpose filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Productivity patterns by day of week</returns>
    public Task<Dictionary<DayOfWeek, double>> GetProductivityPatternsByDayAsync(Guid userId, int? purpose = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates productivity scores for multiple time blocks
    /// </summary>
    /// <param name="updates">Dictionary of time block ID to productivity score</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of time blocks updated</returns>
    public Task<int> BulkUpdateProductivityScoresAsync(Dictionary<Guid, double> updates, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records completion for multiple time blocks
    /// </summary>
    /// <param name="completionData">Dictionary of time block ID to completion data</param>
    /// <param name="userId">The user ID for security validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of time blocks updated</returns>
    public Task<int> BulkCompleteTimeBlocksAsync(Dictionary<Guid, (bool IsCompleted, double? ProductivityScore)> completionData, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets time block efficiency metrics
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="purpose">Optional purpose filter</param>
    /// <param name="startDate">Start date for analysis</param>
    /// <param name="endDate">End date for analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Efficiency metrics</returns>
    public Task<Dictionary<string, object>> GetEfficiencyMetricsAsync(Guid userId, int? purpose = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets interruption patterns for time blocks
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="purpose">Optional purpose filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Interruption pattern data</returns>
    public Task<Dictionary<string, object>> GetInterruptionPatternsAsync(Guid userId, int? purpose = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets time blocks with high interruption counts
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="minInterruptions">Minimum interruption count threshold</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>High interruption time blocks</returns>
    public Task<IEnumerable<TimeBlock>> GetHighInterruptionTimeBlocksAsync(Guid userId, int minInterruptions = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old completed time blocks to manage data size
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="olderThan">Delete time blocks older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of time blocks deleted</returns>
    public Task<int> DeleteOldTimeBlocksAsync(Guid userId, DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets time block statistics for analytics
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startDate">Start date for analysis</param>
    /// <param name="endDate">End date for analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time block statistics</returns>
    public Task<Dictionary<string, object>> GetTimeBlockStatisticsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recommended time block durations based on historical performance
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="purpose">The time block purpose</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recommended duration with confidence score</returns>
    public Task<(TimeSpan RecommendedDuration, double Confidence)> GetRecommendedDurationAsync(Guid userId, int purpose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a time block belongs to the specified user
    /// </summary>
    /// <param name="timeBlockId">The time block ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the time block belongs to the user</returns>
    public Task<bool> TimeBlockBelongsToUserAsync(Guid timeBlockId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets performance trends for machine learning analysis
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="days">Number of days to analyze (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Performance trends data</returns>
    public Task<Dictionary<string, object>> GetPerformanceTrendsAsync(Guid userId, int days = 30, CancellationToken cancellationToken = default);
}
