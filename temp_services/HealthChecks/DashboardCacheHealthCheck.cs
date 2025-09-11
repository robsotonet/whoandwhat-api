using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Infrastructure.HealthChecks;

/// <summary>
/// Health check for dashboard cache performance and availability
/// </summary>
public class DashboardCacheHealthCheck : IHealthCheck
{
    private readonly IDashboardCacheService _dashboardCacheService;
    private readonly ILogger<DashboardCacheHealthCheck> _logger;

    // Performance thresholds for health determination
    private const double MinHitRatioHealthy = 0.70;      // 70% hit ratio for healthy
    private const double MinHitRatioDegraded = 0.50;     // 50% hit ratio for degraded
    private const double MaxResponseTimeHealthy = 100;    // 100ms response time for healthy
    private const double MaxResponseTimeDegraded = 300;   // 300ms response time for degraded

    public DashboardCacheHealthCheck(
        IDashboardCacheService dashboardCacheService,
        ILogger<DashboardCacheHealthCheck> logger)
    {
        _dashboardCacheService = dashboardCacheService ?? throw new ArgumentNullException(nameof(dashboardCacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Starting dashboard cache health check");

            // Get dashboard cache metrics
            var metrics = await _dashboardCacheService.GetDashboardCacheMetricsAsync(cancellationToken);

            // Build health data
            var healthData = new Dictionary<string, object>
            {
                ["TotalRequests"] = metrics.TotalRequests,
                ["CacheHits"] = metrics.CacheHits,
                ["CacheMisses"] = metrics.CacheMisses,
                ["HitRatio"] = metrics.HitRatio,
                ["AverageResponseTime"] = $"{metrics.AverageResponseTime.TotalMilliseconds:F2}ms",
                ["UserAnalyticsRequests"] = metrics.UserAnalyticsRequests,
                ["ProductivityStreakRequests"] = metrics.ProductivityStreakRequests,
                ["AnalyticsSnapshotRequests"] = metrics.AnalyticsSnapshotRequests,
                ["DashboardSummaryRequests"] = metrics.DashboardSummaryRequests,
                ["ProductivityMetricsRequests"] = metrics.ProductivityMetricsRequests,
                ["WarmupOperationsCompleted"] = metrics.WarmupOperationsCompleted,
                ["LastWarmupTime"] = metrics.LastWarmupTime == DateTime.MinValue ? "Never" : metrics.LastWarmupTime.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };

            // Determine health status based on performance metrics
            var healthStatus = DetermineHealthStatus(metrics);
            var statusMessage = GenerateStatusMessage(metrics, healthStatus);

            _logger.LogDebug("Dashboard cache health check completed: {Status}", healthStatus);

            return new HealthCheckResult(healthStatus, statusMessage, data: healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard cache health check failed");
            
            return HealthCheckResult.Unhealthy(
                $"Dashboard cache health check failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["ExceptionType"] = ex.GetType().Name
                });
        }
    }

    private HealthStatus DetermineHealthStatus(DashboardCacheMetrics metrics)
    {
        var hitRatio = metrics.HitRatio;
        var responseTimeMs = metrics.AverageResponseTime.TotalMilliseconds;
        var hasRequests = metrics.TotalRequests > 0;

        // If no requests yet, consider healthy (system just started)
        if (!hasRequests)
        {
            return HealthStatus.Healthy;
        }

        // Critical checks that would make the cache unhealthy
        if (hitRatio < MinHitRatioDegraded && responseTimeMs > MaxResponseTimeDegraded)
        {
            return HealthStatus.Unhealthy;
        }

        // Degraded performance checks
        if (hitRatio < MinHitRatioDegraded || responseTimeMs > MaxResponseTimeDegraded)
        {
            return HealthStatus.Degraded;
        }

        // Good performance but not optimal
        if (hitRatio < MinHitRatioHealthy || responseTimeMs > MaxResponseTimeHealthy)
        {
            return HealthStatus.Degraded;
        }

        // Optimal performance
        return HealthStatus.Healthy;
    }

    private string GenerateStatusMessage(DashboardCacheMetrics metrics, HealthStatus status)
    {
        var hitRatio = metrics.HitRatio;
        var responseTimeMs = metrics.AverageResponseTime.TotalMilliseconds;
        var totalRequests = metrics.TotalRequests;

        var baseMessage = $"Dashboard cache: {totalRequests:N0} requests, {hitRatio:P1} hit ratio, {responseTimeMs:F1}ms avg response";

        return status switch
        {
            HealthStatus.Healthy => $"{baseMessage} - Performing optimally",
            HealthStatus.Degraded => $"{baseMessage} - Performance degraded but functional",
            HealthStatus.Unhealthy => $"{baseMessage} - Critical performance issues detected",
            _ => baseMessage
        };
    }
}

/// <summary>
/// Extension methods for registering dashboard cache health checks
/// </summary>
public static class DashboardCacheHealthCheckExtensions
{
    /// <summary>
    /// Add dashboard cache health check to the health check builder
    /// </summary>
    public static IHealthChecksBuilder AddDashboardCacheHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "dashboard_cache",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck<DashboardCacheHealthCheck>(
            name,
            failureStatus ?? HealthStatus.Degraded,
            tags ?? new[] { "cache", "dashboard", "performance" });
    }
}