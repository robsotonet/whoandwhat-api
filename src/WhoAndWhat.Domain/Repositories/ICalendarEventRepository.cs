using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Repositories;

/// <summary>
/// Repository interface for calendar events
/// </summary>
public interface ICalendarEventRepository
{
    /// <summary>
    /// Gets a calendar event by ID
    /// </summary>
    Task<CalendarEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a calendar event by external event ID
    /// </summary>
    Task<CalendarEvent?> GetByExternalEventIdAsync(string externalEventId, CalendarProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all calendar events for a user
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets calendar events for a user within a date range
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetByUserIdAndDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets calendar events by provider for a user
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetByUserIdAndProviderAsync(Guid userId, CalendarProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events that need synchronization
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetEventsPendingSyncAsync(Guid userId, CalendarProvider? provider = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overlapping events for conflict detection
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetOverlappingEventsAsync(Guid userId, DateTime startTime, DateTime endTime, Guid? excludeEventId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events by related task
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events by related project
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recurring event instances
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetRecurringInstancesAsync(Guid masterEventId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches calendar events by text
    /// </summary>
    Task<IEnumerable<CalendarEvent>> SearchEventsAsync(Guid userId, string searchText, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new calendar event
    /// </summary>
    Task<CalendarEvent> AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing calendar event
    /// </summary>
    Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a calendar event
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk deletes calendar events
    /// </summary>
    Task DeleteBulkAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets event statistics for a user
    /// </summary>
    Task<CalendarEventStatistics> GetEventStatisticsAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for calendar integrations
/// </summary>
public interface ICalendarIntegrationRepository
{
    /// <summary>
    /// Gets a calendar integration by ID
    /// </summary>
    Task<CalendarIntegration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all calendar integrations for a user
    /// </summary>
    Task<IEnumerable<CalendarIntegration>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a calendar integration by user and provider
    /// </summary>
    Task<CalendarIntegration?> GetByUserIdAndProviderAsync(Guid userId, CalendarProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets integrations that need synchronization
    /// </summary>
    Task<IEnumerable<CalendarIntegration>> GetIntegrationsNeedingSyncAsync(DateTime beforeTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unhealthy integrations
    /// </summary>
    Task<IEnumerable<CalendarIntegration>> GetUnhealthyIntegrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets integrations with expired tokens
    /// </summary>
    Task<IEnumerable<CalendarIntegration>> GetIntegrationsWithExpiredTokensAsync(DateTime beforeTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new calendar integration
    /// </summary>
    Task<CalendarIntegration> AddAsync(CalendarIntegration integration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing calendar integration
    /// </summary>
    Task UpdateAsync(CalendarIntegration integration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a calendar integration
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets integration statistics
    /// </summary>
    Task<CalendarIntegrationStatistics> GetIntegrationStatisticsAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for calendar conflicts
/// </summary>
public interface ICalendarConflictRepository
{
    /// <summary>
    /// Gets a calendar conflict by ID
    /// </summary>
    Task<CalendarConflict?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all conflicts for a user
    /// </summary>
    Task<IEnumerable<CalendarConflict>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active conflicts for a user
    /// </summary>
    Task<IEnumerable<CalendarConflict>> GetActiveConflictsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conflicts by type
    /// </summary>
    Task<IEnumerable<CalendarConflict>> GetByConflictTypeAsync(Guid userId, ConflictType conflictType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conflicts by severity
    /// </summary>
    Task<IEnumerable<CalendarConflict>> GetByConflictSeverityAsync(Guid userId, ConflictSeverity severity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conflicts for a specific event
    /// </summary>
    Task<IEnumerable<CalendarConflict>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conflicts for a calendar integration
    /// </summary>
    Task<IEnumerable<CalendarConflict>> GetByIntegrationIdAsync(Guid integrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conflicts that can be auto-resolved
    /// </summary>
    Task<IEnumerable<CalendarConflict>> GetAutoResolvableConflictsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets expired conflicts
    /// </summary>
    Task<IEnumerable<CalendarConflict>> GetExpiredConflictsAsync(DateTime beforeTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new calendar conflict
    /// </summary>
    Task<CalendarConflict> AddAsync(CalendarConflict conflict, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing calendar conflict
    /// </summary>
    Task UpdateAsync(CalendarConflict conflict, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a calendar conflict
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk deletes resolved conflicts
    /// </summary>
    Task DeleteResolvedConflictsAsync(Guid userId, DateTime beforeDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conflict statistics
    /// </summary>
    Task<CalendarConflictStatistics> GetConflictStatisticsAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for calendar events
/// </summary>
public record CalendarEventStatistics(
    int TotalEvents,
    int UpcomingEvents,
    int CompletedEvents,
    int EventsWithConflicts,
    int RecurringEvents,
    int EventsWithAttendees,
    Dictionary<EventType, int> EventsByType,
    Dictionary<CalendarProvider, int> EventsByProvider
);

/// <summary>
/// Statistics for calendar integrations
/// </summary>
public record CalendarIntegrationStatistics(
    int TotalIntegrations,
    int HealthyIntegrations,
    int IntegrationsWithIssues,
    int IntegrationsNeedingAttention,
    Dictionary<CalendarProvider, int> IntegrationsByProvider,
    Dictionary<IntegrationHealthStatus, int> IntegrationsByHealth
);

/// <summary>
/// Statistics for calendar conflicts
/// </summary>
public record CalendarConflictStatistics(
    int TotalConflicts,
    int ActiveConflicts,
    int ResolvedConflicts,
    int AutoResolvedConflicts,
    Dictionary<ConflictType, int> ConflictsByType,
    Dictionary<ConflictSeverity, int> ConflictsBySeverity,
    double AverageResolutionTimeHours
);