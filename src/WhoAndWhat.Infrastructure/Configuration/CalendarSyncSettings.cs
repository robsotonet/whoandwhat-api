using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Calendar synchronization service configuration settings
/// </summary>
public class CalendarSyncSettings
{
    public const string SectionName = "CalendarSync";

    /// <summary>
    /// Enable calendar synchronization service (can be disabled for testing/development)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Primary calendar provider for synchronization
    /// </summary>
    public CalendarProvider DefaultProvider { get; set; } = CalendarProvider.Google;

    /// <summary>
    /// Synchronization mode (OneWayToExternal, OneWayFromExternal, BiDirectional)
    /// </summary>
    public SyncMode SyncMode { get; set; } = SyncMode.BiDirectional;

    /// <summary>
    /// Automatic synchronization interval in minutes
    /// </summary>
    public int AutoSyncIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Request timeout in milliseconds for calendar API calls
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Maximum number of retry attempts for failed sync operations
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Maximum number of events to process in a single batch
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Enable incremental synchronization (only sync changes since last sync)
    /// </summary>
    public bool EnableIncrementalSync { get; set; } = true;

    /// <summary>
    /// Conflict resolution strategy
    /// </summary>
    public ConflictResolutionStrategy ConflictResolution { get; set; } = ConflictResolutionStrategy.LastModifiedWins;

    /// <summary>
    /// Rate limiting configuration for calendar providers
    /// </summary>
    public CalendarRateLimitSettings RateLimit { get; set; } = new();

    /// <summary>
    /// Caching configuration for calendar data
    /// </summary>
    public CalendarCacheSettings Cache { get; set; } = new();

    /// <summary>
    /// Backup and recovery settings
    /// </summary>
    public CalendarBackupSettings Backup { get; set; } = new();

    /// <summary>
    /// Google Calendar provider configuration
    /// </summary>
    public GoogleCalendarSettings GoogleCalendar { get; set; } = new();

    /// <summary>
    /// Microsoft Outlook/Office365 provider configuration
    /// </summary>
    public OutlookCalendarSettings OutlookCalendar { get; set; } = new();

    /// <summary>
    /// Apple iCloud Calendar provider configuration
    /// </summary>
    public ICloudCalendarSettings ICloudCalendar { get; set; } = new();

    /// <summary>
    /// Generic CalDAV provider configuration
    /// </summary>
    public CalDAVSettings CalDAV { get; set; } = new();

    /// <summary>
    /// Monitoring and health check settings
    /// </summary>
    public CalendarMonitoringSettings Monitoring { get; set; } = new();

    /// <summary>
    /// Feature flags for calendar synchronization capabilities
    /// </summary>
    public CalendarFeatureFlags Features { get; set; } = new();
}

/// <summary>
/// Rate limiting settings for calendar API requests
/// </summary>
public class CalendarRateLimitSettings
{
    /// <summary>
    /// Maximum API requests per minute per user
    /// </summary>
    public int RequestsPerMinutePerUser { get; set; } = 60;

    /// <summary>
    /// Maximum API requests per hour per user
    /// </summary>
    public int RequestsPerHourPerUser { get; set; } = 1000;

    /// <summary>
    /// Global rate limit for all users (requests per minute)
    /// </summary>
    public int GlobalRequestsPerMinute { get; set; } = 5000;

    /// <summary>
    /// Enable burst allowance for short periods
    /// </summary>
    public bool EnableBurstAllowance { get; set; } = true;

    /// <summary>
    /// Burst allowance multiplier
    /// </summary>
    public double BurstMultiplier { get; set; } = 1.5;
}

/// <summary>
/// Caching settings for calendar synchronization data
/// </summary>
public class CalendarCacheSettings
{
    /// <summary>
    /// Enable caching of calendar data
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default cache expiration in minutes
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Cache expiration for calendar events in minutes
    /// </summary>
    public int EventExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Cache expiration for sync tokens in minutes
    /// </summary>
    public int SyncTokenExpirationMinutes { get; set; } = 1440; // 24 hours

    /// <summary>
    /// Cache expiration for conflict resolutions in minutes
    /// </summary>
    public int ConflictResolutionExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Enable cache warming for frequently accessed data
    /// </summary>
    public bool EnableCacheWarming { get; set; } = true;

    /// <summary>
    /// Maximum cache size per user in MB
    /// </summary>
    public int MaxCacheSizePerUserMB { get; set; } = 5;
}

/// <summary>
/// Backup and recovery settings for calendar data
/// </summary>
public class CalendarBackupSettings
{
    /// <summary>
    /// Enable automatic backup of calendar sync state
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Backup frequency in hours
    /// </summary>
    public int BackupFrequencyHours { get; set; } = 6;

    /// <summary>
    /// Number of backup files to retain
    /// </summary>
    public int RetainBackupCount { get; set; } = 10;

    /// <summary>
    /// Backup storage path (relative to app data directory)
    /// </summary>
    public string BackupPath { get; set; } = "calendar-backups";

    /// <summary>
    /// Enable compression of backup files
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Enable automatic recovery from backup on sync failures
    /// </summary>
    public bool EnableAutoRecovery { get; set; } = false;
}

/// <summary>
/// Google Calendar provider settings
/// </summary>
public class GoogleCalendarSettings
{
    /// <summary>
    /// Google Calendar API enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OAuth client ID for Google Calendar API
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth client secret for Google Calendar API
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Google Calendar API endpoint URL
    /// </summary>
    public string ApiEndpoint { get; set; } = "https://www.googleapis.com/calendar/v3";

    /// <summary>
    /// OAuth scopes for Google Calendar access
    /// </summary>
    public string Scopes { get; set; } = "https://www.googleapis.com/auth/calendar";

    /// <summary>
    /// Maximum events to fetch in single API call
    /// </summary>
    public int MaxResults { get; set; } = 250;

    /// <summary>
    /// Default calendar ID (primary calendar if empty)
    /// </summary>
    public string? DefaultCalendarId { get; set; }

    /// <summary>
    /// Sync all user calendars or just primary
    /// </summary>
    public bool SyncAllCalendars { get; set; } = false;
}

/// <summary>
/// Microsoft Outlook/Office365 calendar provider settings
/// </summary>
public class OutlookCalendarSettings
{
    /// <summary>
    /// Outlook Calendar API enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Application ID for Microsoft Graph API
    /// </summary>
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>
    /// Application secret for Microsoft Graph API
    /// </summary>
    public string ApplicationSecret { get; set; } = string.Empty;

    /// <summary>
    /// Microsoft Graph API endpoint URL
    /// </summary>
    public string ApiEndpoint { get; set; } = "https://graph.microsoft.com/v1.0";

    /// <summary>
    /// OAuth scopes for Outlook Calendar access
    /// </summary>
    public string Scopes { get; set; } = "https://graph.microsoft.com/calendars.readwrite";

    /// <summary>
    /// Tenant ID for Azure AD (optional)
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Maximum events to fetch in single API call
    /// </summary>
    public int MaxResults { get; set; } = 100;

    /// <summary>
    /// Sync all user calendars or just primary
    /// </summary>
    public bool SyncAllCalendars { get; set; } = false;
}

/// <summary>
/// Apple iCloud Calendar provider settings
/// </summary>
public class ICloudCalendarSettings
{
    /// <summary>
    /// iCloud Calendar API enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// iCloud CalDAV server URL
    /// </summary>
    public string ServerUrl { get; set; } = "https://caldav.icloud.com";

    /// <summary>
    /// Default port for CalDAV connections
    /// </summary>
    public int Port { get; set; } = 443;

    /// <summary>
    /// Use SSL/TLS for connections
    /// </summary>
    public bool UseSSL { get; set; } = true;

    /// <summary>
    /// Calendar principal URL pattern
    /// </summary>
    public string PrincipalUrlPattern { get; set; } = "/principal/{username}";

    /// <summary>
    /// Calendar home URL pattern  
    /// </summary>
    public string CalendarHomeUrlPattern { get; set; } = "/calendars/{username}";
}

/// <summary>
/// Generic CalDAV provider settings
/// </summary>
public class CalDAVSettings
{
    /// <summary>
    /// CalDAV provider enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// CalDAV server URL
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// CalDAV server port
    /// </summary>
    public int Port { get; set; } = 443;

    /// <summary>
    /// Use SSL/TLS for connections
    /// </summary>
    public bool UseSSL { get; set; } = true;

    /// <summary>
    /// Authentication method (Basic, Digest, OAuth)
    /// </summary>
    public string AuthMethod { get; set; } = "Basic";

    /// <summary>
    /// Calendar discovery URL pattern
    /// </summary>
    public string CalendarDiscoveryUrlPattern { get; set; } = "/.well-known/caldav";

    /// <summary>
    /// Custom HTTP headers for CalDAV requests
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

/// <summary>
/// Calendar service monitoring settings
/// </summary>
public class CalendarMonitoringSettings
{
    /// <summary>
    /// Enable detailed monitoring and metrics
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval in seconds
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Log all sync operations for debugging
    /// </summary>
    public bool LogSyncOperations { get; set; } = false;

    /// <summary>
    /// Log conflict detection and resolution details
    /// </summary>
    public bool LogConflictResolutions { get; set; } = true;

    /// <summary>
    /// Enable performance metrics tracking
    /// </summary>
    public bool TrackPerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Alert thresholds for calendar sync monitoring
    /// </summary>
    public CalendarAlertThresholds Alerts { get; set; } = new();
}

/// <summary>
/// Alert thresholds for calendar synchronization monitoring
/// </summary>
public class CalendarAlertThresholds
{
    /// <summary>
    /// Sync operation timeout threshold in milliseconds
    /// </summary>
    public int SyncTimeoutThresholdMs { get; set; } = 60000; // 1 minute

    /// <summary>
    /// Sync failure rate threshold percentage for alerts
    /// </summary>
    public double SyncFailureRateThresholdPercent { get; set; } = 10.0;

    /// <summary>
    /// Conflict rate threshold percentage for alerts
    /// </summary>
    public double ConflictRateThresholdPercent { get; set; } = 25.0;

    /// <summary>
    /// Maximum number of consecutive sync failures before alert
    /// </summary>
    public int ConsecutiveFailuresThreshold { get; set; } = 3;

    /// <summary>
    /// Time window for threshold evaluation in minutes
    /// </summary>
    public int ThresholdTimeWindowMinutes { get; set; } = 30;
}

/// <summary>
/// Feature flags for calendar synchronization capabilities
/// </summary>
public class CalendarFeatureFlags
{
    /// <summary>
    /// Enable automatic synchronization
    /// </summary>
    public bool EnableAutoSync { get; set; } = true;

    /// <summary>
    /// Enable bidirectional synchronization
    /// </summary>
    public bool EnableBiDirectionalSync { get; set; } = true;

    /// <summary>
    /// Enable conflict detection and resolution
    /// </summary>
    public bool EnableConflictResolution { get; set; } = true;

    /// <summary>
    /// Enable task-to-event conversion
    /// </summary>
    public bool EnableTaskToEventConversion { get; set; } = true;

    /// <summary>
    /// Enable event-to-task conversion
    /// </summary>
    public bool EnableEventToTaskConversion { get; set; } = true;

    /// <summary>
    /// Enable recurring event synchronization
    /// </summary>
    public bool EnableRecurringEvents { get; set; } = true;

    /// <summary>
    /// Enable calendar sharing and collaboration features
    /// </summary>
    public bool EnableCalendarSharing { get; set; } = false;

    /// <summary>
    /// Enable advanced scheduling optimization
    /// </summary>
    public bool EnableSchedulingOptimization { get; set; } = true;

    /// <summary>
    /// Enable experimental features (beta testing)
    /// </summary>
    public bool EnableExperimentalFeatures { get; set; } = false;
}

