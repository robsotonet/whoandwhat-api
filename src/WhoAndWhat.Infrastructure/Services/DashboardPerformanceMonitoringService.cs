using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Background service for monitoring and optimizing dashboard cache performance
/// Provides real-time performance metrics collection and cache optimization strategies
/// </summary>
public class DashboardPerformanceMonitoringService : BackgroundService, IDisposable
{
    private readonly IDashboardCacheService _dashboardCacheService;
    private readonly ITaskCacheService _taskCacheService;
    private readonly RedisCacheSettings _cacheSettings;
    private readonly ILogger<DashboardPerformanceMonitoringService> _logger;

    private readonly Timer? _metricsCollectionTimer;
    private readonly Timer? _cacheOptimizationTimer;
    private readonly Timer? _performanceSnapshotTimer;

    private readonly ConcurrentQueue<PerformanceSnapshot> _performanceHistory;
    private volatile PerformanceSnapshot? _latestSnapshot;

    private readonly object _disposeLock = new();
    private bool _disposed;

    private readonly ConcurrentDictionary<string, long> _operationCounts;
    private readonly ConcurrentDictionary<string, TimeSpan> _operationDurations;
    private readonly Stopwatch _serviceUptime;

    public DashboardPerformanceMonitoringService(
        IDashboardCacheService dashboardCacheService,
        ITaskCacheService taskCacheService,
        IOptions<RedisCacheSettings> cacheSettings,
        ILogger<DashboardPerformanceMonitoringService> logger)
    {
        _dashboardCacheService = dashboardCacheService ?? throw new ArgumentNullException(nameof(dashboardCacheService));
        _taskCacheService = taskCacheService ?? throw new ArgumentNullException(nameof(taskCacheService));
        _cacheSettings = cacheSettings?.Value ?? throw new ArgumentNullException(nameof(cacheSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _performanceHistory = new ConcurrentQueue<PerformanceSnapshot>();
        _operationCounts = new ConcurrentDictionary<string, long>();
        _operationDurations = new ConcurrentDictionary<string, TimeSpan>();
        _serviceUptime = Stopwatch.StartNew();

        if (_cacheSettings.EnablePerformanceMonitoring)
        {
            // Initialize performance monitoring timers
            _metricsCollectionTimer = new Timer(CollectMetrics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
            _cacheOptimizationTimer = new Timer(OptimizeCache, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30));
            _performanceSnapshotTimer = new Timer(CreatePerformanceSnapshot, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cacheSettings.EnablePerformanceMonitoring)
        {
            _logger.LogInformation("Dashboard performance monitoring is disabled");
            return;
        }

        _logger.LogInformation("Dashboard performance monitoring service started");

        try
        {
            // Warm up cache services
            await WarmupCacheServices(stoppingToken);

            // Main monitoring loop
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                // Periodic health checks can be implemented here
                await PerformHealthCheck(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dashboard performance monitoring service stopped gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard performance monitoring service encountered an error");
            throw;
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_cacheSettings.EnablePerformanceMonitoring)
        {
            _logger.LogInformation("Performance monitoring is disabled in configuration");
            return;
        }

        _logger.LogInformation("Starting Dashboard Performance Monitoring Service");
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Dashboard Performance Monitoring Service");

        // Stop timers
        _metricsCollectionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _cacheOptimizationTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _performanceSnapshotTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        await base.StopAsync(cancellationToken);

        // Log final metrics
        LogFinalMetrics();
    }

    private async Task WarmupCacheServices(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Warming up cache services");

            var stopwatch = Stopwatch.StartNew();

            // Warm dashboard cache
            var dashboardWarmupTask = _dashboardCacheService.WarmDashboardCacheAsync(cancellationToken);

            // Warm task cache
            var taskWarmupTask = _taskCacheService.WarmCacheAsync(cancellationToken);

            var results = await Task.WhenAll(dashboardWarmupTask, taskWarmupTask);

            stopwatch.Stop();

            _logger.LogInformation("Cache warmup completed in {Duration}ms. Dashboard: {DashboardCount} items, Tasks: {TaskCount} items",
                stopwatch.ElapsedMilliseconds, results[0], results[1]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache warmup failed, continuing with cold cache");
        }
    }

    private async Task PerformHealthCheck(CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Check dashboard cache health
            var dashboardMetrics = await _dashboardCacheService.GetDashboardCacheMetricsAsync(cancellationToken);

            // Check task cache health
            var taskMetrics = await _taskCacheService.GetCacheMetricsAsync(cancellationToken);

            stopwatch.Stop();

            // Log health status
            var overallHitRatio = CalculateOverallHitRatio(dashboardMetrics.HitRatio, taskMetrics.HitRatio);

            if (overallHitRatio < 0.7) // Less than 70% hit ratio
            {
                _logger.LogWarning("Cache performance degraded: Overall hit ratio {HitRatio:P2}", overallHitRatio);
            }
            else
            {
                _logger.LogDebug("Cache health check completed in {Duration}ms. Hit ratio: {HitRatio:P2}",
                    stopwatch.ElapsedMilliseconds, overallHitRatio);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
        }
    }

    private void CollectMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Collecting performance metrics");

            // This would collect and aggregate metrics from cache services
            // For now, we track the operation counts and durations

            var totalOperations = _operationCounts.Values.Sum();
            var averageDuration = _operationDurations.Values.Any()
                ? TimeSpan.FromMilliseconds(_operationDurations.Values.Average(t => t.TotalMilliseconds))
                : TimeSpan.Zero;

            _logger.LogDebug("Metrics collected: {TotalOps} operations, {AvgDuration}ms average duration",
                totalOperations, averageDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect metrics");
        }
    }

    private async void OptimizeCache(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Performing cache optimization");

            // Get current metrics
            var dashboardMetrics = await _dashboardCacheService.GetDashboardCacheMetricsAsync();
            var taskMetrics = await _taskCacheService.GetCacheMetricsAsync();

            // Perform optimization based on metrics
            if (dashboardMetrics.HitRatio < 0.6) // Less than 60% hit ratio
            {
                _logger.LogInformation("Dashboard cache hit ratio low ({HitRatio:P2}), triggering warmup", dashboardMetrics.HitRatio);
                await _dashboardCacheService.WarmDashboardCacheAsync();
            }

            if (taskMetrics.HitRatio < 0.6)
            {
                _logger.LogInformation("Task cache hit ratio low ({HitRatio:P2}), triggering warmup", taskMetrics.HitRatio);
                await _taskCacheService.WarmCacheAsync();
            }

            _logger.LogDebug("Cache optimization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache optimization failed");
        }
    }

    private void CreatePerformanceSnapshot(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var snapshot = new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                ServiceUptime = _serviceUptime.Elapsed,
                TotalOperations = _operationCounts.Values.Sum(),
                AverageOperationDuration = _operationDurations.Values.Any()
                    ? TimeSpan.FromMilliseconds(_operationDurations.Values.Average(t => t.TotalMilliseconds))
                    : TimeSpan.Zero,
                MemoryUsage = GC.GetTotalMemory(false)
            };

            // Store latest snapshot (O(1) access optimization)
            _latestSnapshot = snapshot;

            // Add to history queue
            _performanceHistory.Enqueue(snapshot);

            // Keep only last 100 snapshots to prevent memory bloat
            while (_performanceHistory.Count > 100)
            {
                _performanceHistory.TryDequeue(out _);
            }

            _logger.LogDebug("Performance snapshot created: {TotalOps} operations, {Memory} bytes memory",
                snapshot.TotalOperations, snapshot.MemoryUsage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create performance snapshot");
        }
    }

    private double CalculateOverallHitRatio(double dashboardHitRatio, double taskHitRatio)
    {
        // Weighted average (dashboard cache typically has more varied access patterns)
        return (dashboardHitRatio * 0.6) + (taskHitRatio * 0.4);
    }

    private void LogFinalMetrics()
    {
        try
        {
            var totalOperations = _operationCounts.Values.Sum();
            var uptime = _serviceUptime.Elapsed;

            _logger.LogInformation("Dashboard Performance Monitoring Service final metrics:");
            _logger.LogInformation("  - Total uptime: {Uptime}", uptime);
            _logger.LogInformation("  - Total operations monitored: {TotalOps}", totalOperations);
            _logger.LogInformation("  - Performance snapshots collected: {SnapshotCount}", _performanceHistory.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log final metrics");
        }
    }

    public void TrackOperation(string operationType, TimeSpan duration)
    {
        if (_disposed)
        {
            return;
        }

        _operationCounts.AddOrUpdate(operationType, 1, (_, count) => count + 1);
        _operationDurations.AddOrUpdate(operationType, duration, (_, existing) =>
            TimeSpan.FromMilliseconds((existing.TotalMilliseconds + duration.TotalMilliseconds) / 2));
    }

    public new void Dispose()
    {
        if (!_disposed)
        {
            lock (_disposeLock)
            {
                if (!_disposed)
                {
                    _metricsCollectionTimer?.Dispose();
                    _cacheOptimizationTimer?.Dispose();
                    _performanceSnapshotTimer?.Dispose();
                    _serviceUptime?.Stop();

                    _disposed = true;
                }
            }
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Performance snapshot data structure for tracking service metrics over time
/// </summary>
internal class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public TimeSpan ServiceUptime { get; set; }
    public long TotalOperations { get; set; }
    public TimeSpan AverageOperationDuration { get; set; }
    public long MemoryUsage { get; set; }
}
