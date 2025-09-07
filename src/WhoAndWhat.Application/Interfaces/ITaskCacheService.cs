using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Interface for task-specific caching operations
/// Provides high-performance caching for frequently accessed task data
/// </summary>
public interface ITaskCacheService
{
    /// <summary>
    /// Cache a single task by ID
    /// </summary>
    /// <param name="task">Task to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheTaskAsync(Domain.Entities.Task task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a cached task by ID
    /// </summary>
    /// <param name="taskId">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached task or null if not found</returns>
    public Task<Domain.Entities.Task?> GetCachedTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache a list of tasks for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="tasks">Tasks to cache</param>
    /// <param name="filterKey">Optional filter identifier for cached lists</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheUserTasksAsync(Guid userId, IEnumerable<Domain.Entities.Task> tasks, string? filterKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached task list for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="filterKey">Optional filter identifier for cached lists</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached task list or null if not found</returns>
    public Task<IEnumerable<Domain.Entities.Task>?> GetCachedUserTasksAsync(Guid userId, string? filterKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache user task summary statistics
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="totalTasks">Total number of tasks</param>
    /// <param name="completedTasks">Number of completed tasks</param>
    /// <param name="overdueTasks">Number of overdue tasks</param>
    /// <param name="todayTasks">Number of tasks due today</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheUserTaskSummaryAsync(Guid userId, int totalTasks, int completedTasks, int overdueTasks, int todayTasks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached user task summary statistics
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task summary or null if not cached</returns>
    public Task<UserTaskSummary?> GetCachedUserTaskSummaryAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all cached data for a specific task
    /// </summary>
    /// <param name="taskId">Task identifier</param>
    /// <param name="userId">User identifier (for invalidating user lists)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if invalidation was successful</returns>
    public Task<bool> InvalidateTaskCacheAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all cached task data for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if invalidation was successful</returns>
    public Task<bool> InvalidateUserTaskCacheAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Warm the cache with frequently accessed tasks
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items cached during warming</returns>
    public Task<int> WarmCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache performance metrics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache performance statistics</returns>
    public Task<CachePerformanceMetrics> GetCacheMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all task-related cache data
    /// Use with caution - this will clear all cached tasks
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cache was cleared successfully</returns>
    public Task<bool> ClearAllCacheAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Data transfer object for user task summary statistics
/// </summary>
public class UserTaskSummary
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int TodayTasks { get; set; }
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// Data transfer object for cache performance metrics
/// </summary>
public class CachePerformanceMetrics
{
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public double HitRatio => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
    public TimeSpan AverageResponseTime { get; set; }
    public DateTime MeasurementStartTime { get; set; }
    public DateTime LastResetTime { get; set; }
}