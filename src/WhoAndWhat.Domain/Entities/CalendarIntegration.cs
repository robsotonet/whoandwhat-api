using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Represents a user's integration with an external calendar provider
/// </summary>
public class CalendarIntegration : BaseEntity
{
    /// <summary>
    /// Maximum allowed provider name length
    /// </summary>
    public const int MaxProviderNameLength = 100;

    /// <summary>
    /// Maximum allowed calendar name length
    /// </summary>
    public const int MaxCalendarNameLength = 200;

    // User and Provider Information
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int CalendarProvider { get; set; } // Maps to CalendarProvider enum
    public string ProviderName { get; set; } = null!; // Display name for the provider
    public string? ProviderAccountId { get; set; } // External account identifier

    // Authentication and Access
    public string? AccessToken { get; set; } // Encrypted access token
    public string? RefreshToken { get; set; } // Encrypted refresh token
    public DateTime? TokenExpiresAt { get; set; }
    public string? TokenScopes { get; set; } // Comma-separated list of granted scopes

    // Calendar Configuration
    public string? ConnectedCalendars { get; set; } // JSON storage for connected calendar list
    public string? PrimaryCalendarId { get; set; } // Primary calendar for creating events
    public string? DefaultTimeZone { get; set; }

    // Synchronization Settings
    public bool IsEnabled { get; set; } = true;
    public bool AutoSyncEnabled { get; set; } = true;
    public int SyncDirection { get; set; } = 2; // SyncDirection.Bidirectional
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(15);
    public DateTime? LastSyncTime { get; set; }
    public string? LastSyncToken { get; set; } // Provider-specific sync token
    public DateTime? NextScheduledSync { get; set; }

    // Conflict Resolution
    public int ConflictResolutionStrategy { get; set; } = 3; // ConflictResolutionStrategy.UserResolves
    public bool AutoResolveSimpleConflicts { get; set; } = false;
    public int ConflictToleranceMinutes { get; set; } = 5; // Buffer time for conflict detection

    // Health and Status Monitoring
    public int HealthStatus { get; set; } = (int)IntegrationHealthStatus.Unknown; // Maps to IntegrationHealthStatus enum
    public string? LastError { get; set; }
    public DateTime? LastErrorTime { get; set; }
    public int ConsecutiveFailures { get; set; } = 0;
    public DateTime? LastHealthCheck { get; set; }
    public bool IsQuarantined { get; set; } = false; // Temporarily disabled due to errors

    // Feature Support
    public string? SupportedFeatures { get; set; } // JSON storage for supported features
    public bool SupportsWebhooks { get; set; } = false;
    public string? WebhookId { get; set; }
    public DateTime? WebhookExpiresAt { get; set; }

    // Statistics and Analytics
    public int TotalEventsSynced { get; set; } = 0;
    public int TotalConflictsDetected { get; set; } = 0;
    public int TotalConflictsResolved { get; set; } = 0;
    public DateTime? FirstSyncTime { get; set; }

    // Provider-Specific Configuration
    public string? ProviderConfiguration { get; set; } // JSON storage for provider-specific settings
    public string? ApiVersion { get; set; }
    public string? EndpointUrl { get; set; }

    // Calculated Properties

    /// <summary>
    /// Gets whether the integration is currently healthy
    /// </summary>
    public bool IsHealthy => HealthStatus == (int)IntegrationHealthStatus.Healthy && IsEnabled && !IsQuarantined;

    /// <summary>
    /// Gets whether the access token is expired or near expiration
    /// </summary>
    public bool IsTokenExpired => TokenExpiresAt.HasValue && TokenExpiresAt.Value <= DateTime.UtcNow;

    /// <summary>
    /// Gets whether the access token needs refresh (within 5 minutes of expiry)
    /// </summary>
    public bool NeedsTokenRefresh => TokenExpiresAt.HasValue && TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(5);

    /// <summary>
    /// Gets whether synchronization is due
    /// </summary>
    public bool IsSyncDue => IsEnabled && AutoSyncEnabled && 
        (!LastSyncTime.HasValue || DateTime.UtcNow - LastSyncTime.Value >= SyncInterval);

    /// <summary>
    /// Gets whether the integration has experienced recent failures
    /// </summary>
    public bool HasRecentFailures => ConsecutiveFailures > 0 && 
        LastErrorTime.HasValue && DateTime.UtcNow - LastErrorTime.Value < TimeSpan.FromHours(1);

    /// <summary>
    /// Gets the success rate for conflict resolution
    /// </summary>
    public double ConflictResolutionRate => TotalConflictsDetected > 0 
        ? (double)TotalConflictsResolved / TotalConflictsDetected 
        : 0.0;

    /// <summary>
    /// Gets whether the integration requires user attention
    /// </summary>
    public bool RequiresAttention => IsTokenExpired || IsQuarantined || 
        ConsecutiveFailures > 3 || !IsHealthy;

    // Domain Methods

    /// <summary>
    /// Updates the access token and refresh token
    /// </summary>
    public void UpdateTokens(string accessToken, string? refreshToken, DateTime expiresAt, string? scopes = null)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        TokenExpiresAt = expiresAt;
        
        if (!string.IsNullOrEmpty(scopes))
        {
            TokenScopes = scopes;
        }

        // Clear quarantine if token is successfully updated
        if (IsQuarantined && ConsecutiveFailures < 3)
        {
            IsQuarantined = false;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Records a successful synchronization
    /// </summary>
    public void RecordSuccessfulSync(string? syncToken = null, int eventsSynced = 0)
    {
        LastSyncTime = DateTime.UtcNow;
        LastSyncToken = syncToken;
        NextScheduledSync = DateTime.UtcNow.Add(SyncInterval);
        
        TotalEventsSynced += eventsSynced;
        
        // Reset failure count on successful sync
        if (ConsecutiveFailures > 0)
        {
            ConsecutiveFailures = 0;
            LastError = null;
            LastErrorTime = null;
        }

        // Update health status
        HealthStatus = (int)IntegrationHealthStatus.Healthy;
        LastHealthCheck = DateTime.UtcNow;

        // Remove quarantine after successful sync
        if (IsQuarantined)
        {
            IsQuarantined = false;
        }

        if (!FirstSyncTime.HasValue)
        {
            FirstSyncTime = DateTime.UtcNow;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Records a synchronization failure
    /// </summary>
    public void RecordSyncFailure(string errorMessage)
    {
        ConsecutiveFailures++;
        LastError = errorMessage;
        LastErrorTime = DateTime.UtcNow;
        LastHealthCheck = DateTime.UtcNow;

        // Update health status based on failure severity
        if (ConsecutiveFailures >= 5)
        {
            HealthStatus = (int)IntegrationHealthStatus.Critical;
            IsQuarantined = true; // Quarantine to prevent further failures
        }
        else if (ConsecutiveFailures >= 3)
        {
            HealthStatus = (int)IntegrationHealthStatus.Warning;
        }
        else
        {
            HealthStatus = (int)IntegrationHealthStatus.Error;
        }

        // Schedule next sync with exponential backoff
        var backoffMinutes = Math.Min(60, Math.Pow(2, ConsecutiveFailures - 1) * 5);
        NextScheduledSync = DateTime.UtcNow.AddMinutes(backoffMinutes);

        MarkAsModified();
    }

    /// <summary>
    /// Records conflict detection and resolution statistics
    /// </summary>
    public void RecordConflictResolution(int conflictsDetected, int conflictsResolved)
    {
        TotalConflictsDetected += conflictsDetected;
        TotalConflictsResolved += conflictsResolved;
        MarkAsModified();
    }

    /// <summary>
    /// Enables the integration
    /// </summary>
    public void Enable()
    {
        if (IsEnabled)
        {
            return;
        }

        IsEnabled = true;
        IsQuarantined = false;
        NextScheduledSync = DateTime.UtcNow.Add(SyncInterval);
        MarkAsModified();
    }

    /// <summary>
    /// Disables the integration
    /// </summary>
    public void Disable(string? reason = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        IsEnabled = false;
        AutoSyncEnabled = false;
        NextScheduledSync = null;
        
        if (!string.IsNullOrEmpty(reason))
        {
            LastError = reason;
            LastErrorTime = DateTime.UtcNow;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Sets up webhook integration
    /// </summary>
    public void SetupWebhook(string webhookId, DateTime expiresAt)
    {
        WebhookId = webhookId;
        WebhookExpiresAt = expiresAt;
        SupportsWebhooks = true;
        MarkAsModified();
    }

    /// <summary>
    /// Removes webhook integration
    /// </summary>
    public void RemoveWebhook()
    {
        WebhookId = null;
        WebhookExpiresAt = null;
        SupportsWebhooks = false;
        MarkAsModified();
    }

    /// <summary>
    /// Updates the conflict resolution strategy
    /// </summary>
    public void UpdateConflictStrategy(ConflictResolutionStrategy strategy, bool autoResolveSimple = false, int toleranceMinutes = 5)
    {
        ConflictResolutionStrategy = (int)strategy;
        AutoResolveSimpleConflicts = autoResolveSimple;
        ConflictToleranceMinutes = toleranceMinutes;
        MarkAsModified();
    }

    /// <summary>
    /// Updates synchronization settings
    /// </summary>
    public void UpdateSyncSettings(SyncDirection direction, TimeSpan interval, bool autoSync = true)
    {
        SyncDirection = (int)direction;
        SyncInterval = interval;
        AutoSyncEnabled = autoSync;

        if (autoSync && IsEnabled)
        {
            NextScheduledSync = DateTime.UtcNow.Add(interval);
        }

        MarkAsModified();
    }

    /// <summary>
    /// Performs a health check and updates status
    /// </summary>
    public void PerformHealthCheck(bool isHealthy, string? errorMessage = null)
    {
        LastHealthCheck = DateTime.UtcNow;

        if (isHealthy)
        {
            HealthStatus = (int)IntegrationHealthStatus.Healthy;
            if (ConsecutiveFailures == 0)
            {
                LastError = null;
                LastErrorTime = null;
            }
        }
        else
        {
            HealthStatus = (int)IntegrationHealthStatus.Error;
            if (!string.IsNullOrEmpty(errorMessage))
            {
                LastError = errorMessage;
                LastErrorTime = DateTime.UtcNow;
            }
        }

        MarkAsModified();
    }

    /// <summary>
    /// Gets the connected calendars as a list
    /// </summary>
    public List<ConnectedCalendar> GetConnectedCalendars()
    {
        if (string.IsNullOrEmpty(ConnectedCalendars))
        {
            return new List<ConnectedCalendar>();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<ConnectedCalendar>>(ConnectedCalendars) 
                ?? new List<ConnectedCalendar>();
        }
        catch
        {
            return new List<ConnectedCalendar>();
        }
    }

    /// <summary>
    /// Sets the connected calendars
    /// </summary>
    public void SetConnectedCalendars(List<ConnectedCalendar> calendars)
    {
        ConnectedCalendars = System.Text.Json.JsonSerializer.Serialize(calendars);
        MarkAsModified();
    }

    /// <summary>
    /// Creates a new calendar integration
    /// </summary>
    public static CalendarIntegration CreateIntegration(Guid userId, CalendarProvider provider, 
        string accessToken, string? refreshToken, DateTime tokenExpires, string providerName)
    {
        return new CalendarIntegration
        {
            UserId = userId,
            CalendarProvider = (int)provider,
            ProviderName = providerName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenExpiresAt = tokenExpires,
            IsEnabled = true,
            AutoSyncEnabled = true,
            SyncDirection = 2, // SyncDirection.Bidirectional
            SyncInterval = TimeSpan.FromMinutes(15),
            ConflictResolutionStrategy = 3, // ConflictResolutionStrategy.UserResolves
            HealthStatus = (int)IntegrationHealthStatus.Unknown,
            ConflictToleranceMinutes = 5
        };
    }
}

/// <summary>
/// Represents a connected calendar within a provider
/// </summary>
public record ConnectedCalendar(
    string Id,
    string Name,
    string? Description,
    bool IsPrimary,
    bool IsEnabled,
    string? TimeZone,
    string? Color
);

/// <summary>
/// Synchronization direction enumeration
/// </summary>
public enum SyncDirection
{
    ToExternal = 0,      // Only sync from WhoAndWhat to external calendar
    FromExternal = 1,    // Only sync from external calendar to WhoAndWhat
    Bidirectional = 2    // Sync in both directions
}

/// <summary>
/// Conflict resolution strategy enumeration
/// </summary>
public enum ConflictResolutionStrategy
{
    LastModifiedWins = 0,  // Most recently modified event wins
    ExternalWins = 1,      // External calendar event wins
    InternalWins = 2,      // WhoAndWhat event wins
    UserResolves = 3,      // Present conflict to user for resolution
    CreateDuplicates = 4,  // Create separate events for both
    SmartMerge = 5         // Attempt intelligent merge of event data
}

/// <summary>
/// Integration health status enumeration
/// </summary>
public enum IntegrationHealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
    Disconnected = 5
}