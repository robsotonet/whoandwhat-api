# Calendar Synchronization Service Integration Guide

This document shows how to integrate the calendar synchronization services into the main application.

## Program.cs Integration

Add the following line to `Program.cs` after the Redis caching configuration (around line 90):

```csharp
// Add calendar synchronization services (add after line 89)
builder.Services.AddCalendarSynchronization(builder.Configuration);
```

## Complete Integration Example

Here's how the services section should look in `Program.cs`:

```csharp
    // Redis Configuration (existing - line 89)
    builder.Services.AddRedisCachingConfiguration(builder.Configuration);
    
    // AI Planning Services (existing)
    builder.Services.AddAIPlanningServices(builder.Configuration);
    
    // Calendar Synchronization Services (NEW)
    builder.Services.AddCalendarSynchronization(builder.Configuration);
    
    // Application Insights (existing)
    builder.Services.AddApplicationInsightsConfiguration(builder.Configuration);
```

## Configuration Requirements

Add the following configuration section to `appsettings.json`:

```json
{
  "CalendarSync": {
    "Enabled": true,
    "DefaultProvider": "Google",
    "SyncMode": "BiDirectional",
    "Providers": {
      "Google": {
        "ClientId": "your-google-client-id",
        "ClientSecret": "your-google-client-secret",
        "Scopes": ["https://www.googleapis.com/auth/calendar"],
        "TimeoutSeconds": 30,
        "RateLimit": {
          "RequestsPerMinute": 100,
          "RequestDelayMs": 600
        },
        "CacheSettings": {
          "DefaultExpirationMinutes": 15,
          "EventCacheExpirationMinutes": 30
        },
        "TokenCacheExpirationMinutes": 60
      },
      "Outlook": {
        "ClientId": "your-outlook-client-id",
        "ClientSecret": "your-outlook-client-secret",
        "TenantId": "common",
        "Scopes": ["https://graph.microsoft.com/Calendars.ReadWrite"],
        "TimeoutSeconds": 30,
        "RateLimit": {
          "RequestsPerMinute": 60,
          "RequestDelayMs": 1000
        },
        "CacheSettings": {
          "DefaultExpirationMinutes": 15,
          "EventCacheExpirationMinutes": 30
        },
        "TokenCacheExpirationMinutes": 60
      },
      "ICloud": {
        "Username": "user@icloud.com",
        "AppSpecificPassword": "your-app-specific-password",
        "TimeoutSeconds": 45,
        "RateLimit": {
          "RequestsPerMinute": 30,
          "RequestDelayMs": 2000
        },
        "CacheSettings": {
          "DefaultExpirationMinutes": 15,
          "EventCacheExpirationMinutes": 30
        },
        "TokenCacheExpirationMinutes": 1440
      }
    },
    "ConflictResolution": {
      "AutoResolveThreshold": "Medium",
      "DefaultResolutionStrategy": "PreferInternal",
      "ResolutionCacheExpirationMinutes": 60
    },
    "Performance": {
      "BatchSizeLimit": 50,
      "MaxConcurrentSyncs": 5,
      "EnablePerformanceOptimization": true
    }
  }
}
```

## Health Check Endpoints

The calendar services automatically register health checks that will be available at:

- `/health` - Overall application health including calendar services
- Individual health check details will include:
  - `calendar_sync` - Core synchronization service health
  - `calendar_cache` - Calendar caching service health  
  - `calendar_providers` - External provider availability

## Service Dependencies

The calendar synchronization services depend on:

1. **Redis Caching** - Must be configured first via `AddRedisCachingConfiguration()`
2. **HTTP Client Factory** - Automatically configured for provider services
3. **Configuration** - Calendar settings must be present in configuration
4. **Logging** - Uses the application's logging configuration

## Optional: Provider-Specific Registration

If you only want to enable specific calendar providers:

```csharp
// Enable only Google and Outlook
builder.Services.AddCalendarSynchronization(builder.Configuration, 
    CalendarProvider.Google, 
    CalendarProvider.Outlook);
```

## Background Services

The following background services are automatically registered:

1. **CalendarCacheWarmupService** - Warms cache on application startup
2. **CalendarSyncMonitoringService** - Monitors sync performance metrics
3. **CalendarConflictResolutionService** - Auto-resolves simple conflicts

## Usage in Controllers

Once registered, calendar services can be injected into controllers:

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarSyncService _calendarSyncService;
    private readonly ICalendarConflictDetector _conflictDetector;

    public CalendarController(
        ICalendarSyncService calendarSyncService,
        ICalendarConflictDetector conflictDetector)
    {
        _calendarSyncService = calendarSyncService;
        _conflictDetector = conflictDetector;
    }

    [HttpPost("sync/{provider}")]
    public async Task<IActionResult> SyncCalendar(CalendarProvider provider)
    {
        var userId = User.GetUserId();
        var result = await _calendarSyncService.SyncCalendarAsync(
            userId, provider, SyncMode.BiDirectional);
        
        return Ok(result);
    }
}
```

## Environment-Specific Configuration

### Development

```json
{
  "CalendarSync": {
    "Enabled": true,
    "Providers": {
      "Google": {
        "ClientId": "dev-google-client-id",
        "ClientSecret": "dev-google-secret"
      }
    }
  }
}
```

### Production

```json
{
  "CalendarSync": {
    "Enabled": true,
    "Providers": {
      "Google": {
        "ClientId": "#{GoogleClientId}#",
        "ClientSecret": "#{GoogleClientSecret}#"
      }
    }
  }
}
```

Note: Production secrets should be stored in Azure Key Vault and referenced via configuration tokens.

## Testing Configuration

For testing environments, calendar synchronization can be disabled:

```json
{
  "CalendarSync": {
    "Enabled": false
  }
}
```

When disabled, all calendar operations will return appropriate responses without making external API calls.