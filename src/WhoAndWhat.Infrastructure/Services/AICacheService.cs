using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Redis-based implementation of AI-specific caching operations
/// Provides high-performance caching for AI responses, planning data, and suggestions
/// </summary>
public class AICacheService : IAICacheService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDistributedCache _distributedCache;
    private readonly IDatabase _database;
    private readonly ILogger<AICacheService> _logger;
    private readonly RedisCacheSettings _settings;
    private readonly string _keyPrefix;
    private readonly ConcurrentDictionary<AICacheType, AICacheTypeMetrics> _metricsTracker;
    private bool _disposed;

    public AICacheService(
        IConnectionMultiplexer redis,
        IDistributedCache distributedCache,
        IOptions<RedisCacheSettings> settings,
        ILogger<AICacheService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _database = _redis.GetDatabase(_settings.DatabaseIndex);
        _keyPrefix = $"{_settings.KeyPrefix}:ai";
        _metricsTracker = new ConcurrentDictionary<AICacheType, AICacheTypeMetrics>();
    }

    public async Task<bool> CacheDayPlanAsync(AIGeneratedPlan dayPlan, int expirationMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:day-plan:{dayPlan.UserId}:{dayPlan.PlanDate:yyyy-MM-dd}";
            var json = JsonSerializer.Serialize(dayPlan);
            var expiry = TimeSpan.FromMinutes(expirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation(AICacheType.DayPlans, true);
            _logger.LogDebug("Cached day plan for user {UserId}, date {PlanDate}", dayPlan.UserId, dayPlan.PlanDate);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache day plan for user {UserId}, date {PlanDate}", dayPlan.UserId, dayPlan.PlanDate);
            TrackCacheOperation(AICacheType.DayPlans, false);
            return false;
        }
    }

    public async Task<AIGeneratedPlan?> GetCachedDayPlanAsync(Guid userId, DateTime planDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:day-plan:{userId}:{planDate:yyyy-MM-dd}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation(AICacheType.DayPlans, false);
                return null;
            }

            TrackCacheOperation(AICacheType.DayPlans, true);
            return JsonSerializer.Deserialize<AIGeneratedPlan>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached day plan for user {UserId}, date {PlanDate}", userId, planDate);
            TrackCacheOperation(AICacheType.DayPlans, false);
            return null;
        }
    }

    public async Task<bool> CachePrioritySuggestionAsync(TaskPrioritySuggestion prioritySuggestion, int expirationMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:priority:{prioritySuggestion.TaskId}";
            var json = JsonSerializer.Serialize(prioritySuggestion);
            var expiry = TimeSpan.FromMinutes(expirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation(AICacheType.PrioritySuggestions, true);
            _logger.LogDebug("Cached priority suggestion for task {TaskId}", prioritySuggestion.TaskId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache priority suggestion for task {TaskId}", prioritySuggestion.TaskId);
            TrackCacheOperation(AICacheType.PrioritySuggestions, false);
            return false;
        }
    }

    public async Task<TaskPrioritySuggestion?> GetCachedPrioritySuggestionAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:priority:{taskId}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation(AICacheType.PrioritySuggestions, false);
                return null;
            }

            TrackCacheOperation(AICacheType.PrioritySuggestions, true);
            return JsonSerializer.Deserialize<TaskPrioritySuggestion>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached priority suggestion for task {TaskId}", taskId);
            TrackCacheOperation(AICacheType.PrioritySuggestions, false);
            return null;
        }
    }

    public async Task<bool> CacheProductivityInsightsAsync(ProductivityInsights insights, int expirationMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:insights:{insights.UserId}:{insights.AnalysisDate:yyyy-MM-dd}";
            var json = JsonSerializer.Serialize(insights);
            var expiry = TimeSpan.FromMinutes(expirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation(AICacheType.ProductivityInsights, true);
            _logger.LogDebug("Cached productivity insights for user {UserId}, date {AnalysisDate}", insights.UserId, insights.AnalysisDate);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache productivity insights for user {UserId}, date {AnalysisDate}", insights.UserId, insights.AnalysisDate);
            TrackCacheOperation(AICacheType.ProductivityInsights, false);
            return false;
        }
    }

    public async Task<ProductivityInsights?> GetCachedProductivityInsightsAsync(Guid userId, TimeframeAnalysis analysisTimeframe, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:insights:{userId}:{analysisTimeframe.StartDate:yyyy-MM-dd}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation(AICacheType.ProductivityInsights, false);
                return null;
            }

            TrackCacheOperation(AICacheType.ProductivityInsights, true);
            return JsonSerializer.Deserialize<ProductivityInsights>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached productivity insights for user {UserId}", userId);
            TrackCacheOperation(AICacheType.ProductivityInsights, false);
            return null;
        }
    }

    public async Task<bool> CacheScheduleOptimizationAsync(ScheduleOptimizationResult optimizationResult, int expirationMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:schedule-opt:{optimizationResult.UserId}:{optimizationResult.OptimizationDate:yyyy-MM-dd}";
            var json = JsonSerializer.Serialize(optimizationResult);
            var expiry = TimeSpan.FromMinutes(expirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation(AICacheType.ScheduleOptimizations, true);
            _logger.LogDebug("Cached schedule optimization for user {UserId}, date {OptimizationDate}", optimizationResult.UserId, optimizationResult.OptimizationDate);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache schedule optimization for user {UserId}, date {OptimizationDate}", optimizationResult.UserId, optimizationResult.OptimizationDate);
            TrackCacheOperation(AICacheType.ScheduleOptimizations, false);
            return false;
        }
    }

    public async Task<ScheduleOptimizationResult?> GetCachedScheduleOptimizationAsync(Guid userId, DateTime optimizationDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:schedule-opt:{userId}:{optimizationDate:yyyy-MM-dd}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation(AICacheType.ScheduleOptimizations, false);
                return null;
            }

            TrackCacheOperation(AICacheType.ScheduleOptimizations, true);
            return JsonSerializer.Deserialize<ScheduleOptimizationResult>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached schedule optimization for user {UserId}, date {OptimizationDate}", userId, optimizationDate);
            TrackCacheOperation(AICacheType.ScheduleOptimizations, false);
            return null;
        }
    }

    public async Task<bool> CacheCategorizationSuggestionsAsync(string taskContent, IEnumerable<CategorySuggestion> suggestions, int expirationMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            var contentHash = ComputeContentHash(taskContent);
            var key = $"{_keyPrefix}:categorization:{contentHash}";
            var json = JsonSerializer.Serialize(suggestions);
            var expiry = TimeSpan.FromMinutes(expirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation(AICacheType.CategorizationSuggestions, true);
            _logger.LogDebug("Cached categorization suggestions for content hash {ContentHash}", contentHash);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache categorization suggestions for task content");
            TrackCacheOperation(AICacheType.CategorizationSuggestions, false);
            return false;
        }
    }

    public async Task<IEnumerable<CategorySuggestion>?> GetCachedCategorizationSuggestionsAsync(string taskContent, CancellationToken cancellationToken = default)
    {
        try
        {
            var contentHash = ComputeContentHash(taskContent);
            var key = $"{_keyPrefix}:categorization:{contentHash}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation(AICacheType.CategorizationSuggestions, false);
                return null;
            }

            TrackCacheOperation(AICacheType.CategorizationSuggestions, true);
            return JsonSerializer.Deserialize<IEnumerable<CategorySuggestion>>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached categorization suggestions for task content");
            TrackCacheOperation(AICacheType.CategorizationSuggestions, false);
            return null;
        }
    }

    public async Task<bool> CacheTaskTimeEstimateAsync(Guid taskId, TaskTimeEstimate estimate, int expirationMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:time-estimate:{taskId}";
            var json = JsonSerializer.Serialize(estimate);
            var expiry = TimeSpan.FromMinutes(expirationMinutes);

            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, cancellationToken);

            TrackCacheOperation(AICacheType.TimeEstimates, true);
            _logger.LogDebug("Cached time estimate for task {TaskId}", taskId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache time estimate for task {TaskId}", taskId);
            TrackCacheOperation(AICacheType.TimeEstimates, false);
            return false;
        }
    }

    public async Task<TaskTimeEstimate?> GetCachedTaskTimeEstimateAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{_keyPrefix}:time-estimate:{taskId}";
            var json = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                TrackCacheOperation(AICacheType.TimeEstimates, false);
                return null;
            }

            TrackCacheOperation(AICacheType.TimeEstimates, true);
            return JsonSerializer.Deserialize<TaskTimeEstimate>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached time estimate for task {TaskId}", taskId);
            TrackCacheOperation(AICacheType.TimeEstimates, false);
            return null;
        }
    }

    public async Task<bool> InvalidateUserAICacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = $"{_keyPrefix}:*:{userId}*";
            await InvalidateByPatternAsync(pattern, cancellationToken);
            _logger.LogInformation("Invalidated all AI cache for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate AI cache for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> InvalidateUserAICacheByTypeAsync(Guid userId, IEnumerable<AICacheType> cacheTypes, CancellationToken cancellationToken = default)
    {
        try
        {
            var success = true;
            foreach (var cacheType in cacheTypes)
            {
                var pattern = cacheType switch
                {
                    AICacheType.DayPlans => $"{_keyPrefix}:day-plan:{userId}:*",
                    AICacheType.PrioritySuggestions => $"{_keyPrefix}:priority:*", // Priority suggestions are task-specific, not user-specific
                    AICacheType.ProductivityInsights => $"{_keyPrefix}:insights:{userId}:*",
                    AICacheType.ScheduleOptimizations => $"{_keyPrefix}:schedule-opt:{userId}:*",
                    AICacheType.CategorizationSuggestions => $"{_keyPrefix}:categorization:*", // Content-based, not user-specific
                    AICacheType.TimeEstimates => $"{_keyPrefix}:time-estimate:*", // Task-specific, not user-specific
                    AICacheType.All => $"{_keyPrefix}:*:{userId}*",
                    _ => $"{_keyPrefix}:*:{userId}*"
                };

                try
                {
                    await InvalidateByPatternAsync(pattern, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to invalidate AI cache type {CacheType} for user {UserId}", cacheType, userId);
                    success = false;
                }
            }

            if (success)
            {
                _logger.LogInformation("Successfully invalidated AI cache types {CacheTypes} for user {UserId}",
                    string.Join(",", cacheTypes), userId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate AI cache types for user {UserId}", userId);
            return false;
        }
    }

    public Task<AICacheMetrics> GetAICacheMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var totalEntries = 0;
            var totalSizeBytes = 0L;
            var totalHits = _metricsTracker.Values.Sum(m => m.HitCount);
            var totalMisses = _metricsTracker.Values.Sum(m => m.MissCount);
            var totalRequests = totalHits + totalMisses;
            var hitRate = totalRequests > 0 ? (double)totalHits / totalRequests : 0.0;

            var entriesByType = new Dictionary<AICacheType, int>();
            var hitRatesByType = new Dictionary<AICacheType, double>();

            foreach (var kvp in _metricsTracker)
            {
                var typeRequests = kvp.Value.HitCount + kvp.Value.MissCount;
                var typeHitRate = typeRequests > 0 ? (double)kvp.Value.HitCount / typeRequests : 0.0;

                entriesByType[kvp.Key] = kvp.Value.HitCount; // Approximate entries count
                hitRatesByType[kvp.Key] = typeHitRate;
            }

            var lastResetTime = _metricsTracker.Values.Any()
                ? _metricsTracker.Values.Min(m => m.LastResetTime)
                : DateTime.UtcNow;

            var averageResponseTime = _metricsTracker.Values.Any()
                ? TimeSpan.FromMilliseconds(_metricsTracker.Values.Average(m => m.AverageResponseTimeMs))
                : TimeSpan.Zero;

            return Task.FromResult(new AICacheMetrics(
                TotalEntries: totalEntries,
                TotalSizeBytes: totalSizeBytes,
                HitCount: totalHits,
                MissCount: totalMisses,
                HitRate: hitRate,
                EntriesByType: entriesByType,
                HitRatesByType: hitRatesByType,
                LastResetTime: lastResetTime,
                AverageResponseTime: averageResponseTime
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AI cache metrics");
            return Task.FromResult(new AICacheMetrics(0, 0, 0, 0, 0.0, new Dictionary<AICacheType, int>(),
                new Dictionary<AICacheType, double>(), DateTime.UtcNow, TimeSpan.Zero));
        }
    }

    public async Task<bool> ClearAllAICacheAsync(CancellationToken cancellationToken = default)
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
                    _logger.LogWarning("AI cache clear operation was cancelled");
                    return false;
                }

                var scanResult = server.Execute("SCAN", cursor, "MATCH", pattern, "COUNT", 100);

                if (!scanResult.IsNull && scanResult.Length >= 2)
                {
                    var nextCursor = scanResult[0];
                    var keys = scanResult[1];

                    if (!keys.IsNull)
                    {
                        var redisKeys = keys is RedisResult[] keyArray
                            ? keyArray.Where(k => !k.IsNull).Select(k => (RedisKey)k).ToArray()
                            : Array.Empty<RedisKey>();

                        if (redisKeys.Any())
                        {
                            var deleteResult = await _database.KeyDeleteAsync(redisKeys);
                            deletedCount += (int)deleteResult;
                            _logger.LogDebug("Deleted {Count} AI cache keys", deleteResult);
                        }
                    }

                    if (!(nextCursor is string cursorStr && long.TryParse(cursorStr, out cursor)))
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            } while (cursor != 0);

            _logger.LogInformation("Cleared {Count} AI cache entries", deletedCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all AI cache");
            return false;
        }
    }

    public Task<int> WarmUserAICacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var warmedCount = 0;

            // Here you would implement cache warming logic
            // For example, pre-compute frequently accessed day plans, insights, etc.
            // This is a placeholder implementation

            _logger.LogInformation("AI cache warming completed for user {UserId}, warmed {Count} entries", userId, warmedCount);
            return Task.FromResult(warmedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm AI cache for user {UserId}", userId);
            return Task.FromResult(0);
        }
    }

    #region Private Helper Methods

    private async Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var cursor = 0L;

        do
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var scanResult = server.Execute("SCAN", cursor, "MATCH", pattern, "COUNT", 100);

            if (!scanResult.IsNull && scanResult.Length >= 2)
            {
                var nextCursor = scanResult[0];
                var keys = scanResult[1];

                if (!keys.IsNull)
                {
                    var redisKeys = keys is RedisResult[] keyArray
                        ? keyArray.Where(k => !k.IsNull).Select(k => (RedisKey)k).ToArray()
                        : Array.Empty<RedisKey>();

                    if (redisKeys.Any())
                    {
                        await _database.KeyDeleteAsync(redisKeys);
                    }
                }

                if (!(nextCursor is string cursorStr && long.TryParse(cursorStr, out cursor)))
                {
                    break;
                }
            }
            else
            {
                break;
            }
        } while (cursor != 0);
    }

    private void TrackCacheOperation(AICacheType cacheType, bool isHit)
    {
        if (!_settings.EnablePerformanceMonitoring)
        {
            return;
        }

        _metricsTracker.AddOrUpdate(cacheType,
            new AICacheTypeMetrics
            {
                HitCount = isHit ? 1 : 0,
                MissCount = isHit ? 0 : 1,
                LastResetTime = DateTime.UtcNow,
                AverageResponseTimeMs = 0
            },
            (key, existing) =>
            {
                if (isHit)
                {
                    existing.HitCount++;
                }
                else
                {
                    existing.MissCount++;
                }
                return existing;
            });
    }

    private static string ComputeContentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes)[..32]; // Use first 32 characters for balance between key length and collision resistance
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _metricsTracker.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Metrics for individual AI cache types
/// </summary>
internal class AICacheTypeMetrics
{
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public DateTime LastResetTime { get; set; }
    public double AverageResponseTimeMs { get; set; }
}
