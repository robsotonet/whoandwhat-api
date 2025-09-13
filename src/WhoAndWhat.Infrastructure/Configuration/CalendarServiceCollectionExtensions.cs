using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Services.Calendar;

namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Extensions for configuring calendar synchronization services in the DI container
/// </summary>
public static class CalendarServiceCollectionExtensions
{
    /// <summary>
    /// Configure calendar synchronization infrastructure with multi-provider support
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddCalendarSynchronization(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind calendar sync settings
        var calendarSettings = new CalendarSyncSettings();
        configuration.GetSection(CalendarSyncSettings.SectionName).Bind(calendarSettings);

        services.Configure<CalendarSyncSettings>(options =>
            configuration.GetSection(CalendarSyncSettings.SectionName).Bind(options));

        // Core calendar services
        services.AddScoped<ICalendarSyncService, CalendarSyncService>();
        services.AddScoped<ICalendarConflictDetector, CalendarConflictDetector>();
        services.AddScoped<ICalendarCacheService, CalendarCacheService>();
        services.AddSingleton<CalendarPerformanceOptimizer>();

        // Calendar provider services
        services.AddCalendarProviders(configuration);

        // Performance monitoring and health checks
        services.AddCalendarHealthChecks();

        // Background services for calendar operations
        services.AddCalendarBackgroundServices();

        return services;
    }

    /// <summary>
    /// Register calendar provider services with HTTP clients
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddCalendarProviders(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure HTTP clients for calendar providers
        services.AddHttpClient<GoogleCalendarProviderService>(client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "WhoAndWhat-CalendarSync/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetTimeoutPolicy());

        services.AddHttpClient<OutlookCalendarProviderService>(client =>
        {
            client.BaseAddress = new Uri("https://graph.microsoft.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "WhoAndWhat-CalendarSync/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetTimeoutPolicy());

        services.AddHttpClient<ICloudCalDAVProviderService>(client =>
        {
            client.BaseAddress = new Uri("https://caldav.icloud.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "WhoAndWhat-CalendarSync/1.0");
            client.Timeout = TimeSpan.FromSeconds(45); // CalDAV may be slower
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetTimeoutPolicy());

        // Register provider services as scoped services
        services.AddScoped<ICalendarProviderService, GoogleCalendarProviderService>();
        services.AddScoped<ICalendarProviderService, OutlookCalendarProviderService>();
        services.AddScoped<ICalendarProviderService, ICloudCalDAVProviderService>();

        return services;
    }

    /// <summary>
    /// Add calendar-specific health checks
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddCalendarHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<CalendarSyncHealthCheck>("calendar_sync",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "calendar", "sync", "external" })
            .AddCheck<CalendarCacheHealthCheck>("calendar_cache",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "calendar", "cache", "performance" })
            .AddCheck<CalendarProvidersHealthCheck>("calendar_providers",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "calendar", "providers", "external" });

        return services;
    }

    /// <summary>
    /// Add background services for calendar operations
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddCalendarBackgroundServices(this IServiceCollection services)
    {
        // Calendar cache warming service
        services.AddHostedService<CalendarCacheWarmupService>();

        // Calendar sync monitoring service
        services.AddHostedService<CalendarSyncMonitoringService>();

        // Calendar conflict auto-resolution service
        services.AddHostedService<CalendarConflictResolutionService>();

        return services;
    }

    /// <summary>
    /// Add calendar sync with specific providers only
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="enabledProviders">Providers to enable</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddCalendarSynchronization(
        this IServiceCollection services,
        IConfiguration configuration,
        params CalendarProvider[] enabledProviders)
    {
        // Add core services
        services.AddCalendarSynchronization(configuration);

        // Configure only specific providers
        foreach (var provider in enabledProviders)
        {
            switch (provider)
            {
                case CalendarProvider.Google:
                    services.AddScoped<GoogleCalendarProviderService>();
                    break;
                case CalendarProvider.Outlook:
                    services.AddScoped<OutlookCalendarProviderService>();
                    break;
                case CalendarProvider.ICloud:
                    services.AddScoped<ICloudCalDAVProviderService>();
                    break;
            }
        }

        return services;
    }

    // Private helper methods

    private static Polly.IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.GetLogger();
                    if (outcome.Exception != null)
                    {
                        logger?.LogWarning("Calendar provider HTTP retry {RetryCount} after {Delay}s due to: {Exception}",
                            retryCount, timespan.TotalSeconds, outcome.Exception.Message);
                    }
                    else
                    {
                        logger?.LogWarning("Calendar provider HTTP retry {RetryCount} after {Delay}s due to status: {StatusCode}",
                            retryCount, timespan.TotalSeconds, outcome.Result.StatusCode);
                    }
                });
    }

    private static Polly.IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Polly.Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(60));
    }
}

// Health check implementations

/// <summary>
/// Health check for calendar synchronization service
/// </summary>
public class CalendarSyncHealthCheck : IHealthCheck
{
    private readonly ICalendarSyncService _calendarSyncService;
    private readonly ILogger<CalendarSyncHealthCheck> _logger;

    public CalendarSyncHealthCheck(ICalendarSyncService calendarSyncService, ILogger<CalendarSyncHealthCheck> logger)
    {
        _calendarSyncService = calendarSyncService ?? throw new ArgumentNullException(nameof(calendarSyncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _calendarSyncService.IsCalendarSyncAvailableAsync(cancellationToken);

            if (isAvailable)
            {
                var healthStatus = await _calendarSyncService.GetSyncHealthAsync(cancellationToken);

                var data = new Dictionary<string, object>
                {
                    ["IsEnabled"] = healthStatus.IsEnabled,
                    ["IsHealthy"] = healthStatus.IsHealthy,
                    ["ProvidersChecked"] = healthStatus.ProviderHealthChecks.Count(),
                    ["HealthyProviders"] = healthStatus.ProviderHealthChecks.Count(p => p.IsAvailable),
                    ["CheckTime"] = healthStatus.CheckTime
                };

                return healthStatus.IsHealthy
                    ? HealthCheckResult.Healthy("Calendar sync service is healthy", data)
                    : HealthCheckResult.Degraded("Calendar sync service has issues", data);
            }
            else
            {
                return HealthCheckResult.Degraded("Calendar sync service is not available");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Calendar sync health check failed");
            return HealthCheckResult.Unhealthy($"Calendar sync health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Health check for calendar cache service
/// </summary>
public class CalendarCacheHealthCheck : IHealthCheck
{
    private readonly ICalendarCacheService _cacheService;
    private readonly ILogger<CalendarCacheHealthCheck> _logger;

    public CalendarCacheHealthCheck(ICalendarCacheService cacheService, ILogger<CalendarCacheHealthCheck> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test cache operations
            var testUserId = Guid.NewGuid();
            var testProvider = CalendarProvider.Google;
            var testCalendarId = "test-calendar";
            var testToken = $"test-token-{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Test sync token caching
            var cacheResult = await _cacheService.CacheSyncTokenAsync(testUserId, (Application.DTOs.Calendar.CalendarProvider)testProvider, testCalendarId, testToken, 1, cancellationToken);
            if (!cacheResult)
            {
                return HealthCheckResult.Degraded("Failed to cache test sync token");
            }

            // Test sync token retrieval
            var retrievedToken = await _cacheService.GetCachedSyncTokenAsync(testUserId, (Application.DTOs.Calendar.CalendarProvider)testProvider, testCalendarId, cancellationToken);
            if (retrievedToken != testToken)
            {
                return HealthCheckResult.Degraded("Failed to retrieve cached sync token");
            }

            // Get cache metrics
            var metrics = await _cacheService.GetCalendarCacheMetricsAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["TotalEntries"] = metrics.TotalEntries,
                ["HitRate"] = metrics.HitRate,
                ["TotalSizeBytes"] = metrics.TotalSizeBytes,
                ["LastResetTime"] = metrics.LastResetTime
            };

            return HealthCheckResult.Healthy("Calendar cache service is healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Calendar cache health check failed");
            return HealthCheckResult.Unhealthy($"Calendar cache health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Health check for calendar providers
/// </summary>
public class CalendarProvidersHealthCheck : IHealthCheck
{
    private readonly IEnumerable<ICalendarProviderService> _providerServices;
    private readonly ILogger<CalendarProvidersHealthCheck> _logger;

    public CalendarProvidersHealthCheck(IEnumerable<ICalendarProviderService> providerServices, ILogger<CalendarProvidersHealthCheck> logger)
    {
        _providerServices = providerServices ?? throw new ArgumentNullException(nameof(providerServices));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var providerResults = new Dictionary<string, object>();
            var healthyProviders = 0;
            var totalProviders = 0;

            foreach (var providerService in _providerServices)
            {
                totalProviders++;
                try
                {
                    var isAvailable = await providerService.IsAvailableAsync(cancellationToken);
                    var rateLimitStatus = await providerService.GetRateLimitStatusAsync(cancellationToken);
                    var capabilities = providerService.GetCapabilities();

                    providerResults[providerService.ProviderType.ToString()] = new
                    {
                        IsAvailable = isAvailable,
                        IsThrottled = rateLimitStatus.IsThrottled,
                        RequestsRemaining = rateLimitStatus.RequestsRemaining,
                        SupportsWebhooks = capabilities.SupportsWebhooks,
                        SupportsBatch = capabilities.SupportsBatchOperations
                    };

                    if (isAvailable)
                    {
                        healthyProviders++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Health check failed for provider {Provider}", providerService.ProviderType);
                    providerResults[providerService.ProviderType.ToString()] = new
                    {
                        IsAvailable = false,
                        Error = ex.Message
                    };
                }
            }

            var healthStatus = healthyProviders == totalProviders ? HealthCheckResult.Healthy(
                $"All {totalProviders} calendar providers are healthy",
                providerResults) :
                healthyProviders > 0 ? HealthCheckResult.Degraded(
                $"{healthyProviders}/{totalProviders} calendar providers are healthy",
                providerResults) :
                HealthCheckResult.Unhealthy(
                "No calendar providers are available",
                providerResults);

            return healthStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Calendar providers health check failed");
            return HealthCheckResult.Unhealthy($"Calendar providers health check failed: {ex.Message}");
        }
    }
}

// Background services

/// <summary>
/// Background service for warming calendar cache on application startup
/// </summary>
public class CalendarCacheWarmupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CalendarCacheWarmupService> _logger;

    public CalendarCacheWarmupService(IServiceProvider serviceProvider, ILogger<CalendarCacheWarmupService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for application to fully start up
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICalendarCacheService>();

            _logger.LogInformation("Starting calendar cache warmup process");

            // Simulate cache warmup for active users
            var warmedItems = await cacheService.WarmUserCalendarCacheAsync(
                Guid.Empty, // System user ID for general cache warmup
                [CalendarProvider.Google, CalendarProvider.Outlook, CalendarProvider.ICloud],
                stoppingToken);

            _logger.LogInformation("Calendar cache warmup completed. Warmed {ItemCount} cache items", warmedItems);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Calendar cache warmup cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during calendar cache warmup");
        }
    }
}

/// <summary>
/// Background service for monitoring calendar synchronization performance
/// </summary>
public class CalendarSyncMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CalendarSyncMonitoringService> _logger;

    public CalendarSyncMonitoringService(IServiceProvider serviceProvider, ILogger<CalendarSyncMonitoringService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var performanceOptimizer = scope.ServiceProvider.GetRequiredService<CalendarPerformanceOptimizer>();

                // Get and log performance metrics every 5 minutes
                var metrics = await performanceOptimizer.GetPerformanceMetricsAsync(stoppingToken);

                _logger.LogInformation("Calendar sync performance - Operations: {Total}, Success Rate: {SuccessRate:P1}, Avg Response: {AvgResponse}ms",
                    metrics.TotalOperations, metrics.OverallSuccessRate, metrics.AverageResponseTime.TotalMilliseconds);

                // Wait 5 minutes before next check
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in calendar sync monitoring");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retrying
            }
        }
    }
}

/// <summary>
/// Background service for automatic conflict resolution
/// </summary>
public class CalendarConflictResolutionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CalendarConflictResolutionService> _logger;

    public CalendarConflictResolutionService(IServiceProvider serviceProvider, ILogger<CalendarConflictResolutionService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var conflictDetector = scope.ServiceProvider.GetRequiredService<ICalendarConflictDetector>();

                // Check for conflicts that can be auto-resolved every 10 minutes
                // This is a placeholder - in real implementation, would query for pending conflicts
                _logger.LogDebug("Checking for auto-resolvable calendar conflicts");

                // Wait 10 minutes before next check
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in calendar conflict resolution service");
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); // Wait before retrying
            }
        }
    }
}
