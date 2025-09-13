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
/// Redis-based task caching service with performance monitoring and fallback support
/// </summary>
public class TaskCacheService : ITaskCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly IDistributedCache _distributedCache;
    private readonly RedisCacheSettings _cacheSettings;
    private readonly ILogger<TaskCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _semaphore;

    // Performance tracking
    private long _totalRequests = 0;
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private readonly List<long> _responseTimes = new();
    private readonly DateTime _metricsStartTime = DateTime.UtcNow;
    private DateTime _lastResetTime = DateTime.UtcNow;

    public TaskCacheService(
        IConnectionMultiplexer redis,
        IDistributedCache distributedCache,
        IOptions<RedisCacheSettings> cacheSettings,
        ILogger<TaskCacheService> logger)
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

        _logger.LogInformation("TaskCacheService initialized with Redis database {DatabaseIndex}", _cacheSettings.DatabaseIndex);
    }

    public async Task<bool> CacheTaskAsync(Domain.Entities.AppTask task, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetTaskCacheKey(task.Id);
            var serializedTask = JsonSerializer.Serialize(task, _jsonOptions);
            var expiration = TimeSpan.FromMinutes(_cacheSettings.TaskCacheExpirationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedTask, options, cancellationToken);

            _logger.LogDebug("Cached task {TaskId} with expiration {Expiration}", task.Id, expiration);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache task {TaskId}", task.Id);
            return false;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<Domain.Entities.AppTask?> GetCachedTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _totalRequests);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetTaskCacheKey(taskId);
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedData != null)
            {
                Interlocked.Increment(ref _cacheHits);
                var task = JsonSerializer.Deserialize<Domain.Entities.AppTask>(cachedData, _jsonOptions);
                _logger.LogDebug("Cache hit for task {TaskId}", taskId);
                return task;
            }

            Interlocked.Increment(ref _cacheMisses);
            _logger.LogDebug("Cache miss for task {TaskId}", taskId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached task {TaskId}", taskId);
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<bool> CacheUserTasksAsync(Guid userId, IEnumerable<Domain.Entities.AppTask> tasks, string? filterKey = null, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetUserTasksCacheKey(userId, filterKey);
            var taskList = tasks.ToList();
            var serializedTasks = JsonSerializer.Serialize(taskList, _jsonOptions);
            var expiration = TimeSpan.FromMinutes(_cacheSettings.TaskListCacheExpirationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedTasks, options, cancellationToken);

            _logger.LogDebug("Cached {TaskCount} tasks for user {UserId} with filter '{FilterKey}'",
                taskList.Count, userId, filterKey ?? "none");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache tasks for user {UserId}", userId);
            return false;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<IEnumerable<Domain.Entities.AppTask>?> GetCachedUserTasksAsync(Guid userId, string? filterKey = null, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _totalRequests);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetUserTasksCacheKey(userId, filterKey);
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedData != null)
            {
                Interlocked.Increment(ref _cacheHits);
                var tasks = JsonSerializer.Deserialize<List<Domain.Entities.AppTask>>(cachedData, _jsonOptions);
                _logger.LogDebug("Cache hit for user {UserId} tasks with filter '{FilterKey}'", userId, filterKey ?? "none");
                return tasks;
            }

            Interlocked.Increment(ref _cacheMisses);
            _logger.LogDebug("Cache miss for user {UserId} tasks", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached tasks for user {UserId}", userId);
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<bool> CacheUserTaskSummaryAsync(Guid userId, int totalTasks, int completedTasks, int overdueTasks, int todayTasks, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var summary = new UserTaskSummary
            {
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                OverdueTasks = overdueTasks,
                TodayTasks = todayTasks,
                CachedAt = DateTime.UtcNow
            };

            var cacheKey = GetUserSummaryCacheKey(userId);
            var serializedSummary = JsonSerializer.Serialize(summary, _jsonOptions);
            var expiration = TimeSpan.FromMinutes(_cacheSettings.UserTaskSummaryCacheExpirationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedSummary, options, cancellationToken);

            _logger.LogDebug("Cached task summary for user {UserId}: Total={Total}, Completed={Completed}, Overdue={Overdue}, Today={Today}",
                userId, totalTasks, completedTasks, overdueTasks, todayTasks);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache task summary for user {UserId}", userId);
            return false;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<UserTaskSummary?> GetCachedUserTaskSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _totalRequests);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            var cacheKey = GetUserSummaryCacheKey(userId);
            var cachedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedData != null)
            {
                Interlocked.Increment(ref _cacheHits);
                var summary = JsonSerializer.Deserialize<UserTaskSummary>(cachedData, _jsonOptions);
                _logger.LogDebug("Cache hit for user {UserId} task summary", userId);
                return summary;
            }

            Interlocked.Increment(ref _cacheMisses);
            _logger.LogDebug("Cache miss for user {UserId} task summary", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached task summary for user {UserId}", userId);
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        finally
        {
            _semaphore.Release();
            RecordResponseTime(startTime);
        }
    }

    public async Task<bool> InvalidateTaskCacheAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = new List<System.Threading.Tasks.Task>
            {
                // Invalidate individual task cache
                _distributedCache.RemoveAsync(GetTaskCacheKey(taskId), cancellationToken),
                
                // Invalidate user task lists (all filters)
                InvalidateUserTaskListsAsync(userId, cancellationToken),
                
                // Invalidate user summary
                _distributedCache.RemoveAsync(GetUserSummaryCacheKey(userId), cancellationToken)
            };

            await System.Threading.Tasks.Task.WhenAll(tasks);

            _logger.LogDebug("Invalidated cache for task {TaskId} and user {UserId} related data", taskId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache for task {TaskId}", taskId);
            return false;
        }
    }

    public async Task<bool> InvalidateUserTaskCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = new List<System.Threading.Tasks.Task>
            {
                // Invalidate all user task lists
                InvalidateUserTaskListsAsync(userId, cancellationToken),
                
                // Invalidate user summary
                _distributedCache.RemoveAsync(GetUserSummaryCacheKey(userId), cancellationToken)
            };

            await System.Threading.Tasks.Task.WhenAll(tasks);

            _logger.LogDebug("Invalidated all task cache for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate task cache for user {UserId}", userId);
            return false;
        }
    }

    public async Task<int> WarmCacheAsync(CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.EnableCacheWarming)
        {
            _logger.LogDebug("Cache warming is disabled");
            return 0;
        }

        try
        {
            _logger.LogInformation("Starting cache warming process");

            // This would be implemented to warm cache with frequently accessed tasks
            // For now, we'll return 0 as this requires coordination with the repository layer
            // This will be implemented when DevB provides the task repository implementation

            await System.Threading.Tasks.Task.CompletedTask; // Placeholder for actual implementation

            _logger.LogInformation("Cache warming completed - 0 items warmed (awaiting repository implementation)");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm cache");
            return 0;
        }
    }

    public async Task<CachePerformanceMetrics> GetCacheMetricsAsync(CancellationToken cancellationToken = default)
    {
        await System.Threading.Tasks.Task.CompletedTask; // Async method for future extensibility

        var metrics = new CachePerformanceMetrics
        {
            TotalRequests = Interlocked.Read(ref _totalRequests),
            CacheHits = Interlocked.Read(ref _cacheHits),
            CacheMisses = Interlocked.Read(ref _cacheMisses),
            AverageResponseTime = CalculateAverageResponseTime(),
            MeasurementStartTime = _metricsStartTime,
            LastResetTime = _lastResetTime
        };

        return metrics;
    }

    public async Task<bool> ClearAllCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all keys with our prefix using SCAN for better performance
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"{_cacheSettings.KeyPrefix}:task:*";
            var keysToDelete = new List<RedisKey>();
            
            await foreach (var key in ScanKeysAsync(server, pattern, cancellationToken: cancellationToken))
            {
                keysToDelete.Add(key);
            }

            if (keysToDelete.Count > 0)
            {
                await _database.KeyDeleteAsync(keysToDelete.ToArray());
                _logger.LogWarning("Cleared {KeyCount} task cache keys", keysToDelete.Count);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all task cache");
            return false;
        }
    }

    private async System.Threading.Tasks.Task InvalidateUserTaskListsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            // Remove all user task list variations (different filters) using SCAN
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"{_cacheSettings.KeyPrefix}:task:user:{userId}:list:*";
            var keysToDelete = new List<RedisKey>();
            
            await foreach (var key in ScanKeysAsync(server, pattern, cancellationToken: cancellationToken))
            {
                keysToDelete.Add(key);
            }

            if (keysToDelete.Count > 0)
            {
                await _database.KeyDeleteAsync(keysToDelete.ToArray());
                _logger.LogDebug("Invalidated {KeyCount} user task list cache keys for user {UserId}", keysToDelete.Count, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate user task lists for user {UserId}", userId);
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
                _responseTimes.RemoveRange(0, 100); // Remove in smaller batches for better performance
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

    private string GetTaskCacheKey(Guid taskId)
        => $"{_cacheSettings.KeyPrefix}:task:id:{taskId}";

    private string GetUserTasksCacheKey(Guid userId, string? filterKey)
        => $"{_cacheSettings.KeyPrefix}:task:user:{userId}:list:{filterKey ?? "all"}";

    private string GetUserSummaryCacheKey(Guid userId)
        => $"{_cacheSettings.KeyPrefix}:task:user:{userId}:summary";

    private async IAsyncEnumerable<RedisKey> ScanKeysAsync(
        IServer server, 
        string pattern, 
        int pageSize = 1000,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long cursor = 0;
        do
        {
            var scanResult = await server.ScanAsync(
                database: _database.Database,
                cursor: cursor,
                pattern: pattern,
                pageSize: pageSize);

            cursor = scanResult.Cursor;

            foreach (var key in scanResult.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return key;
            }
        } while (cursor != 0);
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}
