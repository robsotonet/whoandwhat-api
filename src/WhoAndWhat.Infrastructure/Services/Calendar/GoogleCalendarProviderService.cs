using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services.Calendar;

/// <summary>
/// Google Calendar provider implementation with OAuth 2.0 authentication and comprehensive API integration
/// </summary>
public class GoogleCalendarProviderService : ICalendarProviderService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleCalendarProviderService> _logger;
    private readonly CalendarSyncSettings _settings;
    private readonly GoogleCalendarProviderSettings _googleSettings;
    private bool _disposed;

    private const string GoogleCalendarApiBaseUrl = "https://www.googleapis.com/calendar/v3";
    private const string GoogleOAuthBaseUrl = "https://oauth2.googleapis.com/token";

    public CalendarProvider ProviderType => CalendarProvider.Google;

    public GoogleCalendarProviderService(
        HttpClient httpClient,
        IOptions<CalendarSyncSettings> settings,
        ILogger<GoogleCalendarProviderService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _googleSettings = _settings.Providers[CalendarProvider.Google];
        ConfigureHttpClient();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{GoogleCalendarApiBaseUrl}/users/me/calendarList?maxResults=1");
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
            _logger.LogWarning(ex, "Error checking Google Calendar API availability");
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
            _logger.LogInformation("Starting Google Calendar authentication for user {UserId}", userId);

            var tokenRequest = new Dictionary<string, string>
            {
                ["client_id"] = _googleSettings.ClientId,
                ["client_secret"] = _googleSettings.ClientSecret,
                ["code"] = authorizationCode,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            };

            var tokenResponse = await SendTokenRequest(tokenRequest, cancellationToken);
            
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
                tokenResponse.Scopes ?? ["https://www.googleapis.com/auth/calendar"],
                null,
                userInfo.Email,
                userInfo.Name
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate user {UserId} with Google Calendar", userId);
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
            _logger.LogInformation("Refreshing Google Calendar tokens for user {UserId}", userId);

            var refreshRequest = new Dictionary<string, string>
            {
                ["client_id"] = _googleSettings.ClientId,
                ["client_secret"] = _googleSettings.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var tokenResponse = await SendTokenRequest(refreshRequest, cancellationToken);

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
                tokenResponse.RefreshToken ?? refreshToken, // Google might not return new refresh token
                DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                tokenResponse.Scopes ?? ["https://www.googleapis.com/auth/calendar"],
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

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{GoogleCalendarApiBaseUrl}/users/me/calendarList");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var calendarList = JsonSerializer.Deserialize<GoogleCalendarListResponse>(responseContent, GetJsonOptions());

            var calendars = new List<ExternalCalendar>();

            if (calendarList?.Items != null)
            {
                foreach (var cal in calendarList.Items)
                {
                    calendars.Add(new ExternalCalendar(
                        cal.Id,
                        cal.Summary ?? "Unnamed Calendar",
                        cal.Description,
                        cal.Primary ?? false,
                        MapAccessRole(cal.AccessRole),
                        cal.BackgroundColor,
                        cal.ForegroundColor,
                        cal.TimeZone ?? "UTC",
                        cal.Hidden ?? false
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

            var queryParams = new List<string>
            {
                $"timeMin={startDate:yyyy-MM-ddTHH:mm:ssZ}",
                $"timeMax={endDate:yyyy-MM-ddTHH:mm:ssZ}",
                "singleEvents=true",
                "orderBy=startTime"
            };

            if (!string.IsNullOrEmpty(syncToken))
            {
                queryParams.Add($"syncToken={syncToken}");
            }

            var queryString = string.Join("&", queryParams);
            var url = $"{GoogleCalendarApiBaseUrl}/calendars/{Uri.EscapeDataString(calendarId)}/events?{queryString}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var eventList = JsonSerializer.Deserialize<GoogleEventsResponse>(responseContent, GetJsonOptions());

            var events = new List<ExternalCalendarEvent>();

            if (eventList?.Items != null)
            {
                foreach (var evt in eventList.Items)
                {
                    events.Add(ConvertGoogleEventToExternal(evt));
                }
            }

            _logger.LogInformation("Retrieved {EventCount} events for user {UserId} from calendar {CalendarId}", 
                events.Count, userId, calendarId);

            return new ExternalCalendarEventsResult(
                events,
                eventList?.NextSyncToken,
                eventList?.NextPageToken,
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

            var googleEvent = ConvertToGoogleEvent(eventData);
            var json = JsonSerializer.Serialize(googleEvent, GetJsonOptions());

            var url = $"{GoogleCalendarApiBaseUrl}/calendars/{Uri.EscapeDataString(calendarId)}/events";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var createdEvent = JsonSerializer.Deserialize<GoogleEvent>(responseContent, GetJsonOptions());

            return new ExternalEventResult(
                true,
                createdEvent?.Id ?? string.Empty,
                ConvertGoogleEventToExternal(createdEvent!),
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

            var googleEvent = ConvertToGoogleEvent(eventData);
            var json = JsonSerializer.Serialize(googleEvent, GetJsonOptions());

            var url = $"{GoogleCalendarApiBaseUrl}/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}";
            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var updatedEvent = JsonSerializer.Deserialize<GoogleEvent>(responseContent, GetJsonOptions());

            return new ExternalEventResult(
                true,
                updatedEvent?.Id ?? eventId,
                ConvertGoogleEventToExternal(updatedEvent!),
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

            var url = $"{GoogleCalendarApiBaseUrl}/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}";
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
                await Task.Delay(_googleSettings.RateLimit.RequestDelayMs, cancellationToken);
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
                await Task.Delay(_googleSettings.RateLimit.RequestDelayMs, cancellationToken);
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

            var request = new
            {
                timeMin = timeRanges.Min(tr => tr.Start).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                timeMax = timeRanges.Max(tr => tr.End).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                items = calendarIds.Select(id => new { id }).ToArray()
            };

            var json = JsonSerializer.Serialize(request, GetJsonOptions());
            var url = $"{GoogleCalendarApiBaseUrl}/freeBusy";
            
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var freeBusyResponse = JsonSerializer.Deserialize<GoogleFreeBusyResponse>(responseContent, GetJsonOptions());

            var freeBusyInfo = new List<CalendarFreeBusy>();
            
            if (freeBusyResponse?.Calendars != null)
            {
                foreach (var calendar in freeBusyResponse.Calendars)
                {
                    var busyTimes = calendar.Value.Busy?.Select(b => new TimeRange(
                        DateTime.Parse(b.Start), 
                        DateTime.Parse(b.End)
                    )).ToList() ?? [];

                    freeBusyInfo.Add(new CalendarFreeBusy(
                        calendar.Key,
                        busyTimes,
                        calendar.Value.Errors?.Select(e => e.Reason ?? "Unknown error").ToList() ?? []
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

            var watchRequest = new
            {
                id = Guid.NewGuid().ToString(),
                type = "web_hook",
                address = webhookUrl,
                expiration = ((DateTimeOffset)expirationTime).ToUnixTimeMilliseconds().ToString()
            };

            var json = JsonSerializer.Serialize(watchRequest, GetJsonOptions());
            var url = $"{GoogleCalendarApiBaseUrl}/calendars/{Uri.EscapeDataString(calendarId)}/events/watch";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var watchResponse = JsonSerializer.Deserialize<GoogleWatchResponse>(responseContent, GetJsonOptions());

            return new CalendarWatchResult(
                true,
                watchResponse?.Id ?? string.Empty,
                watchResponse?.ResourceId ?? string.Empty,
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

            var stopRequest = new
            {
                id = watchId,
                resourceId = watchId // Google uses the same ID for both
            };

            var json = JsonSerializer.Serialize(stopRequest, GetJsonOptions());
            var url = $"{GoogleCalendarApiBaseUrl}/channels/stop";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

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
            _logger.LogInformation("Processing Google Calendar webhook notification");

            // Google Calendar sends notifications via headers, not body content
            if (headers.TryGetValue("X-Goog-Channel-Id", out var channelId) &&
                headers.TryGetValue("X-Goog-Resource-State", out var resourceState))
            {
                var changeType = MapGoogleResourceState(resourceState);
                
                return new WebhookProcessResult(
                    true,
                    changeType,
                    channelId,
                    headers.GetValueOrDefault("X-Goog-Resource-Id"),
                    null
                );
            }

            return new WebhookProcessResult(
                false,
                WebhookChangeType.Unknown,
                null,
                null,
                "Invalid webhook headers"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Google Calendar webhook");
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
        // Google Calendar API rate limits are typically:
        // - 1,000,000 queries per day
        // - 100 queries per 100 seconds per user
        return new ProviderRateLimitStatus(
            _googleSettings.RateLimit.RequestsPerMinute,
            _googleSettings.RateLimit.RequestsPerMinute, // Assuming we haven't tracked usage
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
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{GoogleCalendarApiBaseUrl}/users/me/calendarList?maxResults=1");
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
                "summary", "description", "start", "end", "location", 
                "attendees", "recurrence", "attachments", "reminders"
            ],
            MaxBatchSize: TimeSpan.FromDays(365), // Google allows up to 1 year of events
            RateLimitPerMinute: _googleSettings.RateLimit.RequestsPerMinute
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
            CalendarProvider.Google
        );
    }

    // Private helper methods

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(GoogleCalendarApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WhoAndWhat-CalendarSync/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(_googleSettings.TimeoutSeconds);
    }

    private async Task<GoogleTokenResponse> SendTokenRequest(
        Dictionary<string, string> parameters, 
        CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(parameters);
        
        using var request = new HttpRequestMessage(HttpMethod.Post, GoogleOAuthBaseUrl);
        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseContent, GetJsonOptions());
            return tokenResponse ?? new GoogleTokenResponse { Success = false, Error = "Failed to deserialize token response" };
        }

        var errorResponse = JsonSerializer.Deserialize<GoogleErrorResponse>(responseContent, GetJsonOptions());
        return new GoogleTokenResponse 
        { 
            Success = false, 
            Error = errorResponse?.ErrorDescription ?? errorResponse?.Error ?? "Unknown error"
        };
    }

    private async Task<(string Email, string Name)> GetUserInfo(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var userInfo = JsonSerializer.Deserialize<GoogleUserInfoResponse>(responseContent, GetJsonOptions());

            return (userInfo?.Email ?? "Unknown", userInfo?.Name ?? "Unknown");
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

    private ExternalCalendarEvent ConvertGoogleEventToExternal(GoogleEvent googleEvent)
    {
        return new ExternalCalendarEvent(
            googleEvent.Id ?? string.Empty,
            googleEvent.Summary ?? "Untitled Event",
            googleEvent.Description,
            ParseDateTime(googleEvent.Start?.DateTime, googleEvent.Start?.Date),
            ParseDateTime(googleEvent.End?.DateTime, googleEvent.End?.Date),
            !string.IsNullOrEmpty(googleEvent.Start?.Date),
            googleEvent.Location,
            googleEvent.Attendees?.Select(a => new ExternalAttendee(
                a.Email ?? string.Empty,
                a.DisplayName,
                MapGoogleResponseStatus(a.ResponseStatus),
                a.Optional ?? false,
                a.Organizer ?? false
            )).ToList() ?? [],
            googleEvent.Recurrence?.ToList() ?? [],
            googleEvent.Reminders?.Overrides?.Select(r => new ExternalEventReminder(
                MapGoogleReminderMethod(r.Method),
                r.Minutes ?? 0
            )).ToList() ?? [],
            [], // Attachments would need additional API calls
            DateTime.TryParse(googleEvent.Created, out var created) ? created : DateTime.UtcNow,
            DateTime.TryParse(googleEvent.Updated, out var updated) ? updated : DateTime.UtcNow,
            googleEvent.Status == "cancelled" ? ExternalEventStatus.Cancelled : ExternalEventStatus.Confirmed,
            googleEvent.Visibility ?? "default",
            new Dictionary<string, object>
            {
                ["googleEventId"] = googleEvent.Id ?? string.Empty,
                ["htmlLink"] = googleEvent.HtmlLink ?? string.Empty,
                ["hangoutLink"] = googleEvent.HangoutLink ?? string.Empty
            }
        );
    }

    private static DateTime ParseDateTime(string? dateTime, string? date)
    {
        if (!string.IsNullOrEmpty(dateTime) && DateTime.TryParse(dateTime, out var dt))
            return dt;
        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var d))
            return d;
        return DateTime.UtcNow;
    }

    private static object ConvertToGoogleEvent(ExternalEventCreateRequest eventData)
    {
        var googleEvent = new
        {
            summary = eventData.Title,
            description = eventData.Description,
            location = eventData.Location,
            start = eventData.IsAllDay 
                ? new { date = eventData.StartTime.ToString("yyyy-MM-dd") }
                : new { dateTime = eventData.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ") },
            end = eventData.IsAllDay 
                ? new { date = eventData.EndTime.ToString("yyyy-MM-dd") }
                : new { dateTime = eventData.EndTime.ToString("yyyy-MM-ddTHH:mm:ssZ") },
            attendees = eventData.Attendees?.Select(a => new
            {
                email = a.Email,
                displayName = a.DisplayName,
                optional = a.IsOptional,
                organizer = a.IsOrganizer,
                responseStatus = MapToGoogleResponseStatus(a.Status)
            }).ToArray(),
            recurrence = eventData.Recurrence?.ToArray(),
            reminders = eventData.Reminders?.Any() == true ? new
            {
                useDefault = false,
                overrides = eventData.Reminders.Select(r => new
                {
                    method = MapToGoogleReminderMethod(r.Method),
                    minutes = r.MinutesBeforeStart
                }).ToArray()
            } : new { useDefault = true }
        };

        return googleEvent;
    }

    private static object ConvertToGoogleEvent(ExternalEventUpdateRequest eventData)
    {
        var googleEvent = new
        {
            summary = eventData.Title,
            description = eventData.Description,
            location = eventData.Location,
            start = eventData.IsAllDay 
                ? new { date = eventData.StartTime.ToString("yyyy-MM-dd") }
                : new { dateTime = eventData.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ") },
            end = eventData.IsAllDay 
                ? new { date = eventData.EndTime.ToString("yyyy-MM-dd") }
                : new { dateTime = eventData.EndTime.ToString("yyyy-MM-ddTHH:mm:ssZ") },
            attendees = eventData.Attendees?.Select(a => new
            {
                email = a.Email,
                displayName = a.DisplayName,
                optional = a.IsOptional,
                organizer = a.IsOrganizer,
                responseStatus = MapToGoogleResponseStatus(a.Status)
            }).ToArray(),
            recurrence = eventData.Recurrence?.ToArray(),
            reminders = eventData.Reminders?.Any() == true ? new
            {
                useDefault = false,
                overrides = eventData.Reminders.Select(r => new
                {
                    method = MapToGoogleReminderMethod(r.Method),
                    minutes = r.MinutesBeforeStart
                }).ToArray()
            } : new { useDefault = true }
        };

        return googleEvent;
    }

    // Mapping helper methods
    private static CalendarAccessRole MapAccessRole(string? accessRole) =>
        accessRole?.ToLowerInvariant() switch
        {
            "owner" => CalendarAccessRole.Owner,
            "writer" => CalendarAccessRole.Writer,
            "reader" => CalendarAccessRole.Reader,
            _ => CalendarAccessRole.Reader
        };

    private static ExternalAttendeeStatus MapGoogleResponseStatus(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "accepted" => ExternalAttendeeStatus.Accepted,
            "declined" => ExternalAttendeeStatus.Declined,
            "tentative" => ExternalAttendeeStatus.Tentative,
            _ => ExternalAttendeeStatus.NeedsAction
        };

    private static string MapToGoogleResponseStatus(ExternalAttendeeStatus status) =>
        status switch
        {
            ExternalAttendeeStatus.Accepted => "accepted",
            ExternalAttendeeStatus.Declined => "declined",
            ExternalAttendeeStatus.Tentative => "tentative",
            _ => "needsAction"
        };

    private static ExternalReminderMethod MapGoogleReminderMethod(string? method) =>
        method?.ToLowerInvariant() switch
        {
            "email" => ExternalReminderMethod.Email,
            "popup" => ExternalReminderMethod.Popup,
            _ => ExternalReminderMethod.Popup
        };

    private static string MapToGoogleReminderMethod(ExternalReminderMethod method) =>
        method switch
        {
            ExternalReminderMethod.Email => "email",
            ExternalReminderMethod.Popup => "popup",
            _ => "popup"
        };

    private static WebhookChangeType MapGoogleResourceState(string resourceState) =>
        resourceState?.ToLowerInvariant() switch
        {
            "exists" => WebhookChangeType.Updated,
            "not_exists" => WebhookChangeType.Deleted,
            "sync" => WebhookChangeType.Sync,
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

// Google API response models
internal record GoogleTokenResponse
{
    public bool Success { get; init; } = true;
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresIn { get; init; }
    public string[]? Scopes { get; init; }
    public string? Error { get; init; }
}

internal record GoogleErrorResponse
{
    public string? Error { get; init; }
    public string? ErrorDescription { get; init; }
}

internal record GoogleUserInfoResponse
{
    public string? Email { get; init; }
    public string? Name { get; init; }
}

internal record GoogleCalendarListResponse
{
    public GoogleCalendar[]? Items { get; init; }
}

internal record GoogleCalendar
{
    public string? Id { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public bool? Primary { get; init; }
    public string? AccessRole { get; init; }
    public string? BackgroundColor { get; init; }
    public string? ForegroundColor { get; init; }
    public string? TimeZone { get; init; }
    public bool? Hidden { get; init; }
}

internal record GoogleEventsResponse
{
    public GoogleEvent[]? Items { get; init; }
    public string? NextSyncToken { get; init; }
    public string? NextPageToken { get; init; }
}

internal record GoogleEvent
{
    public string? Id { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? Location { get; init; }
    public string? Status { get; init; }
    public string? Visibility { get; init; }
    public string? Created { get; init; }
    public string? Updated { get; init; }
    public string? HtmlLink { get; init; }
    public string? HangoutLink { get; init; }
    public GoogleEventDateTime? Start { get; init; }
    public GoogleEventDateTime? End { get; init; }
    public GoogleAttendee[]? Attendees { get; init; }
    public string[]? Recurrence { get; init; }
    public GoogleEventReminders? Reminders { get; init; }
}

internal record GoogleEventDateTime
{
    public string? DateTime { get; init; }
    public string? Date { get; init; }
    public string? TimeZone { get; init; }
}

internal record GoogleAttendee
{
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? ResponseStatus { get; init; }
    public bool? Optional { get; init; }
    public bool? Organizer { get; init; }
}

internal record GoogleEventReminders
{
    public bool UseDefault { get; init; }
    public GoogleReminderOverride[]? Overrides { get; init; }
}

internal record GoogleReminderOverride
{
    public string? Method { get; init; }
    public int? Minutes { get; init; }
}

internal record GoogleFreeBusyResponse
{
    public Dictionary<string, GoogleCalendarFreeBusy>? Calendars { get; init; }
}

internal record GoogleCalendarFreeBusy
{
    public GoogleBusyTime[]? Busy { get; init; }
    public GoogleError[]? Errors { get; init; }
}

internal record GoogleBusyTime
{
    public string Start { get; init; } = string.Empty;
    public string End { get; init; } = string.Empty;
}

internal record GoogleError
{
    public string? Domain { get; init; }
    public string? Reason { get; init; }
}

internal record GoogleWatchResponse
{
    public string? Id { get; init; }
    public string? ResourceId { get; init; }
    public string? ResourceUri { get; init; }
    public string? Expiration { get; init; }
}