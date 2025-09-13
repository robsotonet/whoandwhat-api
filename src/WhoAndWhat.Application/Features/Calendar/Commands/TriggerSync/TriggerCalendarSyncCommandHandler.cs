using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Calendar.Commands.TriggerSync;

public class TriggerCalendarSyncCommandHandler : IRequestHandler<TriggerCalendarSyncCommand, Result<CalendarSyncResult>>
{
    private readonly ICalendarSyncService _calendarSyncService;
    private readonly ILogger<TriggerCalendarSyncCommandHandler> _logger;

    public TriggerCalendarSyncCommandHandler(
        ICalendarSyncService calendarSyncService,
        ILogger<TriggerCalendarSyncCommandHandler> logger)
    {
        _calendarSyncService = calendarSyncService ?? throw new ArgumentNullException(nameof(calendarSyncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CalendarSyncResult>> Handle(TriggerCalendarSyncCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Triggering calendar sync for user {UserId} with provider {Provider}",
                request.UserId, request.Provider?.ToString() ?? "all");

            var syncStartTime = DateTime.UtcNow;

            // Determine which providers to sync
            var providersToSync = request.Provider.HasValue
                ? new List<CalendarProvider> { request.Provider.Value }
                : await GetConnectedProviders(request.UserId, cancellationToken);

            if (!providersToSync.Any())
            {
                return Result<CalendarSyncResult>.Failure("No calendar providers are connected for this user");
            }

            var allResults = new List<CalendarSyncResult>();

            // Sync each provider
            foreach (var provider in providersToSync)
            {
                try
                {
                    _logger.LogDebug("Starting sync for provider {Provider}", provider);
                    
                    var providerResult = await _calendarSyncService.SyncCalendarAsync(
                        request.UserId,
                        provider,
                        request.ForceFullSync,
                        request.SyncDirection,
                        cancellationToken
                    );

                    if (providerResult != null)
                    {
                        allResults.Add(providerResult);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing provider {Provider} for user {UserId}", provider, request.UserId);
                    
                    // Create error result for this provider
                    var errorResult = new CalendarSyncResult(
                        request.UserId,
                        provider,
                        false, // Success
                        0, // EventsSynced
                        0, // EventsCreated
                        0, // EventsUpdated
                        0, // EventsDeleted
                        0, // ConflictsDetected
                        0, // ConflictsResolved
                        TimeSpan.FromMilliseconds(100),
                        null, // NewSyncToken
                        new List<string> { ex.Message },
                        new List<string>(),
                        syncStartTime,
                        DateTime.UtcNow,
                        request.SyncDirection,
                        new SyncStatistics(1, 0, TimeSpan.Zero, TimeSpan.FromMilliseconds(100), 0, 0)
                    );
                    
                    allResults.Add(errorResult);
                }
            }

            // Aggregate results
            var aggregatedResult = AggregateResults(allResults, request, syncStartTime);

            if (aggregatedResult.Success)
            {
                _logger.LogInformation("Calendar sync completed successfully for user {UserId}. " +
                    "Synced: {EventsSynced}, Created: {EventsCreated}, Updated: {EventsUpdated}, Deleted: {EventsDeleted}",
                    request.UserId, aggregatedResult.EventsSynced, aggregatedResult.EventsCreated, 
                    aggregatedResult.EventsUpdated, aggregatedResult.EventsDeleted);
            }
            else
            {
                _logger.LogWarning("Calendar sync completed with errors for user {UserId}. Errors: {Errors}",
                    request.UserId, string.Join(", ", aggregatedResult.Errors));
            }

            return Result<CalendarSyncResult>.Success(aggregatedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during calendar sync for user {UserId}", request.UserId);
            return Result<CalendarSyncResult>.Failure("An error occurred during calendar synchronization");
        }
    }

    private async Task<List<CalendarProvider>> GetConnectedProviders(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            // In a real implementation, this would query the user's connected providers
            // For now, return a mock list
            return new List<CalendarProvider>
            {
                CalendarProvider.Google,
                CalendarProvider.Outlook
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connected providers for user {UserId}", userId);
            return new List<CalendarProvider>();
        }
    }

    private static CalendarSyncResult AggregateResults(
        List<CalendarSyncResult> results,
        TriggerCalendarSyncCommand request,
        DateTime syncStartTime)
    {
        if (!results.Any())
        {
            return new CalendarSyncResult(
                request.UserId,
                request.Provider ?? CalendarProvider.Google,
                false,
                0, 0, 0, 0, 0, 0,
                TimeSpan.Zero,
                null,
                new List<string> { "No sync results available" },
                new List<string>(),
                syncStartTime,
                DateTime.UtcNow,
                request.SyncDirection,
                new SyncStatistics(0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, 0)
            );
        }

        var aggregatedResult = new CalendarSyncResult(
            request.UserId,
            request.Provider ?? results.First().Provider, // Use first provider if syncing all
            results.All(r => r.Success), // Success only if all succeeded
            results.Sum(r => r.EventsSynced),
            results.Sum(r => r.EventsCreated),
            results.Sum(r => r.EventsUpdated),
            results.Sum(r => r.EventsDeleted),
            results.Sum(r => r.ConflictsDetected),
            results.Sum(r => r.ConflictsResolved),
            TimeSpan.FromMilliseconds(results.Sum(r => r.SyncDuration.TotalMilliseconds)),
            results.Where(r => !string.IsNullOrEmpty(r.NewSyncToken)).Select(r => r.NewSyncToken).FirstOrDefault(),
            results.SelectMany(r => r.Errors).ToList(),
            results.SelectMany(r => r.Warnings).ToList(),
            syncStartTime,
            DateTime.UtcNow,
            request.SyncDirection,
            AggregateStatistics(results.Select(r => r.Statistics))
        );

        return aggregatedResult;
    }

    private static SyncStatistics AggregateStatistics(IEnumerable<SyncStatistics> stats)
    {
        var statsList = stats.ToList();
        if (!statsList.Any())
        {
            return new SyncStatistics(0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, 0);
        }

        return new SyncStatistics(
            statsList.Sum(s => s.ApiCallsMade),
            statsList.Sum(s => s.DataTransferred),
            TimeSpan.FromMilliseconds(statsList.Sum(s => s.NetworkTime.TotalMilliseconds)),
            TimeSpan.FromMilliseconds(statsList.Sum(s => s.ProcessingTime.TotalMilliseconds)),
            statsList.Sum(s => s.CacheHits),
            statsList.Sum(s => s.CacheMisses)
        );
    }
}