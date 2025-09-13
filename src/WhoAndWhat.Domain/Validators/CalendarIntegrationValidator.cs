using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Validators;

/// <summary>
/// Validator for CalendarIntegration entity
/// </summary>
public static class CalendarIntegrationValidator
{
    /// <summary>
    /// Validates a calendar integration for creation
    /// </summary>
    public static ValidationResult ValidateForCreation(CalendarIntegration integration)
    {
        if (integration == null)
            return ValidationResult.Fail("Calendar integration is required");

        var errors = new List<string>();

        // Validate required fields
        ValidateRequiredFields(integration, errors);

        // Validate business rules
        ValidateBusinessRules(integration, errors);

        // Validate configuration
        ValidateConfiguration(integration, errors);

        // Validate provider-specific requirements
        ValidateProviderRequirements(integration, errors);

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates a calendar integration for update
    /// </summary>
    public static ValidationResult ValidateForUpdate(CalendarIntegration integration, CalendarIntegration? existingIntegration = null)
    {
        var result = ValidateForCreation(integration);
        if (!result.IsSuccess)
            return result;

        var errors = new List<string>();

        // Additional validation for updates
        if (existingIntegration != null)
        {
            ValidateUpdateConstraints(integration, existingIntegration, errors);
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates calendar integration for deletion
    /// </summary>
    public static ValidationResult ValidateForDeletion(CalendarIntegration integration)
    {
        if (integration == null)
            return ValidationResult.Fail("Calendar integration is required");

        var errors = new List<string>();

        // Check if integration is currently syncing
        if (integration.HealthStatus == (int)IntegrationHealthStatus.Unknown)
        {
            // Could be in the middle of a sync operation
            errors.Add("Cannot delete integration with unknown health status - sync may be in progress");
        }

        // Warn about data loss implications
        if (integration.TotalEventsSynced > 0)
        {
            // This is more of a warning, but we'll allow deletion
            // In real implementation, you might want to require user confirmation
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates token information
    /// </summary>
    public static ValidationResult ValidateTokens(CalendarIntegration integration)
    {
        if (integration == null)
            return ValidationResult.Fail("Calendar integration is required");

        var errors = new List<string>();

        // Validate access token
        if (string.IsNullOrWhiteSpace(integration.AccessToken))
        {
            errors.Add("Access token is required");
        }
        else if (integration.AccessToken.Length > 2000)
        {
            errors.Add("Access token is too long (max 2000 characters)");
        }

        // Validate token expiration
        if (integration.TokenExpiresAt.HasValue)
        {
            if (integration.TokenExpiresAt.Value <= DateTime.UtcNow)
            {
                errors.Add("Access token has expired");
            }

            if (integration.TokenExpiresAt.Value > DateTime.UtcNow.AddYears(1))
            {
                errors.Add("Access token expiration is too far in the future (max 1 year)");
            }
        }

        // Validate refresh token if present
        if (!string.IsNullOrEmpty(integration.RefreshToken) && integration.RefreshToken.Length > 2000)
        {
            errors.Add("Refresh token is too long (max 2000 characters)");
        }

        // Validate token scopes
        if (!string.IsNullOrEmpty(integration.TokenScopes))
        {
            var scopes = integration.TokenScopes.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var scope in scopes)
            {
                if (scope.Trim().Length > 200)
                {
                    errors.Add($"Token scope '{scope}' is too long (max 200 characters)");
                }
            }
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates sync configuration
    /// </summary>
    public static ValidationResult ValidateSyncConfiguration(CalendarIntegration integration)
    {
        if (integration == null)
            return ValidationResult.Fail("Calendar integration is required");

        var errors = new List<string>();

        // Validate sync direction
        if (!Enum.IsDefined(typeof(SyncDirection), integration.SyncDirection))
        {
            errors.Add("Invalid sync direction");
        }

        // Validate sync interval
        if (integration.SyncInterval < TimeSpan.FromMinutes(1))
        {
            errors.Add("Sync interval must be at least 1 minute");
        }

        if (integration.SyncInterval > TimeSpan.FromDays(30))
        {
            errors.Add("Sync interval cannot exceed 30 days");
        }

        // Validate conflict resolution strategy
        if (!Enum.IsDefined(typeof(ConflictResolutionStrategy), integration.ConflictResolutionStrategy))
        {
            errors.Add("Invalid conflict resolution strategy");
        }

        // Validate conflict tolerance
        if (integration.ConflictToleranceMinutes < 0)
        {
            errors.Add("Conflict tolerance cannot be negative");
        }

        if (integration.ConflictToleranceMinutes > 1440) // 24 hours
        {
            errors.Add("Conflict tolerance cannot exceed 24 hours (1440 minutes)");
        }

        // Validate next sync time
        if (integration.NextScheduledSync.HasValue && 
            integration.NextScheduledSync.Value < DateTime.UtcNow.AddMinutes(-5))
        {
            errors.Add("Next scheduled sync cannot be in the past");
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates webhook configuration
    /// </summary>
    public static ValidationResult ValidateWebhookConfiguration(CalendarIntegration integration)
    {
        if (integration == null)
            return ValidationResult.Fail("Calendar integration is required");

        var errors = new List<string>();

        if (integration.SupportsWebhooks)
        {
            // Validate webhook ID if webhooks are supported
            if (!string.IsNullOrEmpty(integration.WebhookId))
            {
                if (integration.WebhookId.Length > 500)
                {
                    errors.Add("Webhook ID is too long (max 500 characters)");
                }
            }

            // Validate webhook expiration
            if (integration.WebhookExpiresAt.HasValue)
            {
                if (integration.WebhookExpiresAt.Value <= DateTime.UtcNow)
                {
                    errors.Add("Webhook has expired");
                }

                if (integration.WebhookExpiresAt.Value > DateTime.UtcNow.AddYears(1))
                {
                    errors.Add("Webhook expiration is too far in the future (max 1 year)");
                }
            }
        }
        else
        {
            // If webhooks not supported, these fields should be empty
            if (!string.IsNullOrEmpty(integration.WebhookId))
            {
                errors.Add("Webhook ID should not be set when webhooks are not supported");
            }

            if (integration.WebhookExpiresAt.HasValue)
            {
                errors.Add("Webhook expiration should not be set when webhooks are not supported");
            }
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    /// <summary>
    /// Validates health and monitoring configuration
    /// </summary>
    public static ValidationResult ValidateHealthConfiguration(CalendarIntegration integration)
    {
        if (integration == null)
            return ValidationResult.Fail("Calendar integration is required");

        var errors = new List<string>();

        // Validate health status
        if (!Enum.IsDefined(typeof(IntegrationHealthStatus), integration.HealthStatus))
        {
            errors.Add("Invalid health status");
        }

        // Validate consecutive failures count
        if (integration.ConsecutiveFailures < 0)
        {
            errors.Add("Consecutive failures count cannot be negative");
        }

        if (integration.ConsecutiveFailures > 100)
        {
            errors.Add("Consecutive failures count seems unreasonably high (max 100)");
        }

        // Validate error message length
        if (!string.IsNullOrEmpty(integration.LastError) && integration.LastError.Length > 2000)
        {
            errors.Add("Last error message is too long (max 2000 characters)");
        }

        // Validate error time
        if (integration.LastErrorTime.HasValue && integration.LastErrorTime.Value > DateTime.UtcNow)
        {
            errors.Add("Last error time cannot be in the future");
        }

        // Validate health check time
        if (integration.LastHealthCheck.HasValue && integration.LastHealthCheck.Value > DateTime.UtcNow)
        {
            errors.Add("Last health check time cannot be in the future");
        }

        // Validate quarantine rules
        if (integration.IsQuarantined && integration.ConsecutiveFailures == 0)
        {
            errors.Add("Integration cannot be quarantined with zero consecutive failures");
        }

        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }

    private static void ValidateRequiredFields(CalendarIntegration integration, List<string> errors)
    {
        if (integration.UserId == Guid.Empty)
        {
            errors.Add("User ID is required");
        }

        if (string.IsNullOrWhiteSpace(integration.ProviderName))
        {
            errors.Add("Provider name is required");
        }

        if (string.IsNullOrWhiteSpace(integration.AccessToken))
        {
            errors.Add("Access token is required");
        }
    }

    private static void ValidateBusinessRules(CalendarIntegration integration, List<string> errors)
    {
        // Validate calendar provider
        if (!Enum.IsDefined(typeof(CalendarProvider), integration.CalendarProvider))
        {
            errors.Add("Invalid calendar provider");
        }

        // Validate that provider is not 'None' for active integrations
        if (integration.CalendarProvider == (int)CalendarProvider.None && integration.IsEnabled)
        {
            errors.Add("Active integrations must specify a valid calendar provider");
        }

        // Validate sync statistics
        if (integration.TotalEventsSynced < 0)
        {
            errors.Add("Total events synced cannot be negative");
        }

        if (integration.TotalConflictsDetected < 0)
        {
            errors.Add("Total conflicts detected cannot be negative");
        }

        if (integration.TotalConflictsResolved < 0)
        {
            errors.Add("Total conflicts resolved cannot be negative");
        }

        if (integration.TotalConflictsResolved > integration.TotalConflictsDetected)
        {
            errors.Add("Conflicts resolved cannot exceed conflicts detected");
        }

        // Validate first sync time
        if (integration.FirstSyncTime.HasValue && integration.FirstSyncTime.Value > DateTime.UtcNow)
        {
            errors.Add("First sync time cannot be in the future");
        }

        // Validate last sync time
        if (integration.LastSyncTime.HasValue && integration.LastSyncTime.Value > DateTime.UtcNow)
        {
            errors.Add("Last sync time cannot be in the future");
        }

        // Validate sync time consistency
        if (integration.FirstSyncTime.HasValue && integration.LastSyncTime.HasValue &&
            integration.FirstSyncTime.Value > integration.LastSyncTime.Value)
        {
            errors.Add("First sync time cannot be after last sync time");
        }
    }

    private static void ValidateConfiguration(CalendarIntegration integration, List<string> errors)
    {
        // Validate provider name length
        if (integration.ProviderName?.Length > CalendarIntegration.MaxProviderNameLength)
        {
            errors.Add($"Provider name cannot exceed {CalendarIntegration.MaxProviderNameLength} characters");
        }

        // Validate provider account ID
        if (!string.IsNullOrEmpty(integration.ProviderAccountId) && 
            integration.ProviderAccountId.Length > 500)
        {
            errors.Add("Provider account ID cannot exceed 500 characters");
        }

        // Validate default time zone
        if (!string.IsNullOrEmpty(integration.DefaultTimeZone))
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(integration.DefaultTimeZone);
            }
            catch
            {
                errors.Add("Invalid default time zone identifier");
            }
        }

        // Validate primary calendar ID
        if (!string.IsNullOrEmpty(integration.PrimaryCalendarId) && 
            integration.PrimaryCalendarId.Length > 500)
        {
            errors.Add("Primary calendar ID cannot exceed 500 characters");
        }

        // Validate API version
        if (!string.IsNullOrEmpty(integration.ApiVersion) && integration.ApiVersion.Length > 50)
        {
            errors.Add("API version cannot exceed 50 characters");
        }

        // Validate endpoint URL
        if (!string.IsNullOrEmpty(integration.EndpointUrl))
        {
            if (integration.EndpointUrl.Length > 2000)
            {
                errors.Add("Endpoint URL cannot exceed 2000 characters");
            }

            if (!Uri.TryCreate(integration.EndpointUrl, UriKind.Absolute, out var uri))
            {
                errors.Add("Invalid endpoint URL format");
            }
            else if (uri.Scheme != "https")
            {
                errors.Add("Endpoint URL must use HTTPS");
            }
        }

        // Validate JSON fields
        ValidateJsonField(integration.ConnectedCalendars, "connected calendars", errors);
        ValidateJsonField(integration.SupportedFeatures, "supported features", errors);
        ValidateJsonField(integration.ProviderConfiguration, "provider configuration", errors);
    }

    private static void ValidateProviderRequirements(CalendarIntegration integration, List<string> errors)
    {
        var provider = (CalendarProvider)integration.CalendarProvider;

        switch (provider)
        {
            case CalendarProvider.Google:
                ValidateGoogleRequirements(integration, errors);
                break;
            case CalendarProvider.Outlook:
                ValidateOutlookRequirements(integration, errors);
                break;
            case CalendarProvider.ICloud:
                ValidateICloudRequirements(integration, errors);
                break;
            case CalendarProvider.CalDAV:
                ValidateCalDAVRequirements(integration, errors);
                break;
            case CalendarProvider.Exchange:
                ValidateExchangeRequirements(integration, errors);
                break;
        }
    }

    private static void ValidateGoogleRequirements(CalendarIntegration integration, List<string> errors)
    {
        // Google Calendar specific validations
        if (string.IsNullOrEmpty(integration.TokenScopes) || 
            !integration.TokenScopes.Contains("calendar"))
        {
            errors.Add("Google Calendar integration requires calendar scope");
        }

        // Google typically requires refresh tokens
        if (string.IsNullOrEmpty(integration.RefreshToken))
        {
            errors.Add("Google Calendar integration should have refresh token for long-term access");
        }
    }

    private static void ValidateOutlookRequirements(CalendarIntegration integration, List<string> errors)
    {
        // Outlook/Microsoft Graph specific validations
        if (string.IsNullOrEmpty(integration.TokenScopes) || 
            !integration.TokenScopes.Contains("Calendars"))
        {
            errors.Add("Outlook integration requires Calendars scope");
        }
    }

    private static void ValidateICloudRequirements(CalendarIntegration integration, List<string> errors)
    {
        // iCloud typically uses CalDAV protocol
        if (string.IsNullOrEmpty(integration.EndpointUrl))
        {
            errors.Add("iCloud integration requires endpoint URL");
        }

        // iCloud has limited webhook support
        if (integration.SupportsWebhooks)
        {
            errors.Add("iCloud typically does not support webhooks - verify configuration");
        }
    }

    private static void ValidateCalDAVRequirements(CalendarIntegration integration, List<string> errors)
    {
        // CalDAV specific validations
        if (string.IsNullOrEmpty(integration.EndpointUrl))
        {
            errors.Add("CalDAV integration requires endpoint URL");
        }

        // CalDAV typically doesn't support webhooks
        if (integration.SupportsWebhooks)
        {
            errors.Add("CalDAV typically does not support webhooks");
        }
    }

    private static void ValidateExchangeRequirements(CalendarIntegration integration, List<string> errors)
    {
        // Exchange specific validations
        if (string.IsNullOrEmpty(integration.EndpointUrl))
        {
            errors.Add("Exchange integration typically requires endpoint URL");
        }
    }

    private static void ValidateUpdateConstraints(CalendarIntegration integration, CalendarIntegration existingIntegration, List<string> errors)
    {
        // Validate ID consistency
        if (integration.Id != existingIntegration.Id)
        {
            errors.Add("Cannot change integration ID during update");
        }

        // Validate user ownership
        if (integration.UserId != existingIntegration.UserId)
        {
            errors.Add("Cannot change integration ownership during update");
        }

        // Validate provider consistency
        if (integration.CalendarProvider != existingIntegration.CalendarProvider)
        {
            errors.Add("Cannot change calendar provider during update. Create new integration instead.");
        }

        // Validate that we don't lose sync history inappropriately
        if (integration.TotalEventsSynced < existingIntegration.TotalEventsSynced)
        {
            errors.Add("Cannot reduce total events synced count during update");
        }

        if (integration.TotalConflictsDetected < existingIntegration.TotalConflictsDetected)
        {
            errors.Add("Cannot reduce total conflicts detected count during update");
        }

        // Validate first sync time consistency
        if (existingIntegration.FirstSyncTime.HasValue && 
            integration.FirstSyncTime != existingIntegration.FirstSyncTime)
        {
            errors.Add("Cannot change first sync time during update");
        }
    }

    private static void ValidateJsonField(string? jsonField, string fieldName, List<string> errors)
    {
        if (string.IsNullOrEmpty(jsonField))
            return;

        try
        {
            System.Text.Json.JsonSerializer.Deserialize<object>(jsonField);
        }
        catch
        {
            errors.Add($"Invalid JSON format in {fieldName}");
        }
    }
}