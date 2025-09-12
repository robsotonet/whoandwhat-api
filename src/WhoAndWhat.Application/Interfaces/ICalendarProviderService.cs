using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Abstract service interface for calendar provider implementations (Google, Outlook, iCloud, etc.)
/// </summary>
public interface ICalendarProviderService
{
    /// <summary>
    /// Calendar provider type this service handles
    /// </summary>
    CalendarProvider ProviderType { get; }

    /// <summary>
    /// Check if this provider is properly configured and available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if provider is available</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticate user with calendar provider and obtain access tokens
    /// </summary>
    /// <param name="userId">User ID to authenticate</param>
    /// <param name="authorizationCode">OAuth authorization code from provider</param>
    /// <param name="redirectUri">OAuth redirect URI used in authorization</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication result with access tokens</returns>
    Task<CalendarAuthResult> AuthenticateAsync(Guid userId, string authorizationCode, string redirectUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh expired access tokens using refresh token
    /// </summary>
    /// <param name="userId">User ID whose tokens to refresh</param>
    /// <param name="refreshToken">Refresh token from previous authentication</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token refresh result with new access tokens</returns>
    Task<TokenRefreshResult> RefreshTokensAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all calendars available for the authenticated user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="accessToken">Valid access token for the provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available calendars</returns>
    Task<IEnumerable<ExternalCalendar>> GetCalendarsAsync(Guid userId, string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get events from external calendar within specified date range
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="accessToken">Valid access token</param>
    /// <param name="startDate">Start date for event retrieval</param>
    /// <param name="endDate">End date for event retrieval</param>
    /// <param name="syncToken">Optional sync token for incremental sync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Events from external calendar with sync information</returns>
    Task<ExternalCalendarEventsResult> GetEventsAsync(Guid userId, string calendarId, string accessToken, DateTime startDate, DateTime endDate, string? syncToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new event in external calendar
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="accessToken">Valid access token</param>
    /// <param name="eventData">Event data to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created event information</returns>
    Task<ExternalEventResult> CreateEventAsync(Guid userId, string calendarId, string accessToken, ExternalEventCreateRequest eventData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing event in external calendar
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="eventId">External event ID to update</param>
    /// <param name="accessToken">Valid access token</param>
    /// <param name="eventData">Updated event data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated event information</returns>
    Task<ExternalEventResult> UpdateEventAsync(Guid userId, string calendarId, string eventId, string accessToken, ExternalEventUpdateRequest eventData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete event from external calendar
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="eventId">External event ID to delete</param>
    /// <param name="accessToken">Valid access token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deletion result</returns>
    Task<ExternalEventDeleteResult> DeleteEventAsync(Guid userId, string calendarId, string eventId, string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch create multiple events in external calendar
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="accessToken">Valid access token</param>
    /// <param name="eventRequests">List of events to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation results for each event</returns>
    Task<IEnumerable<ExternalEventResult>> CreateEventsAsync(Guid userId, string calendarId, string accessToken, IEnumerable<ExternalEventCreateRequest> eventRequests, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch update multiple events in external calendar
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="calendarId">External calendar ID</param>
    /// <param name="accessToken">Valid access token</param>
    /// <param name="eventUpdates">List of events to update with their IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation results for each event</returns>
    Task<IEnumerable<ExternalEventResult>> UpdateEventsAsync(Guid userId, string calendarId, string accessToken, IEnumerable<ExternalEventUpdateWithId> eventUpdates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get free/busy information for specified time ranges
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="calendarIds">List of calendar IDs to check</param>
    /// <param name="accessToken">Valid access token</param>
    /// <param name="timeRanges">Time ranges to check for free/busy status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Free/busy information for requested calendars and times</returns>
    Task<FreeBusyResult> GetFreeBusyAsync(Guid userId, IEnumerable<string> calendarIds, string accessToken, IEnumerable<TimeRange> timeRanges, CancellationToken cancellationToken = default);

    /// <summary>
    /// Watch for changes in external calendar (webhooks/notifications)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="calendarId">External calendar ID to watch</param>
    /// <param name="accessToken">Valid access token</param>
    /// <param name="webhookUrl">URL to receive change notifications</param>
    /// <param name="expirationTime">When the watch should expire</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Watch setup result with watch ID</returns>
    Task<CalendarWatchResult> WatchCalendarAsync(Guid userId, string calendarId, string accessToken, string webhookUrl, DateTime expirationTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop watching calendar changes
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="watchId">Watch ID to stop</param>
    /// <param name="accessToken">Valid access token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Watch stop result</returns>
    Task<CalendarWatchStopResult> StopWatchingAsync(Guid userId, string watchId, string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process webhook notification from calendar provider
    /// </summary>
    /// <param name="webhookData">Raw webhook data from provider</param>
    /// <param name="headers">HTTP headers from webhook request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed webhook result with change information</returns>
    Task<WebhookProcessResult> ProcessWebhookAsync(string webhookData, IDictionary<string, string> headers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get provider-specific rate limiting information
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current rate limiting status</returns>
    Task<ProviderRateLimitStatus> GetRateLimitStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate access token and check if it's still valid
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="accessToken">Access token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token validation result</returns>
    Task<TokenValidationResult> ValidateTokenAsync(Guid userId, string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get provider-specific capabilities and limitations
    /// </summary>
    /// <returns>Provider capabilities information</returns>
    ProviderCapabilities GetCapabilities();

    /// <summary>
    /// Convert internal WhoAndWhat event to provider-specific event format
    /// </summary>
    /// <param name="internalEvent">Internal event data</param>
    /// <param name="conversionOptions">Conversion options and preferences</param>
    /// <returns>Provider-specific event data</returns>
    ExternalEventCreateRequest ConvertFromInternalEvent(InternalCalendarEvent internalEvent, EventConversionOptions conversionOptions);

    /// <summary>
    /// Convert provider-specific event to internal WhoAndWhat event format
    /// </summary>
    /// <param name="externalEvent">External event data</param>
    /// <param name="conversionOptions">Conversion options and preferences</param>
    /// <returns>Internal event data</returns>
    InternalCalendarEvent ConvertToInternalEvent(ExternalCalendarEvent externalEvent, EventConversionOptions conversionOptions);
}