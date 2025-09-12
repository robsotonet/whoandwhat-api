using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Services.Analytics;

/// <summary>
/// Service for processing analytics data in background tasks
/// Handles automated calculation and aggregation of analytics metrics
/// </summary>
public interface IAnalyticsProcessingService
{
    /// <summary>
    /// Processes daily analytics for a specific user
    /// </summary>
    public Task ProcessDailyAnalyticsAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes daily analytics for all active users
    /// </summary>
    public Task ProcessDailyAnalyticsForAllUsersAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user productivity streaks based on daily activity
    /// </summary>
    public Task UpdateProductivityStreaksAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates productivity streaks for all users
    /// </summary>
    public Task UpdateProductivityStreaksForAllUsersAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates weekly analytics snapshots
    /// </summary>
    public Task GenerateWeeklySnapshotsAsync(DateTime weekStartDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates monthly analytics snapshots
    /// </summary>
    public Task GenerateMonthlySnapshotsAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user analytics aggregates
    /// </summary>
    public Task UpdateUserAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user analytics for all users
    /// </summary>
    public Task UpdateUserAnalyticsForAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs data cleanup and retention management
    /// </summary>
    public Task PerformDataCleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculates analytics for a date range (repair/backfill)
    /// </summary>
    public Task RecalculateAnalyticsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and repairs analytics data integrity
    /// </summary>
    public Task ValidateAndRepairAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes analytics events from task operations
    /// </summary>
    public Task ProcessTaskEventAsync(Guid userId, string eventType, Dictionary<string, object> eventData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processing status and metrics
    /// </summary>
    public Task<AnalyticsProcessingStatus> GetProcessingStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Analytics processing status information
/// </summary>
public record AnalyticsProcessingStatus
{
    public DateTime LastProcessingRun { get; init; }
    public DateTime NextScheduledRun { get; init; }
    public int ActiveProcessingJobs { get; init; }
    public int TotalUsersProcessed { get; init; }
    public int FailedProcessingJobs { get; init; }
    public List<string> RecentErrors { get; init; } = new();
    public Dictionary<string, object> PerformanceMetrics { get; init; } = new();
    public bool IsHealthy { get; init; }
    public string SystemStatus { get; init; } = string.Empty;
}
