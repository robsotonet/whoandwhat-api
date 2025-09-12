namespace WhoAndWhat.Application.Configuration;

/// <summary>
/// Cache configuration settings interface for application services
/// </summary>
public interface ICacheSettings
{
    /// <summary>
    /// Default cache expiration time in minutes
    /// </summary>
    public int DefaultExpirationMinutes { get; }

    /// <summary>
    /// Task list cache expiration time in minutes
    /// </summary>
    public int TaskListCacheExpirationMinutes { get; }

    /// <summary>
    /// Cache key prefix to avoid collisions
    /// </summary>
    public string KeyPrefix { get; }
}

/// <summary>
/// Default cache settings implementation
/// </summary>
public class CacheSettings : ICacheSettings
{
    public int DefaultExpirationMinutes { get; set; } = 30;
    public int TaskListCacheExpirationMinutes { get; set; } = 5;
    public string KeyPrefix { get; set; } = "whoandwhat";
}
