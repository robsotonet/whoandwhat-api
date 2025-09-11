using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Data;

namespace WhoAndWhat.Infrastructure.Repositories.Analytics;

/// <summary>
/// EF Core implementation of analytics repository
/// Optimized for analytical queries and time-series data operations
/// </summary>
public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly AnalyticsDbContext _context;
    private readonly ILogger<AnalyticsRepository> _logger;

    public AnalyticsRepository(AnalyticsDbContext context, ILogger<AnalyticsRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Task Metrics Operations

    public async Task<TaskMetrics?> GetTaskMetricsByDateAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.TaskMetrics
                .FirstOrDefaultAsync(tm => tm.UserId == userId && tm.Date.Date == date.Date, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task metrics for user {UserId} on date {Date}", userId, date);
            throw;
        }
    }

    public async Task<List<TaskMetrics>> GetTaskMetricsRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.TaskMetrics
                .Where(tm => tm.UserId == userId && tm.Date >= startDate.Date && tm.Date <= endDate.Date)
                .OrderBy(tm => tm.Date)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task metrics range for user {UserId} from {StartDate} to {EndDate}", userId, startDate, endDate);
            throw;
        }
    }

    public async Task<TaskMetrics> SaveTaskMetricsAsync(TaskMetrics taskMetrics, CancellationToken cancellationToken = default)
    {
        try
        {
            var existingMetrics = await GetTaskMetricsByDateAsync(taskMetrics.UserId, taskMetrics.Date, cancellationToken);

            if (existingMetrics != null)
            {
                // Update existing metrics
                existingMetrics.UpdateTaskCounts(
                    taskMetrics.TasksCompleted,
                    taskMetrics.TasksOverdue,
                    taskMetrics.TotalTasks,
                    taskMetrics.TasksCreated);

                existingMetrics.ProductiveHours = taskMetrics.ProductiveHours;
                existingMetrics.EfficiencyScore = taskMetrics.EfficiencyScore;
                existingMetrics.CategoryBreakdown = taskMetrics.CategoryBreakdown;
                existingMetrics.PriorityBreakdown = taskMetrics.PriorityBreakdown;

                _context.TaskMetrics.Update(existingMetrics);
                await _context.SaveChangesAsync(cancellationToken);
                return existingMetrics;
            }
            else
            {
                // Create new metrics
                _context.TaskMetrics.Add(taskMetrics);
                await _context.SaveChangesAsync(cancellationToken);
                return taskMetrics;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving task metrics for user {UserId} on date {Date}", taskMetrics.UserId, taskMetrics.Date);
            throw;
        }
    }

    public async Task<List<TaskMetrics>> SaveTaskMetricsAsync(IEnumerable<TaskMetrics> taskMetrics, CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = taskMetrics.ToList();
            var savedMetrics = new List<TaskMetrics>();

            foreach (var metric in metrics)
            {
                var saved = await SaveTaskMetricsAsync(metric, cancellationToken);
                savedMetrics.Add(saved);
            }

            return savedMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch saving task metrics");
            throw;
        }
    }

    public async Task<int> GetTaskMetricsCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.TaskMetrics
                .CountAsync(tm => tm.UserId == userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task metrics count for user {UserId}", userId);
            throw;
        }
    }

    #endregion

    #region Productivity Streak Operations

    public async Task<List<ProductivityStreak>> GetActiveStreaksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ProductivityStreaks
                .Where(ps => ps.UserId == userId && ps.IsActive)
                .OrderByDescending(ps => ps.CurrentLength)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active streaks for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<ProductivityStreak>> GetAllStreaksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ProductivityStreaks
                .Where(ps => ps.UserId == userId)
                .OrderByDescending(ps => ps.StartDate)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all streaks for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ProductivityStreak?> GetStreakByIdAsync(Guid streakId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ProductivityStreaks
                .FirstOrDefaultAsync(ps => ps.Id == streakId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving streak {StreakId}", streakId);
            throw;
        }
    }

    public async Task<ProductivityStreak> SaveStreakAsync(ProductivityStreak streak, CancellationToken cancellationToken = default)
    {
        try
        {
            var existingStreak = await _context.ProductivityStreaks
                .FirstOrDefaultAsync(ps => ps.Id == streak.Id, cancellationToken);

            if (existingStreak != null)
            {
                _context.Entry(existingStreak).CurrentValues.SetValues(streak);
                _context.ProductivityStreaks.Update(existingStreak);
            }
            else
            {
                _context.ProductivityStreaks.Add(streak);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return existingStreak ?? streak;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving streak {StreakId} for user {UserId}", streak.Id, streak.UserId);
            throw;
        }
    }

    public async Task<List<ProductivityStreak>> SaveStreaksAsync(IEnumerable<ProductivityStreak> streaks, CancellationToken cancellationToken = default)
    {
        try
        {
            var streakList = streaks.ToList();
            var savedStreaks = new List<ProductivityStreak>();

            foreach (var streak in streakList)
            {
                var saved = await SaveStreakAsync(streak, cancellationToken);
                savedStreaks.Add(saved);
            }

            return savedStreaks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch saving streaks");
            throw;
        }
    }

    public async Task<List<ProductivityStreak>> GetStreaksByTypeAsync(Guid userId, StreakType streakType, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ProductivityStreaks
                .Where(ps => ps.UserId == userId && ps.StreakType == streakType)
                .OrderByDescending(ps => ps.CurrentLength)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving streaks by type {StreakType} for user {UserId}", streakType.Name, userId);
            throw;
        }
    }

    public async Task<ProductivityStreak?> GetLongestStreakAsync(Guid userId, StreakType? streakType = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.ProductivityStreaks.Where(ps => ps.UserId == userId);

            if (streakType != null)
            {
                query = query.Where(ps => ps.StreakType == streakType);
            }

            return await query
                .OrderByDescending(ps => ps.CurrentLength)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving longest streak for user {UserId}, type {StreakType}", userId, streakType?.Name ?? "All");
            throw;
        }
    }

    #endregion

    #region User Analytics Operations

    public async Task<UserAnalytics?> GetUserAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.UserAnalytics
                .FirstOrDefaultAsync(ua => ua.UserId == userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user analytics for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserAnalytics> SaveUserAnalyticsAsync(UserAnalytics userAnalytics, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await GetUserAnalyticsAsync(userAnalytics.UserId, cancellationToken);

            if (existing != null)
            {
                _context.Entry(existing).CurrentValues.SetValues(userAnalytics);
                _context.UserAnalytics.Update(existing);
                await _context.SaveChangesAsync(cancellationToken);
                return existing;
            }
            else
            {
                _context.UserAnalytics.Add(userAnalytics);
                await _context.SaveChangesAsync(cancellationToken);
                return userAnalytics;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user analytics for user {UserId}", userAnalytics.UserId);
            throw;
        }
    }

    public async Task<List<UserAnalytics>> GetTopUsersAsync(string metric, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.UserAnalytics.AsQueryable();

            query = metric.ToLower() switch
            {
                "tasks_completed" => query.OrderByDescending(ua => ua.TotalTasksCompleted),
                "completion_rate" => query.OrderByDescending(ua => ua.AverageCompletionRate),
                "current_streak" => query.OrderByDescending(ua => ua.CurrentStreak),
                "longest_streak" => query.OrderByDescending(ua => ua.LongestStreak),
                "productive_hours" => query.OrderByDescending(ua => ua.TotalProductiveHours),
                _ => query.OrderByDescending(ua => ua.TotalTasksCompleted)
            };

            return await query.Take(limit).ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving top users by metric {Metric}", metric);
            throw;
        }
    }

    #endregion

    #region Analytics Snapshot Operations

    public async Task<AnalyticsSnapshot?> GetSnapshotAsync(Guid userId, DateTime snapshotDate, SnapshotType snapshotType, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.AnalyticsSnapshots
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SnapshotDate.Date == snapshotDate.Date && s.SnapshotType == snapshotType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving snapshot for user {UserId} on {Date} type {Type}", userId, snapshotDate, snapshotType);
            throw;
        }
    }

    public async Task<List<AnalyticsSnapshot>> GetSnapshotsAsync(Guid userId, SnapshotType snapshotType, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.AnalyticsSnapshots
                .Where(s => s.UserId == userId && s.SnapshotType == snapshotType);

            if (startDate.HasValue)
            {
                query = query.Where(s => s.SnapshotDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(s => s.SnapshotDate <= endDate.Value.Date);
            }

            return await query.OrderBy(s => s.SnapshotDate).ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving snapshots for user {UserId} type {Type}", userId, snapshotType);
            throw;
        }
    }

    public async Task<AnalyticsSnapshot> SaveSnapshotAsync(AnalyticsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await GetSnapshotAsync(snapshot.UserId, snapshot.SnapshotDate, snapshot.SnapshotType, cancellationToken);

            if (existing != null)
            {
                _context.Entry(existing).CurrentValues.SetValues(snapshot);
                _context.AnalyticsSnapshots.Update(existing);
                await _context.SaveChangesAsync(cancellationToken);
                return existing;
            }
            else
            {
                _context.AnalyticsSnapshots.Add(snapshot);
                await _context.SaveChangesAsync(cancellationToken);
                return snapshot;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving snapshot for user {UserId} on {Date}", snapshot.UserId, snapshot.SnapshotDate);
            throw;
        }
    }

    public async Task<List<AnalyticsSnapshot>> SaveSnapshotsAsync(IEnumerable<AnalyticsSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshotList = snapshots.ToList();
            var savedSnapshots = new List<AnalyticsSnapshot>();

            foreach (var snapshot in snapshotList)
            {
                var saved = await SaveSnapshotAsync(snapshot, cancellationToken);
                savedSnapshots.Add(saved);
            }

            return savedSnapshots;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch saving snapshots");
            throw;
        }
    }

    #endregion

    #region Aggregated Queries

    public async Task<Dictionary<DateTime, int>> GetTaskCompletionTrendAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = await GetTaskMetricsRangeAsync(userId, startDate, endDate, cancellationToken);
            return metrics.ToDictionary(m => m.Date, m => m.TasksCompleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task completion trend for user {UserId}", userId);
            throw;
        }
    }

    public async Task<Dictionary<string, int>> GetCategoryDistributionAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = await GetTaskMetricsRangeAsync(userId, startDate, endDate, cancellationToken);
            var distribution = new Dictionary<string, int>();

            foreach (var metric in metrics)
            {
                foreach (var category in metric.CategoryBreakdown)
                {
                    if (distribution.ContainsKey(category.Key))
                    {
                        distribution[category.Key] += category.Value;
                    }
                    else
                    {
                        distribution[category.Key] = category.Value;
                    }
                }
            }

            return distribution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving category distribution for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<DateTime>> GetProductiveDatesAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = await GetTaskMetricsRangeAsync(userId, startDate, endDate, cancellationToken);
            return metrics.Where(m => m.IsProductiveDay()).Select(m => m.Date).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving productive dates for user {UserId}", userId);
            throw;
        }
    }

    public async Task<double> GetAverageCompletionRateAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var completionRates = await _context.TaskMetrics
                .Where(tm => tm.UserId == userId && tm.Date >= startDate.Date && tm.Date <= endDate.Date)
                .Select(tm => tm.GetCompletionRate())
                .ToListAsync(cancellationToken);

            return completionRates.Any() ? completionRates.Average() : 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating average completion rate for user {UserId}", userId);
            throw;
        }
    }

    #endregion

    #region Leaderboard Queries

    public async Task<List<(Guid UserId, int StreakLength, DateTime StartDate, bool IsActive)>> GetStreakLeaderboardAsync(StreakType streakType, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ProductivityStreaks
                .Where(ps => ps.StreakType == streakType && ps.IsActive)
                .OrderByDescending(ps => ps.CurrentLength)
                .Take(limit)
                .Select(ps => new ValueTuple<Guid, int, DateTime, bool>(ps.UserId, ps.CurrentLength, ps.StartDate, ps.IsActive))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving streak leaderboard for type {StreakType}", streakType.Name);
            throw;
        }
    }

    public async Task<List<(Guid UserId, double CompletionRate, int TasksCompleted)>> GetProductivityLeaderboardAsync(DateTime startDate, DateTime endDate, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.TaskMetrics
                .Where(tm => tm.Date >= startDate.Date && tm.Date <= endDate.Date)
                .GroupBy(tm => tm.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    CompletionRate = g.Average(tm => (double)tm.TasksCompleted / (tm.TotalTasks == 0 ? 1 : tm.TotalTasks)),
                    TasksCompleted = g.Sum(tm => tm.TasksCompleted)
                })
                .OrderByDescending(x => x.CompletionRate)
                .Take(limit)
                .Select(x => new ValueTuple<Guid, double, int>(x.UserId, x.CompletionRate, x.TasksCompleted))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving productivity leaderboard");
            throw;
        }
    }

    #endregion

    #region Data Cleanup Operations

    public async Task<int> DeleteOldTaskMetricsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _context.TaskMetrics
                .Where(tm => tm.Date < cutoffDate.Date)
                .CountAsync(cancellationToken);

            await _context.TaskMetrics
                .Where(tm => tm.Date < cutoffDate.Date)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} task metrics records older than {CutoffDate}", count, cutoffDate);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting old task metrics before {CutoffDate}", cutoffDate);
            throw;
        }
    }

    public async Task<int> DeleteOldSnapshotsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _context.AnalyticsSnapshots
                .Where(s => s.SnapshotDate < cutoffDate.Date)
                .CountAsync(cancellationToken);

            await _context.AnalyticsSnapshots
                .Where(s => s.SnapshotDate < cutoffDate.Date)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} snapshot records older than {CutoffDate}", count, cutoffDate);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting old snapshots before {CutoffDate}", cutoffDate);
            throw;
        }
    }

    public async Task<int> ArchiveCompletedStreaksAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _context.ProductivityStreaks
                .Where(ps => !ps.IsActive && ps.EndDate.HasValue && ps.EndDate < cutoffDate.Date)
                .CountAsync(cancellationToken);

            // For now, we'll keep completed streaks. In the future, we might move them to an archive table.
            _logger.LogInformation("Found {Count} completed streaks older than {CutoffDate} that could be archived", count, cutoffDate);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving completed streaks before {CutoffDate}", cutoffDate);
            throw;
        }
    }

    #endregion

    #region Bulk Operations

    public async Task<int> BulkInsertTaskMetricsAsync(IEnumerable<TaskMetrics> taskMetrics, CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = taskMetrics.ToList();
            _context.TaskMetrics.AddRange(metrics);
            var result = await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Bulk inserted {Count} task metrics records", metrics.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk inserting task metrics");
            throw;
        }
    }

    public async Task<int> BulkUpdateUserAnalyticsAsync(IEnumerable<UserAnalytics> userAnalytics, CancellationToken cancellationToken = default)
    {
        try
        {
            var analytics = userAnalytics.ToList();
            _context.UserAnalytics.UpdateRange(analytics);
            var result = await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Bulk updated {Count} user analytics records", analytics.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating user analytics");
            throw;
        }
    }

    #endregion

    #region Performance Analytics

    public async Task<Dictionary<string, object>> GetPerformanceMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = new Dictionary<string, object>();

            // Query performance stats
            var totalRecords = await _context.TaskMetrics
                .Where(tm => tm.Date >= startDate.Date && tm.Date <= endDate.Date)
                .CountAsync(cancellationToken);

            var activeUsers = await _context.TaskMetrics
                .Where(tm => tm.Date >= startDate.Date && tm.Date <= endDate.Date)
                .Select(tm => tm.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            var totalStreaks = await _context.ProductivityStreaks
                .Where(ps => ps.StartDate >= startDate.Date && ps.StartDate <= endDate.Date)
                .CountAsync(cancellationToken);

            metrics["total_task_metrics"] = totalRecords;
            metrics["active_users"] = activeUsers;
            metrics["total_streaks"] = totalStreaks;
            metrics["date_range"] = new { start = startDate, end = endDate };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics");
            throw;
        }
    }

    public Task<List<(DateTime Date, TimeSpan ResponseTime)>> GetResponseTimeMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically be implemented with application performance monitoring
            // For now, return empty list as this requires additional infrastructure
            _logger.LogInformation("Response time metrics requested for {StartDate} to {EndDate}", startDate, endDate);
            return Task.FromResult(new List<(DateTime Date, TimeSpan ResponseTime)>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving response time metrics");
            throw;
        }
    }

    #endregion
}
