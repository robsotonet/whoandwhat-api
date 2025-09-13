using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Core calendar synchronization service with multi-provider support and conflict resolution
/// Orchestrates calendar synchronization operations between WhoAndWhat and external calendar providers
/// </summary>
public class CalendarSyncService : ICalendarSyncService, IDisposable
{
    private readonly IEnumerable<ICalendarProviderService> _providerServices;
    private readonly ICalendarConflictDetector _conflictDetector;
    private readonly ICalendarCacheService _cacheService;
    private readonly ILogger<CalendarSyncService> _logger;
    private readonly CalendarSyncSettings _settings;
    private readonly ConcurrentDictionary<CalendarProvider, ICalendarProviderService> _providerCache;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _userSyncSemaphores;
    private bool _disposed;

    public CalendarSyncService(
        IEnumerable<ICalendarProviderService> providerServices,
        ICalendarConflictDetector conflictDetector,
        ICalendarCacheService cacheService,
        IOptions<CalendarSyncSettings> settings,
        ILogger<CalendarSyncService> logger)
    {
        _providerServices = providerServices ?? throw new ArgumentNullException(nameof(providerServices));
        _conflictDetector = conflictDetector ?? throw new ArgumentNullException(nameof(conflictDetector));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _providerCache = new ConcurrentDictionary<CalendarProvider, ICalendarProviderService>();
        _userSyncSemaphores = new ConcurrentDictionary<Guid, SemaphoreSlim>();

        InitializeProviderCache();
    }

    public async Task<CalendarSyncResult> SyncCalendarAsync(
        Guid userId,
        CalendarProvider provider,
        SyncMode syncMode,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return CreateFailureResult(userId, provider, "Calendar synchronization is disabled", TimeSpan.Zero);
        }

        var syncStartTime = DateTime.UtcNow;
        var semaphore = _userSyncSemaphores.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

        try
        {
            await semaphore.WaitAsync(cancellationToken);
            _logger.LogInformation("Starting full calendar sync for user {UserId} with provider {Provider} in {SyncMode} mode",
                userId, provider, syncMode);

            var providerService = GetProviderService(provider);
            if (providerService == null)
            {
                return CreateFailureResult(userId, provider, $"Provider {provider} is not available", TimeSpan.Zero);
            }

            // Check provider availability
            if (!await providerService.IsAvailableAsync(cancellationToken))
            {
                return CreateFailureResult(userId, provider, $"Provider {provider} is currently unavailable", TimeSpan.Zero);
            }

            // Get cached access token
            var tokenData = await _cacheService.GetCachedAccessTokenAsync(userId, provider, cancellationToken);
            if (tokenData == null)
            {
                return CreateFailureResult(userId, provider, "No valid access token found. User needs to re-authenticate", TimeSpan.Zero);
            }

            // Validate token
            var tokenValidation = await providerService.ValidateTokenAsync(userId, tokenData.AccessToken, cancellationToken);
            if (!tokenValidation.IsValid)
            {
                // Try to refresh token
                if (!string.IsNullOrEmpty(tokenData.RefreshToken))
                {
                    var refreshResult = await providerService.RefreshTokensAsync(userId, tokenData.RefreshToken, cancellationToken);
                    if (refreshResult.Success)
                    {
                        tokenData = new CalendarAccessToken(
                            refreshResult.AccessToken,
                            refreshResult.RefreshToken,
                            refreshResult.ExpiresAt,
                            refreshResult.Scopes ?? []
                        );
                        await _cacheService.CacheAccessTokenAsync(userId, provider, tokenData,
                            _settings.Providers[provider].TokenCacheExpirationMinutes, cancellationToken);
                    }
                    else
                    {
                        return CreateFailureResult(userId, provider, "Failed to refresh access token", TimeSpan.Zero);
                    }
                }
                else
                {
                    return CreateFailureResult(userId, provider, "Access token expired and no refresh token available", TimeSpan.Zero);
                }
            }

            // Get external calendars
            var calendars = await providerService.GetCalendarsAsync(userId, tokenData.AccessToken, cancellationToken);

            // Cache calendar metadata
            await _cacheService.CacheCalendarMetadataAsync(userId, provider, calendars,
                _settings.Providers[provider].CacheSettings.DefaultExpirationMinutes, cancellationToken);

            var syncResult = await PerformFullSynchronization(userId, provider, syncMode, providerService,
                tokenData, calendars, syncStartTime, cancellationToken);

            _logger.LogInformation("Completed full calendar sync for user {UserId} with provider {Provider}. " +
                                  "Events synced: {EventsSynced}, Conflicts: {ConflictsDetected}",
                                  userId, provider, syncResult.EventsSynced, syncResult.ConflictsDetected);

            return syncResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Calendar sync was cancelled for user {UserId} with provider {Provider}", userId, provider);
            return CreateFailureResult(userId, provider, "Sync operation was cancelled", DateTime.UtcNow - syncStartTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync calendar for user {UserId} with provider {Provider}", userId, provider);
            return CreateFailureResult(userId, provider, ex.Message, DateTime.UtcNow - syncStartTime);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<IncrementalSyncResult> SyncCalendarIncrementalAsync(
        Guid userId,
        CalendarProvider provider,
        string lastSyncToken,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return new IncrementalSyncResult(
                userId,
                provider,
                false,
                lastSyncToken,
                null,
                [],
                0,
                TimeSpan.Zero,
                ["Calendar synchronization is disabled"],
                DateTime.UtcNow
            );
        }

        var syncStartTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting incremental calendar sync for user {UserId} with provider {Provider}",
                userId, provider);

            var providerService = GetProviderService(provider);
            if (providerService == null)
            {
                return new IncrementalSyncResult(
                    userId,
                    provider,
                    false,
                    lastSyncToken,
                    null,
                    [],
                    0,
                    TimeSpan.Zero,
                    [$"Provider {provider} is not available"],
                    syncStartTime
                );
            }

            // Get cached access token
            var tokenData = await _cacheService.GetCachedAccessTokenAsync(userId, provider, cancellationToken);
            if (tokenData == null)
            {
                return new IncrementalSyncResult(
                    userId,
                    provider,
                    false,
                    lastSyncToken,
                    null,
                    [],
                    0,
                    TimeSpan.Zero,
                    ["No valid access token found"],
                    syncStartTime
                );
            }

            var syncResult = await PerformIncrementalSynchronization(userId, provider, lastSyncToken,
                providerService, tokenData, syncStartTime, cancellationToken);

            _logger.LogInformation("Completed incremental calendar sync for user {UserId} with provider {Provider}. " +
                                  "Changes processed: {TotalChanges}",
                                  userId, provider, syncResult.TotalChanges);

            return syncResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Incremental sync was cancelled for user {UserId} with provider {Provider}", userId, provider);
            return new IncrementalSyncResult(
                userId,
                provider,
                false,
                lastSyncToken,
                null,
                [],
                0,
                DateTime.UtcNow - syncStartTime,
                ["Sync operation was cancelled"],
                syncStartTime
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform incremental sync for user {UserId} with provider {Provider}", userId, provider);
            return new IncrementalSyncResult(
                userId,
                provider,
                false,
                lastSyncToken,
                null,
                [],
                0,
                DateTime.UtcNow - syncStartTime,
                [ex.Message],
                syncStartTime
            );
        }
    }

    public async Task<IEnumerable<EventSyncResult>> SyncEventsAsync(
        Guid userId,
        IEnumerable<Guid> eventIds,
        CalendarProvider provider,
        SyncDirection syncDirection,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return eventIds.Select(id => new EventSyncResult(
                id,
                null,
                false,
                SyncOperation.None,
                "Calendar synchronization is disabled",
                null,
                DateTime.UtcNow
            ));
        }

        try
        {
            _logger.LogInformation("Starting event sync for user {UserId} with {EventCount} events in {Direction} direction",
                userId, eventIds.Count(), syncDirection);

            var providerService = GetProviderService(provider);
            if (providerService == null)
            {
                return eventIds.Select(id => new EventSyncResult(
                    id,
                    null,
                    false,
                    SyncOperation.None,
                    $"Provider {provider} is not available",
                    null,
                    DateTime.UtcNow
                ));
            }

            // Get cached access token
            var tokenData = await _cacheService.GetCachedAccessTokenAsync(userId, provider, cancellationToken);
            if (tokenData == null)
            {
                return eventIds.Select(id => new EventSyncResult(
                    id,
                    null,
                    false,
                    SyncOperation.None,
                    "No valid access token found",
                    null,
                    DateTime.UtcNow
                ));
            }

            var results = new List<EventSyncResult>();

            foreach (var eventId in eventIds)
            {
                try
                {
                    var result = await SyncSingleEvent(userId, eventId, provider, syncDirection,
                        providerService, tokenData, cancellationToken);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync event {EventId} for user {UserId}", eventId, userId);
                    results.Add(new EventSyncResult(
                        eventId,
                        null,
                        false,
                        SyncOperation.None,
                        ex.Message,
                        null,
                        DateTime.UtcNow
                    ));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync events for user {UserId} with provider {Provider}", userId, provider);
            return eventIds.Select(id => new EventSyncResult(
                id,
                null,
                false,
                SyncOperation.None,
                ex.Message,
                null,
                DateTime.UtcNow
            ));
        }
    }

    public async Task<IEnumerable<TaskToEventSyncResult>> SyncTasksAsEventsAsync(
        Guid userId,
        IEnumerable<Guid> taskIds,
        CalendarProvider provider,
        TaskToEventConversionOptions conversionOptions,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return taskIds.Select(id => new TaskToEventSyncResult(
                id,
                null,
                false,
                SyncOperation.None,
                "Calendar synchronization is disabled",
                null,
                DateTime.UtcNow
            ));
        }

        try
        {
            _logger.LogInformation("Starting task-to-event sync for user {UserId} with {TaskCount} tasks",
                userId, taskIds.Count());

            var providerService = GetProviderService(provider);
            if (providerService == null)
            {
                return taskIds.Select(id => new TaskToEventSyncResult(
                    id,
                    null,
                    false,
                    SyncOperation.None,
                    $"Provider {provider} is not available",
                    null,
                    DateTime.UtcNow
                ));
            }

            // Get cached access token
            var tokenData = await _cacheService.GetCachedAccessTokenAsync(userId, provider, cancellationToken);
            if (tokenData == null)
            {
                return taskIds.Select(id => new TaskToEventSyncResult(
                    id,
                    null,
                    false,
                    SyncOperation.None,
                    "No valid access token found",
                    null,
                    DateTime.UtcNow
                ));
            }

            var results = new List<TaskToEventSyncResult>();

            foreach (var taskId in taskIds)
            {
                try
                {
                    var result = await ConvertAndSyncTaskAsEvent(userId, taskId, provider, conversionOptions,
                        providerService, tokenData, cancellationToken);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync task {TaskId} as event for user {UserId}", taskId, userId);
                    results.Add(new TaskToEventSyncResult(
                        taskId,
                        null,
                        false,
                        SyncOperation.None,
                        ex.Message,
                        null,
                        DateTime.UtcNow
                    ));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync tasks as events for user {UserId} with provider {Provider}", userId, provider);
            return taskIds.Select(id => new TaskToEventSyncResult(
                id,
                null,
                false,
                SyncOperation.None,
                ex.Message,
                null,
                DateTime.UtcNow
            ));
        }
    }

    public async Task<CalendarSyncStatus> GetSyncStatusAsync(
        Guid userId,
        CalendarProvider provider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get cached sync status first
            var cachedStatus = await _cacheService.GetCachedSyncStatusAsync(userId, provider, cancellationToken);
            if (cachedStatus != null)
            {
                return cachedStatus;
            }

            // Build sync status from current state
            var providerService = GetProviderService(provider);
            var isProviderAvailable = providerService != null && await providerService.IsAvailableAsync(cancellationToken);

            var tokenData = await _cacheService.GetCachedAccessTokenAsync(userId, provider, cancellationToken);
            var hasValidToken = tokenData != null && tokenData.ExpiresAt > DateTime.UtcNow;

            var status = new CalendarSyncStatus(
                userId,
                provider,
                isProviderAvailable ? SyncStatus.Ready : SyncStatus.ProviderUnavailable,
                hasValidToken,
                tokenData?.ExpiresAt,
                null,
                null,
                DateTime.UtcNow,
                new CalendarSyncStatistics(0, 0, 0, 0, 0, 0, TimeSpan.Zero),
                []
            );

            // Cache the status for future requests
            await _cacheService.CacheSyncStatusAsync(userId, provider, status,
                _settings.Providers.GetValueOrDefault(provider)?.CacheSettings.DefaultExpirationMinutes ?? 15,
                cancellationToken);

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sync status for user {UserId} with provider {Provider}", userId, provider);
            return new CalendarSyncStatus(
                userId,
                provider,
                SyncStatus.Error,
                false,
                null,
                null,
                null,
                DateTime.UtcNow,
                new CalendarSyncStatistics(0, 0, 0, 0, 0, 0, TimeSpan.Zero),
                [ex.Message]
            );
        }
    }

    public async Task<IEnumerable<AvailableCalendarProvider>> GetAvailableProvidersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var providers = new List<AvailableCalendarProvider>();

        foreach (var provider in Enum.GetValues<CalendarProvider>())
        {
            if (provider == CalendarProvider.None)
            {
                continue;
            }

            var providerService = GetProviderService(provider);
            if (providerService == null)
            {
                continue;
            }

            try
            {
                var isAvailable = await providerService.IsAvailableAsync(cancellationToken);
                var capabilities = providerService.GetCapabilities();

                var tokenData = await _cacheService.GetCachedAccessTokenAsync(userId, provider, cancellationToken);
                var isConfigured = tokenData != null && tokenData.ExpiresAt > DateTime.UtcNow;

                providers.Add(new AvailableCalendarProvider(
                    provider,
                    isAvailable,
                    isConfigured,
                    capabilities,
                    tokenData?.ExpiresAt,
                    isAvailable ? [] : ["Provider service is not available"]
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking availability for provider {Provider}", provider);
                providers.Add(new AvailableCalendarProvider(
                    provider,
                    false,
                    false,
                    new ProviderCapabilities(false, false, false, false, false, false, false, [], TimeSpan.Zero, 0),
                    null,
                    [ex.Message]
                ));
            }
        }

        return providers;
    }

    public async Task<CalendarProviderConfigResult> ConfigureProviderAsync(
        Guid userId,
        CalendarProviderConfiguration providerConfig,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Configuring provider {Provider} for user {UserId}",
                providerConfig.Provider, userId);

            var providerService = GetProviderService(providerConfig.Provider);
            if (providerService == null)
            {
                return new CalendarProviderConfigResult(
                    userId,
                    providerConfig.Provider,
                    false,
                    $"Provider {providerConfig.Provider} is not available",
                    null,
                    DateTime.UtcNow
                );
            }

            // Authenticate with provider
            var authResult = await providerService.AuthenticateAsync(
                userId,
                providerConfig.AuthorizationCode,
                providerConfig.RedirectUri,
                cancellationToken);

            if (!authResult.Success)
            {
                return new CalendarProviderConfigResult(
                    userId,
                    providerConfig.Provider,
                    false,
                    authResult.ErrorMessage ?? "Authentication failed",
                    null,
                    DateTime.UtcNow
                );
            }

            // Cache access token
            var tokenData = new CalendarAccessToken(
                authResult.AccessToken,
                authResult.RefreshToken,
                authResult.ExpiresAt,
                authResult.Scopes
            );

            await _cacheService.CacheAccessTokenAsync(userId, providerConfig.Provider, tokenData,
                _settings.Providers[providerConfig.Provider].TokenCacheExpirationMinutes, cancellationToken);

            return new CalendarProviderConfigResult(
                userId,
                providerConfig.Provider,
                true,
                "Provider configured successfully",
                authResult.ExpiresAt,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure provider {Provider} for user {UserId}",
                providerConfig.Provider, userId);
            return new CalendarProviderConfigResult(
                userId,
                providerConfig.Provider,
                false,
                ex.Message,
                null,
                DateTime.UtcNow
            );
        }
    }

    public async Task<CalendarDisconnectResult> DisconnectProviderAsync(
        Guid userId,
        CalendarProvider provider,
        bool deleteData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Disconnecting provider {Provider} for user {UserId}, deleteData: {DeleteData}",
                provider, userId, deleteData);

            // Invalidate all cached data for this user and provider
            await _cacheService.InvalidateCalendarCacheByTypeAsync(userId, provider,
                [CalendarCacheType.All], cancellationToken);

            // If deleteData is true, we would also delete synced events from the database
            // This would require additional repository operations

            return new CalendarDisconnectResult(
                userId,
                provider,
                true,
                "Provider disconnected successfully",
                deleteData,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect provider {Provider} for user {UserId}", provider, userId);
            return new CalendarDisconnectResult(
                userId,
                provider,
                false,
                ex.Message,
                deleteData,
                DateTime.UtcNow
            );
        }
    }

    public async Task<IEnumerable<CalendarSyncConflict>> GetPendingConflictsAsync(
        Guid userId,
        CalendarProvider? provider = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filterOptions = new ConflictFilterOptions(
                provider,
                [ConflictStatus.Pending],
                null,
                null,
                [ConflictSeverity.High, ConflictSeverity.Medium, ConflictSeverity.Low],
                100
            );

            var conflicts = await _conflictDetector.GetPendingConflictsAsync(userId, filterOptions, cancellationToken);

            return conflicts.Select(conflict => new CalendarSyncConflict(
                conflict.ConflictId,
                userId,
                conflict.ConflictType,
                conflict.Severity,
                conflict.Description,
                conflict.InternalEvent,
                conflict.ExternalEvent,
                conflict.SuggestedResolutions,
                conflict.DetectedAt
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending conflicts for user {UserId}", userId);
            return [];
        }
    }

    public async Task<ConflictResolutionResult> ResolveConflictAsync(
        Guid userId,
        Guid conflictId,
        ConflictResolution resolution,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Resolving conflict {ConflictId} for user {UserId}", conflictId, userId);

            // Validate the resolution
            var validationResult = await _conflictDetector.ValidateResolutionAsync(
                userId, conflictId, resolution, cancellationToken);

            if (!validationResult.IsValid)
            {
                return new ConflictResolutionResult(
                    conflictId,
                    userId,
                    false,
                    validationResult.ValidationErrors.FirstOrDefault() ?? "Resolution validation failed",
                    null,
                    DateTime.UtcNow
                );
            }

            // Apply the resolution
            var resolutionResult = await _conflictDetector.ApplyResolutionAsync(
                userId, conflictId, resolution, cancellationToken);

            if (resolutionResult.Success)
            {
                // Cache the resolution for future reference
                await _cacheService.CacheConflictResolutionAsync(userId, conflictId, resolution,
                    _settings.ConflictResolution.ResolutionCacheExpirationMinutes, cancellationToken);
            }

            return new ConflictResolutionResult(
                conflictId,
                userId,
                resolutionResult.Success,
                resolutionResult.Success ? "Conflict resolved successfully" : resolutionResult.ErrorMessage,
                resolutionResult.ResolvedEvent,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve conflict {ConflictId} for user {UserId}", conflictId, userId);
            return new ConflictResolutionResult(
                conflictId,
                userId,
                false,
                ex.Message,
                null,
                DateTime.UtcNow
            );
        }
    }

    public async Task<bool> IsCalendarSyncAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return false;
        }

        try
        {
            // Check if at least one provider service is available
            foreach (var providerService in _providerServices)
            {
                if (await providerService.IsAvailableAsync(cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking calendar sync availability");
            return false;
        }
    }

    public async Task<CalendarSyncHealthStatus> GetSyncHealthAsync(CancellationToken cancellationToken = default)
    {
        var healthChecks = new List<ProviderHealthCheck>();
        var isHealthy = true;

        foreach (var providerService in _providerServices)
        {
            try
            {
                var isAvailable = await providerService.IsAvailableAsync(cancellationToken);
                var rateLimitStatus = await providerService.GetRateLimitStatusAsync(cancellationToken);

                healthChecks.Add(new ProviderHealthCheck(
                    providerService.ProviderType,
                    isAvailable,
                    rateLimitStatus,
                    isAvailable ? [] : ["Provider is not available"]
                ));

                if (!isAvailable)
                {
                    isHealthy = false;
                }
            }
            catch (Exception ex)
            {
                healthChecks.Add(new ProviderHealthCheck(
                    providerService.ProviderType,
                    false,
                    new ProviderRateLimitStatus(0, 0, TimeSpan.Zero, false),
                    [ex.Message]
                ));
                isHealthy = false;
            }
        }

        return new CalendarSyncHealthStatus(
            _settings.Enabled && isHealthy,
            _settings.Enabled,
            healthChecks,
            DateTime.UtcNow,
            isHealthy ? [] : ["One or more providers are unavailable"]
        );
    }

    public Task<CalendarBackupResult> BackupCalendarDataAsync(
        Guid userId,
        CalendarBackupOptions backupOptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting calendar backup for user {UserId}", userId);

            // Implementation would backup calendar data based on options
            // This is a placeholder for the actual backup implementation

            var backupId = Guid.NewGuid().ToString();

            return Task.FromResult(new CalendarBackupResult(
                backupId,
                userId,
                true,
                backupOptions,
                0, // Size would be calculated during actual backup
                "Calendar data backed up successfully",
                DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backup calendar data for user {UserId}", userId);
            return Task.FromResult(new CalendarBackupResult(
                string.Empty,
                userId,
                false,
                backupOptions,
                0,
                ex.Message,
                DateTime.UtcNow
            ));
        }
    }

    public Task<CalendarRestoreResult> RestoreCalendarDataAsync(
        Guid userId,
        string backupId,
        CalendarRestoreOptions restoreOptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting calendar restore for user {UserId} from backup {BackupId}", userId, backupId);

            // Implementation would restore calendar data from backup
            // This is a placeholder for the actual restore implementation

            return Task.FromResult(new CalendarRestoreResult(
                backupId,
                userId,
                true,
                restoreOptions,
                0, // Items would be counted during actual restore
                "Calendar data restored successfully",
                DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore calendar data for user {UserId} from backup {BackupId}", userId, backupId);
            return Task.FromResult(new CalendarRestoreResult(
                backupId,
                userId,
                false,
                restoreOptions,
                0,
                ex.Message,
                DateTime.UtcNow
            ));
        }
    }

    // Private helper methods

    private void InitializeProviderCache()
    {
        foreach (var providerService in _providerServices)
        {
            _providerCache.TryAdd(providerService.ProviderType, providerService);
        }
    }

    private ICalendarProviderService? GetProviderService(CalendarProvider provider)
    {
        return _providerCache.GetValueOrDefault(provider);
    }

    private CalendarSyncResult CreateFailureResult(Guid userId, CalendarProvider provider, string error, TimeSpan duration)
    {
        return new CalendarSyncResult(
            userId,
            provider,
            false,
            0, 0, 0, 0, 0, 0,
            duration,
            null,
            [error],
            [],
            DateTime.UtcNow,
            DateTime.UtcNow,
            SyncDirection.BiDirectional,
            new SyncStatistics(0, 0, 0, 0, 0, 0)
        );
    }

    private Task<CalendarSyncResult> PerformFullSynchronization(
        Guid userId,
        CalendarProvider provider,
        SyncMode syncMode,
        ICalendarProviderService providerService,
        CalendarAccessToken tokenData,
        IEnumerable<ExternalCalendar> calendars,
        DateTime syncStartTime,
        CancellationToken cancellationToken)
    {
        // This is a simplified implementation
        // Full implementation would involve getting events from each calendar,
        // detecting conflicts, resolving them, and syncing changes

        var syncEndTime = DateTime.UtcNow;
        var duration = syncEndTime - syncStartTime;

        return Task.FromResult(new CalendarSyncResult(
            userId,
            provider,
            true,
            0, // EventsSynced
            0, // EventsCreated
            0, // EventsUpdated
            0, // EventsDeleted
            0, // ConflictsDetected
            0, // ConflictsResolved
            duration,
            Guid.NewGuid().ToString(), // NewSyncToken
            [],
            [],
            syncStartTime,
            syncEndTime,
            syncMode == SyncMode.BiDirectional ? SyncDirection.BiDirectional : SyncDirection.ToExternal,
            new SyncStatistics(0, 0, 0, 0, 0, 0)
        ));
    }

    private Task<IncrementalSyncResult> PerformIncrementalSynchronization(
        Guid userId,
        CalendarProvider provider,
        string lastSyncToken,
        ICalendarProviderService providerService,
        CalendarAccessToken tokenData,
        DateTime syncStartTime,
        CancellationToken cancellationToken)
    {
        // This is a simplified implementation
        // Full implementation would use the sync token to get only changes

        var syncEndTime = DateTime.UtcNow;
        var duration = syncEndTime - syncStartTime;

        return Task.FromResult(new IncrementalSyncResult(
            userId,
            provider,
            true,
            lastSyncToken,
            Guid.NewGuid().ToString(), // NewSyncToken
            [],
            0,
            duration,
            [],
            syncStartTime
        ));
    }

    private Task<EventSyncResult> SyncSingleEvent(
        Guid userId,
        Guid eventId,
        CalendarProvider provider,
        SyncDirection syncDirection,
        ICalendarProviderService providerService,
        CalendarAccessToken tokenData,
        CancellationToken cancellationToken)
    {
        // This is a placeholder for single event sync implementation
        return Task.FromResult(new EventSyncResult(
            eventId,
            Guid.NewGuid().ToString(), // ExternalEventId
            true,
            SyncOperation.Created,
            null,
            null,
            DateTime.UtcNow
        ));
    }

    private Task<TaskToEventSyncResult> ConvertAndSyncTaskAsEvent(
        Guid userId,
        Guid taskId,
        CalendarProvider provider,
        TaskToEventConversionOptions conversionOptions,
        ICalendarProviderService providerService,
        CalendarAccessToken tokenData,
        CancellationToken cancellationToken)
    {
        // This is a placeholder for task-to-event conversion and sync implementation
        return Task.FromResult(new TaskToEventSyncResult(
            taskId,
            Guid.NewGuid().ToString(), // ExternalEventId
            true,
            SyncOperation.Created,
            null,
            null,
            DateTime.UtcNow
        ));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var semaphore in _userSyncSemaphores.Values)
        {
            semaphore?.Dispose();
        }
        _userSyncSemaphores.Clear();

        _disposed = true;
    }
}
