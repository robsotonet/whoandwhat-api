using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Services;

/// <summary>
/// Domain service for calendar synchronization business logic
/// </summary>
public class CalendarSyncDomainService
{
    /// <summary>
    /// Determines if a calendar integration is ready for synchronization
    /// </summary>
    public bool CanSync(CalendarIntegration integration)
    {
        if (integration == null)
            return false;

        // Must be enabled and healthy
        if (!integration.IsEnabled || !integration.IsHealthy)
            return false;

        // Token must not be expired
        if (integration.IsTokenExpired)
            return false;

        // Must not be quarantined
        if (integration.IsQuarantined)
            return false;

        // Must not have too many consecutive failures
        if (integration.ConsecutiveFailures >= 5)
            return false;

        return true;
    }

    /// <summary>
    /// Determines the optimal sync strategy based on integration state
    /// </summary>
    public SyncStrategy DetermineSyncStrategy(CalendarIntegration integration)
    {
        if (integration == null)
            return SyncStrategy.None;

        // First sync - do full sync
        if (!integration.FirstSyncTime.HasValue)
            return SyncStrategy.Full;

        // If no sync token, must do full sync
        if (string.IsNullOrEmpty(integration.LastSyncToken))
            return SyncStrategy.Full;

        // If last sync was too long ago, do full sync
        var timeSinceLastSync = DateTime.UtcNow - integration.LastSyncTime;
        if (timeSinceLastSync > TimeSpan.FromDays(7))
            return SyncStrategy.Full;

        // If there were recent failures, do full sync to recover
        if (integration.ConsecutiveFailures > 2)
            return SyncStrategy.Full;

        // Otherwise, use incremental sync
        return SyncStrategy.Incremental;
    }

    /// <summary>
    /// Calculates the next sync time based on integration configuration and history
    /// </summary>
    public DateTime CalculateNextSyncTime(CalendarIntegration integration, bool wasSuccessful)
    {
        var baseInterval = integration.SyncInterval;

        if (!wasSuccessful)
        {
            // Exponential backoff for failures
            var backoffMultiplier = Math.Min(8, Math.Pow(2, integration.ConsecutiveFailures));
            var backoffInterval = TimeSpan.FromMinutes(baseInterval.TotalMinutes * backoffMultiplier);
            return DateTime.UtcNow.Add(backoffInterval);
        }

        // For successful syncs, use the configured interval
        return DateTime.UtcNow.Add(baseInterval);
    }

    /// <summary>
    /// Determines if an event should be synchronized based on business rules
    /// </summary>
    public bool ShouldSyncEvent(CalendarEvent calendarEvent, CalendarIntegration integration)
    {
        if (calendarEvent == null || integration == null)
            return false;

        // Must belong to the same user
        if (calendarEvent.UserId != integration.UserId)
            return false;

        // Don't sync cancelled events unless they were previously synced
        if (calendarEvent.Status == (int)EventStatus.Cancelled && string.IsNullOrEmpty(calendarEvent.ExternalEventId))
            return false;

        // Check sync direction
        switch ((SyncDirection)integration.SyncDirection)
        {
            case SyncDirection.ToExternal:
                // Only sync internal events to external calendar
                return !calendarEvent.IsExternal;

            case SyncDirection.FromExternal:
                // Only sync external events to internal calendar
                return calendarEvent.IsExternal && 
                       calendarEvent.CalendarProvider == integration.CalendarProvider;

            case SyncDirection.Bidirectional:
                // Sync both directions, but avoid infinite loops
                return !IsSyncLoop(calendarEvent, integration);

            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves sync conflicts based on integration strategy
    /// </summary>
    public ConflictResolutionAction ResolveConflict(CalendarConflict conflict, CalendarIntegration integration)
    {
        if (conflict == null || integration == null)
            return ConflictResolutionAction.UserDecision;

        var strategy = (ConflictResolutionStrategy)integration.ConflictResolutionStrategy;

        return strategy switch
        {
            ConflictResolutionStrategy.LastModifiedWins => ResolveByLastModified(conflict),
            ConflictResolutionStrategy.ExternalWins => ConflictResolutionAction.KeepExternal,
            ConflictResolutionStrategy.InternalWins => ConflictResolutionAction.KeepInternal,
            ConflictResolutionStrategy.CreateDuplicates => ConflictResolutionAction.CreateBoth,
            ConflictResolutionStrategy.SmartMerge => AttemptSmartMerge(conflict),
            ConflictResolutionStrategy.UserResolves => ConflictResolutionAction.UserDecision,
            _ => ConflictResolutionAction.UserDecision
        };
    }

    /// <summary>
    /// Validates sync operation preconditions
    /// </summary>
    public SyncValidationResult ValidateSyncOperation(CalendarIntegration integration, SyncOperation operation)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (integration == null)
        {
            errors.Add("Integration is required");
            return new SyncValidationResult(false, errors, warnings);
        }

        // Check if integration can sync
        if (!CanSync(integration))
        {
            errors.Add("Integration is not ready for synchronization");

            if (!integration.IsEnabled)
                errors.Add("Integration is disabled");
            
            if (integration.IsTokenExpired)
                errors.Add("Access token has expired");
            
            if (integration.IsQuarantined)
                errors.Add("Integration is quarantined due to errors");
        }

        // Check operation-specific requirements
        switch (operation)
        {
            case SyncOperation.Create:
                if (integration.SyncDirection == (int)SyncDirection.FromExternal)
                    warnings.Add("Creating events in external calendar while sync direction is FromExternal");
                break;

            case SyncOperation.Update:
                if (!integration.SupportsWebhooks && integration.SyncInterval > TimeSpan.FromHours(1))
                    warnings.Add("Updates may not be detected quickly without webhook support and with long sync intervals");
                break;

            case SyncOperation.Delete:
                if (integration.ConflictResolutionStrategy == (int)ConflictResolutionStrategy.CreateDuplicates)
                    warnings.Add("Delete operations with CreateDuplicates strategy may cause unexpected behavior");
                break;
        }

        // Check rate limits and health
        if (integration.ConsecutiveFailures > 2)
            warnings.Add($"Integration has {integration.ConsecutiveFailures} consecutive failures");

        if (integration.LastSyncTime.HasValue && 
            DateTime.UtcNow - integration.LastSyncTime.Value < TimeSpan.FromMinutes(1))
            warnings.Add("Very frequent sync operations may hit rate limits");

        return new SyncValidationResult(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Calculates sync priority based on various factors
    /// </summary>
    public SyncPriority CalculateSyncPriority(CalendarEvent calendarEvent, CalendarIntegration integration)
    {
        if (calendarEvent == null || integration == null)
            return SyncPriority.Low;

        var priorityScore = 0;

        // Event priority affects sync priority
        priorityScore += calendarEvent.Priority switch
        {
            3 => 40, // Priority.Urgent
            2 => 30, // Priority.High
            1 => 20, // Priority.Medium
            0 => 10, // Priority.Low
            _ => 10
        };

        // Upcoming events have higher priority
        if (calendarEvent.IsUpcoming)
        {
            var timeUntilEvent = calendarEvent.StartTime - DateTime.UtcNow;
            if (timeUntilEvent <= TimeSpan.FromHours(1))
                priorityScore += 30;
            else if (timeUntilEvent <= TimeSpan.FromHours(24))
                priorityScore += 20;
            else if (timeUntilEvent <= TimeSpan.FromDays(7))
                priorityScore += 10;
        }

        // Recent changes have higher priority
        if (calendarEvent.UpdatedAt > DateTime.UtcNow.AddMinutes(-30))
            priorityScore += 20;

        // Events with attendees have higher priority
        if (calendarEvent.HasAttendees)
            priorityScore += 15;

        // Events with conflicts need immediate attention
        if (calendarEvent.HasConflicts)
            priorityScore += 25;

        return priorityScore switch
        {
            >= 80 => SyncPriority.Critical,
            >= 60 => SyncPriority.High,
            >= 40 => SyncPriority.Medium,
            >= 20 => SyncPriority.Low,
            _ => SyncPriority.Low
        };
    }

    /// <summary>
    /// Determines if sync would create an infinite loop
    /// </summary>
    public bool WouldCreateSyncLoop(CalendarEvent sourceEvent, CalendarIntegration integration)
    {
        // Check if event was recently synced from this integration
        if (sourceEvent.LastSyncTime.HasValue && 
            DateTime.UtcNow - sourceEvent.LastSyncTime.Value < TimeSpan.FromMinutes(5))
        {
            return sourceEvent.CalendarProvider == integration.CalendarProvider;
        }

        return false;
    }

    /// <summary>
    /// Gets recommended sync batch size based on integration health and history
    /// </summary>
    public int GetRecommendedBatchSize(CalendarIntegration integration)
    {
        if (integration == null)
            return 1;

        // Start with base batch size
        var batchSize = 10;

        // Adjust based on success rate
        var successRate = integration.ConflictResolutionRate;
        if (successRate >= 0.95)
            batchSize = 50;
        else if (successRate >= 0.90)
            batchSize = 25;
        else if (successRate >= 0.80)
            batchSize = 15;
        else if (successRate < 0.60)
            batchSize = 5;

        // Adjust based on consecutive failures
        if (integration.ConsecutiveFailures > 0)
            batchSize = Math.Max(1, batchSize / (integration.ConsecutiveFailures + 1));

        // Cap based on provider capabilities
        var maxBatchSize = integration.CalendarProvider switch
        {
            (int)CalendarProvider.Google => 100,
            (int)CalendarProvider.Outlook => 50,
            (int)CalendarProvider.ICloud => 25,
            _ => 25
        };

        return Math.Min(batchSize, maxBatchSize);
    }

    /// <summary>
    /// Determines if a full resync is needed
    /// </summary>
    public bool NeedsFullResync(CalendarIntegration integration)
    {
        if (integration == null)
            return false;

        // Never synced before
        if (!integration.FirstSyncTime.HasValue)
            return true;

        // No sync token available
        if (string.IsNullOrEmpty(integration.LastSyncToken))
            return true;

        // Too many consecutive failures
        if (integration.ConsecutiveFailures >= 5)
            return true;

        // Haven't synced in a very long time
        if (integration.LastSyncTime.HasValue && 
            DateTime.UtcNow - integration.LastSyncTime.Value > TimeSpan.FromDays(30))
            return true;

        // Integration was recently reset or reconfigured
        if (integration.UpdatedAt > integration.LastSyncTime)
            return true;

        return false;
    }

    /// <summary>
    /// Creates sync metadata for tracking and debugging
    /// </summary>
    public Dictionary<string, object> CreateSyncMetadata(CalendarIntegration integration, SyncOperation operation)
    {
        return new Dictionary<string, object>
        {
            ["integrationId"] = integration.Id,
            ["provider"] = integration.CalendarProvider,
            ["operation"] = operation.ToString(),
            ["syncStrategy"] = DetermineSyncStrategy(integration).ToString(),
            ["batchSize"] = GetRecommendedBatchSize(integration),
            ["syncDirection"] = integration.SyncDirection,
            ["timestamp"] = DateTime.UtcNow,
            ["consecutiveFailures"] = integration.ConsecutiveFailures,
            ["healthStatus"] = integration.HealthStatus
        };
    }

    private bool IsSyncLoop(CalendarEvent calendarEvent, CalendarIntegration integration)
    {
        // Simple loop detection - check if event was recently modified by sync
        return calendarEvent.LastSyncTime.HasValue &&
               DateTime.UtcNow - calendarEvent.LastSyncTime.Value < TimeSpan.FromMinutes(2) &&
               calendarEvent.CalendarProvider == integration.CalendarProvider;
    }

    private ConflictResolutionAction ResolveByLastModified(CalendarConflict conflict)
    {
        if (conflict.InternalEvent?.UpdatedAt > conflict.ConflictDetectedAt.AddMinutes(-5))
            return ConflictResolutionAction.KeepInternal;
        
        return ConflictResolutionAction.KeepExternal;
    }

    private ConflictResolutionAction AttemptSmartMerge(CalendarConflict conflict)
    {
        // Simple smart merge logic - for now, defer to user for complex conflicts
        if (conflict.ConflictingFields?.Contains("StartTime") == true ||
            conflict.ConflictingFields?.Contains("EndTime") == true)
        {
            return ConflictResolutionAction.UserDecision;
        }

        // For simple field conflicts, attempt merge
        return ConflictResolutionAction.Merge;
    }
}

/// <summary>
/// Result of sync validation
/// </summary>
public sealed record SyncValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings)
{
    /// <summary>
    /// Gets whether there are any warnings
    /// </summary>
    public bool HasWarnings => Warnings.Any();

    /// <summary>
    /// Gets whether there are any errors
    /// </summary>
    public bool HasErrors => Errors.Any();

    /// <summary>
    /// Gets combined error and warning messages
    /// </summary>
    public List<string> AllMessages => Errors.Concat(Warnings).ToList();
}

/// <summary>
/// Synchronization strategy enumeration
/// </summary>
public enum SyncStrategy
{
    None = 0,        // No synchronization
    Full = 1,        // Full synchronization of all events
    Incremental = 2  // Incremental sync using sync tokens
}

/// <summary>
/// Synchronization operation types
/// </summary>
public enum SyncOperation
{
    Create = 0,
    Update = 1, 
    Delete = 2,
    Batch = 3
}

/// <summary>
/// Synchronization priority levels
/// </summary>
public enum SyncPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}