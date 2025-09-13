using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Interface for dashboard-specific caching operations
/// Provides high-performance caching for analytics, metrics, and dashboard data
/// </summary>
public interface IDashboardCacheService
{
    /// <summary>
    /// Cache user analytics data
    /// </summary>
    /// <param name="userAnalytics">User analytics to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheUserAnalyticsAsync(UserAnalytics userAnalytics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached user analytics data
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached user analytics or null if not found</returns>
    public Task<UserAnalytics?> GetCachedUserAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache user's current productivity streak
    /// </summary>
    /// <param name="productivityStreak">Productivity streak to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheProductivityStreakAsync(ProductivityStreak productivityStreak, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached user's current productivity streak
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached productivity streak or null if not found</returns>
    public Task<ProductivityStreak?> GetCachedProductivityStreakAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache analytics snapshot
    /// </summary>
    /// <param name="analyticsSnapshot">Analytics snapshot to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheAnalyticsSnapshotAsync(AnalyticsSnapshot analyticsSnapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached analytics snapshot by date and type
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="snapshotDate">Snapshot date</param>
    /// <param name="snapshotType">Snapshot type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached analytics snapshot or null if not found</returns>
    public Task<AnalyticsSnapshot?> GetCachedAnalyticsSnapshotAsync(Guid userId, DateTime snapshotDate, SnapshotType snapshotType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache recent analytics snapshots for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="snapshots">List of snapshots to cache</param>
    /// <param name="snapshotType">Snapshot type for cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheRecentAnalyticsSnapshotsAsync(Guid userId, IEnumerable<AnalyticsSnapshot> snapshots, SnapshotType snapshotType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached recent analytics snapshots for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="snapshotType">Snapshot type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached snapshots or null if not found</returns>
    public Task<IEnumerable<AnalyticsSnapshot>?> GetCachedRecentAnalyticsSnapshotsAsync(Guid userId, SnapshotType snapshotType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache dashboard summary with key metrics
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="dashboardSummary">Dashboard summary data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheDashboardSummaryAsync(Guid userId, DashboardSummary dashboardSummary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached dashboard summary
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached dashboard summary or null if not found</returns>
    public Task<DashboardSummary?> GetCachedDashboardSummaryAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache productivity metrics for a specific period
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="period">Time period for metrics</param>
    /// <param name="metrics">Productivity metrics data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheProductivityMetricsAsync(Guid userId, string period, ProductivityMetrics metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached productivity metrics for a specific period
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="period">Time period for metrics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached productivity metrics or null if not found</returns>
    public Task<ProductivityMetrics?> GetCachedProductivityMetricsAsync(Guid userId, string period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all cached dashboard data for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if invalidation was successful</returns>
    public Task<bool> InvalidateUserDashboardCacheAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate specific analytics data when task changes occur
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="invalidationType">Type of data to invalidate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if invalidation was successful</returns>
    public Task<bool> InvalidateAnalyticsCacheAsync(Guid userId, AnalyticsCacheInvalidationType invalidationType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Warm the cache with frequently accessed dashboard data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items cached during warming</returns>
    public Task<int> WarmDashboardCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Warm specific user's dashboard cache
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items cached during warming</returns>
    public Task<int> WarmUserDashboardCacheAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dashboard cache performance metrics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dashboard cache performance statistics</returns>
    public Task<DashboardCacheMetrics> GetDashboardCacheMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all dashboard cache data
    /// Use with caution - this will clear all cached dashboard data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cache was cleared successfully</returns>
    public Task<bool> ClearAllDashboardCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Precompute and cache dashboard data for multiple users
    /// Used during off-peak hours to improve dashboard load times
    /// </summary>
    /// <param name="userIds">List of user identifiers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of users processed</returns>
    public Task<int> PrecomputeDashboardDataAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data transfer object for dashboard summary information
/// </summary>
public class DashboardSummary
{
    public Guid UserId { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int TodayTasks { get; set; }
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public double CompletionRate { get; set; }
    public double OverallEfficiencyScore { get; set; }
    public string MostProductiveCategory { get; set; } = string.Empty;
    public string ProductivityTrend { get; set; } = string.Empty;
    public UserExperienceLevel ExperienceLevel { get; set; }
    public List<string> PersonalizedInsights { get; set; } = new();
    public int UserScore { get; set; }
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// Data transfer object for productivity metrics over a specific period
/// </summary>
public class ProductivityMetrics
{
    public Guid UserId { get; set; }
    public string Period { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double AverageTasksPerDay { get; set; }
    public double AverageProductiveHoursPerDay { get; set; }
    public double ProductivityConsistency { get; set; }
    public Dictionary<string, int> CategoryCompletionStats { get; set; } = new();
    public Dictionary<string, int> PriorityCompletionStats { get; set; } = new();
    public Dictionary<string, double> DailyProductivityScores { get; set; } = new();
    public int TotalProductiveDays { get; set; }
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// Data transfer object for dashboard cache performance metrics
/// </summary>
public class DashboardCacheMetrics : CachePerformanceMetrics
{
    public long UserAnalyticsRequests { get; set; }
    public long ProductivityStreakRequests { get; set; }
    public long AnalyticsSnapshotRequests { get; set; }
    public long DashboardSummaryRequests { get; set; }
    public long ProductivityMetricsRequests { get; set; }
    public Dictionary<string, long> CacheTypeHitRatios { get; set; } = new();
    public TimeSpan AverageDashboardLoadTime { get; set; }
    public int WarmupOperationsCompleted { get; set; }
    public DateTime LastWarmupTime { get; set; }
}

/// <summary>
/// Enumeration for types of analytics cache invalidation
/// </summary>
public enum AnalyticsCacheInvalidationType
{
    All = 0,
    TaskCompletion = 1,
    StreakUpdate = 2,
    AnalyticsSnapshot = 3,
    ProductivityMetrics = 4,
    DashboardSummary = 5
}
