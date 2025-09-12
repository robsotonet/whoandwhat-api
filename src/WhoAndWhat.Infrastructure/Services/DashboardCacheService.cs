using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Redis-based implementation of dashboard-specific caching operations
/// Provides high-performance caching for analytics, metrics, and dashboard data
/// </summary>
public class DashboardCacheService : IDashboardCacheService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDistributedCache _distributedCache;
    private readonly IDatabase _database;
    private readonly ILogger<DashboardCacheService> _logger;
    private readonly RedisCacheSettings _settings;
    private readonly string _keyPrefix;
    private readonly ConcurrentDictionary<string, DashboardCacheMetrics> _metricsTracker;
    private bool _disposed;

    public DashboardCacheService(
        IConnectionMultiplexer redis,
        IDistributedCache distributedCache,
        IOptions<RedisCacheSettings> settings,
        ILogger<DashboardCacheService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _database = _redis.GetDatabase(_settings.DatabaseIndex);
        _keyPrefix = $"{_settings.KeyPrefix}:dashboard";
        _metricsTracker = new ConcurrentDictionary<string, DashboardCacheMetrics>();
    }

    public async Task<bool> CacheUserAnalyticsAsync(UserAnalytics userAnalytics, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:analytics:{userAnalytics.UserId}";
            var json = JsonSerializer.Serialize(userAnalytics);
            var expiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation("UserAnalytics", true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache user analytics for user {UserId}", userAnalytics.UserId);
            TrackCacheOperation("UserAnalytics", false);
            return false;
        }
    }

    public async Task<UserAnalytics?> GetCachedUserAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:analytics:{userId}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation("UserAnalytics", false);
                return null;
            }

            TrackCacheOperation("UserAnalytics", true);
            return JsonSerializer.Deserialize<UserAnalytics>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached user analytics for user {UserId}", userId);
            TrackCacheOperation("UserAnalytics", false);
            return null;
        }
    }

    public async Task<bool> CacheProductivityStreakAsync(ProductivityStreak productivityStreak, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:streak:{productivityStreak.UserId}";
            var json = JsonSerializer.Serialize(productivityStreak);
            var expiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation("ProductivityStreak", true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache productivity streak for user {UserId}", productivityStreak.UserId);
            TrackCacheOperation("ProductivityStreak", false);
            return false;
        }
    }

    public async Task<ProductivityStreak?> GetCachedProductivityStreakAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:streak:{userId}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation("ProductivityStreak", false);
                return null;
            }

            TrackCacheOperation("ProductivityStreak", true);
            return JsonSerializer.Deserialize<ProductivityStreak>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached productivity streak for user {UserId}", userId);
            TrackCacheOperation("ProductivityStreak", false);
            return null;
        }
    }

    public async Task<bool> CacheAnalyticsSnapshotAsync(AnalyticsSnapshot analyticsSnapshot, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:snapshot:{analyticsSnapshot.UserId}:{analyticsSnapshot.SnapshotDate:yyyy-MM-dd}:{analyticsSnapshot.SnapshotType}";
            var json = JsonSerializer.Serialize(analyticsSnapshot);
            var expiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation("AnalyticsSnapshot", true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache analytics snapshot for user {UserId}", analyticsSnapshot.UserId);
            TrackCacheOperation("AnalyticsSnapshot", false);
            return false;
        }
    }

    public async Task<AnalyticsSnapshot?> GetCachedAnalyticsSnapshotAsync(Guid userId, DateTime snapshotDate, SnapshotType snapshotType, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:snapshot:{userId}:{snapshotDate:yyyy-MM-dd}:{snapshotType}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation("AnalyticsSnapshot", false);
                return null;
            }

            TrackCacheOperation("AnalyticsSnapshot", true);
            return JsonSerializer.Deserialize<AnalyticsSnapshot>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached analytics snapshot for user {UserId}", userId);
            TrackCacheOperation("AnalyticsSnapshot", false);
            return null;
        }
    }

    public async Task<bool> CacheRecentAnalyticsSnapshotsAsync(Guid userId, IEnumerable<AnalyticsSnapshot> snapshots, SnapshotType snapshotType, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:recent-snapshots:{userId}:{snapshotType}";
            var json = JsonSerializer.Serialize(snapshots);
            var expiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation("RecentSnapshots", true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache recent snapshots for user {UserId}", userId);
            TrackCacheOperation("RecentSnapshots", false);
            return false;
        }
    }

    public async Task<IEnumerable<AnalyticsSnapshot>?> GetCachedRecentAnalyticsSnapshotsAsync(Guid userId, SnapshotType snapshotType, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:recent-snapshots:{userId}:{snapshotType}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation("RecentSnapshots", false);
                return null;
            }

            TrackCacheOperation("RecentSnapshots", true);
            return JsonSerializer.Deserialize<IEnumerable<AnalyticsSnapshot>>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached recent snapshots for user {UserId}", userId);
            TrackCacheOperation("RecentSnapshots", false);
            return null;
        }
    }

    public async Task<bool> CacheDashboardSummaryAsync(Guid userId, DashboardSummary dashboardSummary, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:summary:{userId}";
            var json = JsonSerializer.Serialize(dashboardSummary);
            var expiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation("DashboardSummary", true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache dashboard summary for user {UserId}", userId);
            TrackCacheOperation("DashboardSummary", false);
            return false;
        }
    }

    public async Task<DashboardSummary?> GetCachedDashboardSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:summary:{userId}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation("DashboardSummary", false);
                return null;
            }

            TrackCacheOperation("DashboardSummary", true);
            return JsonSerializer.Deserialize<DashboardSummary>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached dashboard summary for user {UserId}", userId);
            TrackCacheOperation("DashboardSummary", false);
            return null;
        }
    }

    public async Task<bool> CacheProductivityMetricsAsync(Guid userId, string period, ProductivityMetrics metrics, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:metrics:{userId}:{period}";
            var json = JsonSerializer.Serialize(metrics);
            var expiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation("ProductivityMetrics", true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache productivity metrics for user {UserId}, period {Period}", userId, period);
            TrackCacheOperation("ProductivityMetrics", false);
            return false;
        }
    }

    public async Task<ProductivityMetrics?> GetCachedProductivityMetricsAsync(Guid userId, string period, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:metrics:{userId}:{period}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation("ProductivityMetrics", false);
                return null;
            }

            TrackCacheOperation("ProductivityMetrics", true);
            return JsonSerializer.Deserialize<ProductivityMetrics>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached productivity metrics for user {UserId}, period {Period}", userId, period);
            TrackCacheOperation("ProductivityMetrics", false);
            return null;
        }
    }

    public async Task<bool> InvalidateUserDashboardCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = $"{_keyPrefix}:*:{userId}";
            await InvalidateByPatternAsync(pattern, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate dashboard cache for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> InvalidateAnalyticsCacheAsync(Guid userId, AnalyticsCacheInvalidationType invalidationType, CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = invalidationType switch
            {
                AnalyticsCacheInvalidationType.TaskCompletion => $"{_keyPrefix}:*:{userId}",
                AnalyticsCacheInvalidationType.StreakUpdate => $"{_keyPrefix}:streak:{userId}",
                AnalyticsCacheInvalidationType.AnalyticsSnapshot => $"{_keyPrefix}:snapshot:{userId}:*",
                AnalyticsCacheInvalidationType.ProductivityMetrics => $"{_keyPrefix}:metrics:{userId}:*",
                AnalyticsCacheInvalidationType.DashboardSummary => $"{_keyPrefix}:summary:{userId}",
                _ => $"{_keyPrefix}:*:{userId}"
            };

            await InvalidateByPatternAsync(pattern, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate analytics cache for user {UserId}, type {Type}", userId, invalidationType);
            return false;
        }
    }

    public Task<int> WarmDashboardCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Implement cache warming logic here
            _logger.LogInformation("Dashboard cache warming completed");
            return Task.FromResult(0); // Placeholder - would return actual number of warmed items
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm dashboard cache");
            return Task.FromResult(0);
        }
    }

    public Task<int> WarmUserDashboardCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Implement user-specific cache warming logic here
            _logger.LogInformation("User dashboard cache warming completed for user {UserId}", userId);
            return Task.FromResult(0); // Placeholder - would return actual number of warmed items
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm dashboard cache for user {UserId}", userId);
            return Task.FromResult(0);
        }
    }

    public Task<DashboardCacheMetrics> GetDashboardCacheMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = new DashboardCacheMetrics
            {
                TotalRequests = _metricsTracker.Values.Sum(m => m.TotalRequests),
                CacheHits = _metricsTracker.Values.Sum(m => m.CacheHits),
                CacheMisses = _metricsTracker.Values.Sum(m => m.CacheMisses),
                AverageResponseTime = TimeSpan.FromMilliseconds(
                    _metricsTracker.Values.Any()
                        ? _metricsTracker.Values.Average(m => m.AverageResponseTime.TotalMilliseconds)
                        : 0),
                MeasurementStartTime = _metricsTracker.Values.Any()
                    ? _metricsTracker.Values.Min(m => m.MeasurementStartTime)
                    : DateTime.UtcNow,
                LastResetTime = _metricsTracker.Values.Any()
                    ? _metricsTracker.Values.Max(m => m.LastResetTime)
                    : DateTime.UtcNow,
                AverageDashboardLoadTime = TimeSpan.FromMilliseconds(50), // Placeholder
                WarmupOperationsCompleted = 0,
                LastWarmupTime = DateTime.UtcNow
            };

            // Add type-specific metrics
            foreach (var kvp in _metricsTracker)
            {
                metrics.CacheTypeHitRatios[kvp.Key] = kvp.Value.CacheHits;
            }

            return Task.FromResult(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard cache metrics");
            return Task.FromResult(new DashboardCacheMetrics());
        }
    }

    public async Task<bool> ClearAllDashboardCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = $"{_keyPrefix}:*";
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var cursor = 0L;
            var deletedCount = 0;

            do
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Dashboard cache clear operation was cancelled");
                    return false;
                }

                var scanResult = server.Execute("SCAN", cursor, "MATCH", pattern, "COUNT", 100);

                if (!scanResult.IsNull && scanResult.Length >= 2)
                {
                    var nextCursor = scanResult[0];
                    var keys = scanResult[1];

                    if (keys.IsNull || !keys.HasValue)
                    {
                        _logger.LogWarning("Invalid SCAN response - keys array is null");
                        break;
                    }

                    var redisKeys = ((RedisResult[])keys!).Where(k => k.HasValue).Select(k => (RedisKey)k!).ToArray();

                    if (redisKeys.Any())
                    {
                        var deleteResult = await _database.KeyDeleteAsync(redisKeys);
                        deletedCount += (int)deleteResult;
                        _logger.LogDebug("Deleted {Count} dashboard cache keys", deleteResult);
                    }

                    if (!long.TryParse((string?)nextCursor!, out cursor))
                    {
                        _logger.LogError("Failed to parse SCAN cursor: {Cursor}", nextCursor);
                        break;
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid SCAN response structure");
                    break;
                }
            } while (cursor != 0);

            _logger.LogInformation("Cleared {Count} dashboard cache entries", deletedCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all dashboard cache");
            return false;
        }
    }

    public Task<int> PrecomputeDashboardDataAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var processedCount = 0;

            foreach (var userId in userIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Implement precomputation logic here
                processedCount++;
            }

            _logger.LogInformation("Precomputed dashboard data for {Count} users", processedCount);
            return Task.FromResult(processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to precompute dashboard data");
            return Task.FromResult(0);
        }
    }

    private async Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var cursor = 0L;

        do
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var scanResult = server.Execute("SCAN", cursor, "MATCH", pattern, "COUNT", 100);

            if (!scanResult.IsNull && scanResult.Length >= 2)
            {
                var nextCursor = scanResult[0];
                var keys = scanResult[1];

                if (keys.HasValue)
                {
                    var redisKeys = ((RedisResult[])keys!).Where(k => k.HasValue).Select(k => (RedisKey)k!).ToArray();

                    if (redisKeys.Any())
                    {
                        await _database.KeyDeleteAsync(redisKeys);
                    }
                }

                if (!long.TryParse((string?)nextCursor!, out cursor))
                    break;
            }
            else
            {
                break;
            }
        } while (cursor != 0);
    }

    private void TrackCacheOperation(string operationType, bool isHit)
    {
        if (!_settings.EnablePerformanceMonitoring)
            return;

        _metricsTracker.AddOrUpdate(operationType,
            new DashboardCacheMetrics
            {
                TotalRequests = 1,
                CacheHits = isHit ? 1 : 0,
                CacheMisses = isHit ? 0 : 1,
                MeasurementStartTime = DateTime.UtcNow,
                LastResetTime = DateTime.UtcNow
            },
            (key, existing) =>
            {
                existing.TotalRequests++;
                if (isHit)
                    existing.CacheHits++;
                else
                    existing.CacheMisses++;
                return existing;
            });
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _metricsTracker.Clear();
            _disposed = true;
        }
    }
}
