using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Services;

namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Extensions for configuring Redis caching services in the DI container
/// </summary>
public static class CacheServiceCollectionExtensions
{
    /// <summary>
    /// Configure Redis caching infrastructure with performance monitoring and health checks
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddRedisCaching(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind Redis cache settings
        var cacheSettings = new RedisCacheSettings();
        configuration.GetSection(RedisCacheSettings.SectionName).Bind(cacheSettings);

        services.Configure<RedisCacheSettings>(options => configuration.GetSection(RedisCacheSettings.SectionName).Bind(options));

        // Configure Redis connection
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IConnectionMultiplexer>>();

            try
            {
                var configurationOptions = ConfigurationOptions.Parse(cacheSettings.ConnectionString);
                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectTimeout = cacheSettings.ConnectionTimeoutMs;
                configurationOptions.SyncTimeout = cacheSettings.CommandTimeoutMs;
                configurationOptions.ConnectRetry = cacheSettings.MaxRetryAttempts;
                configurationOptions.ReconnectRetryPolicy = new LinearRetry(1000); // 1 second between retries

                var connection = ConnectionMultiplexer.Connect(configurationOptions);

                // Log connection events
                connection.ConnectionFailed += (sender, e) =>
                {
                    logger.LogError("Redis connection failed: {EndPoint} - {FailureType}: {Exception}",
                        e.EndPoint, e.FailureType, e.Exception?.Message);
                };

                connection.ConnectionRestored += (sender, e) =>
                {
                    logger.LogInformation("Redis connection restored: {EndPoint}", e.EndPoint);
                };

                connection.ErrorMessage += (sender, e) =>
                {
                    logger.LogError("Redis error: {EndPoint} - {Message}", e.EndPoint, e.Message);
                };

                logger.LogInformation("Redis connection established successfully to {ConnectionString}",
                    cacheSettings.ConnectionString);

                return connection;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to Redis at {ConnectionString}. Caching will be disabled.",
                    cacheSettings.ConnectionString);

                if (!cacheSettings.EnableCacheFallback)
                {
                    throw;
                }

                // Return a dummy connection that will gracefully fail
                // This allows the application to start without Redis when fallback is enabled
                return ConnectionMultiplexer.Connect("localhost:9999", opt => opt.AbortOnConnectFail = false);
            }
        });

        // Configure distributed cache using Redis
        services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions = ConfigurationOptions.Parse(cacheSettings.ConnectionString);
            options.ConfigurationOptions.AbortOnConnectFail = false;
            options.InstanceName = cacheSettings.KeyPrefix;
        });

        // Register task cache service
        services.AddScoped<ITaskCacheService, TaskCacheService>();

        // Add Redis health checks
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis_cache",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "cache", "redis" });

        return services;
    }

    /// <summary>
    /// Add cache warming as a hosted service (optional)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddCacheWarming(this IServiceCollection services)
    {
        services.AddHostedService<CacheWarmupService>();
        return services;
    }
}

/// <summary>
/// Health check implementation for Redis cache
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisHealthCheck> logger)
    {
        _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _connectionMultiplexer.GetDatabase();

            // Test basic Redis operations
            var testKey = "health_check_" + Guid.NewGuid().ToString("N")[..8];
            var testValue = DateTime.UtcNow.ToString("O");

            // Test write
            var setResult = await database.StringSetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
            if (!setResult)
            {
                return HealthCheckResult.Degraded("Failed to write test value to Redis");
            }

            // Test read
            var getValue = await database.StringGetAsync(testKey);
            if (!getValue.HasValue || getValue != testValue)
            {
                return HealthCheckResult.Degraded("Failed to read test value from Redis");
            }

            // Clean up test key
            await database.KeyDeleteAsync(testKey);

            // Get connection info
            var endpoints = _connectionMultiplexer.GetEndPoints();
            var serverInfo = new Dictionary<string, object>();

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var server = _connectionMultiplexer.GetServer(endpoint);
                    if (server.IsConnected)
                    {
                        var info = await server.InfoAsync();
                        var redisVersion = info.FirstOrDefault(group => group.Key == "Server")?.
                            FirstOrDefault(kvp => kvp.Key == "redis_version").Value ?? "Unknown";

                        serverInfo[endpoint.ToString()!] = new
                        {
                            Status = "Connected",
                            Version = redisVersion
                        };
                    }
                    else
                    {
                        serverInfo[endpoint.ToString()!] = new { Status = "Disconnected" };
                    }
                }
                catch (Exception ex)
                {
                    serverInfo[endpoint.ToString()!] = new { Status = "Error", Error = ex.Message };
                }
            }

            return HealthCheckResult.Healthy("Redis cache is healthy", serverInfo);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection issue during health check");
            return HealthCheckResult.Degraded($"Redis connection issue: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            return HealthCheckResult.Unhealthy($"Redis health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Background service for cache warming on application startup
/// </summary>
public class CacheWarmupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CacheWarmupService> _logger;

    public CacheWarmupService(IServiceProvider serviceProvider, ILogger<CacheWarmupService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for application to fully start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var taskCacheService = scope.ServiceProvider.GetRequiredService<ITaskCacheService>();

            _logger.LogInformation("Starting cache warmup process");
            var warmedItems = await taskCacheService.WarmCacheAsync(stoppingToken);
            _logger.LogInformation("Cache warmup completed. Warmed {ItemCount} cache items", warmedItems);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cache warmup cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache warmup");
        }
    }
}
