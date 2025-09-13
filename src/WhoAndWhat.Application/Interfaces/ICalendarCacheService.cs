using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Cache service interface for calendar synchronization data and operations
/// </summary>
public interface ICalendarCacheService
{
    /// <summary>
    /// Cache external calendar events for a user and provider
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="events">Events to cache</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheCalendarEventsAsync(Guid userId, CalendarProvider provider, string calendarId, IEnumerable<ExternalCalendarEvent> events, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached calendar events
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="dateRange">Optional date range filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached events if available</returns>
    public Task<IEnumerable<ExternalCalendarEvent>?> GetCachedCalendarEventsAsync(Guid userId, CalendarProvider provider, string calendarId, TimeRange? dateRange = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache calendar synchronization token for incremental sync
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="syncToken">Synchronization token</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheSyncTokenAsync(Guid userId, CalendarProvider provider, string calendarId, string syncToken, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached synchronization token
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached sync token if available</returns>
    public Task<string?> GetCachedSyncTokenAsync(Guid userId, CalendarProvider provider, string calendarId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache calendar conflict resolutions
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="conflictId">Conflict ID</param>
    /// <param name="resolution">Resolution data</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheConflictResolutionAsync(Guid userId, Guid conflictId, ConflictResolution resolution, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached conflict resolution
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="conflictId">Conflict ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached resolution if available</returns>
    public Task<ConflictResolution?> GetCachedConflictResolutionAsync(Guid userId, Guid conflictId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache calendar provider access tokens
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="tokenData">Access token data</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheAccessTokenAsync(Guid userId, CalendarProvider provider, CalendarAccessToken tokenData, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached access token
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached access token if available</returns>
    public Task<CalendarAccessToken?> GetCachedAccessTokenAsync(Guid userId, CalendarProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache calendar synchronization status
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="syncStatus">Synchronization status</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheSyncStatusAsync(Guid userId, CalendarProvider provider, CalendarSyncStatus syncStatus, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached synchronization status
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached sync status if available</returns>
    public Task<CalendarSyncStatus?> GetCachedSyncStatusAsync(Guid userId, CalendarProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache free/busy information
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="timeRange">Time range for free/busy data</param>
    /// <param name="freeBusyData">Free/busy information</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheFreeBusyDataAsync(Guid userId, CalendarProvider provider, TimeRange timeRange, FreeBusyResult freeBusyData, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached free/busy information
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="timeRange">Time range to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached free/busy data if available</returns>
    public Task<FreeBusyResult?> GetCachedFreeBusyDataAsync(Guid userId, CalendarProvider provider, TimeRange timeRange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache external calendar metadata (calendar list, properties)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="calendars">Calendar metadata</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheCalendarMetadataAsync(Guid userId, CalendarProvider provider, IEnumerable<ExternalCalendar> calendars, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached calendar metadata
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached calendar metadata if available</returns>
    public Task<IEnumerable<ExternalCalendar>?> GetCachedCalendarMetadataAsync(Guid userId, CalendarProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all calendar cache for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if invalidated successfully</returns>
    public Task<bool> InvalidateUserCalendarCacheAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate specific calendar cache types for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Optional provider filter</param>
    /// <param name="cacheTypes">Types of cache to invalidate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if invalidated successfully</returns>
    public Task<bool> InvalidateCalendarCacheByTypeAsync(Guid userId, CalendarProvider? provider, IEnumerable<CalendarCacheType> cacheTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cache when events are modified
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="calendarId">Calendar ID</param>
    /// <param name="modifiedEventIds">IDs of events that were modified</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if invalidated successfully</returns>
    public Task<bool> InvalidateEventCacheAsync(Guid userId, CalendarProvider provider, string calendarId, IEnumerable<string> modifiedEventIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get calendar cache metrics and statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache metrics</returns>
    public Task<CalendarCacheMetrics> GetCalendarCacheMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all calendar cache data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cleared successfully</returns>
    public Task<bool> ClearAllCalendarCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Warm calendar cache for frequently accessed user data
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="providers">Providers to warm cache for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of cache entries warmed</returns>
    public Task<int> WarmUserCalendarCacheAsync(Guid userId, IEnumerable<CalendarProvider> providers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Preload upcoming events into cache
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="lookAheadDays">Number of days ahead to preload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of events preloaded</returns>
    public Task<int> PreloadUpcomingEventsAsync(Guid userId, CalendarProvider provider, int lookAheadDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache event conversion mappings between internal and external events
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="mappings">Event ID mappings</param>
    /// <param name="expirationMinutes">Cache expiration in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached successfully</returns>
    public Task<bool> CacheEventMappingsAsync(Guid userId, IEnumerable<EventIdMapping> mappings, int expirationMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached event ID mappings
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached mappings if available</returns>
    public Task<IEnumerable<EventIdMapping>?> GetCachedEventMappingsAsync(Guid userId, CalendarProvider provider, CancellationToken cancellationToken = default);
}

/// <summary>
/// Types of calendar cache for selective invalidation
/// </summary>
public enum CalendarCacheType
{
    Events,
    SyncTokens,
    ConflictResolutions,
    AccessTokens,
    SyncStatus,
    FreeBusyData,
    CalendarMetadata,
    EventMappings,
    All
}

/// <summary>
/// Calendar cache metrics and statistics
/// </summary>
public sealed record CalendarCacheMetrics(
    int TotalEntries,
    long TotalSizeBytes,
    int HitCount,
    int MissCount,
    double HitRate,
    Dictionary<CalendarCacheType, int> EntriesByType,
    Dictionary<CalendarCacheType, double> HitRatesByType,
    Dictionary<CalendarProvider, int> EntriesByProvider,
    DateTime LastResetTime,
    TimeSpan AverageResponseTime
);
