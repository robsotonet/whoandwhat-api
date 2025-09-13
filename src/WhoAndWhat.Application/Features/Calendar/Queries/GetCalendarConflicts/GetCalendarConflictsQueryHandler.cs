using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Calendar.Queries.GetCalendarConflicts;

public class GetCalendarConflictsQueryHandler : IRequestHandler<GetCalendarConflictsQuery, Result<CalendarConflictsResponse>>
{
    private readonly ICalendarConflictDetector _conflictDetector;
    private readonly ICalendarSyncService _calendarSyncService;
    private readonly ILogger<GetCalendarConflictsQueryHandler> _logger;

    public GetCalendarConflictsQueryHandler(
        ICalendarConflictDetector conflictDetector,
        ICalendarSyncService calendarSyncService,
        ILogger<GetCalendarConflictsQueryHandler> logger)
    {
        _conflictDetector = conflictDetector ?? throw new ArgumentNullException(nameof(conflictDetector));
        _calendarSyncService = calendarSyncService ?? throw new ArgumentNullException(nameof(calendarSyncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CalendarConflictsResponse>> Handle(GetCalendarConflictsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting calendar conflicts for user {UserId}", request.UserId);

            // Get all conflicts for the user based on filter options
            var allConflicts = await _conflictDetector.GetPendingConflictsAsync(
                request.UserId, 
                request.FilterOptions, 
                cancellationToken);

            // Convert DetectedConflict to CalendarSyncConflict for filtering
            var syncConflicts = allConflicts.Select(ConvertToSyncConflict).ToList();
            
            // Apply additional filtering if needed
            var filteredConflicts = ApplyFilters(syncConflicts, request.FilterOptions).ToList();

            // Calculate statistics
            var statistics = CalculateConflictStatistics(filteredConflicts);

            // Count unresolved conflicts
            var unresolvedCount = filteredConflicts.Count(c => c.RequiresUserAction);

            var response = new CalendarConflictsResponse(
                filteredConflicts,
                allConflicts.Count(),
                unresolvedCount,
                statistics,
                DateTime.UtcNow
            );

            _logger.LogInformation("Retrieved {ConflictCount} conflicts ({UnresolvedCount} unresolved) for user {UserId}",
                filteredConflicts.Count, unresolvedCount, request.UserId);

            return Result<CalendarConflictsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting calendar conflicts for user {UserId}", request.UserId);
            return Result<CalendarConflictsResponse>.Failure("An error occurred while getting calendar conflicts");
        }
    }

    private static IEnumerable<CalendarSyncConflict> ApplyFilters(
        IEnumerable<CalendarSyncConflict> conflicts,
        ConflictFilterOptions filterOptions)
    {
        var filtered = conflicts;

        // Filter by conflict types
        if (filterOptions.IncludeTypes != null && filterOptions.IncludeTypes.Any())
        {
            filtered = filtered.Where(c => filterOptions.IncludeTypes.Contains(c.Type));
        }

        // Filter by severity levels
        if (filterOptions.IncludeSeverities != null && filterOptions.IncludeSeverities.Any())
        {
            filtered = filtered.Where(c => filterOptions.IncludeSeverities.Contains(c.Severity));
        }

        // Filter by date range
        if (filterOptions.FromDate.HasValue)
        {
            filtered = filtered.Where(c => c.DetectedAt >= filterOptions.FromDate.Value);
        }

        if (filterOptions.ToDate.HasValue)
        {
            filtered = filtered.Where(c => c.DetectedAt <= filterOptions.ToDate.Value);
        }

        // Filter by resolution status
        if (filterOptions.OnlyUnresolved.HasValue && filterOptions.OnlyUnresolved.Value)
        {
            filtered = filtered.Where(c => c.RequiresUserAction);
        }

        // Filter by provider
        if (filterOptions.Provider.HasValue)
        {
            filtered = filtered.Where(c => c.Provider == filterOptions.Provider.Value);
        }

        return filtered;
    }

    private static ConflictStatistics CalculateConflictStatistics(List<CalendarSyncConflict> conflicts)
    {
        var totalConflicts = conflicts.Count;
        var resolvedConflicts = conflicts.Count(c => !c.RequiresUserAction);
        var pendingConflicts = conflicts.Count(c => c.RequiresUserAction);

        var conflictsByType = conflicts
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        var conflictsBySeverity = conflicts
            .GroupBy(c => c.Severity)
            .ToDictionary(g => g.Key, g => g.Count());

        // Calculate average resolution time for resolved conflicts
        var resolvedConflictsWithTiming = conflicts
            .Where(c => !c.RequiresUserAction && c.DetectedAt > DateTime.MinValue)
            .ToList();

        var averageResolutionTime = resolvedConflictsWithTiming.Any()
            ? TimeSpan.FromMinutes(resolvedConflictsWithTiming.Average(c => 
                (DateTime.UtcNow - c.DetectedAt).TotalMinutes)) // Simplified calculation
            : TimeSpan.Zero;

        // Calculate auto-resolution rate
        var autoResolutionRate = totalConflicts > 0
            ? (float)resolvedConflicts / totalConflicts
            : 0f;

        return new ConflictStatistics(
            totalConflicts,
            resolvedConflicts,
            pendingConflicts,
            conflictsByType,
            conflictsBySeverity,
            averageResolutionTime,
            autoResolutionRate
        );
    }

    /// <summary>
    /// Converts DetectedConflict to CalendarSyncConflict for compatibility
    /// </summary>
    private static CalendarSyncConflict ConvertToSyncConflict(DetectedConflict detected)
    {
        return new CalendarSyncConflict(
            ConflictId: detected.ConflictId,
            UserId: detected.UserId,
            Provider: CalendarProvider.Google, // Default provider - would need actual logic
            Type: detected.Type,
            Severity: detected.Severity,
            Title: $"Conflict detected: {detected.Type}",
            Description: detected.Description,
            InternalEvent: detected.InternalEvent,
            ExternalEvent: detected.ExternalEvent,
            ResolutionOptions: new List<ConflictResolutionOption>(), // Default empty - would need actual options
            DetectedAt: detected.DetectedAt,
            RequiresUserAction: detected.Severity == ConflictSeverity.High,
            ConflictMetadata: detected.ConflictDetails
        );
    }
}