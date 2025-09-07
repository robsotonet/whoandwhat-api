namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for Redis caching infrastructure
/// </summary>
public class RedisCacheSettings
{
    /// <summary>
    /// Configuration section name for Redis cache settings
    /// </summary>
    public const string SectionName = "RedisCache";

    /// <summary>
    /// Redis connection string
    /// Default: localhost:6379 for development
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Default cache expiration time in minutes
    /// Default: 30 minutes
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Task cache expiration time in minutes
    /// Default: 15 minutes (tasks change frequently)
    /// </summary>
    public int TaskCacheExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Task list cache expiration time in minutes
    /// Default: 5 minutes (task lists change very frequently)
    /// </summary>
    public int TaskListCacheExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// User task summary cache expiration time in minutes
    /// Default: 60 minutes (summaries are expensive to calculate)
    /// </summary>
    public int UserTaskSummaryCacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Enable cache warming on application startup
    /// Default: true
    /// </summary>
    public bool EnableCacheWarming { get; set; } = true;

    /// <summary>
    /// Maximum number of items to cache warm
    /// Default: 1000 (prevent memory exhaustion)
    /// </summary>
    public int MaxCacheWarmItems { get; set; } = 1000;

    /// <summary>
    /// Redis database index to use
    /// Default: 0
    /// </summary>
    public int DatabaseIndex { get; set; } = 0;

    /// <summary>
    /// Enable cache performance monitoring
    /// Default: true
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Cache key prefix to avoid collisions
    /// Default: "whoandwhat"
    /// </summary>
    public string KeyPrefix { get; set; } = "whoandwhat";

    /// <summary>
    /// Connection timeout in milliseconds
    /// Default: 5000 (5 seconds)
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Command timeout in milliseconds
    /// Default: 3000 (3 seconds)
    /// </summary>
    public int CommandTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Maximum number of connection retries
    /// Default: 3
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Enable cache fallback when Redis is unavailable
    /// Default: true (degrade gracefully)
    /// </summary>
    public bool EnableCacheFallback { get; set; } = true;
}