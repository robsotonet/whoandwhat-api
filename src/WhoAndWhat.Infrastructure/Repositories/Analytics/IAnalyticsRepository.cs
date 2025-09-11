using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Infrastructure.Repositories.Analytics;

/// <summary>
/// Repository interface for analytics data access
/// </summary>
public interface IAnalyticsRepository
{
    /// <summary>
    /// Task Metrics Operations
    /// </summary>
    public Task<TaskMetrics?> GetTaskMetricsByDateAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);
    public Task<List<TaskMetrics>> GetTaskMetricsRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    public Task<TaskMetrics> SaveTaskMetricsAsync(TaskMetrics taskMetrics, CancellationToken cancellationToken = default);
    public Task<List<TaskMetrics>> SaveTaskMetricsAsync(IEnumerable<TaskMetrics> taskMetrics, CancellationToken cancellationToken = default);
    public Task<int> GetTaskMetricsCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Productivity Streak Operations
    /// </summary>
    public Task<List<ProductivityStreak>> GetActiveStreaksAsync(Guid userId, CancellationToken cancellationToken = default);
    public Task<List<ProductivityStreak>> GetAllStreaksAsync(Guid userId, CancellationToken cancellationToken = default);
    public Task<ProductivityStreak?> GetStreakByIdAsync(Guid streakId, CancellationToken cancellationToken = default);
    public Task<ProductivityStreak> SaveStreakAsync(ProductivityStreak streak, CancellationToken cancellationToken = default);
    public Task<List<ProductivityStreak>> SaveStreaksAsync(IEnumerable<ProductivityStreak> streaks, CancellationToken cancellationToken = default);
    public Task<List<ProductivityStreak>> GetStreaksByTypeAsync(Guid userId, StreakType streakType, CancellationToken cancellationToken = default);
    public Task<ProductivityStreak?> GetLongestStreakAsync(Guid userId, StreakType? streakType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// User Analytics Operations
    /// </summary>
    public Task<UserAnalytics?> GetUserAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default);
    public Task<UserAnalytics> SaveUserAnalyticsAsync(UserAnalytics userAnalytics, CancellationToken cancellationToken = default);
    public Task<List<UserAnalytics>> GetTopUsersAsync(string metric, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analytics Snapshot Operations
    /// </summary>
    public Task<AnalyticsSnapshot?> GetSnapshotAsync(Guid userId, DateTime snapshotDate, SnapshotType snapshotType, CancellationToken cancellationToken = default);
    public Task<List<AnalyticsSnapshot>> GetSnapshotsAsync(Guid userId, SnapshotType snapshotType, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    public Task<AnalyticsSnapshot> SaveSnapshotAsync(AnalyticsSnapshot snapshot, CancellationToken cancellationToken = default);
    public Task<List<AnalyticsSnapshot>> SaveSnapshotsAsync(IEnumerable<AnalyticsSnapshot> snapshots, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregated Queries
    /// </summary>
    public Task<Dictionary<DateTime, int>> GetTaskCompletionTrendAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    public Task<Dictionary<string, int>> GetCategoryDistributionAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    public Task<List<DateTime>> GetProductiveDatesAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    public Task<double> GetAverageCompletionRateAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaderboard Queries
    /// </summary>
    public Task<List<(Guid UserId, int StreakLength, DateTime StartDate, bool IsActive)>> GetStreakLeaderboardAsync(StreakType streakType, int limit = 10, CancellationToken cancellationToken = default);
    public Task<List<(Guid UserId, double CompletionRate, int TasksCompleted)>> GetProductivityLeaderboardAsync(DateTime startDate, DateTime endDate, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Data Cleanup Operations
    /// </summary>
    public Task<int> DeleteOldTaskMetricsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
    public Task<int> DeleteOldSnapshotsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
    public Task<int> ArchiveCompletedStreaksAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk Operations
    /// </summary>
    public Task<int> BulkInsertTaskMetricsAsync(IEnumerable<TaskMetrics> taskMetrics, CancellationToken cancellationToken = default);
    public Task<int> BulkUpdateUserAnalyticsAsync(IEnumerable<UserAnalytics> userAnalytics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performance Analytics
    /// </summary>
    public Task<Dictionary<string, object>> GetPerformanceMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    public Task<List<(DateTime Date, TimeSpan ResponseTime)>> GetResponseTimeMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Snapshot type enumeration
/// </summary>
public enum SnapshotType
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Annual = 5
}
