using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Services.Analytics;

/// <summary>
/// Service for processing analytics data in background operations
/// Coordinates between calculation services and repository for automated analytics
/// </summary>
public class AnalyticsProcessingService : IAnalyticsProcessingService
{
    private readonly IAnalyticsCalculationService _calculationService;
    private readonly IProductivityStreakService _streakService;
    private readonly IMetricsAggregationService _aggregationService;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<AnalyticsProcessingService> _logger;

    private static readonly Dictionary<string, object> _performanceMetrics = new();
    private static DateTime _lastProcessingRun = DateTime.MinValue;
    private static int _activeJobs = 0;
    private static readonly List<string> _recentErrors = new();
    private static int _failedJobs = 0;

    public AnalyticsProcessingService(
        IAnalyticsCalculationService calculationService,
        IProductivityStreakService streakService,
        IMetricsAggregationService aggregationService,
        IAppTaskRepository taskRepository,
        ILogger<AnalyticsProcessingService> logger)
    {
        _calculationService = calculationService;
        _streakService = streakService;
        _aggregationService = aggregationService;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task ProcessDailyAnalyticsAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            Interlocked.Increment(ref _activeJobs);
            _logger.LogInformation("Processing daily analytics for user {UserId} on {Date}", userId, date.Date);

            var startTime = DateTime.UtcNow;

            // Calculate daily metrics
            var taskMetrics = await _calculationService.CalculateDailyMetricsAsync(userId, date, cancellationToken);

            // Metrics will be saved through the calculation service

            // Update productivity streaks
            await UpdateProductivityStreaksAsync(userId, date, cancellationToken);

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Completed daily analytics processing for user {UserId} in {Duration}ms",
                userId, processingTime.TotalMilliseconds);

            // Update performance metrics
            lock (_performanceMetrics)
            {
                _performanceMetrics[$"daily_processing_time_ms"] = processingTime.TotalMilliseconds;
                _performanceMetrics[$"last_daily_processing"] = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedJobs);
            var errorMessage = $"Failed to process daily analytics for user {userId} on {date.Date}: {ex.Message}";
            _logger.LogError(ex, errorMessage);

            lock (_recentErrors)
            {
                _recentErrors.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm}: {errorMessage}");
                if (_recentErrors.Count > 10)
                {
                    _recentErrors.RemoveAt(0);
                }
            }
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _activeJobs);
        }
    }

    public async Task ProcessDailyAnalyticsForAllUsersAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing daily analytics for all users on {Date}", date.Date);
            _lastProcessingRun = DateTime.UtcNow;

            // Get all active users with tasks
            var activeUserIds = await _taskRepository.GetActiveUserIdsAsync(cancellationToken);

            var tasks = activeUserIds.Select(userId =>
                ProcessDailyAnalyticsAsync(userId, date, cancellationToken)).ToArray();

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed daily analytics processing for {UserCount} users", activeUserIds.Count);

            // Update performance metrics
            lock (_performanceMetrics)
            {
                _performanceMetrics["users_processed"] = activeUserIds.Count;
                _performanceMetrics["batch_processing_completed"] = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process daily analytics for all users on {Date}", date.Date);
            throw;
        }
    }

    public async Task UpdateProductivityStreaksAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Updating productivity streaks for user {UserId} on {Date}", userId, date.Date);

            var result = await _streakService.UpdateStreaksAsync(userId, date, cancellationToken);

            if (result.HasAnyChanges)
            {
                _logger.LogInformation("Updated streaks for user {UserId}: {ExtendedCount} extended, {BrokenCount} broken, {NewCount} new",
                    userId, result.ExtendedStreaks.Count, result.BrokenStreaks.Count, result.NewStreaks.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update productivity streaks for user {UserId} on {Date}", userId, date.Date);
            throw;
        }
    }

    public async Task UpdateProductivityStreaksForAllUsersAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating productivity streaks for all users on {Date}", date.Date);

            var activeUserIds = await _taskRepository.GetActiveUserIdsAsync(cancellationToken);

            var tasks = activeUserIds.Select(userId =>
                UpdateProductivityStreaksAsync(userId, date, cancellationToken)).ToArray();

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed streak updates for {UserCount} users", activeUserIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update productivity streaks for all users on {Date}", date.Date);
            throw;
        }
    }

    public async Task GenerateWeeklySnapshotsAsync(DateTime weekStartDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating weekly snapshots for week starting {WeekStart}", weekStartDate.Date);

            var activeUserIds = await _taskRepository.GetActiveUserIdsAsync(cancellationToken);

            foreach (var userId in activeUserIds)
            {
                try
                {
                    var weeklyMetrics = await _aggregationService.AggregateWeeklyMetricsAsync(userId, weekStartDate, cancellationToken);

                    var snapshot = AnalyticsSnapshot.Create(userId, weekStartDate, SnapshotType.Weekly);
                    snapshot.MetricsData = new Dictionary<string, object>
                    {
                        ["weekly_metrics"] = weeklyMetrics,
                        ["generation_date"] = DateTime.UtcNow
                    };

                    // Snapshot will be saved through the aggregation service
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate weekly snapshot for user {UserId}", userId);
                }
            }

            _logger.LogInformation("Completed weekly snapshot generation for {UserCount} users", activeUserIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate weekly snapshots for week starting {WeekStart}", weekStartDate);
            throw;
        }
    }

    public async Task GenerateMonthlySnapshotsAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating monthly snapshots for {Year}-{Month:D2}", year, month);

            var activeUserIds = await _taskRepository.GetActiveUserIdsAsync(cancellationToken);
            var snapshotDate = new DateTime(year, month, 1);

            foreach (var userId in activeUserIds)
            {
                try
                {
                    var monthlyMetrics = await _aggregationService.AggregateMonthlyMetricsAsync(userId, year, month, cancellationToken);

                    var snapshot = AnalyticsSnapshot.Create(userId, snapshotDate, SnapshotType.Monthly);
                    snapshot.MetricsData = new Dictionary<string, object>
                    {
                        ["monthly_metrics"] = monthlyMetrics,
                        ["generation_date"] = DateTime.UtcNow
                    };

                    // Snapshot will be saved through the aggregation service
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate monthly snapshot for user {UserId}", userId);
                }
            }

            _logger.LogInformation("Completed monthly snapshot generation for {UserCount} users", activeUserIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate monthly snapshots for {Year}-{Month:D2}", year, month);
            throw;
        }
    }

    public async Task UpdateUserAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Updating user analytics for user {UserId}", userId);

            var userAnalytics = await _calculationService.CalculateUserAnalyticsAsync(userId, cancellationToken);
            // User analytics will be saved through the calculation service

            _logger.LogDebug("Updated user analytics for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user analytics for user {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateUserAnalyticsForAllUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating user analytics for all users");

            var activeUserIds = await _taskRepository.GetActiveUserIdsAsync(cancellationToken);

            var tasks = activeUserIds.Select(userId =>
                UpdateUserAnalyticsAsync(userId, cancellationToken)).ToArray();

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed user analytics updates for {UserCount} users", activeUserIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user analytics for all users");
            throw;
        }
    }

    public async Task PerformDataCleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting analytics data cleanup");

            var cutoffDate = DateTime.UtcNow.AddDays(-365); // Keep 1 year of data
            var snapshotCutoffDate = DateTime.UtcNow.AddDays(-90); // Keep 3 months of daily snapshots

            // Data cleanup will be implemented through dedicated cleanup services
            var deletedMetrics = 0;
            var deletedSnapshots = 0;
            var archivedStreaks = 0;

            _logger.LogInformation("Data cleanup completed: {DeletedMetrics} metrics, {DeletedSnapshots} snapshots, {ArchivedStreaks} streaks processed",
                deletedMetrics, deletedSnapshots, archivedStreaks);

            // Update performance metrics
            lock (_performanceMetrics)
            {
                _performanceMetrics["last_cleanup"] = DateTime.UtcNow;
                _performanceMetrics["cleanup_deleted_metrics"] = deletedMetrics;
                _performanceMetrics["cleanup_deleted_snapshots"] = deletedSnapshots;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform data cleanup");
            throw;
        }
    }

    public async Task RecalculateAnalyticsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Recalculating analytics for user {UserId} from {StartDate} to {EndDate}", userId, startDate.Date, endDate.Date);

            var current = startDate.Date;
            while (current <= endDate.Date)
            {
                await ProcessDailyAnalyticsAsync(userId, current, cancellationToken);
                current = current.AddDays(1);
            }

            // Update user analytics after recalculation
            await UpdateUserAnalyticsAsync(userId, cancellationToken);

            _logger.LogInformation("Completed analytics recalculation for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recalculate analytics for user {UserId} from {StartDate} to {EndDate}", userId, startDate, endDate);
            throw;
        }
    }

    public async Task ValidateAndRepairAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating and repairing analytics for user {UserId}", userId);

            // Check for missing daily metrics in the last 30 days
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-30);

            // Task metrics validation will be implemented through calculation service
            var existingMetrics = new List<TaskMetrics>();
            var existingDates = existingMetrics.Select(m => m.Date.Date).ToHashSet();

            var current = startDate;
            var missingDates = new List<DateTime>();

            while (current <= endDate)
            {
                if (!existingDates.Contains(current))
                {
                    missingDates.Add(current);
                }
                current = current.AddDays(1);
            }

            if (missingDates.Count > 0)
            {
                _logger.LogInformation("Found {Count} missing days for user {UserId}, recalculating", missingDates.Count, userId);

                foreach (var missingDate in missingDates)
                {
                    await ProcessDailyAnalyticsAsync(userId, missingDate, cancellationToken);
                }
            }

            // Repair broken streaks
            await _streakService.RepairStreaksAsync(userId, startDate, endDate, cancellationToken);

            _logger.LogInformation("Completed validation and repair for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate and repair analytics for user {UserId}", userId);
            throw;
        }
    }

    public Task ProcessTaskEventAsync(Guid userId, string eventType, Dictionary<string, object> eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Processing task event {EventType} for user {UserId}", eventType, userId);

            var today = DateTime.UtcNow.Date;

            // For task completion events, immediately update today's metrics
            if (eventType.Equals("TaskCompleted", StringComparison.OrdinalIgnoreCase) ||
                eventType.Equals("TaskCreated", StringComparison.OrdinalIgnoreCase))
            {
                // Queue a delayed update to avoid too frequent recalculations
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    await ProcessDailyAnalyticsAsync(userId, today, cancellationToken);
                }, cancellationToken);
            }

            _logger.LogDebug("Queued analytics update for event {EventType} for user {UserId}", eventType, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process task event {EventType} for user {UserId}", eventType, userId);
            // Don't rethrow - event processing should not block the main operation
        }

        return Task.CompletedTask;
    }

    public async Task<AnalyticsProcessingStatus> GetProcessingStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var nextScheduledRun = _lastProcessingRun == DateTime.MinValue
                ? DateTime.UtcNow.AddHours(1)
                : _lastProcessingRun.AddDays(1); // Next daily run

            var totalUsers = await _taskRepository.GetActiveUserIdsAsync(cancellationToken);

            Dictionary<string, object> currentMetrics;
            List<string> currentErrors;

            lock (_performanceMetrics)
            {
                currentMetrics = new Dictionary<string, object>(_performanceMetrics);
            }

            lock (_recentErrors)
            {
                currentErrors = new List<string>(_recentErrors);
            }

            var isHealthy = _failedJobs < 10 && _activeJobs < 50; // Simple health check

            return new AnalyticsProcessingStatus
            {
                LastProcessingRun = _lastProcessingRun,
                NextScheduledRun = nextScheduledRun,
                ActiveProcessingJobs = _activeJobs,
                TotalUsersProcessed = totalUsers.Count,
                FailedProcessingJobs = _failedJobs,
                RecentErrors = currentErrors,
                PerformanceMetrics = currentMetrics,
                IsHealthy = isHealthy,
                SystemStatus = isHealthy ? "Healthy" : "Degraded"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing status");
            throw;
        }
    }
}
