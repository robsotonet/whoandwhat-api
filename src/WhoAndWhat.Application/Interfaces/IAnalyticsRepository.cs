using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Repository interface for analytics-related operations
/// </summary>
public interface IAnalyticsRepository
{
    /// <summary>
    /// Gets user analytics for a specific user and period
    /// </summary>
    public Task<UserAnalytics?> GetUserAnalyticsAsync(Guid userId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves user analytics data
    /// </summary>
    public Task SaveUserAnalyticsAsync(UserAnalytics userAnalytics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets analytics snapshot by user and type
    /// </summary>
    public Task<IEnumerable<AnalyticsSnapshot>> GetAnalyticsSnapshotsAsync(Guid userId, string snapshotType, DateTime? fromDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves analytics snapshot
    /// </summary>
    public Task SaveAnalyticsSnapshotAsync(AnalyticsSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets task metrics for a specific task
    /// </summary>
    public Task<IEnumerable<TaskMetrics>> GetTaskMetricsAsync(Guid taskId, string? metricType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves task metrics
    /// </summary>
    public Task SaveTaskMetricsAsync(TaskMetrics taskMetrics, CancellationToken cancellationToken = default);
}
