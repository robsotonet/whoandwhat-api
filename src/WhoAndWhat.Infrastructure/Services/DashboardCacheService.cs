using System.Runtime.CompilerServices;
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
/// Redis-based dashboard caching service with performance monitoring and fallback support
/// </summary>
public class DashboardCacheService : IDashboardCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly IDistributedCache _distributedCache;
    private readonly RedisCacheSettings _cacheSettings;
    private readonly ILogger<DashboardCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _semaphore;

    // Performance tracking
    private long _totalRequests = 0;
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private long _userAnalyticsRequests = 0;
    private long _productivityStreakRequests = 0;
    private long _analyticsSnapshotRequests = 0;
    private long _dashboardSummaryRequests = 0;
    private long _productivityMetricsRequests = 0;
    private readonly Dictionary<string, long> _cacheTypeHitRatios = new();
    private readonly List<long> _responseTimes = new();
    private readonly DateTime _metricsStartTime = DateTime.UtcNow;
    private DateTime _lastResetTime = DateTime.UtcNow;
    private DateTime _lastWarmupTime = DateTime.MinValue;
    private int _warmupOperationsCompleted = 0;

    public DashboardCacheService(
        IConnectionMultiplexer redis,
        IDistributedCache distributedCache,
        IOptions<RedisCacheSettings> cacheSettings,
        ILogger<DashboardCacheService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _cacheSettings = cacheSettings.Value ?? throw new ArgumentNullException(nameof(cacheSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _database = _redis.GetDatabase(_cacheSettings.DatabaseIndex);
        _semaphore = new SemaphoreSlim(100, 100); // Limit concurrent operations

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        _logger.LogInformation("DashboardCacheService initialized with Redis database {DatabaseIndex}", _cacheSettings.DatabaseIndex);
    }

    public async Task<bool> CacheUserAnalyticsAsync(UserAnalytics userAnalytics, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetUserAnalyticsCacheKey(userAnalytics.UserId);
            var serializedData = JsonSerializer.Serialize(userAnalytics, _jsonOptions);
            var expiration = TimeSpan.FromMinutes(_cacheSettings.UserTaskSummaryCacheExpirationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);

            _logger.LogDebug("Cached user analytics for user {UserId} with expiration {Expiration}",
                userAnalytics.UserId, expiration);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache user analytics for user {UserId}", userAnalytics.UserId);
            return false;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<UserAnalytics?> GetCachedUserAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _userAnalyticsRequests);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetUserAnalyticsCacheKey(userId);
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedData != null)
            {
                Interlocked.Increment(ref _cacheHits);
                UpdateCacheTypeMetrics("userAnalytics", true);
                var userAnalytics = JsonSerializer.Deserialize<UserAnalytics>(cachedData, _jsonOptions);
                _logger.LogDebug("Cache hit for user analytics {UserId}", userId);
                return userAnalytics;
            }

            Interlocked.Increment(ref _cacheMisses);
            UpdateCacheTypeMetrics("userAnalytics", false);
            _logger.LogDebug("Cache miss for user analytics {UserId}", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached user analytics for user {UserId}", userId);
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<bool> CacheProductivityStreakAsync(ProductivityStreak productivityStreak, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetProductivityStreakCacheKey(productivityStreak.UserId);
            var serializedData = JsonSerializer.Serialize(productivityStreak, _jsonOptions);
            var expiration = TimeSpan.FromMinutes(_cacheSettings.TaskCacheExpirationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);

            _logger.LogDebug("Cached productivity streak for user {UserId} with expiration {Expiration}",
                productivityStreak.UserId, expiration);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache productivity streak for user {UserId}", productivityStreak.UserId);
            return false;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<ProductivityStreak?> GetCachedProductivityStreakAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _productivityStreakRequests);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetProductivityStreakCacheKey(userId);
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedData != null)
            {
                Interlocked.Increment(ref _cacheHits);
                UpdateCacheTypeMetrics("productivityStreak", true);
                var streak = JsonSerializer.Deserialize<ProductivityStreak>(cachedData, _jsonOptions);
                _logger.LogDebug("Cache hit for productivity streak {UserId}", userId);
                return streak;
            }

            Interlocked.Increment(ref _cacheMisses);
            UpdateCacheTypeMetrics("productivityStreak", false);
            _logger.LogDebug("Cache miss for productivity streak {UserId}", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached productivity streak for user {UserId}", userId);
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<bool> CacheAnalyticsSnapshotAsync(AnalyticsSnapshot analyticsSnapshot, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetAnalyticsSnapshotCacheKey(analyticsSnapshot.UserId, analyticsSnapshot.SnapshotDate, analyticsSnapshot.SnapshotType);
            var serializedData = JsonSerializer.Serialize(analyticsSnapshot, _jsonOptions);
            var expiration = TimeSpan.FromMinutes(_cacheSettings.UserTaskSummaryCacheExpirationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);

            _logger.LogDebug("Cached analytics snapshot for user {UserId}, date {SnapshotDate}, type {SnapshotType}",
                analyticsSnapshot.UserId, analyticsSnapshot.SnapshotDate, analyticsSnapshot.SnapshotType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache analytics snapshot for user {UserId}", analyticsSnapshot.UserId);
            return false;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<AnalyticsSnapshot?> GetCachedAnalyticsSnapshotAsync(Guid userId, DateTime snapshotDate, SnapshotType snapshotType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _analyticsSnapshotRequests);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetAnalyticsSnapshotCacheKey(userId, snapshotDate, snapshotType);
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedData != null)
            {
                Interlocked.Increment(ref _cacheHits);
                UpdateCacheTypeMetrics("analyticsSnapshot", true);
                var snapshot = JsonSerializer.Deserialize<AnalyticsSnapshot>(cachedData, _jsonOptions);
                _logger.LogDebug("Cache hit for analytics snapshot {UserId}, {SnapshotDate}, {SnapshotType}", 
                    userId, snapshotDate, snapshotType);
                return snapshot;
            }

            Interlocked.Increment(ref _cacheMisses);
            UpdateCacheTypeMetrics("analyticsSnapshot", false);
            _logger.LogDebug("Cache miss for analytics snapshot {UserId}, {SnapshotDate}, {SnapshotType}", 
                userId, snapshotDate, snapshotType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached analytics snapshot for user {UserId}", userId);
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<bool> CacheRecentAnalyticsSnapshotsAsync(Guid userId, IEnumerable<AnalyticsSnapshot> snapshots, SnapshotType snapshotType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetRecentAnalyticsSnapshotsCacheKey(userId, snapshotType);
            var snapshotList = snapshots.ToList();
            var serializedData = JsonSerializer.Serialize(snapshotList, _jsonOptions);
            var expiration = TimeSpan.FromMinutes(_cacheSettings.TaskListCacheExpirationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);

            _logger.LogDebug("Cached {SnapshotCount} recent analytics snapshots for user {UserId}, type {SnapshotType}",
                snapshotList.Count, userId, snapshotType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache recent analytics snapshots for user {UserId}", userId);
            return false;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<IEnumerable<AnalyticsSnapshot>?> GetCachedRecentAnalyticsSnapshotsAsync(Guid userId, SnapshotType snapshotType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _analyticsSnapshotRequests);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetRecentAnalyticsSnapshotsCacheKey(userId, snapshotType);
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedData != null)
            {
                Interlocked.Increment(ref _cacheHits);
                UpdateCacheTypeMetrics("recentSnapshots", true);
                var snapshots = JsonSerializer.Deserialize<List<AnalyticsSnapshot>>(cachedData, _jsonOptions);
                _logger.LogDebug("Cache hit for recent analytics snapshots {UserId}, type {SnapshotType}", userId, snapshotType);
                return snapshots;
            }

            Interlocked.Increment(ref _cacheMisses);
            UpdateCacheTypeMetrics("recentSnapshots", false);
            _logger.LogDebug("Cache miss for recent analytics snapshots {UserId}, type {SnapshotType}", userId, snapshotType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached recent analytics snapshots for user {UserId}", userId);
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<bool> CacheDashboardSummaryAsync(Guid userId, DashboardSummary dashboardSummary, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetDashboardSummaryCacheKey(userId);
            var serializedData = JsonSerializer.Serialize(dashboardSummary, _jsonOptions);
            var expiration = TimeSpan.FromMinutes(_cacheSettings.TaskListCacheExpirationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);

            _logger.LogDebug("Cached dashboard summary for user {UserId} with expiration {Expiration}",
                userId, expiration);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache dashboard summary for user {UserId}", userId);
            return false;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<DashboardSummary?> GetCachedDashboardSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _dashboardSummaryRequests);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetDashboardSummaryCacheKey(userId);
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedData != null)
            {
                Interlocked.Increment(ref _cacheHits);
                UpdateCacheTypeMetrics("dashboardSummary", true);
                var summary = JsonSerializer.Deserialize<DashboardSummary>(cachedData, _jsonOptions);
                _logger.LogDebug("Cache hit for dashboard summary {UserId}", userId);
                return summary;
            }

            Interlocked.Increment(ref _cacheMisses);
            UpdateCacheTypeMetrics("dashboardSummary", false);
            _logger.LogDebug("Cache miss for dashboard summary {UserId}", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached dashboard summary for user {UserId}", userId);
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<bool> CacheProductivityMetricsAsync(Guid userId, string period, ProductivityMetrics metrics, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetProductivityMetricsCacheKey(userId, period);
            var serializedData = JsonSerializer.Serialize(metrics, _jsonOptions);
            var expiration = TimeSpan.FromMinutes(_cacheSettings.UserTaskSummaryCacheExpirationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);

            _logger.LogDebug("Cached productivity metrics for user {UserId}, period {Period}",
                userId, period);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache productivity metrics for user {UserId}", userId);
            return false;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<ProductivityMetrics?> GetCachedProductivityMetricsAsync(Guid userId, string period, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _productivityMetricsRequests);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetProductivityMetricsCacheKey(userId, period);
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedData != null)
            {
                Interlocked.Increment(ref _cacheHits);
                UpdateCacheTypeMetrics("productivityMetrics", true);
                var metrics = JsonSerializer.Deserialize<ProductivityMetrics>(cachedData, _jsonOptions);
                _logger.LogDebug("Cache hit for productivity metrics {UserId}, period {Period}", userId, period);
                return metrics;
            }

            Interlocked.Increment(ref _cacheMisses);
            UpdateCacheTypeMetrics("productivityMetrics", false);
            _logger.LogDebug("Cache miss for productivity metrics {UserId}, period {Period}", userId, period);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached productivity metrics for user {UserId}", userId);
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<bool> InvalidateUserDashboardCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = new List<Task>
            {
                // Invalidate all dashboard-related cache entries for the user
                _distributedCache.RemoveAsync(GetUserAnalyticsCacheKey(userId), cancellationToken),
                _distributedCache.RemoveAsync(GetProductivityStreakCacheKey(userId), cancellationToken),
                _distributedCache.RemoveAsync(GetDashboardSummaryCacheKey(userId), cancellationToken),
                InvalidateUserAnalyticsSnapshotsAsync(userId, cancellationToken),
                InvalidateUserProductivityMetricsAsync(userId, cancellationToken)
            };

            await Task.WhenAll(tasks);

            _logger.LogDebug("Invalidated all dashboard cache for user {UserId}", userId);
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
            var tasks = new List<Task>();

            switch (invalidationType)
            {
                case AnalyticsCacheInvalidationType.All:
                    return await InvalidateUserDashboardCacheAsync(userId, cancellationToken);

                case AnalyticsCacheInvalidationType.TaskCompletion:
                    tasks.AddRange(new[]
                    {
                        _distributedCache.RemoveAsync(GetUserAnalyticsCacheKey(userId), cancellationToken),
                        _distributedCache.RemoveAsync(GetDashboardSummaryCacheKey(userId), cancellationToken),
                        InvalidateUserProductivityMetricsAsync(userId, cancellationToken)
                    });
                    break;

                case AnalyticsCacheInvalidationType.StreakUpdate:
                    tasks.AddRange(new[]
                    {
                        _distributedCache.RemoveAsync(GetProductivityStreakCacheKey(userId), cancellationToken),
                        _distributedCache.RemoveAsync(GetDashboardSummaryCacheKey(userId), cancellationToken)
                    });
                    break;

                case AnalyticsCacheInvalidationType.AnalyticsSnapshot:
                    tasks.Add(InvalidateUserAnalyticsSnapshotsAsync(userId, cancellationToken));
                    break;

                case AnalyticsCacheInvalidationType.ProductivityMetrics:
                    tasks.Add(InvalidateUserProductivityMetricsAsync(userId, cancellationToken));
                    break;

                case AnalyticsCacheInvalidationType.DashboardSummary:
                    tasks.Add(_distributedCache.RemoveAsync(GetDashboardSummaryCacheKey(userId), cancellationToken));
                    break;
            }

            await Task.WhenAll(tasks);

            _logger.LogDebug("Invalidated analytics cache for user {UserId}, type {InvalidationType}",
                userId, invalidationType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate analytics cache for user {UserId}, type {InvalidationType}",
                userId, invalidationType);
            return false;
        }
    }

    public async Task<int> WarmDashboardCacheAsync(CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.EnableCacheWarming)
        {
            _logger.LogDebug("Dashboard cache warming is disabled");
            return 0;
        }

        try
        {
            _logger.LogInformation("Starting dashboard cache warming process");

            // This would be implemented to warm cache with frequently accessed dashboard data
            // For now, we'll return 0 as this requires coordination with analytics services
            // This will be implemented when the analytics services are integrated

            await Task.CompletedTask; // Placeholder for actual implementation

            _lastWarmupTime = DateTime.UtcNow;
            _warmupOperationsCompleted++;

            _logger.LogInformation("Dashboard cache warming completed - 0 items warmed (awaiting analytics services integration)");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm dashboard cache");
            return 0;
        }
    }

    public async Task<int> WarmUserDashboardCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.EnableCacheWarming)
        {
            return 0;
        }

        try
        {
            _logger.LogDebug("Starting dashboard cache warming for user {UserId}", userId);

            // This would be implemented to warm specific user's dashboard cache
            // For now, we'll return 0 as this requires coordination with analytics services

            await Task.CompletedTask; // Placeholder for actual implementation

            _logger.LogDebug("Dashboard cache warming completed for user {UserId} - 0 items warmed", userId);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm dashboard cache for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<DashboardCacheMetrics> GetDashboardCacheMetricsAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Async method for future extensibility

        var baseMetrics = GetBaseCacheMetrics();
        var metrics = new DashboardCacheMetrics
        {
            TotalRequests = baseMetrics.TotalRequests,
            CacheHits = baseMetrics.CacheHits,
            CacheMisses = baseMetrics.CacheMisses,
            AverageResponseTime = baseMetrics.AverageResponseTime,
            MeasurementStartTime = baseMetrics.MeasurementStartTime,
            LastResetTime = baseMetrics.LastResetTime,
            UserAnalyticsRequests = Interlocked.Read(ref _userAnalyticsRequests),
            ProductivityStreakRequests = Interlocked.Read(ref _productivityStreakRequests),
            AnalyticsSnapshotRequests = Interlocked.Read(ref _analyticsSnapshotRequests),
            DashboardSummaryRequests = Interlocked.Read(ref _dashboardSummaryRequests),
            ProductivityMetricsRequests = Interlocked.Read(ref _productivityMetricsRequests),
            CacheTypeHitRatios = new Dictionary<string, long>(_cacheTypeHitRatios),
            AverageDashboardLoadTime = CalculateAverageResponseTime(),
            WarmupOperationsCompleted = _warmupOperationsCompleted,
            LastWarmupTime = _lastWarmupTime
        };

        return metrics;
    }

    public async Task<bool> ClearAllDashboardCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"{_cacheSettings.KeyPrefix}:dashboard:*";
            
            var keysToDelete = new List<RedisKey>();
            const int batchSize = 100; // Process keys in batches to avoid memory issues
            var totalDeleted = 0;
            
            // Use SCAN instead of KEYS for better performance in production
            await foreach (var key in ScanDashboardKeysAsync(server, pattern, batchSize, cancellationToken))
            {
                keysToDelete.Add(key);
                
                // Process in batches to avoid large memory usage
                if (keysToDelete.Count >= batchSize)
                {
                    await _database.KeyDeleteAsync(keysToDelete.ToArray());
                    totalDeleted += keysToDelete.Count;
                    keysToDelete.Clear();
                }
                
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
            }
            
            // Delete remaining keys
            if (keysToDelete.Count > 0)
            {
                await _database.KeyDeleteAsync(keysToDelete.ToArray());
                totalDeleted += keysToDelete.Count;
            }
            if (totalDeleted > 0)
            {
                _logger.LogWarning("Cleared {KeyCount} dashboard cache keys using SCAN pattern", totalDeleted);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dashboard cache clear operation was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all dashboard cache");
            return false;
        }
    }

    /// <summary>
    /// Scans dashboard cache keys using Redis SCAN command for better performance than KEYS.
    /// Uses true cursor-based iteration to avoid blocking the Redis server in production environments.
    /// Implements the Redis SCAN protocol: SCAN cursor MATCH pattern COUNT count
    /// </summary>
    private async IAsyncEnumerable<RedisKey> ScanDashboardKeysAsync(IServer server, string pattern, int count = 100, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var cursor = 0L;
        var database = _database.Database;
        
        do
        {
            // Check for cancellation before each SCAN operation
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                // Execute true Redis SCAN command: SCAN [cursor] MATCH [pattern] COUNT [count]
                var result = await Task.Run(() => 
                    server.Execute("SCAN", cursor, "MATCH", pattern, "COUNT", count), 
                    cancellationToken);
                
                // SCAN returns array: [new_cursor, [keys_array]]
                var resultArray = (RedisResult[])result;
                if (resultArray.Length < 2)
                {
                    _logger.LogWarning("Invalid SCAN response format from Redis server");
                    yield break;
                }
                
                // Parse the new cursor for next iteration
                var nextCursorStr = (string)resultArray[0];
                if (!long.TryParse(nextCursorStr, out cursor))
                {
                    _logger.LogError("Failed to parse SCAN cursor: {Cursor}", nextCursorStr);
                    yield break;
                }
                
                // Extract and yield keys from the response
                var keysArray = (RedisResult[])resultArray[1];
                foreach (var keyResult in keysArray)
                {
                    var keyStr = (string)keyResult;
                    if (!string.IsNullOrEmpty(keyStr))
                    {
                        yield return (RedisKey)keyStr;
                    }
                }
                
                // Log progress for monitoring (only for large scans)
                if (keysArray.Length > 0 && cursor % 10000 == 0)
                {
                    _logger.LogDebug("SCAN progress: cursor={Cursor}, keys_found={KeyCount}", cursor, keysArray.Length);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Redis SCAN operation cancelled at cursor {Cursor}", cursor);
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Redis SCAN at cursor {Cursor}", cursor);
                yield break;
            }
            
        } while (cursor != 0); // Continue until cursor returns to 0 (scan complete)
    }

    public async Task<int> PrecomputeDashboardDataAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var userIdList = userIds.ToList();
        var processedCount = 0;

        _logger.LogInformation("Starting dashboard data precomputation for {UserCount} users", userIdList.Count);

        try
        {
            foreach (var userId in userIdList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    // This would be implemented to precompute and cache dashboard data
                    // For now, we'll just count the users processed
                    await Task.Delay(10, cancellationToken); // Simulate work

                    processedCount++;

                    if (processedCount % 100 == 0)
                    {
                        _logger.LogDebug("Precomputed dashboard data for {ProcessedCount}/{TotalCount} users",
                            processedCount, userIdList.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to precompute dashboard data for user {UserId}", userId);
                }
            }

            _logger.LogInformation("Dashboard data precomputation completed. Processed {ProcessedCount}/{TotalCount} users",
                processedCount, userIdList.Count);

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard data precomputation failed after processing {ProcessedCount} users", processedCount);
            return processedCount;
        }
    }

    private async Task InvalidateUserAnalyticsSnapshotsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            // Remove all analytics snapshot cache entries for the user
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"{_cacheSettings.KeyPrefix}:dashboard:user:{userId}:snapshot:*";
            var keys = server.Keys(_database.Database, pattern).ToArray();

            if (keys.Length > 0)
            {
                await _database.KeyDeleteAsync(keys);
                _logger.LogDebug("Invalidated {KeyCount} analytics snapshot cache keys for user {UserId}", 
                    keys.Length, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate analytics snapshots for user {UserId}", userId);
        }
    }

    private async Task InvalidateUserProductivityMetricsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            // Remove all productivity metrics cache entries for the user
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"{_cacheSettings.KeyPrefix}:dashboard:user:{userId}:metrics:*";
            var keys = server.Keys(_database.Database, pattern).ToArray();

            if (keys.Length > 0)
            {
                await _database.KeyDeleteAsync(keys);
                _logger.LogDebug("Invalidated {KeyCount} productivity metrics cache keys for user {UserId}", 
                    keys.Length, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate productivity metrics for user {UserId}", userId);
        }
    }

    private void UpdateCacheTypeMetrics(string cacheType, bool hit)
    {
        if (!_cacheSettings.EnablePerformanceMonitoring)
        {
            return;
        }

        if (hit)
        {
            _cacheTypeHitRatios[$"{cacheType}_hits"] = _cacheTypeHitRatios.GetValueOrDefault($"{cacheType}_hits", 0) + 1;
        }
        else
        {
            _cacheTypeHitRatios[$"{cacheType}_misses"] = _cacheTypeHitRatios.GetValueOrDefault($"{cacheType}_misses", 0) + 1;
        }
    }

    private void RecordResponseTime(DateTime startTime)
    {
        if (!_cacheSettings.EnablePerformanceMonitoring)
        {
            return;
        }

        var responseTime = (DateTime.UtcNow - startTime).Ticks;
        lock (_responseTimes)
        {
            _responseTimes.Add(responseTime);

            // Keep only recent response times (last 1000 requests)
            if (_responseTimes.Count > 1000)
            {
                _responseTimes.RemoveRange(0, _responseTimes.Count - 1000);
            }
        }
    }

    private TimeSpan CalculateAverageResponseTime()
    {
        lock (_responseTimes)
        {
            if (_responseTimes.Count == 0)
            {
                return TimeSpan.Zero;
            }

            var averageTicks = _responseTimes.Sum() / _responseTimes.Count;
            return new TimeSpan(averageTicks);
        }
    }

    private CachePerformanceMetrics GetBaseCacheMetrics()
    {
        return new CachePerformanceMetrics
        {
            TotalRequests = Interlocked.Read(ref _totalRequests),
            CacheHits = Interlocked.Read(ref _cacheHits),
            CacheMisses = Interlocked.Read(ref _cacheMisses),
            AverageResponseTime = CalculateAverageResponseTime(),
            MeasurementStartTime = _metricsStartTime,
            LastResetTime = _lastResetTime
        };
    }

    // Cache key generation methods
    private string GetUserAnalyticsCacheKey(Guid userId)
        => $"{_cacheSettings.KeyPrefix}:dashboard:user:{userId}:analytics";

    private string GetProductivityStreakCacheKey(Guid userId)
        => $"{_cacheSettings.KeyPrefix}:dashboard:user:{userId}:streak";

    private string GetAnalyticsSnapshotCacheKey(Guid userId, DateTime snapshotDate, SnapshotType snapshotType)
        => $"{_cacheSettings.KeyPrefix}:dashboard:user:{userId}:snapshot:{snapshotDate:yyyy-MM-dd}:{snapshotType}";

    private string GetRecentAnalyticsSnapshotsCacheKey(Guid userId, SnapshotType snapshotType)
        => $"{_cacheSettings.KeyPrefix}:dashboard:user:{userId}:snapshots:recent:{snapshotType}";

    private string GetDashboardSummaryCacheKey(Guid userId)
        => $"{_cacheSettings.KeyPrefix}:dashboard:user:{userId}:summary";

    private string GetProductivityMetricsCacheKey(Guid userId, string period)
        => $"{_cacheSettings.KeyPrefix}:dashboard:user:{userId}:metrics:{period}";

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}