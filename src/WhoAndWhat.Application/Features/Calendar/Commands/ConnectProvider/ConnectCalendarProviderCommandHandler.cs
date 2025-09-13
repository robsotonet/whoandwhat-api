using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Calendar.Commands.ConnectProvider;

public class ConnectCalendarProviderCommandHandler : IRequestHandler<ConnectCalendarProviderCommand, Result<CalendarProviderConfigResult>>
{
    private readonly ICalendarProviderService _calendarProviderService;
    private readonly ILogger<ConnectCalendarProviderCommandHandler> _logger;

    public ConnectCalendarProviderCommandHandler(
        ICalendarProviderService calendarProviderService,
        ILogger<ConnectCalendarProviderCommandHandler> logger)
    {
        _calendarProviderService = calendarProviderService ?? throw new ArgumentNullException(nameof(calendarProviderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CalendarProviderConfigResult>> Handle(ConnectCalendarProviderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Connecting calendar provider {Provider} for user {UserId}", 
                request.Provider, request.UserId);

            // Validate the configuration
            var validationResult = ValidateConfiguration(request.Provider, request.Configuration);
            if (!validationResult.IsValid)
            {
                return Result<CalendarProviderConfigResult>.Failure(validationResult.ErrorMessage);
            }

            // Check if the provider is already connected
            var existingConnection = await CheckExistingConnection(request.UserId, request.Provider, cancellationToken);
            if (existingConnection.IsConnected)
            {
                return Result<CalendarProviderConfigResult>.Failure(
                    $"{request.Provider} is already connected for this user");
            }

            // Configure the provider
            var configResult = await _calendarProviderService.ConfigureProviderAsync(
                request.UserId,
                request.Provider,
                request.Configuration,
                cancellationToken
            );

            if (configResult == null)
            {
                return Result<CalendarProviderConfigResult>.Failure(
                    "Failed to configure calendar provider");
            }

            // Log the result
            if (configResult.Success)
            {
                _logger.LogInformation("Successfully configured calendar provider {Provider} for user {UserId}",
                    request.Provider, request.UserId);
            }
            else
            {
                _logger.LogWarning("Failed to configure calendar provider {Provider} for user {UserId}: {Error}",
                    request.Provider, request.UserId, configResult.ErrorMessage);
            }

            return Result<CalendarProviderConfigResult>.Success(configResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting calendar provider {Provider} for user {UserId}", 
                request.Provider, request.UserId);
            return Result<CalendarProviderConfigResult>.Failure(
                "An error occurred while connecting the calendar provider");
        }
    }

    private static (bool IsValid, string ErrorMessage) ValidateConfiguration(
        CalendarProvider provider, 
        CalendarProviderConfiguration configuration)
    {
        // Basic validation
        if (configuration == null)
        {
            return (false, "Configuration cannot be null");
        }

        // Provider-specific validation
        switch (provider)
        {
            case CalendarProvider.Google:
                if (string.IsNullOrEmpty(configuration.ClientId))
                {
                    return (false, "Google Calendar requires Client ID");
                }
                if (string.IsNullOrEmpty(configuration.ClientSecret))
                {
                    return (false, "Google Calendar requires Client Secret");
                }
                if (!configuration.Scopes.Any())
                {
                    return (false, "Google Calendar requires at least one scope");
                }
                break;

            case CalendarProvider.Outlook:
                if (string.IsNullOrEmpty(configuration.ClientId))
                {
                    return (false, "Outlook Calendar requires Application ID");
                }
                if (string.IsNullOrEmpty(configuration.ClientSecret))
                {
                    return (false, "Outlook Calendar requires Application Secret");
                }
                break;

            case CalendarProvider.ICloud:
                // iCloud uses username/password or app-specific passwords
                if (!configuration.CustomSettings.ContainsKey("username") || 
                    !configuration.CustomSettings.ContainsKey("password"))
                {
                    return (false, "iCloud Calendar requires username and password");
                }
                break;

            case CalendarProvider.CalDAV:
                if (!configuration.CustomSettings.ContainsKey("serverUrl"))
                {
                    return (false, "CalDAV requires server URL");
                }
                break;

            default:
                return (false, $"Unsupported calendar provider: {provider}");
        }

        return (true, string.Empty);
    }

    private async Task<(bool IsConnected, string? ErrorMessage)> CheckExistingConnection(
        Guid userId, 
        CalendarProvider provider, 
        CancellationToken cancellationToken)
    {
        try
        {
            // In a real implementation, this would check the database for existing connections
            // For now, return false to allow connection
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existing connection for user {UserId} and provider {Provider}",
                userId, provider);
            return (false, "Error checking existing connection");
        }
    }
}