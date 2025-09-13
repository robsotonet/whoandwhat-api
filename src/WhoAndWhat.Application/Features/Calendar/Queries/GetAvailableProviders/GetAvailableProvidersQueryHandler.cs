using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Calendar.Queries.GetAvailableProviders;

public class GetAvailableProvidersQueryHandler : IRequestHandler<GetAvailableProvidersQuery, Result<AvailableProvidersResponse>>
{
    private readonly ICalendarProviderService _calendarProviderService;
    private readonly ILogger<GetAvailableProvidersQueryHandler> _logger;

    public GetAvailableProvidersQueryHandler(
        ICalendarProviderService calendarProviderService,
        ILogger<GetAvailableProvidersQueryHandler> logger)
    {
        _calendarProviderService = calendarProviderService ?? throw new ArgumentNullException(nameof(calendarProviderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<AvailableProvidersResponse>> Handle(GetAvailableProvidersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting available calendar providers for user {UserId}", request.UserId);

            // Get the list of all supported providers
            var allProviders = GetAllSupportedProviders();

            // Check which providers are already connected for this user
            var connectedProviders = await GetConnectedProviders(request.UserId, cancellationToken);

            // Create the response with provider details
            var availableProviders = allProviders.Select(provider => new AvailableCalendarProvider(
                provider.Provider,
                provider.DisplayName,
                provider.Description,
                connectedProviders.Contains(provider.Provider), // IsConfigured
                true, // IsEnabled (all providers are enabled by default)
                provider.RequiresOAuth,
                provider.RequiredScopes,
                provider.SupportedFeatures,
                provider.Capabilities
            )).ToList();

            var response = new AvailableProvidersResponse(
                availableProviders,
                connectedProviders,
                DateTime.UtcNow
            );

            _logger.LogInformation("Retrieved {ProviderCount} available providers for user {UserId}, {ConnectedCount} connected",
                availableProviders.Count, request.UserId, connectedProviders.Count);

            return Result<AvailableProvidersResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available providers for user {UserId}", request.UserId);
            return Result<AvailableProvidersResponse>.Failure("An error occurred while getting available providers");
        }
    }

    private static List<ProviderInfo> GetAllSupportedProviders()
    {
        return new List<ProviderInfo>
        {
            new ProviderInfo(
                CalendarProvider.Google,
                "Google Calendar",
                "Sync with your Google Calendar to manage events alongside tasks",
                true, // RequiresOAuth
                new List<string> { "https://www.googleapis.com/auth/calendar" },
                new List<string> { "Event Management", "Two-way Sync", "Real-time Updates", "Multiple Calendars" },
                new ProviderCapabilities(
                    true, // SupportsBatchOperations
                    true, // SupportsIncrementalSync
                    true, // SupportsWebhooks
                    true, // SupportsFreeBusy
                    true, // SupportsRecurringEvents
                    true, // SupportsAttendees
                    true, // SupportsReminders
                    1000, // MaxBatchSize
                    100, // MaxRecurringInstances
                    new List<string> { "title", "description", "start", "end", "attendees", "reminders" }
                )
            ),

            new ProviderInfo(
                CalendarProvider.Outlook,
                "Microsoft Outlook",
                "Connect your Outlook calendar to sync work and personal events",
                true, // RequiresOAuth
                new List<string> { "https://graph.microsoft.com/calendars.readwrite" },
                new List<string> { "Event Management", "Two-way Sync", "Exchange Integration", "Teams Integration" },
                new ProviderCapabilities(
                    true, // SupportsBatchOperations
                    true, // SupportsIncrementalSync
                    true, // SupportsWebhooks
                    true, // SupportsFreeBusy
                    true, // SupportsRecurringEvents
                    true, // SupportsAttendees
                    true, // SupportsReminders
                    500, // MaxBatchSize
                    50, // MaxRecurringInstances
                    new List<string> { "subject", "body", "start", "end", "attendees", "reminders" }
                )
            ),

            new ProviderInfo(
                CalendarProvider.ICloud,
                "Apple iCloud",
                "Sync with your iCloud calendar for seamless Apple device integration",
                false, // RequiresOAuth (uses username/password)
                new List<string>(), // No OAuth scopes
                new List<string> { "Event Management", "CalDAV Protocol", "Apple Device Sync" },
                new ProviderCapabilities(
                    false, // SupportsBatchOperations
                    false, // SupportsIncrementalSync
                    false, // SupportsWebhooks
                    true, // SupportsFreeBusy
                    true, // SupportsRecurringEvents
                    false, // SupportsAttendees
                    true, // SupportsReminders
                    50, // MaxBatchSize
                    20, // MaxRecurringInstances
                    new List<string> { "summary", "description", "dtstart", "dtend" }
                )
            ),

            new ProviderInfo(
                CalendarProvider.CalDAV,
                "CalDAV Server",
                "Connect to any CalDAV-compatible calendar server",
                false, // RequiresOAuth (uses username/password)
                new List<string>(), // No OAuth scopes
                new List<string> { "Event Management", "CalDAV Protocol", "Server Flexibility" },
                new ProviderCapabilities(
                    false, // SupportsBatchOperations
                    false, // SupportsIncrementalSync
                    false, // SupportsWebhooks
                    true, // SupportsFreeBusy
                    true, // SupportsRecurringEvents
                    false, // SupportsAttendees
                    true, // SupportsReminders
                    25, // MaxBatchSize
                    15, // MaxRecurringInstances
                    new List<string> { "summary", "description", "dtstart", "dtend" }
                )
            ),

            new ProviderInfo(
                CalendarProvider.Exchange,
                "Microsoft Exchange",
                "Enterprise-grade calendar sync for Exchange servers",
                true, // RequiresOAuth
                new List<string> { "https://outlook.office365.com/EWS.AccessAsUser.All" },
                new List<string> { "Event Management", "Enterprise Features", "Advanced Security", "On-Premise Support" },
                new ProviderCapabilities(
                    true, // SupportsBatchOperations
                    true, // SupportsIncrementalSync
                    false, // SupportsWebhooks (depends on server)
                    true, // SupportsFreeBusy
                    true, // SupportsRecurringEvents
                    true, // SupportsAttendees
                    true, // SupportsReminders
                    200, // MaxBatchSize
                    30, // MaxRecurringInstances
                    new List<string> { "subject", "body", "start", "end", "attendees", "reminders" }
                )
            )
        };
    }

    private async Task<List<CalendarProvider>> GetConnectedProviders(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            // In a real implementation, this would query the database for user's connected providers
            // For now, return a mock list based on user preferences or return empty list
            
            // Mock implementation - return some connected providers for demonstration
            return new List<CalendarProvider>
            {
                CalendarProvider.Google
                // Add other connected providers as needed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connected providers for user {UserId}", userId);
            return new List<CalendarProvider>();
        }
    }
}

// Supporting types for provider information

/// <summary>
/// Internal provider information
/// </summary>
public record ProviderInfo(
    CalendarProvider Provider,
    string DisplayName,
    string Description,
    bool RequiresOAuth,
    List<string> RequiredScopes,
    List<string> SupportedFeatures,
    ProviderCapabilities Capabilities
);