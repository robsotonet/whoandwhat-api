using WhoAndWhat.Application.DTOs.Calendar;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service for calendar synchronization, conflict resolution, and integration management
/// </summary>
public interface ICalendarSyncService
{
    /// <summary>
    /// Perform full calendar synchronization for a user with specified provider
    /// </summary>
    /// <param name="userId">User ID to sync calendar for</param>
    /// <param name="provider">Calendar provider to sync with</param>
    /// <param name="syncMode">Synchronization mode (bidirectional, one-way, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Synchronization result with success status and statistics</returns>
    public Task<CalendarSyncResult> SyncCalendarAsync(Guid userId, CalendarProvider provider, SyncMode syncMode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform incremental calendar synchronization (only sync changes since last sync)
    /// </summary>
    /// <param name="userId">User ID to sync calendar for</param>
    /// <param name="provider">Calendar provider to sync with</param>
    /// <param name="lastSyncToken">Token from previous sync operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Incremental sync result with changes and new sync token</returns>
    public Task<IncrementalSyncResult> SyncCalendarIncrementalAsync(Guid userId, CalendarProvider provider, string lastSyncToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sync specific events between WhoAndWhat and external calendar
    /// </summary>
    /// <param name="userId">User ID who owns the events</param>
    /// <param name="eventIds">List of event IDs to synchronize</param>
    /// <param name="provider">Target calendar provider</param>
    /// <param name="syncDirection">Direction of synchronization</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Event sync results with individual operation status</returns>
    public Task<IEnumerable<EventSyncResult>> SyncEventsAsync(Guid userId, IEnumerable<Guid> eventIds, CalendarProvider provider, SyncDirection syncDirection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert WhoAndWhat tasks to calendar events and sync to external calendar
    /// </summary>
    /// <param name="userId">User ID who owns the tasks</param>
    /// <param name="taskIds">List of task IDs to convert and sync</param>
    /// <param name="provider">Target calendar provider</param>
    /// <param name="conversionOptions">Options for task-to-event conversion</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task conversion and sync results</returns>
    public Task<IEnumerable<TaskToEventSyncResult>> SyncTasksAsEventsAsync(Guid userId, IEnumerable<Guid> taskIds, CalendarProvider provider, TaskToEventConversionOptions conversionOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get synchronization status and statistics for a user's calendar integration
    /// </summary>
    /// <param name="userId">User ID to get sync status for</param>
    /// <param name="provider">Calendar provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed sync status and statistics</returns>
    public Task<CalendarSyncStatus> GetSyncStatusAsync(Guid userId, CalendarProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all calendar providers configured and available for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available calendar providers with configuration status</returns>
    public Task<IEnumerable<AvailableCalendarProvider>> GetAvailableProvidersAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure calendar provider for a user (OAuth setup, credentials, preferences)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="providerConfig">Calendar provider configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration result with success status</returns>
    public Task<CalendarProviderConfigResult> ConfigureProviderAsync(Guid userId, CalendarProviderConfiguration providerConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect and remove calendar provider integration for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Calendar provider to disconnect</param>
    /// <param name="deleteData">Whether to delete synced data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Disconnection result</returns>
    public Task<CalendarDisconnectResult> DisconnectProviderAsync(Guid userId, CalendarProvider provider, bool deleteData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get calendar synchronization conflicts that require user resolution
    /// </summary>
    /// <param name="userId">User ID to get conflicts for</param>
    /// <param name="provider">Optional provider filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of conflicts requiring resolution</returns>
    public Task<IEnumerable<CalendarSyncConflict>> GetPendingConflictsAsync(Guid userId, CalendarProvider? provider = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a specific calendar synchronization conflict
    /// </summary>
    /// <param name="userId">User ID who owns the conflict</param>
    /// <param name="conflictId">Conflict ID to resolve</param>
    /// <param name="resolution">User's conflict resolution choice</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conflict resolution result</returns>
    public Task<ConflictResolutionResult> ResolveConflictAsync(Guid userId, Guid conflictId, ConflictResolution resolution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if calendar sync service is properly configured and available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is ready and responding</returns>
    public Task<bool> IsCalendarSyncAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get calendar sync service health status with detailed diagnostics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed health status including provider availability</returns>
    public Task<CalendarSyncHealthStatus> GetSyncHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Backup calendar synchronization data for a user
    /// </summary>
    /// <param name="userId">User ID to backup data for</param>
    /// <param name="backupOptions">Backup configuration options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup operation result</returns>
    public Task<CalendarBackupResult> BackupCalendarDataAsync(Guid userId, CalendarBackupOptions backupOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restore calendar synchronization data from backup
    /// </summary>
    /// <param name="userId">User ID to restore data for</param>
    /// <param name="backupId">Backup ID to restore from</param>
    /// <param name="restoreOptions">Restore configuration options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Restore operation result</returns>
    public Task<CalendarRestoreResult> RestoreCalendarDataAsync(Guid userId, string backupId, CalendarRestoreOptions restoreOptions, CancellationToken cancellationToken = default);
}
