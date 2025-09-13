using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services.Calendar;

/// <summary>
/// Microsoft Outlook Calendar provider implementation with Graph API integration
/// Supports Outlook.com, Office 365, and Exchange Online calendars
/// </summary>
public class OutlookCalendarProviderService : ICalendarProviderService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OutlookCalendarProviderService> _logger;
    private readonly CalendarSyncSettings _settings;
    private readonly OutlookCalendarProviderSettings _outlookSettings;
    private bool _disposed;

    private const string MicrosoftGraphBaseUrl = "https://graph.microsoft.com/v1.0";
    private const string MicrosoftOAuthBaseUrl = "https://login.microsoftonline.com";

    public CalendarProvider ProviderType => CalendarProvider.Outlook;

    public OutlookCalendarProviderService(
        HttpClient httpClient,
        IOptions<CalendarSyncSettings> settings,
        ILogger<OutlookCalendarProviderService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _outlookSettings = _settings.Providers[CalendarProvider.Outlook];
        ConfigureHttpClient();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{MicrosoftGraphBaseUrl}/me/calendars?$top=1");
            request.Headers.Add("Authorization", "Bearer test_token_for_health_check");
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            // We expect 401 (unauthorized) which means the API is available but we need proper auth
            // 503 or timeout would indicate service unavailability
            return response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable;
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
            _logger.LogWarning(ex, "Error checking Microsoft Graph API availability");
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
            _logger.LogInformation("Starting Outlook Calendar authentication for user {UserId}", userId);

            var tokenRequest = new Dictionary<string, string>
            {
                ["client_id"] = _outlookSettings.ClientId,
                ["client_secret"] = _outlookSettings.ClientSecret,
                ["code"] = authorizationCode,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
                ["scope"] = "https://graph.microsoft.com/Calendars.ReadWrite offline_access"
            };

            var tokenEndpoint = $"{MicrosoftOAuthBaseUrl}/{_outlookSettings.TenantId}/oauth2/v2.0/token";
            var tokenResponse = await SendTokenRequest(tokenEndpoint, tokenRequest, cancellationToken);
            
            if (!tokenResponse.Success)
            {
                return new CalendarAuthResult(
                    false,
                    string.Empty,
                    string.Empty,
                    DateTime.UtcNow,
                    [],
                    tokenResponse.Error
                );
            }

            // Verify token by getting user profile
            var userInfo = await GetUserInfo(tokenResponse.AccessToken!, cancellationToken);
            
            return new CalendarAuthResult(
                true,
                tokenResponse.AccessToken!,
                tokenResponse.RefreshToken!,
                DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                tokenResponse.Scopes ?? ["https://graph.microsoft.com/Calendars.ReadWrite"],
                null,
                userInfo.Email,
                userInfo.Name
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate user {UserId} with Outlook Calendar", userId);
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
            _logger.LogInformation("Refreshing Outlook Calendar tokens for user {UserId}", userId);

            var refreshRequest = new Dictionary<string, string>
            {
                ["client_id"] = _outlookSettings.ClientId,
                ["client_secret"] = _outlookSettings.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = "https://graph.microsoft.com/Calendars.ReadWrite offline_access"
            };

            var tokenEndpoint = $"{MicrosoftOAuthBaseUrl}/{_outlookSettings.TenantId}/oauth2/v2.0/token";
            var tokenResponse = await SendTokenRequest(tokenEndpoint, refreshRequest, cancellationToken);

            if (!tokenResponse.Success)
            {
                return new TokenRefreshResult(
                    false,
                    string.Empty,
                    string.Empty,
                    DateTime.UtcNow,
                    [],
                    tokenResponse.Error
                );
            }

            return new TokenRefreshResult(
                true,
                tokenResponse.AccessToken!,
                tokenResponse.RefreshToken ?? refreshToken,
                DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                tokenResponse.Scopes ?? ["https://graph.microsoft.com/Calendars.ReadWrite"],
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

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{MicrosoftGraphBaseUrl}/me/calendars");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var calendarList = JsonSerializer.Deserialize<OutlookCalendarListResponse>(responseContent, GetJsonOptions());

            var calendars = new List<ExternalCalendar>();

            if (calendarList?.Value != null)
            {
                foreach (var cal in calendarList.Value)
                {
                    calendars.Add(new ExternalCalendar(
                        cal.Id ?? string.Empty,
                        cal.Name ?? "Unnamed Calendar",
                        cal.Description,
                        cal.IsDefaultCalendar ?? false,
                        MapOutlookPermissions(cal.CanEdit),
                        cal.HexColor,
                        "#000000", // Outlook doesn't provide foreground color
                        "UTC", // Default timezone, Outlook uses user's timezone
                        false // Outlook doesn't have hidden calendars in this context
                    ));
                }
            }

            _logger.LogInformation("Retrieved {CalendarCount} calendars for user {UserId}", calendars.Count, userId);
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

            var startTimeFilter = $"start/dateTime ge '{startDate:yyyy-MM-ddTHH:mm:ss.fffK}'";
            var endTimeFilter = $"end/dateTime le '{endDate:yyyy-MM-ddTHH:mm:ss.fffK}'";
            var filter = $"$filter={startTimeFilter} and {endTimeFilter}";
            var orderBy = "$orderby=start/dateTime";
            var select = "$select=id,subject,body,start,end,isAllDay,location,attendees,recurrence,responseRequested,showAs,importance,sensitivity,createdDateTime,lastModifiedDateTime,webLink";

            var url = $"{MicrosoftGraphBaseUrl}/me/calendars/{calendarId}/events?{filter}&{orderBy}&{select}";

            // Note: Microsoft Graph doesn't use sync tokens the same way as Google Calendar
            // Instead, it uses delta queries with deltaLink/nextLink for incremental sync
            if (!string.IsNullOrEmpty(syncToken))
            {
                url = syncToken; // syncToken would be a delta link from previous request
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var eventList = JsonSerializer.Deserialize<OutlookEventsResponse>(responseContent, GetJsonOptions());

            var events = new List<ExternalCalendarEvent>();

            if (eventList?.Value != null)
            {
                foreach (var evt in eventList.Value)
                {
                    events.Add(ConvertOutlookEventToExternal(evt));
                }
            }

            _logger.LogInformation("Retrieved {EventCount} events for user {UserId} from calendar {CalendarId}", 
                events.Count, userId, calendarId);

            return new ExternalCalendarEventsResult(
                events,
                eventList?.ODataDeltaLink, // Next sync token
                eventList?.ODataNextLink, // Next page token
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

            var outlookEvent = ConvertToOutlookEvent(eventData);
            var json = JsonSerializer.Serialize(outlookEvent, GetJsonOptions());

            var url = $"{MicrosoftGraphBaseUrl}/me/calendars/{calendarId}/events";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var createdEvent = JsonSerializer.Deserialize<OutlookEvent>(responseContent, GetJsonOptions());

            return new ExternalEventResult(
                true,
                createdEvent?.Id ?? string.Empty,
                ConvertOutlookEventToExternal(createdEvent!),
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

            var outlookEvent = ConvertToOutlookEvent(eventData);
            var json = JsonSerializer.Serialize(outlookEvent, GetJsonOptions());

            var url = $"{MicrosoftGraphBaseUrl}/me/calendars/{calendarId}/events/{eventId}";
            using var request = new HttpRequestMessage(HttpMethod.Patch, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var updatedEvent = JsonSerializer.Deserialize<OutlookEvent>(responseContent, GetJsonOptions());

            return new ExternalEventResult(
                true,
                updatedEvent?.Id ?? eventId,
                ConvertOutlookEventToExternal(updatedEvent!),
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

            var url = $"{MicrosoftGraphBaseUrl}/me/calendars/{calendarId}/events/{eventId}";
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

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
                await Task.Delay(_outlookSettings.RateLimit.RequestDelayMs, cancellationToken);
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
                await Task.Delay(_outlookSettings.RateLimit.RequestDelayMs, cancellationToken);
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

            var schedules = calendarIds.ToList();
            var startTime = timeRanges.Min(tr => tr.Start).ToString("yyyy-MM-ddTHH:mm:ss.fffK");
            var endTime = timeRanges.Max(tr => tr.End).ToString("yyyy-MM-ddTHH:mm:ss.fffK");

            var requestBody = new
            {
                schedules = schedules,
                startTime = new { dateTime = startTime, timeZone = "UTC" },
                endTime = new { dateTime = endTime, timeZone = "UTC" },
                availabilityViewInterval = 60 // 60-minute intervals
            };

            var json = JsonSerializer.Serialize(requestBody, GetJsonOptions());
            var url = $"{MicrosoftGraphBaseUrl}/me/calendar/getSchedule";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var freeBusyResponse = JsonSerializer.Deserialize<OutlookFreeBusyResponse>(responseContent, GetJsonOptions());

            var freeBusyInfo = new List<CalendarFreeBusy>();
            
            if (freeBusyResponse?.Value != null)
            {
                for (int i = 0; i < schedules.Count && i < freeBusyResponse.Value.Length; i++)
                {
                    var schedule = freeBusyResponse.Value[i];
                    var busyTimes = schedule.BusyViewpoints?.Where(b => b == "2") // "2" means busy in Outlook
                        .Select((_, index) => new TimeRange(
                            DateTime.Parse(startTime).AddMinutes(index * 60),
                            DateTime.Parse(startTime).AddMinutes((index + 1) * 60)
                        )).ToList() ?? [];

                    freeBusyInfo.Add(new CalendarFreeBusy(
                        schedules[i],
                        busyTimes,
                        schedule.Error != null ? [schedule.Error.Message ?? "Unknown error"] : []
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
        try
        {
            _logger.LogInformation("Setting up calendar watch for user {UserId} on calendar {CalendarId}", userId, calendarId);

            var subscription = new
            {
                changeType = "created,updated,deleted",
                notificationUrl = webhookUrl,
                resource = $"me/calendars/{calendarId}/events",
                expirationDateTime = expirationTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                clientState = userId.ToString() // Optional client state for validation
            };

            var json = JsonSerializer.Serialize(subscription, GetJsonOptions());
            var url = $"{MicrosoftGraphBaseUrl}/subscriptions";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var subscriptionResponse = JsonSerializer.Deserialize<OutlookSubscriptionResponse>(responseContent, GetJsonOptions());

            return new CalendarWatchResult(
                true,
                subscriptionResponse?.Id ?? string.Empty,
                subscriptionResponse?.Resource ?? string.Empty,
                expirationTime,
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up calendar watch for user {UserId} on calendar {CalendarId}", userId, calendarId);
            return new CalendarWatchResult(
                false,
                string.Empty,
                string.Empty,
                expirationTime,
                ex.Message
            );
        }
    }

    public async Task<CalendarWatchStopResult> StopWatchingAsync(
        Guid userId, 
        string watchId, 
        string accessToken, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Stopping calendar watch {WatchId} for user {UserId}", watchId, userId);

            var url = $"{MicrosoftGraphBaseUrl}/subscriptions/{watchId}";
            
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            return new CalendarWatchStopResult(
                true,
                watchId,
                "Watch stopped successfully"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop calendar watch {WatchId} for user {UserId}", watchId, userId);
            return new CalendarWatchStopResult(
                false,
                watchId,
                ex.Message
            );
        }
    }

    public async Task<WebhookProcessResult> ProcessWebhookAsync(
        string webhookData, 
        IDictionary<string, string> headers, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing Outlook Calendar webhook notification");

            var notification = JsonSerializer.Deserialize<OutlookWebhookNotification>(webhookData, GetJsonOptions());
            
            if (notification?.Value?.Any() == true)
            {
                var firstNotification = notification.Value.First();
                var changeType = MapOutlookChangeType(firstNotification.ChangeType);
                
                return new WebhookProcessResult(
                    true,
                    changeType,
                    firstNotification.SubscriptionId,
                    firstNotification.Resource,
                    firstNotification.ResourceData
                );
            }

            return new WebhookProcessResult(
                false,
                WebhookChangeType.Unknown,
                null,
                null,
                "Invalid webhook payload"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Outlook Calendar webhook");
            return new WebhookProcessResult(
                false,
                WebhookChangeType.Unknown,
                null,
                null,
                ex.Message
            );
        }
    }

    public async Task<ProviderRateLimitStatus> GetRateLimitStatusAsync(CancellationToken cancellationToken = default)
    {
        // Microsoft Graph API rate limits are typically:
        // - 10,000 requests per 10 minutes per application per tenant
        // - Varies by endpoint and license type
        return new ProviderRateLimitStatus(
            _outlookSettings.RateLimit.RequestsPerMinute,
            _outlookSettings.RateLimit.RequestsPerMinute, // Assuming we haven't tracked usage
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
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{MicrosoftGraphBaseUrl}/me/calendars?$top=1");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            return new TokenValidationResult(
                response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                response.IsSuccessStatusCode ? DateTime.UtcNow.AddHours(1) : null // Estimate 1 hour remaining
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
            SupportsBatchOperations: true,
            SupportsWebhooks: true,
            SupportsIncrementalSync: true,
            SupportsFreeBusy: true,
            SupportsRecurringEvents: true,
            SupportsAttendees: true,
            SupportsAttachments: true,
            SupportedEventFields: [
                "subject", "body", "start", "end", "location", "attendees", 
                "recurrence", "attachments", "importance", "sensitivity", "showAs"
            ],
            MaxBatchSize: TimeSpan.FromDays(365), // Outlook allows broad date ranges
            RateLimitPerMinute: _outlookSettings.RateLimit.RequestsPerMinute
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
            CalendarProvider.Outlook
        );
    }

    // Private helper methods

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(MicrosoftGraphBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WhoAndWhat-CalendarSync/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(_outlookSettings.TimeoutSeconds);
    }

    private async Task<OutlookTokenResponse> SendTokenRequest(
        string endpoint,
        Dictionary<string, string> parameters, 
        CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(parameters);
        
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var tokenResponse = JsonSerializer.Deserialize<OutlookTokenResponse>(responseContent, GetJsonOptions());
            return tokenResponse ?? new OutlookTokenResponse { Success = false, Error = "Failed to deserialize token response" };
        }

        var errorResponse = JsonSerializer.Deserialize<OutlookErrorResponse>(responseContent, GetJsonOptions());
        return new OutlookTokenResponse 
        { 
            Success = false, 
            Error = errorResponse?.ErrorDescription ?? errorResponse?.Error ?? "Unknown error"
        };
    }

    private async Task<(string Email, string Name)> GetUserInfo(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{MicrosoftGraphBaseUrl}/me?$select=mail,displayName");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var userInfo = JsonSerializer.Deserialize<OutlookUserInfoResponse>(responseContent, GetJsonOptions());

            return (userInfo?.Mail ?? "Unknown", userInfo?.DisplayName ?? "Unknown");
        }
        catch
        {
            return ("Unknown", "Unknown");
        }
    }

    private static async Task EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}. Content: {errorContent}");
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    private ExternalCalendarEvent ConvertOutlookEventToExternal(OutlookEvent outlookEvent)
    {
        return new ExternalCalendarEvent(
            outlookEvent.Id ?? string.Empty,
            outlookEvent.Subject ?? "Untitled Event",
            outlookEvent.Body?.Content,
            ParseOutlookDateTime(outlookEvent.Start),
            ParseOutlookDateTime(outlookEvent.End),
            outlookEvent.IsAllDay ?? false,
            outlookEvent.Location?.DisplayName,
            outlookEvent.Attendees?.Select(a => new ExternalAttendee(
                a.EmailAddress?.Address ?? string.Empty,
                a.EmailAddress?.Name,
                MapOutlookResponseStatus(a.Status?.Response),
                a.Type == "optional",
                false // Outlook doesn't have explicit organizer flag in attendees
            )).ToList() ?? [],
            outlookEvent.Recurrence != null ? [JsonSerializer.Serialize(outlookEvent.Recurrence)] : [],
            [], // Reminders would need additional processing
            [], // Attachments would need additional API calls
            DateTime.TryParse(outlookEvent.CreatedDateTime, out var created) ? created : DateTime.UtcNow,
            DateTime.TryParse(outlookEvent.LastModifiedDateTime, out var updated) ? updated : DateTime.UtcNow,
            outlookEvent.IsCancelled == true ? ExternalEventStatus.Cancelled : ExternalEventStatus.Confirmed,
            outlookEvent.Sensitivity ?? "normal",
            new Dictionary<string, object>
            {
                ["outlookEventId"] = outlookEvent.Id ?? string.Empty,
                ["webLink"] = outlookEvent.WebLink ?? string.Empty,
                ["importance"] = outlookEvent.Importance ?? "normal",
                ["showAs"] = outlookEvent.ShowAs ?? "busy"
            }
        );
    }

    private static DateTime ParseOutlookDateTime(OutlookDateTime? dateTime)
    {
        if (dateTime?.DateTime != null && DateTime.TryParse(dateTime.DateTime, out var dt))
            return dt;
        return DateTime.UtcNow;
    }

    private static object ConvertToOutlookEvent(ExternalEventCreateRequest eventData)
    {
        var outlookEvent = new
        {
            subject = eventData.Title,
            body = eventData.Description != null ? new { contentType = "text", content = eventData.Description } : null,
            start = new 
            { 
                dateTime = eventData.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"), 
                timeZone = "UTC" 
            },
            end = new 
            { 
                dateTime = eventData.EndTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"), 
                timeZone = "UTC" 
            },
            isAllDay = eventData.IsAllDay,
            location = eventData.Location != null ? new { displayName = eventData.Location } : null,
            attendees = eventData.Attendees?.Select(a => new
            {
                emailAddress = new
                {
                    address = a.Email,
                    name = a.DisplayName
                },
                type = a.IsOptional ? "optional" : "required"
            }).ToArray()
        };

        return outlookEvent;
    }

    private static object ConvertToOutlookEvent(ExternalEventUpdateRequest eventData)
    {
        var outlookEvent = new
        {
            subject = eventData.Title,
            body = eventData.Description != null ? new { contentType = "text", content = eventData.Description } : null,
            start = new 
            { 
                dateTime = eventData.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"), 
                timeZone = "UTC" 
            },
            end = new 
            { 
                dateTime = eventData.EndTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"), 
                timeZone = "UTC" 
            },
            isAllDay = eventData.IsAllDay,
            location = eventData.Location != null ? new { displayName = eventData.Location } : null,
            attendees = eventData.Attendees?.Select(a => new
            {
                emailAddress = new
                {
                    address = a.Email,
                    name = a.DisplayName
                },
                type = a.IsOptional ? "optional" : "required"
            }).ToArray()
        };

        return outlookEvent;
    }

    // Mapping helper methods
    private static CalendarAccessRole MapOutlookPermissions(bool? canEdit) =>
        canEdit == true ? CalendarAccessRole.Writer : CalendarAccessRole.Reader;

    private static ExternalAttendeeStatus MapOutlookResponseStatus(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "accepted" => ExternalAttendeeStatus.Accepted,
            "declined" => ExternalAttendeeStatus.Declined,
            "tentativelyaccepted" => ExternalAttendeeStatus.Tentative,
            _ => ExternalAttendeeStatus.NeedsAction
        };

    private static WebhookChangeType MapOutlookChangeType(string? changeType) =>
        changeType?.ToLowerInvariant() switch
        {
            "created" => WebhookChangeType.Created,
            "updated" => WebhookChangeType.Updated,
            "deleted" => WebhookChangeType.Deleted,
            _ => WebhookChangeType.Unknown
        };

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

// Outlook API response models
internal record OutlookTokenResponse
{
    public bool Success { get; init; } = true;
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresIn { get; init; }
    public string[]? Scopes { get; init; }
    public string? Error { get; init; }
}

internal record OutlookErrorResponse
{
    public string? Error { get; init; }
    public string? ErrorDescription { get; init; }
}

internal record OutlookUserInfoResponse
{
    public string? Mail { get; init; }
    public string? DisplayName { get; init; }
}

internal record OutlookCalendarListResponse
{
    public OutlookCalendar[]? Value { get; init; }
}

internal record OutlookCalendar
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? HexColor { get; init; }
    public bool? IsDefaultCalendar { get; init; }
    public bool? CanEdit { get; init; }
}

internal record OutlookEventsResponse
{
    public OutlookEvent[]? Value { get; init; }
    public string? ODataNextLink { get; init; }
    public string? ODataDeltaLink { get; init; }
}

internal record OutlookEvent
{
    public string? Id { get; init; }
    public string? Subject { get; init; }
    public OutlookEventBody? Body { get; init; }
    public OutlookDateTime? Start { get; init; }
    public OutlookDateTime? End { get; init; }
    public bool? IsAllDay { get; init; }
    public OutlookLocation? Location { get; init; }
    public OutlookAttendee[]? Attendees { get; init; }
    public object? Recurrence { get; init; }
    public string? Importance { get; init; }
    public string? Sensitivity { get; init; }
    public string? ShowAs { get; init; }
    public bool? IsCancelled { get; init; }
    public string? CreatedDateTime { get; init; }
    public string? LastModifiedDateTime { get; init; }
    public string? WebLink { get; init; }
}

internal record OutlookEventBody
{
    public string? ContentType { get; init; }
    public string? Content { get; init; }
}

internal record OutlookDateTime
{
    public string? DateTime { get; init; }
    public string? TimeZone { get; init; }
}

internal record OutlookLocation
{
    public string? DisplayName { get; init; }
}

internal record OutlookAttendee
{
    public OutlookEmailAddress? EmailAddress { get; init; }
    public OutlookResponseStatus? Status { get; init; }
    public string? Type { get; init; }
}

internal record OutlookEmailAddress
{
    public string? Name { get; init; }
    public string? Address { get; init; }
}

internal record OutlookResponseStatus
{
    public string? Response { get; init; }
    public string? Time { get; init; }
}

internal record OutlookFreeBusyResponse
{
    public OutlookScheduleInfo[]? Value { get; init; }
}

internal record OutlookScheduleInfo
{
    public string? ScheduleId { get; init; }
    public string[]? BusyViewpoints { get; init; }
    public OutlookFreeBusyError? Error { get; init; }
}

internal record OutlookFreeBusyError
{
    public string? Code { get; init; }
    public string? Message { get; init; }
}

internal record OutlookSubscriptionResponse
{
    public string? Id { get; init; }
    public string? Resource { get; init; }
    public string? NotificationUrl { get; init; }
    public string? ExpirationDateTime { get; init; }
}

internal record OutlookWebhookNotification
{
    public OutlookNotificationItem[]? Value { get; init; }
}

internal record OutlookNotificationItem
{
    public string? SubscriptionId { get; init; }
    public string? ChangeType { get; init; }
    public string? Resource { get; init; }
    public object? ResourceData { get; init; }
}