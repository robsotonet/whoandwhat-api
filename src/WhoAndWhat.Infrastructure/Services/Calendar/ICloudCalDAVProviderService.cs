using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services.Calendar;

/// <summary>
/// iCloud CalDAV provider implementation supporting Apple iCloud Calendar and generic CalDAV servers
/// Uses CalDAV protocol (RFC 4791) for calendar operations
/// </summary>
public class ICloudCalDAVProviderService : ICalendarProviderService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ICloudCalDAVProviderService> _logger;
    private readonly CalendarSyncSettings _settings;
    private readonly ICloudCalendarProviderSettings _iCloudSettings;
    private bool _disposed;

    private const string ICloudCalDAVBaseUrl = "https://caldav.icloud.com";
    private const string CalDAVNamespace = "DAV:";
    private const string CalDAVCalendarNamespace = "urn:ietf:params:xml:ns:caldav";

    public CalendarProvider ProviderType => CalendarProvider.ICloud;

    public ICloudCalDAVProviderService(
        HttpClient httpClient,
        IOptions<CalendarSyncSettings> settings,
        ILogger<ICloudCalDAVProviderService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _iCloudSettings = _settings.Providers[CalendarProvider.ICloud];
        ConfigureHttpClient();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var optionsRequest = new HttpRequestMessage(HttpMethod.Options, $"{ICloudCalDAVBaseUrl}/");
            var response = await _httpClient.SendAsync(optionsRequest, cancellationToken);
            
            // CalDAV server should respond to OPTIONS request with DAV headers
            return response.Headers.Contains("DAV") || response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking iCloud CalDAV availability");
            return false;
        }
    }

    public async Task<CalendarAuthResult> AuthenticateAsync(
        Guid userId, 
        string authorizationCode, 
        string redirectUri, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting iCloud CalDAV authentication for user {UserId}", userId);

            // For iCloud CalDAV, "authorizationCode" would be the app-specific password
            // The redirectUri would contain the username (Apple ID)
            var credentials = ParseCredentials(authorizationCode, redirectUri);
            
            if (string.IsNullOrEmpty(credentials.Username) || string.IsNullOrEmpty(credentials.Password))
            {
                return new CalendarAuthResult(
                    false,
                    string.Empty,
                    string.Empty,
                    DateTime.UtcNow,
                    [],
                    "Invalid credentials format. Username and app-specific password required."
                );
            }

            // Test authentication by discovering principal URL
            var principalUrl = await DiscoverPrincipalUrl(credentials.Username, credentials.Password, cancellationToken);
            
            if (string.IsNullOrEmpty(principalUrl))
            {
                return new CalendarAuthResult(
                    false,
                    string.Empty,
                    string.Empty,
                    DateTime.UtcNow,
                    [],
                    "Authentication failed. Invalid username or app-specific password."
                );
            }

            // For CalDAV, we store the credentials as "tokens" (base64 encoded)
            var accessToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}"));
            
            return new CalendarAuthResult(
                true,
                accessToken,
                accessToken, // CalDAV doesn't have refresh tokens, so we use the same token
                DateTime.UtcNow.AddYears(1), // App-specific passwords are long-lived
                ["caldav"],
                null,
                credentials.Username,
                credentials.Username.Split('@')[0] // Use username part as display name
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate user {UserId} with iCloud CalDAV", userId);
            return new CalendarAuthResult(
                false,
                string.Empty,
                string.Empty,
                DateTime.UtcNow,
                [],
                ex.Message
            );
        }
    }

    public async Task<TokenRefreshResult> RefreshTokensAsync(
        Guid userId, 
        string refreshToken, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating iCloud CalDAV credentials for user {UserId}", userId);

            // CalDAV doesn't have token refresh, but we can validate the existing credentials
            var credentials = ParseTokenToCredentials(refreshToken);
            var principalUrl = await DiscoverPrincipalUrl(credentials.Username, credentials.Password, cancellationToken);

            if (string.IsNullOrEmpty(principalUrl))
            {
                return new TokenRefreshResult(
                    false,
                    string.Empty,
                    string.Empty,
                    DateTime.UtcNow,
                    [],
                    "Credentials are no longer valid"
                );
            }

            return new TokenRefreshResult(
                true,
                refreshToken, // Same token
                refreshToken,
                DateTime.UtcNow.AddYears(1), // Extend expiration
                ["caldav"],
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh tokens for user {UserId}", userId);
            return new TokenRefreshResult(
                false,
                string.Empty,
                string.Empty,
                DateTime.UtcNow,
                [],
                ex.Message
            );
        }
    }

    public async Task<IEnumerable<ExternalCalendar>> GetCalendarsAsync(
        Guid userId, 
        string accessToken, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving calendars for user {UserId}", userId);

            var credentials = ParseTokenToCredentials(accessToken);
            var principalUrl = await DiscoverPrincipalUrl(credentials.Username, credentials.Password, cancellationToken);
            
            if (string.IsNullOrEmpty(principalUrl))
            {
                return [];
            }

            var calendarHomeUrl = await DiscoverCalendarHomeSet(principalUrl, credentials, cancellationToken);
            
            if (string.IsNullOrEmpty(calendarHomeUrl))
            {
                return [];
            }

            var calendars = await DiscoverCalendars(calendarHomeUrl, credentials, cancellationToken);
            
            _logger.LogInformation("Retrieved {CalendarCount} calendars for user {UserId}", calendars.Count(), userId);
            return calendars;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get calendars for user {UserId}", userId);
            return [];
        }
    }

    public async Task<ExternalCalendarEventsResult> GetEventsAsync(
        Guid userId, 
        string calendarId, 
        string accessToken, 
        DateTime startDate, 
        DateTime endDate, 
        string? syncToken = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving events for user {UserId} from calendar {CalendarId}", userId, calendarId);

            var credentials = ParseTokenToCredentials(accessToken);
            var calendarUrl = DecodeCalendarId(calendarId);

            // Build CalDAV REPORT query for calendar events
            var reportQuery = BuildCalendarQueryXml(startDate, endDate);

            using var request = new HttpRequestMessage(new HttpMethod("REPORT"), calendarUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}")
            ));
            request.Headers.Add("Depth", "1");
            request.Content = new StringContent(reportQuery, Encoding.UTF8, "application/xml");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var events = await ParseCalendarEventsFromResponse(responseContent, calendarUrl, credentials, cancellationToken);

            _logger.LogInformation("Retrieved {EventCount} events for user {UserId} from calendar {CalendarId}", 
                events.Count(), userId, calendarId);

            return new ExternalCalendarEventsResult(
                events,
                GenerateSyncToken(calendarUrl, DateTime.UtcNow), // Simple timestamp-based sync token
                null, // CalDAV doesn't use page tokens
                true,
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events for user {UserId} from calendar {CalendarId}", userId, calendarId);
            return new ExternalCalendarEventsResult(
                [],
                null,
                null,
                false,
                ex.Message
            );
        }
    }

    public async Task<ExternalEventResult> CreateEventAsync(
        Guid userId, 
        string calendarId, 
        string accessToken, 
        ExternalEventCreateRequest eventData, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating event for user {UserId} in calendar {CalendarId}", userId, calendarId);

            var credentials = ParseTokenToCredentials(accessToken);
            var calendarUrl = DecodeCalendarId(calendarId);
            var eventId = Guid.NewGuid().ToString();
            var eventUrl = $"{calendarUrl.TrimEnd('/')}/{eventId}.ics";

            var icalData = ConvertToICalendar(eventData, eventId);

            using var request = new HttpRequestMessage(HttpMethod.Put, eventUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}")
            ));
            request.Headers.Add("If-None-Match", "*"); // Only create if doesn't exist
            request.Content = new StringContent(icalData, Encoding.UTF8, "text/calendar");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            // Fetch the created event to get the full data
            var createdEvent = await GetSingleEvent(eventUrl, credentials, cancellationToken);

            return new ExternalEventResult(
                true,
                eventId,
                createdEvent,
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create event for user {UserId} in calendar {CalendarId}", userId, calendarId);
            return new ExternalEventResult(
                false,
                string.Empty,
                null,
                ex.Message
            );
        }
    }

    public async Task<ExternalEventResult> UpdateEventAsync(
        Guid userId, 
        string calendarId, 
        string eventId, 
        string accessToken, 
        ExternalEventUpdateRequest eventData, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating event {EventId} for user {UserId} in calendar {CalendarId}", 
                eventId, userId, calendarId);

            var credentials = ParseTokenToCredentials(accessToken);
            var calendarUrl = DecodeCalendarId(calendarId);
            var eventUrl = $"{calendarUrl.TrimEnd('/')}/{eventId}.ics";

            var icalData = ConvertToICalendar(eventData, eventId);

            using var request = new HttpRequestMessage(HttpMethod.Put, eventUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}")
            ));
            request.Content = new StringContent(icalData, Encoding.UTF8, "text/calendar");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            // Fetch the updated event to get the full data
            var updatedEvent = await GetSingleEvent(eventUrl, credentials, cancellationToken);

            return new ExternalEventResult(
                true,
                eventId,
                updatedEvent,
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update event {EventId} for user {UserId} in calendar {CalendarId}", 
                eventId, userId, calendarId);
            return new ExternalEventResult(
                false,
                eventId,
                null,
                ex.Message
            );
        }
    }

    public async Task<ExternalEventDeleteResult> DeleteEventAsync(
        Guid userId, 
        string calendarId, 
        string eventId, 
        string accessToken, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting event {EventId} for user {UserId} from calendar {CalendarId}", 
                eventId, userId, calendarId);

            var credentials = ParseTokenToCredentials(accessToken);
            var calendarUrl = DecodeCalendarId(calendarId);
            var eventUrl = $"{calendarUrl.TrimEnd('/')}/{eventId}.ics";

            using var request = new HttpRequestMessage(HttpMethod.Delete, eventUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}")
            ));

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            return new ExternalEventDeleteResult(
                true,
                eventId,
                "Event deleted successfully"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete event {EventId} for user {UserId} from calendar {CalendarId}", 
                eventId, userId, calendarId);
            return new ExternalEventDeleteResult(
                false,
                eventId,
                ex.Message
            );
        }
    }

    public async Task<IEnumerable<ExternalEventResult>> CreateEventsAsync(
        Guid userId, 
        string calendarId, 
        string accessToken, 
        IEnumerable<ExternalEventCreateRequest> eventRequests, 
        CancellationToken cancellationToken = default)
    {
        var results = new List<ExternalEventResult>();
        
        foreach (var eventRequest in eventRequests)
        {
            var result = await CreateEventAsync(userId, calendarId, accessToken, eventRequest, cancellationToken);
            results.Add(result);
            
            // Add small delay to respect rate limits
            if (results.Count > 1)
            {
                await Task.Delay(_iCloudSettings.RateLimit.RequestDelayMs, cancellationToken);
            }
        }

        return results;
    }

    public async Task<IEnumerable<ExternalEventResult>> UpdateEventsAsync(
        Guid userId, 
        string calendarId, 
        string accessToken, 
        IEnumerable<ExternalEventUpdateWithId> eventUpdates, 
        CancellationToken cancellationToken = default)
    {
        var results = new List<ExternalEventResult>();
        
        foreach (var eventUpdate in eventUpdates)
        {
            var result = await UpdateEventAsync(userId, calendarId, eventUpdate.EventId, accessToken, 
                eventUpdate.UpdateRequest, cancellationToken);
            results.Add(result);
            
            // Add small delay to respect rate limits
            if (results.Count > 1)
            {
                await Task.Delay(_iCloudSettings.RateLimit.RequestDelayMs, cancellationToken);
            }
        }

        return results;
    }

    public async Task<FreeBusyResult> GetFreeBusyAsync(
        Guid userId, 
        IEnumerable<string> calendarIds, 
        string accessToken, 
        IEnumerable<TimeRange> timeRanges, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting free/busy information for user {UserId}", userId);

            var credentials = ParseTokenToCredentials(accessToken);
            var freeBusyInfo = new List<CalendarFreeBusy>();

            foreach (var calendarId in calendarIds)
            {
                try
                {
                    var calendarUrl = DecodeCalendarId(calendarId);
                    var busyTimes = new List<TimeRange>();

                    foreach (var timeRange in timeRanges)
                    {
                        // Get events in the time range and mark as busy
                        var eventsResult = await GetEventsAsync(userId, calendarId, accessToken, 
                            timeRange.Start, timeRange.End, null, cancellationToken);
                        
                        if (eventsResult.Success && eventsResult.Events != null)
                        {
                            var eventBusyTimes = eventsResult.Events
                                .Where(e => !e.IsAllDay) // All-day events don't typically block time
                                .Select(e => new TimeRange(e.StartTime, e.EndTime));
                            
                            busyTimes.AddRange(eventBusyTimes);
                        }
                    }

                    freeBusyInfo.Add(new CalendarFreeBusy(
                        calendarId,
                        busyTimes,
                        []
                    ));
                }
                catch (Exception ex)
                {
                    freeBusyInfo.Add(new CalendarFreeBusy(
                        calendarId,
                        [],
                        [ex.Message]
                    ));
                }
            }

            return new FreeBusyResult(
                freeBusyInfo,
                true,
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get free/busy information for user {UserId}", userId);
            return new FreeBusyResult(
                [],
                false,
                ex.Message
            );
        }
    }

    public async Task<CalendarWatchResult> WatchCalendarAsync(
        Guid userId, 
        string calendarId, 
        string accessToken, 
        string webhookUrl, 
        DateTime expirationTime, 
        CancellationToken cancellationToken = default)
    {
        // CalDAV doesn't have built-in webhook support like Google Calendar or Outlook
        // This would require a polling mechanism or server-specific extensions
        _logger.LogWarning("CalDAV does not support native webhooks. Consider implementing polling for calendar {CalendarId}", calendarId);
        
        return new CalendarWatchResult(
            false,
            string.Empty,
            string.Empty,
            expirationTime,
            "CalDAV does not support native webhook notifications. Polling is required for change detection."
        );
    }

    public async Task<CalendarWatchStopResult> StopWatchingAsync(
        Guid userId, 
        string watchId, 
        string accessToken, 
        CancellationToken cancellationToken = default)
    {
        // Since CalDAV doesn't support webhooks, there's nothing to stop
        return new CalendarWatchStopResult(
            true,
            watchId,
            "No active watch to stop (CalDAV doesn't support webhooks)"
        );
    }

    public async Task<WebhookProcessResult> ProcessWebhookAsync(
        string webhookData, 
        IDictionary<string, string> headers, 
        CancellationToken cancellationToken = default)
    {
        // CalDAV doesn't support webhooks natively
        return new WebhookProcessResult(
            false,
            WebhookChangeType.Unknown,
            null,
            null,
            "CalDAV does not support webhook notifications"
        );
    }

    public async Task<ProviderRateLimitStatus> GetRateLimitStatusAsync(CancellationToken cancellationToken = default)
    {
        // iCloud CalDAV has informal rate limits, typically much lower than API-based services
        return new ProviderRateLimitStatus(
            _iCloudSettings.RateLimit.RequestsPerMinute,
            _iCloudSettings.RateLimit.RequestsPerMinute, // Assuming we haven't tracked usage
            TimeSpan.FromMinutes(1),
            false // Not currently throttled
        );
    }

    public async Task<TokenValidationResult> ValidateTokenAsync(
        Guid userId, 
        string accessToken, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var credentials = ParseTokenToCredentials(accessToken);
            var principalUrl = await DiscoverPrincipalUrl(credentials.Username, credentials.Password, cancellationToken);

            var isValid = !string.IsNullOrEmpty(principalUrl);
            
            return new TokenValidationResult(
                isValid,
                isValid ? null : "Invalid credentials or server unreachable",
                isValid ? DateTime.UtcNow.AddMonths(3) : null // App-specific passwords are long-lived
            );
        }
        catch (Exception ex)
        {
            return new TokenValidationResult(
                false,
                ex.Message,
                null
            );
        }
    }

    public ProviderCapabilities GetCapabilities()
    {
        return new ProviderCapabilities(
            SupportsBatchOperations: false, // CalDAV operates on individual resources
            SupportsWebhooks: false, // CalDAV doesn't have native webhook support
            SupportsIncrementalSync: true, // Can use ETags and timestamps
            SupportsFreeBusy: true, // Can derive from events
            SupportsRecurringEvents: true, // iCalendar supports RRULE
            SupportsAttendees: true, // iCalendar supports attendees
            SupportsAttachments: true, // iCalendar supports attachments
            SupportedEventFields: [
                "summary", "description", "dtstart", "dtend", "location", 
                "attendee", "rrule", "attach", "alarm"
            ],
            MaxBatchSize: TimeSpan.FromDays(31), // Reasonable range for CalDAV queries
            RateLimitPerMinute: _iCloudSettings.RateLimit.RequestsPerMinute
        );
    }

    public ExternalEventCreateRequest ConvertFromInternalEvent(
        InternalCalendarEvent internalEvent, 
        EventConversionOptions conversionOptions)
    {
        return new ExternalEventCreateRequest(
            internalEvent.Title,
            internalEvent.Description,
            internalEvent.StartTime,
            internalEvent.EndTime,
            internalEvent.IsAllDay,
            internalEvent.Location,
            internalEvent.Attendees?.Select(a => new ExternalAttendee(
                a.Email,
                a.DisplayName,
                MapAttendeeStatus(a.Status),
                a.IsOptional,
                a.IsOrganizer
            )).ToList() ?? [],
            internalEvent.Recurrence != null ? [internalEvent.Recurrence] : [],
            internalEvent.Reminders?.Select(r => new ExternalEventReminder(
                MapReminderMethod(r.Method),
                r.MinutesBeforeStart
            )).ToList() ?? [],
            internalEvent.Attachments?.Select(a => new ExternalEventAttachment(
                a.Url,
                a.Title,
                a.MimeType,
                a.FileId
            )).ToList() ?? [],
            conversionOptions.AdditionalProperties ?? new Dictionary<string, object>()
        );
    }

    public InternalCalendarEvent ConvertToInternalEvent(
        ExternalCalendarEvent externalEvent, 
        EventConversionOptions conversionOptions)
    {
        return new InternalCalendarEvent(
            Guid.NewGuid(),
            externalEvent.Id,
            externalEvent.Title,
            externalEvent.Description,
            externalEvent.StartTime,
            externalEvent.EndTime,
            externalEvent.IsAllDay,
            externalEvent.Location,
            externalEvent.Attendees?.Select(a => new InternalAttendee(
                a.Email,
                a.DisplayName,
                MapExternalAttendeeStatus(a.Status),
                a.IsOptional,
                a.IsOrganizer
            )).ToList() ?? [],
            externalEvent.Recurrence?.FirstOrDefault(),
            externalEvent.Reminders?.Select(r => new InternalEventReminder(
                MapExternalReminderMethod(r.Method),
                r.MinutesBeforeStart
            )).ToList() ?? [],
            externalEvent.Attachments?.Select(a => new InternalEventAttachment(
                a.Url,
                a.Title,
                a.MimeType,
                a.FileId
            )).ToList() ?? [],
            externalEvent.CreatedTime,
            externalEvent.UpdatedTime,
            CalendarProvider.ICloud
        );
    }

    // Private helper methods

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WhoAndWhat-CalendarSync/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(_iCloudSettings.TimeoutSeconds);
    }

    private static (string Username, string Password) ParseCredentials(string authorizationCode, string redirectUri)
    {
        // For iCloud CalDAV, authorizationCode is the app-specific password
        // redirectUri contains the username (Apple ID email)
        return (redirectUri, authorizationCode);
    }

    private static (string Username, string Password) ParseTokenToCredentials(string token)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split(':', 2);
            return parts.Length == 2 ? (parts[0], parts[1]) : (string.Empty, string.Empty);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private async Task<string?> DiscoverPrincipalUrl(string username, string password, CancellationToken cancellationToken)
    {
        try
        {
            var propFindXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <d:propfind xmlns:d=""DAV:"">
                  <d:prop>
                    <d:current-user-principal />
                  </d:prop>
                </d:propfind>";

            using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), $"{ICloudCalDAVBaseUrl}/");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", 
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
            request.Headers.Add("Depth", "0");
            request.Content = new StringContent(propFindXml, Encoding.UTF8, "application/xml");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(responseContent);
            
            var principalElement = doc.Descendants(XName.Get("current-user-principal", CalDAVNamespace)).FirstOrDefault();
            var hrefElement = principalElement?.Descendants(XName.Get("href", CalDAVNamespace)).FirstOrDefault();
            
            return hrefElement?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover principal URL for user {Username}", username);
            return null;
        }
    }

    private async Task<string?> DiscoverCalendarHomeSet(string principalUrl, (string Username, string Password) credentials, CancellationToken cancellationToken)
    {
        try
        {
            var propFindXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <d:propfind xmlns:d=""DAV:"" xmlns:c=""urn:ietf:params:xml:ns:caldav"">
                  <d:prop>
                    <c:calendar-home-set />
                  </d:prop>
                </d:propfind>";

            var fullUrl = principalUrl.StartsWith("http") ? principalUrl : $"{ICloudCalDAVBaseUrl}{principalUrl}";

            using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), fullUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", 
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}")));
            request.Headers.Add("Depth", "0");
            request.Content = new StringContent(propFindXml, Encoding.UTF8, "application/xml");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(responseContent);
            
            var calendarHomeElement = doc.Descendants(XName.Get("calendar-home-set", CalDAVCalendarNamespace)).FirstOrDefault();
            var hrefElement = calendarHomeElement?.Descendants(XName.Get("href", CalDAVNamespace)).FirstOrDefault();
            
            return hrefElement?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover calendar home set for principal {PrincipalUrl}", principalUrl);
            return null;
        }
    }

    private async Task<IEnumerable<ExternalCalendar>> DiscoverCalendars(string calendarHomeUrl, (string Username, string Password) credentials, CancellationToken cancellationToken)
    {
        try
        {
            var propFindXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <d:propfind xmlns:d=""DAV:"" xmlns:c=""urn:ietf:params:xml:ns:caldav"" xmlns:cs=""http://calendarserver.org/ns/"">
                  <d:prop>
                    <d:resourcetype />
                    <d:displayname />
                    <cs:getctag />
                    <c:calendar-description />
                    <c:calendar-color />
                  </d:prop>
                </d:propfind>";

            var fullUrl = calendarHomeUrl.StartsWith("http") ? calendarHomeUrl : $"{ICloudCalDAVBaseUrl}{calendarHomeUrl}";

            using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), fullUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", 
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}")));
            request.Headers.Add("Depth", "1");
            request.Content = new StringContent(propFindXml, Encoding.UTF8, "application/xml");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(responseContent);
            
            var calendars = new List<ExternalCalendar>();
            var responseElements = doc.Descendants(XName.Get("response", CalDAVNamespace));

            foreach (var responseElement in responseElements)
            {
                var hrefElement = responseElement.Element(XName.Get("href", CalDAVNamespace));
                var propstatElement = responseElement.Element(XName.Get("propstat", CalDAVNamespace));
                var propElement = propstatElement?.Element(XName.Get("prop", CalDAVNamespace));
                var resourceTypeElement = propElement?.Element(XName.Get("resourcetype", CalDAVNamespace));
                
                // Check if this is a calendar resource
                if (resourceTypeElement?.Element(XName.Get("calendar", CalDAVCalendarNamespace)) != null)
                {
                    var displayName = propElement?.Element(XName.Get("displayname", CalDAVNamespace))?.Value ?? "Unnamed Calendar";
                    var description = propElement?.Element(XName.Get("calendar-description", CalDAVCalendarNamespace))?.Value;
                    var color = propElement?.Element(XName.Get("calendar-color", "http://apple.com/ns/ical/"))?.Value ?? "#3174ad";
                    
                    var calendarUrl = hrefElement?.Value;
                    if (!string.IsNullOrEmpty(calendarUrl))
                    {
                        var calendarId = EncodeCalendarId(calendarUrl);
                        
                        calendars.Add(new ExternalCalendar(
                            calendarId,
                            displayName,
                            description,
                            displayName.ToLower().Contains("calendar"), // Heuristic for primary calendar
                            CalendarAccessRole.Writer, // Assume write access for owned calendars
                            color,
                            "#FFFFFF", // Default foreground color
                            "UTC", // Default timezone
                            false
                        ));
                    }
                }
            }

            return calendars;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover calendars in home set {CalendarHomeUrl}", calendarHomeUrl);
            return [];
        }
    }

    private static string EncodeCalendarId(string calendarUrl)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(calendarUrl));
    }

    private static string DecodeCalendarId(string calendarId)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(calendarId));
    }

    private static string BuildCalendarQueryXml(DateTime startDate, DateTime endDate)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <c:calendar-query xmlns:d=""DAV:"" xmlns:c=""urn:ietf:params:xml:ns:caldav"">
              <d:prop>
                <d:getetag />
                <c:calendar-data />
              </d:prop>
              <c:filter>
                <c:comp-filter name=""VCALENDAR"">
                  <c:comp-filter name=""VEVENT"">
                    <c:time-range start=""{startDate:yyyyMMddTHHmmssZ}"" end=""{endDate:yyyyMMddTHHmmssZ}""/>
                  </c:comp-filter>
                </c:comp-filter>
              </c:filter>
            </c:calendar-query>";
    }

    private async Task<IEnumerable<ExternalCalendarEvent>> ParseCalendarEventsFromResponse(
        string responseContent, 
        string calendarUrl, 
        (string Username, string Password) credentials, 
        CancellationToken cancellationToken)
    {
        try
        {
            var events = new List<ExternalCalendarEvent>();
            var doc = XDocument.Parse(responseContent);
            var responseElements = doc.Descendants(XName.Get("response", CalDAVNamespace));

            foreach (var responseElement in responseElements)
            {
                var propstatElement = responseElement.Element(XName.Get("propstat", CalDAVNamespace));
                var propElement = propstatElement?.Element(XName.Get("prop", CalDAVNamespace));
                var calendarDataElement = propElement?.Element(XName.Get("calendar-data", CalDAVCalendarNamespace));
                
                if (!string.IsNullOrEmpty(calendarDataElement?.Value))
                {
                    var iCalData = calendarDataElement.Value;
                    var parsedEvents = ParseICalendarEvents(iCalData);
                    events.AddRange(parsedEvents);
                }
            }

            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse calendar events from CalDAV response");
            return [];
        }
    }

    private static IEnumerable<ExternalCalendarEvent> ParseICalendarEvents(string iCalData)
    {
        var events = new List<ExternalCalendarEvent>();
        
        try
        {
            // Basic iCalendar parsing - in a real implementation, you'd use a proper iCalendar library
            var lines = iCalData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            ExternalCalendarEvent? currentEvent = null;
            var eventBuilder = new Dictionary<string, string>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                if (trimmedLine.StartsWith("BEGIN:VEVENT"))
                {
                    eventBuilder.Clear();
                }
                else if (trimmedLine.StartsWith("END:VEVENT"))
                {
                    if (eventBuilder.Count > 0)
                    {
                        var parsedEvent = BuildEventFromProperties(eventBuilder);
                        if (parsedEvent != null)
                        {
                            events.Add(parsedEvent);
                        }
                    }
                    eventBuilder.Clear();
                }
                else if (trimmedLine.Contains(':'))
                {
                    var colonIndex = trimmedLine.IndexOf(':');
                    var property = trimmedLine.Substring(0, colonIndex);
                    var value = trimmedLine.Substring(colonIndex + 1);
                    
                    // Handle property parameters (e.g., DTSTART;VALUE=DATE:20230101)
                    var propertyParts = property.Split(';');
                    var propertyName = propertyParts[0];
                    
                    eventBuilder[propertyName] = value;
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - return what we could parse
        }

        return events;
    }

    private static ExternalCalendarEvent? BuildEventFromProperties(Dictionary<string, string> properties)
    {
        try
        {
            if (!properties.ContainsKey("UID"))
                return null;

            var eventId = properties["UID"];
            var title = properties.GetValueOrDefault("SUMMARY", "Untitled Event");
            var description = properties.GetValueOrDefault("DESCRIPTION");
            var location = properties.GetValueOrDefault("LOCATION");
            
            var startTime = ParseICalDateTime(properties.GetValueOrDefault("DTSTART"));
            var endTime = ParseICalDateTime(properties.GetValueOrDefault("DTEND"));
            var isAllDay = properties.GetValueOrDefault("DTSTART", "").Contains("VALUE=DATE");
            
            var createdTime = ParseICalDateTime(properties.GetValueOrDefault("CREATED")) ?? DateTime.UtcNow;
            var updatedTime = ParseICalDateTime(properties.GetValueOrDefault("LAST-MODIFIED")) ?? DateTime.UtcNow;

            return new ExternalCalendarEvent(
                eventId,
                title,
                description,
                startTime ?? DateTime.UtcNow,
                endTime ?? DateTime.UtcNow.AddHours(1),
                isAllDay,
                location,
                [], // Attendees would require more complex parsing
                [], // Recurrence would require RRULE parsing
                [], // Reminders would require VALARM parsing
                [], // Attachments would require ATTACH parsing
                createdTime,
                updatedTime,
                ExternalEventStatus.Confirmed,
                "public",
                new Dictionary<string, object>
                {
                    ["icalData"] = string.Join("\n", properties.Select(p => $"{p.Key}:{p.Value}"))
                }
            );
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? ParseICalDateTime(string? iCalDateTime)
    {
        if (string.IsNullOrEmpty(iCalDateTime))
            return null;

        try
        {
            // Handle different iCalendar date formats
            if (iCalDateTime.Length == 8) // YYYYMMDD
            {
                return DateTime.ParseExact(iCalDateTime, "yyyyMMdd", null);
            }
            else if (iCalDateTime.Length == 15 && iCalDateTime.EndsWith("Z")) // YYYYMMDDTHHMMSSZ
            {
                return DateTime.ParseExact(iCalDateTime, "yyyyMMddTHHmmssZ", null);
            }
            else if (iCalDateTime.Length == 15) // YYYYMMDDTHHMMSS
            {
                return DateTime.ParseExact(iCalDateTime, "yyyyMMddTHHmmss", null);
            }
        }
        catch
        {
            // Fallback to standard parsing
            if (DateTime.TryParse(iCalDateTime, out var result))
                return result;
        }

        return null;
    }

    private static string ConvertToICalendar(ExternalEventCreateRequest eventData, string eventId)
    {
        var startFormat = eventData.IsAllDay ? "VALUE=DATE:{0:yyyyMMdd}" : "{0:yyyyMMddTHHmmssZ}";
        var endFormat = eventData.IsAllDay ? "VALUE=DATE:{0:yyyyMMdd}" : "{0:yyyyMMddTHHmmssZ}";

        var ical = new StringBuilder();
        ical.AppendLine("BEGIN:VCALENDAR");
        ical.AppendLine("VERSION:2.0");
        ical.AppendLine("PRODID:-//WhoAndWhat//Calendar Sync//EN");
        ical.AppendLine("BEGIN:VEVENT");
        ical.AppendLine($"UID:{eventId}");
        ical.AppendLine($"DTSTART:{string.Format(startFormat, eventData.StartTime)}");
        ical.AppendLine($"DTEND:{string.Format(endFormat, eventData.EndTime)}");
        ical.AppendLine($"SUMMARY:{EscapeICalText(eventData.Title)}");
        
        if (!string.IsNullOrEmpty(eventData.Description))
        {
            ical.AppendLine($"DESCRIPTION:{EscapeICalText(eventData.Description)}");
        }
        
        if (!string.IsNullOrEmpty(eventData.Location))
        {
            ical.AppendLine($"LOCATION:{EscapeICalText(eventData.Location)}");
        }

        ical.AppendLine($"CREATED:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        ical.AppendLine($"LAST-MODIFIED:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        ical.AppendLine("STATUS:CONFIRMED");
        ical.AppendLine("END:VEVENT");
        ical.AppendLine("END:VCALENDAR");

        return ical.ToString();
    }

    private static string ConvertToICalendar(ExternalEventUpdateRequest eventData, string eventId)
    {
        var startFormat = eventData.IsAllDay ? "VALUE=DATE:{0:yyyyMMdd}" : "{0:yyyyMMddTHHmmssZ}";
        var endFormat = eventData.IsAllDay ? "VALUE=DATE:{0:yyyyMMdd}" : "{0:yyyyMMddTHHmmssZ}";

        var ical = new StringBuilder();
        ical.AppendLine("BEGIN:VCALENDAR");
        ical.AppendLine("VERSION:2.0");
        ical.AppendLine("PRODID:-//WhoAndWhat//Calendar Sync//EN");
        ical.AppendLine("BEGIN:VEVENT");
        ical.AppendLine($"UID:{eventId}");
        ical.AppendLine($"DTSTART:{string.Format(startFormat, eventData.StartTime)}");
        ical.AppendLine($"DTEND:{string.Format(endFormat, eventData.EndTime)}");
        ical.AppendLine($"SUMMARY:{EscapeICalText(eventData.Title)}");
        
        if (!string.IsNullOrEmpty(eventData.Description))
        {
            ical.AppendLine($"DESCRIPTION:{EscapeICalText(eventData.Description)}");
        }
        
        if (!string.IsNullOrEmpty(eventData.Location))
        {
            ical.AppendLine($"LOCATION:{EscapeICalText(eventData.Location)}");
        }

        ical.AppendLine($"LAST-MODIFIED:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        ical.AppendLine("STATUS:CONFIRMED");
        ical.AppendLine("END:VEVENT");
        ical.AppendLine("END:VCALENDAR");

        return ical.ToString();
    }

    private static string EscapeICalText(string text)
    {
        return text.Replace("\\", "\\\\")
                  .Replace(",", "\\,")
                  .Replace(";", "\\;")
                  .Replace("\n", "\\n")
                  .Replace("\r", "");
    }

    private async Task<ExternalCalendarEvent?> GetSingleEvent(string eventUrl, (string Username, string Password) credentials, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, eventUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", 
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}")));

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var iCalData = await response.Content.ReadAsStringAsync(cancellationToken);
            var events = ParseICalendarEvents(iCalData);
            
            return events.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string GenerateSyncToken(string calendarUrl, DateTime timestamp)
    {
        var tokenData = $"{calendarUrl}|{timestamp:O}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData));
    }

    private static async Task EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}. Content: {errorContent}");
        }
    }

    // Internal mapping methods for conversion
    private static ExternalAttendeeStatus MapAttendeeStatus(InternalAttendeeStatus status) =>
        status switch
        {
            InternalAttendeeStatus.Accepted => ExternalAttendeeStatus.Accepted,
            InternalAttendeeStatus.Declined => ExternalAttendeeStatus.Declined,
            InternalAttendeeStatus.Tentative => ExternalAttendeeStatus.Tentative,
            _ => ExternalAttendeeStatus.NeedsAction
        };

    private static InternalAttendeeStatus MapExternalAttendeeStatus(ExternalAttendeeStatus status) =>
        status switch
        {
            ExternalAttendeeStatus.Accepted => InternalAttendeeStatus.Accepted,
            ExternalAttendeeStatus.Declined => InternalAttendeeStatus.Declined,
            ExternalAttendeeStatus.Tentative => InternalAttendeeStatus.Tentative,
            _ => InternalAttendeeStatus.NeedsAction
        };

    private static ExternalReminderMethod MapReminderMethod(InternalReminderMethod method) =>
        method switch
        {
            InternalReminderMethod.Email => ExternalReminderMethod.Email,
            InternalReminderMethod.Popup => ExternalReminderMethod.Popup,
            InternalReminderMethod.Push => ExternalReminderMethod.Push,
            _ => ExternalReminderMethod.Popup
        };

    private static InternalReminderMethod MapExternalReminderMethod(ExternalReminderMethod method) =>
        method switch
        {
            ExternalReminderMethod.Email => InternalReminderMethod.Email,
            ExternalReminderMethod.Popup => InternalReminderMethod.Popup,
            ExternalReminderMethod.Push => InternalReminderMethod.Push,
            _ => InternalReminderMethod.Popup
        };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}