using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Service for monitoring and analyzing dashboard cache performance
/// </summary>
public class DashboardPerformanceMonitoringService : IHostedService, IDisposable
{
    private readonly IDashboardCacheService _dashboardCacheService;
    private readonly ITaskCacheService _taskCacheService;
    private readonly RedisCacheSettings _cacheSettings;
    private readonly ILogger<DashboardPerformanceMonitoringService> _logger;
    
    private Timer? _performanceTimer;
    private Timer? _alertTimer;
    
    // Performance tracking
    private readonly ConcurrentQueue<DashboardPerformanceSnapshot> _performanceHistory = new();
    private readonly ConcurrentDictionary<string, PerformanceThreshold> _thresholds = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastAlerts = new();
    
    /// <summary>
    /// Cache the latest performance snapshot for O(1) access instead of O(n) LastOrDefault() enumeration.
    /// Thread-safe with ReaderWriterLockSlim for proper synchronization across multiple timer threads.
    /// </summary>
    private DashboardPerformanceSnapshot? _latestSnapshot;
    private readonly ReaderWriterLockSlim _latestSnapshotLock = new();

    public DashboardPerformanceMonitoringService(
        IDashboardCacheService dashboardCacheService,
        ITaskCacheService taskCacheService,
        IOptions<RedisCacheSettings> cacheSettings,
        ILogger<DashboardPerformanceMonitoringService> logger)
    {
        _dashboardCacheService = dashboardCacheService ?? throw new ArgumentNullException(nameof(dashboardCacheService));
        _taskCacheService = taskCacheService ?? throw new ArgumentNullException(nameof(taskCacheService));
        _cacheSettings = cacheSettings.Value ?? throw new ArgumentNullException(nameof(cacheSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeThresholds();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_cacheSettings.EnablePerformanceMonitoring)
        {
            _logger.LogInformation("Dashboard performance monitoring is disabled");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting dashboard performance monitoring service");

        // Schedule performance data collection every 5 minutes
        _performanceTimer = new Timer(CollectPerformanceMetrics, null, 
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

        // Schedule alert checking every 30 seconds
        _alertTimer = new Timer(CheckPerformanceAlerts, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping dashboard performance monitoring service");

        _performanceTimer?.Change(Timeout.Infinite, 0);
        _alertTimer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    private async void CollectPerformanceMetrics(object? state)
    {
        try
        {
            var dashboardMetrics = await _dashboardCacheService.GetDashboardCacheMetricsAsync();
            var taskMetrics = await _taskCacheService.GetCacheMetricsAsync();

            var snapshot = new DashboardPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                DashboardMetrics = dashboardMetrics,
                TaskMetrics = taskMetrics,
                OverallHitRatio = CalculateOverallHitRatio(dashboardMetrics, taskMetrics),
                AverageResponseTime = CalculateAverageResponseTime(dashboardMetrics, taskMetrics),
                TotalRequests = dashboardMetrics.TotalRequests + taskMetrics.TotalRequests,
                ErrorRate = 0.0 // Would be calculated from error tracking if available
            };

            _performanceHistory.Enqueue(snapshot);
            
            // Update latest snapshot for efficient access (O(1) vs O(n) enumeration)
            // Use write lock for thread-safe update
            _latestSnapshotLock.EnterWriteLock();
            try
            {
                _latestSnapshot = snapshot;
            }
            finally
            {
                _latestSnapshotLock.ExitWriteLock();
            }

            // Keep only last 288 snapshots (24 hours at 5-minute intervals)
            while (_performanceHistory.Count > 288)
            {
                _performanceHistory.TryDequeue(out _);
            }

            _logger.LogDebug("Collected dashboard performance metrics: Hit Ratio: {HitRatio:P2}, Avg Response: {AvgResponse}ms, Total Requests: {TotalRequests}",
                snapshot.OverallHitRatio,
                snapshot.AverageResponseTime.TotalMilliseconds,
                snapshot.TotalRequests);

            // Log performance summary every hour
            if (DateTime.UtcNow.Minute == 0)
            {
                LogPerformanceSummary();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting dashboard performance metrics");
        }
    }

    private async void CheckPerformanceAlerts(object? state)
    {
        try
        {
            if (_performanceHistory.IsEmpty)
            {
                return;
            }

            // Use cached latest snapshot for O(1) access instead of O(n) enumeration
            // Use read lock for thread-safe access
            DashboardPerformanceSnapshot? latestSnapshot;
            _latestSnapshotLock.EnterReadLock();
            try
            {
                latestSnapshot = _latestSnapshot;
            }
            finally
            {
                _latestSnapshotLock.ExitReadLock();
            }
            if (latestSnapshot == null)
            {
                return;
            }

            await CheckThresholdViolations(latestSnapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking performance alerts");
        }
    }

    private async Task CheckThresholdViolations(DashboardPerformanceSnapshot snapshot)
    {
        // Check hit ratio threshold
        var hitRatioThreshold = _thresholds["HitRatio"];
        if (snapshot.OverallHitRatio < hitRatioThreshold.MinValue)
        {
            await TriggerAlert("HitRatio", 
                $"Dashboard cache hit ratio ({snapshot.OverallHitRatio:P2}) is below threshold ({hitRatioThreshold.MinValue:P2})",
                AlertSeverity.Warning);
        }

        // Check response time threshold
        var responseTimeThreshold = _thresholds["ResponseTime"];
        if (snapshot.AverageResponseTime.TotalMilliseconds > responseTimeThreshold.MaxValue)
        {
            await TriggerAlert("ResponseTime",
                $"Dashboard cache response time ({snapshot.AverageResponseTime.TotalMilliseconds:F2}ms) is above threshold ({responseTimeThreshold.MaxValue}ms)",
                AlertSeverity.Warning);
        }

        // Check error rate threshold (if implemented)
        var errorRateThreshold = _thresholds["ErrorRate"];
        if (snapshot.ErrorRate > errorRateThreshold.MaxValue)
        {
            await TriggerAlert("ErrorRate",
                $"Dashboard cache error rate ({snapshot.ErrorRate:P2}) is above threshold ({errorRateThreshold.MaxValue:P2})",
                AlertSeverity.Critical);
        }

        // Check for trend-based alerts
        await CheckTrendAlerts(snapshot);
    }

    private async Task CheckTrendAlerts(DashboardPerformanceSnapshot currentSnapshot)
    {
        var recentSnapshots = _performanceHistory
            .Where(s => s.Timestamp > DateTime.UtcNow.AddMinutes(-30))
            .OrderByDescending(s => s.Timestamp)
            .Take(6)
            .ToList();

        if (recentSnapshots.Count < 3)
        {
            return; // Not enough data for trend analysis
        }

        // Check for declining hit ratio trend
        var hitRatioTrend = CalculateTrend(recentSnapshots.Select(s => s.OverallHitRatio).ToList());
        if (hitRatioTrend < -0.05) // 5% decline
        {
            await TriggerAlert("HitRatioTrend",
                $"Dashboard cache hit ratio is trending downward ({hitRatioTrend:P2} over 30 minutes)",
                AlertSeverity.Info);
        }

        // Check for increasing response time trend
        var responseTimeTrend = CalculateTrend(recentSnapshots.Select(s => s.AverageResponseTime.TotalMilliseconds).ToList());
        if (responseTimeTrend > 10) // 10ms increase
        {
            await TriggerAlert("ResponseTimeTrend",
                $"Dashboard cache response time is trending upward (+{responseTimeTrend:F2}ms over 30 minutes)",
                AlertSeverity.Info);
        }
    }

    private async Task TriggerAlert(string alertType, string message, AlertSeverity severity)
    {
        var alertKey = $"{alertType}_{severity}";
        var now = DateTime.UtcNow;

        // Throttle alerts - don't send the same alert more than once every 15 minutes
        // Use atomic AddOrUpdate to prevent race conditions between check and update
        var wasThrottled = false;
        _lastAlerts.AddOrUpdate(alertKey, 
            // Key doesn't exist - add it and allow alert
            now, 
            // Key exists - check if enough time has passed, update if not throttled
            (key, lastAlert) => 
            {
                var timeSinceLastAlert = now.Subtract(lastAlert).TotalMinutes;
                if (timeSinceLastAlert < 15)
                {
                    wasThrottled = true;
                    return lastAlert; // Keep existing timestamp (throttled)
                }
                return now; // Update timestamp (not throttled)
            });
        
        if (wasThrottled)
        {
            return;
        }

        switch (severity)
        {
            case AlertSeverity.Critical:
                _logger.LogError("CRITICAL: Dashboard Performance Alert - {AlertType}: {Message}", alertType, message);
                break;
            case AlertSeverity.Warning:
                _logger.LogWarning("WARNING: Dashboard Performance Alert - {AlertType}: {Message}", alertType, message);
                break;
            case AlertSeverity.Info:
                _logger.LogInformation("INFO: Dashboard Performance Alert - {AlertType}: {Message}", alertType, message);
                break;
        }

        // Here you could integrate with external alerting systems like:
        // - Azure Application Insights alerts
        // - Email notifications
        // - Slack/Teams webhooks
        // - PagerDuty integration
        await Task.CompletedTask;
    }

    private void LogPerformanceSummary()
    {
        if (_performanceHistory.IsEmpty)
        {
            return;
        }

        var last24Hours = _performanceHistory
            .Where(s => s.Timestamp > DateTime.UtcNow.AddHours(-24))
            .ToList();

        if (!last24Hours.Any())
        {
            return;
        }

        var avgHitRatio = last24Hours.Average(s => s.OverallHitRatio);
        var avgResponseTime = TimeSpan.FromTicks((long)last24Hours.Average(s => s.AverageResponseTime.Ticks));
        var totalRequests = last24Hours.Sum(s => s.TotalRequests);
        var avgErrorRate = last24Hours.Average(s => s.ErrorRate);

        _logger.LogInformation(
            "Dashboard Performance Summary (24h): Hit Ratio: {HitRatio:P2}, Avg Response: {AvgResponse}ms, Total Requests: {TotalRequests:N0}, Error Rate: {ErrorRate:P2}",
            avgHitRatio, avgResponseTime.TotalMilliseconds, totalRequests, avgErrorRate);

        // Check for performance degradation over time
        var firstHalf = last24Hours.Take(last24Hours.Count / 2);
        var secondHalf = last24Hours.Skip(last24Hours.Count / 2);

        if (firstHalf.Any() && secondHalf.Any())
        {
            var hitRatioDelta = secondHalf.Average(s => s.OverallHitRatio) - firstHalf.Average(s => s.OverallHitRatio);
            var responseTimeDelta = secondHalf.Average(s => s.AverageResponseTime.TotalMilliseconds) - 
                                   firstHalf.Average(s => s.AverageResponseTime.TotalMilliseconds);

            if (Math.Abs(hitRatioDelta) > 0.05 || Math.Abs(responseTimeDelta) > 20)
            {
                _logger.LogWarning(
                    "Dashboard Performance Trend (12h comparison): Hit Ratio: {HitRatioDelta:+0.00%;-0.00%}, Response Time: {ResponseTimeDelta:+0.00;-0.00}ms",
                    hitRatioDelta, responseTimeDelta);
            }
        }
    }

    private void InitializeThresholds()
    {
        _thresholds["HitRatio"] = new PerformanceThreshold
        {
            MinValue = 0.80, // 80% hit ratio minimum
            MaxValue = 1.0
        };

        _thresholds["ResponseTime"] = new PerformanceThreshold
        {
            MinValue = 0,
            MaxValue = 200 // 200ms maximum response time
        };

        _thresholds["ErrorRate"] = new PerformanceThreshold
        {
            MinValue = 0,
            MaxValue = 0.01 // 1% error rate maximum
        };
    }

    private static double CalculateOverallHitRatio(DashboardCacheMetrics dashboardMetrics, CachePerformanceMetrics taskMetrics)
    {
        var totalRequests = dashboardMetrics.TotalRequests + taskMetrics.TotalRequests;
        var totalHits = dashboardMetrics.CacheHits + taskMetrics.CacheHits;
        
        return totalRequests > 0 ? (double)totalHits / totalRequests : 0.0;
    }

    private static TimeSpan CalculateAverageResponseTime(DashboardCacheMetrics dashboardMetrics, CachePerformanceMetrics taskMetrics)
    {
        // Weighted average based on request count
        var dashboardRequests = dashboardMetrics.TotalRequests;
        var taskRequests = taskMetrics.TotalRequests;
        var totalRequests = dashboardRequests + taskRequests;

        if (totalRequests == 0)
        {
            return TimeSpan.Zero;
        }

        var weightedTicks = (dashboardMetrics.AverageResponseTime.Ticks * dashboardRequests +
                            taskMetrics.AverageResponseTime.Ticks * taskRequests) / totalRequests;

        return new TimeSpan(weightedTicks);
    }

    private static double CalculateTrend(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        // Simple linear regression to calculate trend
        var n = values.Count;
        var sumX = n * (n + 1) / 2; // Sum of indices (1, 2, 3, ...)
        var sumY = values.Sum();
        var sumXY = values.Select((y, i) => y * (i + 1)).Sum();
        var sumX2 = n * (n + 1) * (2 * n + 1) / 6; // Sum of squares of indices

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return slope;
    }

    public void Dispose()
    {
        _performanceTimer?.Dispose();
        _alertTimer?.Dispose();
        _latestSnapshotLock?.Dispose();
    }
}

/// <summary>
/// Snapshot of dashboard performance metrics at a specific time
/// </summary>
public class DashboardPerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public DashboardCacheMetrics DashboardMetrics { get; set; } = new();
    public CachePerformanceMetrics TaskMetrics { get; set; } = new();
    public double OverallHitRatio { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public long TotalRequests { get; set; }
    public double ErrorRate { get; set; }
}

/// <summary>
/// Performance threshold configuration
/// </summary>
public class PerformanceThreshold
{
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}