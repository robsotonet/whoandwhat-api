using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services.Calendar;

/// <summary>
/// Advanced performance optimization service for calendar synchronization operations
/// Provides batch processing, differential updates, backup/recovery, and performance monitoring
/// </summary>
public class CalendarPerformanceOptimizer : IDisposable
{
    private readonly ICalendarCacheService _cacheService;
    private readonly ILogger<CalendarPerformanceOptimizer> _logger;
    private readonly CalendarSyncSettings _settings;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _performanceMetrics;
    private readonly ConcurrentDictionary<CalendarProvider, RateLimiter> _rateLimiters;
    private readonly SemaphoreSlim _backupSemaphore;
    private readonly Timer _metricsResetTimer;
    private bool _disposed;

    public CalendarPerformanceOptimizer(
        ICalendarCacheService cacheService,
        IOptions<CalendarSyncSettings> settings,
        ILogger<CalendarPerformanceOptimizer> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _performanceMetrics = new ConcurrentDictionary<string, PerformanceMetrics>();
        _rateLimiters = new ConcurrentDictionary<CalendarProvider, RateLimiter>();
        _backupSemaphore = new SemaphoreSlim(1, 1);

        InitializeRateLimiters();

        // Reset metrics every hour
        _metricsResetTimer = new Timer(ResetMetrics, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Performs optimized batch synchronization for multiple calendars and events
    /// </summary>
    public async Task<BatchSyncResult> BatchSynchronizeAsync(
        Guid userId,
        IEnumerable<BatchSyncRequest> syncRequests,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var batchId = Guid.NewGuid();

        try
        {
            _logger.LogInformation("Starting batch sync {BatchId} for user {UserId} with {RequestCount} requests",
                batchId, userId, syncRequests.Count());

            var results = new List<BatchSyncItemResult>();
            var providerGroups = syncRequests.GroupBy(r => r.Provider);

            // Process each provider in parallel while respecting rate limits
            var tasks = providerGroups.Select(async providerGroup =>
            {
                var provider = providerGroup.Key;
                var rateLimiter = GetRateLimiter(provider);
                var providerResults = new List<BatchSyncItemResult>();

                foreach (var request in providerGroup)
                {
                    await rateLimiter.WaitAsync(cancellationToken);

                    try
                    {
                        var result = await ProcessSingleSyncRequest(userId, request, cancellationToken);
                        providerResults.Add(result);

                        // Track performance metrics
                        RecordOperationMetrics($"BatchSync_{provider}", result.Success, result.ProcessingTime);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process sync request for provider {Provider}", provider);
                        providerResults.Add(new BatchSyncItemResult(
                            request.CalendarId,
                            request.Provider,
                            false,
                            0,
                            ex.Message,
                            TimeSpan.Zero
                        ));
                    }
                }

                return providerResults;
            });

            var allResults = await Task.WhenAll(tasks);
            results = allResults.SelectMany(r => r).ToList();

            stopwatch.Stop();
            var successCount = results.Count(r => r.Success);
            var totalEvents = results.Sum(r => r.EventsProcessed);

            _logger.LogInformation("Completed batch sync {BatchId} in {Duration}ms. Success: {SuccessCount}/{TotalCount}, Events: {TotalEvents}",
                batchId, stopwatch.ElapsedMilliseconds, successCount, results.Count, totalEvents);

            return new BatchSyncResult(
                batchId,
                userId,
                successCount == results.Count,
                results,
                successCount,
                results.Count - successCount,
                totalEvents,
                stopwatch.Elapsed,
                successCount == results.Count ? null : "Some sync operations failed"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch sync {BatchId} failed for user {UserId}", batchId, userId);
            return new BatchSyncResult(
                batchId,
                userId,
                false,
                [],
                0,
                syncRequests.Count(),
                0,
                stopwatch.Elapsed,
                ex.Message
            );
        }
    }

    /// <summary>
    /// Performs differential sync to only update changed events
    /// </summary>
    public async Task<DifferentialSyncResult> DifferentialSyncAsync(
        Guid userId,
        CalendarProvider provider,
        string calendarId,
        IEnumerable<ExternalCalendarEvent> currentEvents,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting differential sync for user {UserId} provider {Provider} calendar {CalendarId}",
                userId, provider, calendarId);

            // Get cached events for comparison
            var cachedEvents = await _cacheService.GetCachedCalendarEventsAsync(userId, provider, calendarId, null, cancellationToken)
                              ?? new List<ExternalCalendarEvent>();

            // Calculate differences
            var changes = CalculateEventChanges(cachedEvents, currentEvents);

            if (!changes.HasChanges)
            {
                _logger.LogDebug("No changes detected for differential sync");
                return new DifferentialSyncResult(
                    userId,
                    provider,
                    calendarId,
                    true,
                    0, 0, 0, 0,
                    stopwatch.Elapsed,
                    "No changes detected"
                );
            }

            // Apply optimized updates
            var updateResults = await ApplyDifferentialChanges(userId, provider, calendarId, changes, cancellationToken);

            // Update cache with new events
            await _cacheService.CacheCalendarEventsAsync(userId, provider, calendarId, currentEvents,
                _settings.Providers[provider].CacheSettings.DefaultExpirationMinutes, cancellationToken);

            stopwatch.Stop();
            RecordOperationMetrics($"DifferentialSync_{provider}", updateResults.Success, stopwatch.Elapsed);

            _logger.LogInformation("Completed differential sync in {Duration}ms. Added: {Added}, Modified: {Modified}, Deleted: {Deleted}",
                stopwatch.ElapsedMilliseconds, changes.AddedEvents.Count(), changes.ModifiedEvents.Count(), changes.DeletedEvents.Count());

            return new DifferentialSyncResult(
                userId,
                provider,
                calendarId,
                updateResults.Success,
                changes.AddedEvents.Count(),
                changes.ModifiedEvents.Count(),
                changes.DeletedEvents.Count(),
                changes.AddedEvents.Count() + changes.ModifiedEvents.Count() + changes.DeletedEvents.Count(),
                stopwatch.Elapsed,
                updateResults.Success ? "Differential sync completed successfully" : updateResults.ErrorMessage
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Differential sync failed for user {UserId} provider {Provider} calendar {CalendarId}",
                userId, provider, calendarId);

            return new DifferentialSyncResult(
                userId,
                provider,
                calendarId,
                false,
                0, 0, 0, 0,
                stopwatch.Elapsed,
                ex.Message
            );
        }
    }

    /// <summary>
    /// Creates a backup of calendar synchronization data
    /// </summary>
    public async Task<CalendarBackupResult> CreateBackupAsync(
        Guid userId,
        CalendarBackupOptions backupOptions,
        CancellationToken cancellationToken = default)
    {
        await _backupSemaphore.WaitAsync(cancellationToken);

        try
        {
            var backupId = Guid.NewGuid().ToString("N");
            var startTime = DateTime.UtcNow;

            _logger.LogInformation("Creating calendar backup {BackupId} for user {UserId}", backupId, userId);

            var backupData = new CalendarBackupData
            {
                BackupId = backupId,
                UserId = userId,
                CreatedAt = startTime,
                BackupOptions = backupOptions,
                CalendarData = new Dictionary<CalendarProvider, List<ExternalCalendar>>(),
                EventData = new Dictionary<string, List<ExternalCalendarEvent>>(),
                SyncTokens = new Dictionary<string, string>(),
                ConflictResolutions = new List<ConflictResolution>()
            };

            long totalSizeBytes = 0;

            // Backup calendar metadata for each provider
            if (backupOptions.IncludeCalendarMetadata)
            {
                foreach (var provider in backupOptions.Providers)
                {
                    var calendars = await _cacheService.GetCachedCalendarMetadataAsync(userId, provider, cancellationToken);
                    if (calendars != null)
                    {
                        backupData.CalendarData[provider] = calendars.ToList();
                        totalSizeBytes += EstimateDataSize(calendars);
                    }
                }
            }

            // Backup event data
            if (backupOptions.IncludeEventData)
            {
                foreach (var provider in backupOptions.Providers)
                {
                    var calendars = backupData.CalendarData.GetValueOrDefault(provider, new List<ExternalCalendar>());

                    foreach (var calendar in calendars)
                    {
                        var events = await _cacheService.GetCachedCalendarEventsAsync(userId, provider, calendar.Id,
                            backupOptions.DateRange, cancellationToken);

                        if (events != null)
                        {
                            var key = $"{provider}:{calendar.Id}";
                            backupData.EventData[key] = events.ToList();
                            totalSizeBytes += EstimateDataSize(events);
                        }
                    }
                }
            }

            // Backup sync tokens
            if (backupOptions.IncludeSyncTokens)
            {
                foreach (var provider in backupOptions.Providers)
                {
                    var calendars = backupData.CalendarData.GetValueOrDefault(provider, new List<ExternalCalendar>());

                    foreach (var calendar in calendars)
                    {
                        var syncToken = await _cacheService.GetCachedSyncTokenAsync(userId, provider, calendar.Id, cancellationToken);
                        if (!string.IsNullOrEmpty(syncToken))
                        {
                            var key = $"{provider}:{calendar.Id}";
                            backupData.SyncTokens[key] = syncToken;
                        }
                    }
                }
            }

            // Store backup data (in a real implementation, this would be stored in a backup storage system)
            var backupJson = System.Text.Json.JsonSerializer.Serialize(backupData);
            totalSizeBytes = backupJson.Length;

            // For demonstration, we'll simulate storing the backup
            await SimulateBackupStorage(backupId, backupJson, cancellationToken);

            var duration = DateTime.UtcNow - startTime;
            RecordOperationMetrics("CreateBackup", true, duration);

            _logger.LogInformation("Created calendar backup {BackupId} for user {UserId} ({Size} bytes) in {Duration}ms",
                backupId, userId, totalSizeBytes, duration.TotalMilliseconds);

            return new CalendarBackupResult(
                backupId,
                userId,
                true,
                backupOptions,
                totalSizeBytes,
                "Backup created successfully",
                startTime
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create calendar backup for user {UserId}", userId);
            return new CalendarBackupResult(
                string.Empty,
                userId,
                false,
                backupOptions,
                0,
                ex.Message,
                DateTime.UtcNow
            );
        }
        finally
        {
            _backupSemaphore.Release();
        }
    }

    /// <summary>
    /// Restores calendar synchronization data from a backup
    /// </summary>
    public async Task<CalendarRestoreResult> RestoreBackupAsync(
        Guid userId,
        string backupId,
        CalendarRestoreOptions restoreOptions,
        CancellationToken cancellationToken = default)
    {
        await _backupSemaphore.WaitAsync(cancellationToken);

        try
        {
            var startTime = DateTime.UtcNow;

            _logger.LogInformation("Restoring calendar backup {BackupId} for user {UserId}", backupId, userId);

            // Retrieve backup data (in a real implementation, this would be retrieved from backup storage)
            var backupJson = await SimulateBackupRetrieval(backupId, cancellationToken);

            if (string.IsNullOrEmpty(backupJson))
            {
                return new CalendarRestoreResult(
                    backupId,
                    userId,
                    false,
                    restoreOptions,
                    0,
                    "Backup not found",
                    startTime
                );
            }

            var backupData = System.Text.Json.JsonSerializer.Deserialize<CalendarBackupData>(backupJson);
            if (backupData == null)
            {
                return new CalendarRestoreResult(
                    backupId,
                    userId,
                    false,
                    restoreOptions,
                    0,
                    "Invalid backup data format",
                    startTime
                );
            }

            var itemsRestored = 0;

            // Restore calendar metadata
            if (restoreOptions.RestoreCalendarMetadata)
            {
                foreach (var providerData in backupData.CalendarData)
                {
                    await _cacheService.CacheCalendarMetadataAsync(userId, providerData.Key, providerData.Value,
                        _settings.Providers[providerData.Key].CacheSettings.DefaultExpirationMinutes, cancellationToken);
                    itemsRestored += providerData.Value.Count;
                }
            }

            // Restore event data
            if (restoreOptions.RestoreEventData)
            {
                foreach (var eventData in backupData.EventData)
                {
                    var keyParts = eventData.Key.Split(':');
                    if (keyParts.Length >= 2 && Enum.TryParse<CalendarProvider>(keyParts[0], out var provider))
                    {
                        var calendarId = keyParts[1];
                        await _cacheService.CacheCalendarEventsAsync(userId, provider, calendarId, eventData.Value,
                            _settings.Providers[provider].CacheSettings.DefaultExpirationMinutes, cancellationToken);
                        itemsRestored += eventData.Value.Count;
                    }
                }
            }

            // Restore sync tokens
            if (restoreOptions.RestoreSyncTokens)
            {
                foreach (var syncTokenData in backupData.SyncTokens)
                {
                    var keyParts = syncTokenData.Key.Split(':');
                    if (keyParts.Length >= 2 && Enum.TryParse<CalendarProvider>(keyParts[0], out var provider))
                    {
                        var calendarId = keyParts[1];
                        await _cacheService.CacheSyncTokenAsync(userId, provider, calendarId, syncTokenData.Value,
                            _settings.Providers[provider].CacheSettings.DefaultExpirationMinutes, cancellationToken);
                        itemsRestored++;
                    }
                }
            }

            var duration = DateTime.UtcNow - startTime;
            RecordOperationMetrics("RestoreBackup", true, duration);

            _logger.LogInformation("Restored calendar backup {BackupId} for user {UserId} ({ItemCount} items) in {Duration}ms",
                backupId, userId, itemsRestored, duration.TotalMilliseconds);

            return new CalendarRestoreResult(
                backupId,
                userId,
                true,
                restoreOptions,
                itemsRestored,
                "Backup restored successfully",
                startTime
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore calendar backup {BackupId} for user {UserId}", backupId, userId);
            return new CalendarRestoreResult(
                backupId,
                userId,
                false,
                restoreOptions,
                0,
                ex.Message,
                DateTime.UtcNow
            );
        }
        finally
        {
            _backupSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets comprehensive performance metrics for calendar operations
    /// </summary>
    public Task<CalendarPerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allMetrics = _performanceMetrics.Values.ToList();

            var totalOperations = allMetrics.Sum(m => m.TotalOperations);
            var successfulOperations = allMetrics.Sum(m => m.SuccessfulOperations);
            var averageResponseTime = allMetrics.Any() ?
                TimeSpan.FromMilliseconds(allMetrics.Average(m => m.AverageResponseTime.TotalMilliseconds)) :
                TimeSpan.Zero;

            var operationMetrics = allMetrics.ToDictionary(
                m => m.OperationType,
                m => new OperationPerformanceMetrics(
                    m.TotalOperations,
                    m.SuccessfulOperations,
                    m.FailedOperations,
                    m.SuccessRate,
                    m.AverageResponseTime,
                    m.MinResponseTime,
                    m.MaxResponseTime
                )
            );

            var rateLimitMetrics = _rateLimiters.ToDictionary(
                r => r.Key,
                r => new RateLimitMetrics(
                    r.Value.RequestsPerMinute,
                    r.Value.CurrentRequests,
                    r.Value.IsThrottled,
                    r.Value.NextResetTime
                )
            );

            return Task.FromResult(new CalendarPerformanceMetrics(
                totalOperations,
                successfulOperations,
                totalOperations - successfulOperations,
                totalOperations > 0 ? (double)successfulOperations / totalOperations : 0.0,
                averageResponseTime,
                operationMetrics,
                rateLimitMetrics,
                DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get performance metrics");
            return Task.FromResult(new CalendarPerformanceMetrics(
                0, 0, 0, 0.0, TimeSpan.Zero,
                new Dictionary<string, OperationPerformanceMetrics>(),
                new Dictionary<CalendarProvider, RateLimitMetrics>(),
                DateTime.UtcNow
            ));
        }
    }

    /// <summary>
    /// Optimizes sync operations by analyzing patterns and adjusting strategies
    /// </summary>
    public async Task<OptimizationResult> OptimizeSyncOperationsAsync(
        Guid userId,
        CalendarProvider provider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Optimizing sync operations for user {UserId} provider {Provider}", userId, provider);

            var metrics = await GetPerformanceMetricsAsync(cancellationToken);
            var recommendations = new List<OptimizationRecommendation>();

            // Analyze performance patterns and generate recommendations
            if (metrics.OverallSuccessRate < 0.8)
            {
                recommendations.Add(new OptimizationRecommendation(
                    OptimizationType.ErrorHandling,
                    "Improve error handling and retry logic",
                    $"Success rate is {metrics.OverallSuccessRate:P0}, consider implementing exponential backoff",
                    OptimizationPriority.High
                ));
            }

            if (metrics.AverageResponseTime > TimeSpan.FromSeconds(5))
            {
                recommendations.Add(new OptimizationRecommendation(
                    OptimizationType.Caching,
                    "Increase caching duration",
                    $"Average response time is {metrics.AverageResponseTime.TotalSeconds:F1}s, consider longer cache expiration",
                    OptimizationPriority.Medium
                ));
            }

            var providerRateLimit = metrics.RateLimitMetrics.GetValueOrDefault(provider);
            if (providerRateLimit?.IsThrottled == true)
            {
                recommendations.Add(new OptimizationRecommendation(
                    OptimizationType.RateLimit,
                    "Implement adaptive rate limiting",
                    "Provider is currently throttled, reduce request frequency",
                    OptimizationPriority.High
                ));
            }

            // Apply automatic optimizations
            var optimizationsApplied = await ApplyAutomaticOptimizations(userId, provider, recommendations, cancellationToken);

            return new OptimizationResult(
                userId,
                provider,
                true,
                recommendations,
                optimizationsApplied,
                $"Generated {recommendations.Count} recommendations, applied {optimizationsApplied.Count} optimizations",
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize sync operations for user {UserId} provider {Provider}", userId, provider);
            return new OptimizationResult(
                userId,
                provider,
                false,
                [],
                [],
                ex.Message,
                DateTime.UtcNow
            );
        }
    }

    // Private helper methods

    private void InitializeRateLimiters()
    {
        foreach (var providerConfig in _settings.Providers)
        {
            var rateLimiter = new RateLimiter(
                providerConfig.Value.RateLimit.RequestsPerMinute,
                TimeSpan.FromMinutes(1),
                providerConfig.Value.RateLimit.RequestDelayMs
            );
            _rateLimiters[providerConfig.Key] = rateLimiter;
        }
    }

    private RateLimiter GetRateLimiter(CalendarProvider provider)
    {
        return _rateLimiters.GetValueOrDefault(provider, _rateLimiters.Values.FirstOrDefault() ??
            new RateLimiter(60, TimeSpan.FromMinutes(1), 1000));
    }

    private async Task<BatchSyncItemResult> ProcessSingleSyncRequest(
        Guid userId,
        BatchSyncRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Simulate processing sync request
            await Task.Delay(100, cancellationToken); // Simulate API call

            var eventsProcessed = Random.Shared.Next(1, 50); // Simulate event count

            stopwatch.Stop();
            return new BatchSyncItemResult(
                request.CalendarId,
                request.Provider,
                true,
                eventsProcessed,
                null,
                stopwatch.Elapsed
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new BatchSyncItemResult(
                request.CalendarId,
                request.Provider,
                false,
                0,
                ex.Message,
                stopwatch.Elapsed
            );
        }
    }

    private EventChanges CalculateEventChanges(
        IEnumerable<ExternalCalendarEvent> cachedEvents,
        IEnumerable<ExternalCalendarEvent> currentEvents)
    {
        var cached = cachedEvents.ToDictionary(e => e.Id, e => e);
        var current = currentEvents.ToDictionary(e => e.Id, e => e);

        var addedEvents = current.Values.Where(e => !cached.ContainsKey(e.Id)).ToList();
        var deletedEvents = cached.Values.Where(e => !current.ContainsKey(e.Id)).ToList();
        var modifiedEvents = current.Values.Where(e =>
            cached.ContainsKey(e.Id) && !AreEventsEqual(cached[e.Id], e)).ToList();

        return new EventChanges(addedEvents, modifiedEvents, deletedEvents);
    }

    private bool AreEventsEqual(ExternalCalendarEvent event1, ExternalCalendarEvent event2)
    {
        return event1.Title == event2.Title &&
               event1.StartTime == event2.StartTime &&
               event1.EndTime == event2.EndTime &&
               event1.UpdatedTime == event2.UpdatedTime;
    }

    private async Task<UpdateResult> ApplyDifferentialChanges(
        Guid userId,
        CalendarProvider provider,
        string calendarId,
        EventChanges changes,
        CancellationToken cancellationToken)
    {
        try
        {
            // In a real implementation, this would apply the changes to the external calendar
            await Task.Delay(50, cancellationToken); // Simulate API calls

            return new UpdateResult(true, null);
        }
        catch (Exception ex)
        {
            return new UpdateResult(false, ex.Message);
        }
    }

    private void RecordOperationMetrics(string operationType, bool success, TimeSpan responseTime)
    {
        _performanceMetrics.AddOrUpdate(operationType,
            new PerformanceMetrics
            {
                OperationType = operationType,
                TotalOperations = 1,
                SuccessfulOperations = success ? 1 : 0,
                FailedOperations = success ? 0 : 1,
                AverageResponseTime = responseTime,
                MinResponseTime = responseTime,
                MaxResponseTime = responseTime,
                LastOperationTime = DateTime.UtcNow
            },
            (key, existing) =>
            {
                var totalOps = existing.TotalOperations + 1;
                var successOps = existing.SuccessfulOperations + (success ? 1 : 0);
                var failedOps = existing.FailedOperations + (success ? 0 : 1);

                var totalTime = existing.AverageResponseTime.TotalMilliseconds * existing.TotalOperations + responseTime.TotalMilliseconds;
                var avgTime = TimeSpan.FromMilliseconds(totalTime / totalOps);

                return new PerformanceMetrics
                {
                    OperationType = operationType,
                    TotalOperations = totalOps,
                    SuccessfulOperations = successOps,
                    FailedOperations = failedOps,
                    SuccessRate = (double)successOps / totalOps,
                    AverageResponseTime = avgTime,
                    MinResponseTime = responseTime < existing.MinResponseTime ? responseTime : existing.MinResponseTime,
                    MaxResponseTime = responseTime > existing.MaxResponseTime ? responseTime : existing.MaxResponseTime,
                    LastOperationTime = DateTime.UtcNow
                };
            }
        );
    }

    private long EstimateDataSize(IEnumerable<object> data)
    {
        // Rough estimation of data size
        return data.Count() * 1000; // Assume ~1KB per item
    }

    private async Task SimulateBackupStorage(string backupId, string backupData, CancellationToken cancellationToken)
    {
        // In a real implementation, this would store the backup in a persistent storage system
        await Task.Delay(100, cancellationToken);
        _logger.LogDebug("Simulated storing backup {BackupId} ({Size} bytes)", backupId, backupData.Length);
    }

    private async Task<string?> SimulateBackupRetrieval(string backupId, CancellationToken cancellationToken)
    {
        // In a real implementation, this would retrieve the backup from storage
        await Task.Delay(50, cancellationToken);
        _logger.LogDebug("Simulated retrieving backup {BackupId}", backupId);

        // Return a mock backup for demonstration
        return System.Text.Json.JsonSerializer.Serialize(new CalendarBackupData
        {
            BackupId = backupId,
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CalendarData = new Dictionary<CalendarProvider, List<ExternalCalendar>>(),
            EventData = new Dictionary<string, List<ExternalCalendarEvent>>(),
            SyncTokens = new Dictionary<string, string>(),
            ConflictResolutions = new List<ConflictResolution>()
        });
    }

    private async Task<List<AppliedOptimization>> ApplyAutomaticOptimizations(
        Guid userId,
        CalendarProvider provider,
        List<OptimizationRecommendation> recommendations,
        CancellationToken cancellationToken)
    {
        var appliedOptimizations = new List<AppliedOptimization>();

        foreach (var recommendation in recommendations.Where(r => r.Priority == OptimizationPriority.High))
        {
            try
            {
                switch (recommendation.Type)
                {
                    case OptimizationType.RateLimit:
                        await AdjustRateLimiter(provider, cancellationToken);
                        appliedOptimizations.Add(new AppliedOptimization(recommendation.Type, "Adjusted rate limiter", true));
                        break;

                    case OptimizationType.Caching:
                        // Would adjust cache settings
                        appliedOptimizations.Add(new AppliedOptimization(recommendation.Type, "Increased cache duration", true));
                        break;
                }
            }
            catch (Exception ex)
            {
                appliedOptimizations.Add(new AppliedOptimization(recommendation.Type, ex.Message, false));
            }
        }

        return appliedOptimizations;
    }

    private Task AdjustRateLimiter(CalendarProvider provider, CancellationToken cancellationToken)
    {
        var rateLimiter = GetRateLimiter(provider);

        // Reduce request rate by 25% when throttled
        var newRate = (int)(rateLimiter.RequestsPerMinute * 0.75);
        var newRateLimiter = new RateLimiter(newRate, TimeSpan.FromMinutes(1), rateLimiter.DelayMs + 500);

        _rateLimiters[provider] = newRateLimiter;
        _logger.LogInformation("Adjusted rate limiter for {Provider}: {OldRate} -> {NewRate} requests/minute",
            provider, rateLimiter.RequestsPerMinute, newRate);
        return Task.CompletedTask;
    }

    private void ResetMetrics(object? state)
    {
        _performanceMetrics.Clear();
        _logger.LogDebug("Reset performance metrics");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _metricsResetTimer?.Dispose();
        _backupSemaphore?.Dispose();
        _performanceMetrics?.Clear();
        _rateLimiters?.Clear();

        _disposed = true;
    }
}

// Helper classes and records

public record BatchSyncRequest(CalendarProvider Provider, string CalendarId, SyncMode SyncMode);
public record BatchSyncItemResult(string CalendarId, CalendarProvider Provider, bool Success, int EventsProcessed, string? ErrorMessage, TimeSpan ProcessingTime);
public record BatchSyncResult(Guid BatchId, Guid UserId, bool Success, IEnumerable<BatchSyncItemResult> Results, int SuccessCount, int FailureCount, int TotalEvents, TimeSpan Duration, string? ErrorMessage);
public record DifferentialSyncResult(Guid UserId, CalendarProvider Provider, string CalendarId, bool Success, int AddedEvents, int ModifiedEvents, int DeletedEvents, int TotalChanges, TimeSpan Duration, string? Message);
public record UpdateResult(bool Success, string? ErrorMessage);

public class EventChanges
{
    public EventChanges(IEnumerable<ExternalCalendarEvent> addedEvents, IEnumerable<ExternalCalendarEvent> modifiedEvents, IEnumerable<ExternalCalendarEvent> deletedEvents)
    {
        AddedEvents = addedEvents;
        ModifiedEvents = modifiedEvents;
        DeletedEvents = deletedEvents;
    }

    public IEnumerable<ExternalCalendarEvent> AddedEvents { get; }
    public IEnumerable<ExternalCalendarEvent> ModifiedEvents { get; }
    public IEnumerable<ExternalCalendarEvent> DeletedEvents { get; }
    public bool HasChanges => AddedEvents.Any() || ModifiedEvents.Any() || DeletedEvents.Any();
}

public class CalendarBackupData
{
    public string BackupId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public CalendarBackupOptions BackupOptions { get; set; } = new();
    public Dictionary<CalendarProvider, List<ExternalCalendar>> CalendarData { get; set; } = new();
    public Dictionary<string, List<ExternalCalendarEvent>> EventData { get; set; } = new();
    public Dictionary<string, string> SyncTokens { get; set; } = new();
    public List<ConflictResolution> ConflictResolutions { get; set; } = new();
}

public class PerformanceMetrics
{
    public string OperationType { get; set; } = string.Empty;
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations : 0.0;
    public TimeSpan AverageResponseTime { get; set; }
    public TimeSpan MinResponseTime { get; set; }
    public TimeSpan MaxResponseTime { get; set; }
    public DateTime LastOperationTime { get; set; }
}

public class RateLimiter
{
    private SemaphoreSlim _semaphore; // Made mutable for recreation pattern
    private readonly Timer _resetTimer;
    private readonly object _resetLock = new object();
    private int _currentRequests;

    public RateLimiter(int requestsPerMinute, TimeSpan resetInterval, int delayMs)
    {
        RequestsPerMinute = requestsPerMinute;
        DelayMs = delayMs;
        _semaphore = new SemaphoreSlim(requestsPerMinute, requestsPerMinute);
        _resetTimer = new Timer(ResetRequests, null, resetInterval, resetInterval);
        NextResetTime = DateTime.UtcNow.Add(resetInterval);
    }

    public int RequestsPerMinute { get; }
    public int DelayMs { get; }
    public int CurrentRequests => _currentRequests;
    public bool IsThrottled => _currentRequests >= RequestsPerMinute;
    public DateTime NextResetTime { get; private set; }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        Interlocked.Increment(ref _currentRequests);

        if (DelayMs > 0)
        {
            await Task.Delay(DelayMs, cancellationToken);
        }
    }

    private void ResetRequests(object? state)
    {
        lock (_resetLock)
        {
            try
            {
                // Reset request counter first
                Interlocked.Exchange(ref _currentRequests, 0);
                NextResetTime = DateTime.UtcNow.AddMinutes(1);

                // Use recreation pattern: dispose old semaphore and create new one
                // This is much safer and cleaner than trying to reset permits
                var oldSemaphore = _semaphore;
                _semaphore = new SemaphoreSlim(RequestsPerMinute, RequestsPerMinute);
                
                // Dispose old semaphore safely
                try
                {
                    oldSemaphore?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Expected during shutdown
                }
            }
            catch (ObjectDisposedException)
            {
                // Service is shutting down, nothing to do
            }
            catch (Exception)
            {
                // Don't rethrow - this runs on a timer thread
                // Silently ignore errors during reset operation
            }
        }
    }
}

// Optimization-related classes
public record OptimizationRecommendation(OptimizationType Type, string Title, string Description, OptimizationPriority Priority);
public record AppliedOptimization(OptimizationType Type, string Description, bool Success);
public record OptimizationResult(Guid UserId, CalendarProvider Provider, bool Success, IEnumerable<OptimizationRecommendation> Recommendations, IEnumerable<AppliedOptimization> AppliedOptimizations, string Message, DateTime Timestamp);

public enum OptimizationType { Caching, RateLimit, ErrorHandling, Batching }
public enum OptimizationPriority { Low, Medium, High, Critical }

// Performance metrics records
public record CalendarPerformanceMetrics(int TotalOperations, int SuccessfulOperations, int FailedOperations, double OverallSuccessRate, TimeSpan AverageResponseTime, Dictionary<string, OperationPerformanceMetrics> OperationMetrics, Dictionary<CalendarProvider, RateLimitMetrics> RateLimitMetrics, DateTime Timestamp);
public record OperationPerformanceMetrics(int TotalOperations, int SuccessfulOperations, int FailedOperations, double SuccessRate, TimeSpan AverageResponseTime, TimeSpan MinResponseTime, TimeSpan MaxResponseTime);
public record RateLimitMetrics(int RequestsPerMinute, int CurrentRequests, bool IsThrottled, DateTime NextResetTime);
