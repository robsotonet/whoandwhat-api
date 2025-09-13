namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Value object representing the synchronization status of a calendar integration
/// </summary>
public sealed record SyncStatus
{
    private SyncStatus(
        SyncState state,
        DateTime? lastSyncTime,
        DateTime? nextSyncTime,
        string? lastSyncToken,
        int successfulSyncs,
        int failedSyncs,
        TimeSpan? averageSyncDuration,
        string? lastError,
        DateTime? lastErrorTime,
        SyncHealthLevel healthLevel,
        Dictionary<string, object> metadata)
    {
        State = state;
        LastSyncTime = lastSyncTime;
        NextSyncTime = nextSyncTime;
        LastSyncToken = lastSyncToken;
        SuccessfulSyncs = successfulSyncs;
        FailedSyncs = failedSyncs;
        AverageSyncDuration = averageSyncDuration;
        LastError = lastError;
        LastErrorTime = lastErrorTime;
        HealthLevel = healthLevel;
        Metadata = metadata;
    }

    /// <summary>
    /// Current synchronization state
    /// </summary>
    public SyncState State { get; }

    /// <summary>
    /// When the last synchronization occurred
    /// </summary>
    public DateTime? LastSyncTime { get; }

    /// <summary>
    /// When the next synchronization is scheduled
    /// </summary>
    public DateTime? NextSyncTime { get; }

    /// <summary>
    /// Token from the last successful sync (for incremental sync)
    /// </summary>
    public string? LastSyncToken { get; }

    /// <summary>
    /// Number of successful synchronizations
    /// </summary>
    public int SuccessfulSyncs { get; }

    /// <summary>
    /// Number of failed synchronizations
    /// </summary>
    public int FailedSyncs { get; }

    /// <summary>
    /// Average duration of synchronization operations
    /// </summary>
    public TimeSpan? AverageSyncDuration { get; }

    /// <summary>
    /// Last error message (if any)
    /// </summary>
    public string? LastError { get; }

    /// <summary>
    /// When the last error occurred
    /// </summary>
    public DateTime? LastErrorTime { get; }

    /// <summary>
    /// Overall health level of the sync integration
    /// </summary>
    public SyncHealthLevel HealthLevel { get; }

    /// <summary>
    /// Additional metadata about sync status
    /// </summary>
    public Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Gets the total number of sync attempts
    /// </summary>
    public int TotalSyncs => SuccessfulSyncs + FailedSyncs;

    /// <summary>
    /// Gets the success rate as a percentage
    /// </summary>
    public double SuccessRate => TotalSyncs > 0 ? (double)SuccessfulSyncs / TotalSyncs * 100 : 0;

    /// <summary>
    /// Gets whether the sync is currently healthy
    /// </summary>
    public bool IsHealthy => HealthLevel == SyncHealthLevel.Healthy;

    /// <summary>
    /// Gets whether the sync is currently active/in progress
    /// </summary>
    public bool IsActive => State == SyncState.InProgress;

    /// <summary>
    /// Gets whether the sync is idle (not running)
    /// </summary>
    public bool IsIdle => State == SyncState.Idle;

    /// <summary>
    /// Gets whether the sync has recent errors
    /// </summary>
    public bool HasRecentErrors => LastErrorTime.HasValue && 
        DateTime.UtcNow - LastErrorTime.Value < TimeSpan.FromHours(1);

    /// <summary>
    /// Gets whether the sync is overdue
    /// </summary>
    public bool IsOverdue => NextSyncTime.HasValue && DateTime.UtcNow > NextSyncTime.Value && State != SyncState.InProgress;

    /// <summary>
    /// Gets the time since last sync
    /// </summary>
    public TimeSpan? TimeSinceLastSync => LastSyncTime.HasValue ? DateTime.UtcNow - LastSyncTime.Value : null;

    /// <summary>
    /// Gets the time until next sync
    /// </summary>
    public TimeSpan? TimeUntilNextSync => NextSyncTime.HasValue ? NextSyncTime.Value - DateTime.UtcNow : null;

    /// <summary>
    /// Gets whether sync has never run
    /// </summary>
    public bool HasNeverSynced => !LastSyncTime.HasValue && TotalSyncs == 0;

    /// <summary>
    /// Gets a human-readable status description
    /// </summary>
    public string StatusDescription => GenerateStatusDescription();

    /// <summary>
    /// Creates a new sync status for a never-synced integration
    /// </summary>
    public static SyncStatus CreateNew(DateTime? nextSyncTime = null)
    {
        return new SyncStatus(
            SyncState.Idle,
            null,
            nextSyncTime,
            null,
            0,
            0,
            null,
            null,
            null,
            SyncHealthLevel.Unknown,
            new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Creates a sync status indicating sync is in progress
    /// </summary>
    public static SyncStatus CreateInProgress(DateTime startTime, string? previousToken = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["syncStartTime"] = startTime
        };

        return new SyncStatus(
            SyncState.InProgress,
            null,
            null,
            previousToken,
            0,
            0,
            null,
            null,
            null,
            SyncHealthLevel.Unknown,
            metadata
        );
    }

    /// <summary>
    /// Updates the status after a successful sync
    /// </summary>
    public SyncStatus WithSuccessfulSync(string? syncToken = null, TimeSpan? duration = null, DateTime? nextSync = null)
    {
        var newAverageDuration = CalculateNewAverageDuration(duration);
        var newHealthLevel = CalculateHealthLevel(SuccessfulSyncs + 1, FailedSyncs);

        return new SyncStatus(
            SyncState.Idle,
            DateTime.UtcNow,
            nextSync,
            syncToken ?? LastSyncToken,
            SuccessfulSyncs + 1,
            FailedSyncs,
            newAverageDuration,
            null, // Clear error on successful sync
            LastErrorTime,
            newHealthLevel,
            UpdateMetadata("lastSyncResult", "success")
        );
    }

    /// <summary>
    /// Updates the status after a failed sync
    /// </summary>
    public SyncStatus WithFailedSync(string errorMessage, DateTime? nextSync = null, TimeSpan? duration = null)
    {
        var newAverageDuration = CalculateNewAverageDuration(duration);
        var newHealthLevel = CalculateHealthLevel(SuccessfulSyncs, FailedSyncs + 1);

        return new SyncStatus(
            SyncState.Error,
            LastSyncTime, // Don't update last sync time on failure
            nextSync,
            LastSyncToken,
            SuccessfulSyncs,
            FailedSyncs + 1,
            newAverageDuration,
            errorMessage,
            DateTime.UtcNow,
            newHealthLevel,
            UpdateMetadata("lastSyncResult", "failed")
        );
    }

    /// <summary>
    /// Updates the sync state
    /// </summary>
    public SyncStatus WithState(SyncState newState)
    {
        if (State == newState) return this;
        
        return this with { State = newState };
    }

    /// <summary>
    /// Schedules the next sync
    /// </summary>
    public SyncStatus WithNextSync(DateTime nextSyncTime)
    {
        return this with { NextSyncTime = nextSyncTime };
    }

    /// <summary>
    /// Updates the health level
    /// </summary>
    public SyncStatus WithHealthLevel(SyncHealthLevel healthLevel)
    {
        return this with { HealthLevel = healthLevel };
    }

    /// <summary>
    /// Adds metadata to the sync status
    /// </summary>
    public SyncStatus WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMetadata };
    }

    /// <summary>
    /// Gets metadata value by key
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }

    /// <summary>
    /// Clears any error state
    /// </summary>
    public SyncStatus ClearError()
    {
        if (string.IsNullOrEmpty(LastError)) return this;
        
        return this with 
        { 
            LastError = null, 
            State = State == SyncState.Error ? SyncState.Idle : State 
        };
    }

    /// <summary>
    /// Resets sync statistics
    /// </summary>
    public SyncStatus ResetStatistics()
    {
        return this with 
        { 
            SuccessfulSyncs = 0, 
            FailedSyncs = 0, 
            AverageSyncDuration = null,
            HealthLevel = SyncHealthLevel.Unknown
        };
    }

    /// <summary>
    /// Checks if sync is due based on interval
    /// </summary>
    public bool IsSyncDue(TimeSpan syncInterval)
    {
        if (!LastSyncTime.HasValue) return true; // Never synced
        if (IsActive) return false; // Already syncing
        
        return DateTime.UtcNow - LastSyncTime.Value >= syncInterval;
    }

    /// <summary>
    /// Gets sync performance metrics
    /// </summary>
    public SyncPerformanceMetrics GetPerformanceMetrics()
    {
        return new SyncPerformanceMetrics(
            SuccessRate,
            TotalSyncs,
            AverageSyncDuration,
            TimeSinceLastSync,
            HasRecentErrors,
            HealthLevel
        );
    }

    private TimeSpan? CalculateNewAverageDuration(TimeSpan? newDuration)
    {
        if (!newDuration.HasValue) return AverageSyncDuration;
        
        if (!AverageSyncDuration.HasValue) return newDuration;
        
        // Simple moving average with the last duration
        var totalSyncsWithDuration = Math.Max(1, TotalSyncs);
        var totalMilliseconds = (AverageSyncDuration.Value.TotalMilliseconds * (totalSyncsWithDuration - 1) + 
                               newDuration.Value.TotalMilliseconds) / totalSyncsWithDuration;
        
        return TimeSpan.FromMilliseconds(totalMilliseconds);
    }

    private SyncHealthLevel CalculateHealthLevel(int successfulCount, int failedCount)
    {
        var total = successfulCount + failedCount;
        if (total == 0) return SyncHealthLevel.Unknown;
        
        var successRate = (double)successfulCount / total;
        
        return successRate switch
        {
            >= 0.95 => SyncHealthLevel.Healthy,
            >= 0.80 => SyncHealthLevel.Warning,
            >= 0.50 => SyncHealthLevel.Degraded,
            _ => SyncHealthLevel.Critical
        };
    }

    private Dictionary<string, object> UpdateMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return newMetadata;
    }

    private string GenerateStatusDescription()
    {
        return State switch
        {
            SyncState.Idle when HasNeverSynced => "Never synchronized",
            SyncState.Idle when LastSyncTime.HasValue => $"Last synced {GetTimeAgoText(LastSyncTime.Value)}",
            SyncState.InProgress => "Synchronizing...",
            SyncState.Error => $"Error: {LastError}",
            SyncState.Disabled => "Synchronization disabled",
            SyncState.Paused => "Synchronization paused",
            _ => "Unknown status"
        };
    }

    private string GetTimeAgoText(DateTime time)
    {
        var timeSpan = DateTime.UtcNow - time;
        
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays != 1 ? "s" : "")} ago";
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours != 1 ? "s" : "")} ago";
        if (timeSpan.TotalMinutes >= 1)
            return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes != 1 ? "s" : "")} ago";
        
        return "just now";
    }

    /// <summary>
    /// Validates the sync status
    /// </summary>
    public bool IsValid(out string? validationError)
    {
        validationError = null;

        if (SuccessfulSyncs < 0)
        {
            validationError = "Successful syncs cannot be negative";
            return false;
        }

        if (FailedSyncs < 0)
        {
            validationError = "Failed syncs cannot be negative";
            return false;
        }

        if (LastSyncTime.HasValue && LastSyncTime.Value > DateTime.UtcNow)
        {
            validationError = "Last sync time cannot be in the future";
            return false;
        }

        if (LastErrorTime.HasValue && LastErrorTime.Value > DateTime.UtcNow)
        {
            validationError = "Last error time cannot be in the future";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts to JSON for storage
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }

    /// <summary>
    /// Creates from JSON
    /// </summary>
    public static SyncStatus FromJson(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<SyncStatus>(json) ?? CreateNew();
        }
        catch
        {
            return CreateNew();
        }
    }
}

/// <summary>
/// Performance metrics for sync operations
/// </summary>
public sealed record SyncPerformanceMetrics(
    double SuccessRate,
    int TotalSyncs,
    TimeSpan? AverageDuration,
    TimeSpan? TimeSinceLastSync,
    bool HasRecentErrors,
    SyncHealthLevel HealthLevel)
{
    /// <summary>
    /// Gets a performance score from 0-100
    /// </summary>
    public double PerformanceScore
    {
        get
        {
            var score = SuccessRate; // Base score from success rate
            
            // Deduct points for recent errors
            if (HasRecentErrors) score -= 20;
            
            // Deduct points based on health level
            score -= HealthLevel switch
            {
                SyncHealthLevel.Warning => 10,
                SyncHealthLevel.Degraded => 25,
                SyncHealthLevel.Critical => 50,
                _ => 0
            };
            
            return Math.Max(0, Math.Min(100, score));
        }
    }

    /// <summary>
    /// Gets a letter grade for performance
    /// </summary>
    public char PerformanceGrade => PerformanceScore switch
    {
        >= 90 => 'A',
        >= 80 => 'B', 
        >= 70 => 'C',
        >= 60 => 'D',
        _ => 'F'
    };
}

/// <summary>
/// Synchronization state enumeration
/// </summary>
public enum SyncState
{
    Idle = 0,       // Not currently syncing
    InProgress = 1, // Currently synchronizing
    Error = 2,      // Last sync failed
    Disabled = 3,   // Sync is disabled
    Paused = 4      // Sync is temporarily paused
}

/// <summary>
/// Synchronization health level
/// </summary>
public enum SyncHealthLevel
{
    Unknown = 0,    // Health level not determined
    Healthy = 1,    // Sync is working well
    Warning = 2,    // Minor issues, still functional
    Degraded = 3,   // Significant issues, reduced reliability
    Critical = 4    // Major issues, requires attention
}