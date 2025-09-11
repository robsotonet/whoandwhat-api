using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Services.Analytics;

namespace WhoAndWhat.Infrastructure.BackgroundServices;

/// <summary>
/// Background hosted service for automated analytics processing
/// Handles scheduled analytics calculations and maintenance tasks
/// </summary>
public class AnalyticsBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsBackgroundService> _logger;
    private readonly TimeSpan _dailyProcessingInterval = TimeSpan.FromHours(1); // Check every hour
    private readonly TimeSpan _maintenanceInterval = TimeSpan.FromHours(6); // Maintenance every 6 hours

    private DateTime _lastDailyProcessing = DateTime.MinValue;
    private DateTime _lastMaintenance = DateTime.MinValue;

    public AnalyticsBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analytics background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledTasks(stoppingToken);

                // Wait before next iteration
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Analytics background service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in analytics background service execution");

                // Wait longer after an error to avoid rapid failure loops
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task ProcessScheduledTasks(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var processingService = scope.ServiceProvider.GetRequiredService<IAnalyticsProcessingService>();

        var now = DateTime.UtcNow;
        var today = now.Date;

        // Daily analytics processing
        await ProcessDailyAnalytics(processingService, today, cancellationToken);

        // Weekly snapshots (run on Mondays)
        if (now.DayOfWeek == DayOfWeek.Monday && now.Hour >= 2)
        {
            await ProcessWeeklySnapshots(processingService, today, cancellationToken);
        }

        // Monthly snapshots (run on the 1st day of the month)
        if (now.Day == 1 && now.Hour >= 3)
        {
            await ProcessMonthlySnapshots(processingService, today, cancellationToken);
        }

        // Maintenance tasks
        await ProcessMaintenanceTasks(processingService, now, cancellationToken);
    }

    private async Task ProcessDailyAnalytics(IAnalyticsProcessingService processingService, DateTime today, CancellationToken cancellationToken)
    {
        try
        {
            var timeSinceLastProcessing = DateTime.UtcNow - _lastDailyProcessing;

            // Process yesterday's data if it's a new day and we haven't processed recently
            if (timeSinceLastProcessing >= _dailyProcessingInterval)
            {
                var yesterday = today.AddDays(-1);

                _logger.LogInformation("Starting daily analytics processing for {Date}", yesterday);

                await processingService.ProcessDailyAnalyticsForAllUsersAsync(yesterday, cancellationToken);
                await processingService.UpdateProductivityStreaksForAllUsersAsync(yesterday, cancellationToken);
                await processingService.UpdateUserAnalyticsForAllUsersAsync(cancellationToken);

                _lastDailyProcessing = DateTime.UtcNow;

                _logger.LogInformation("Completed daily analytics processing for {Date}", yesterday);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process daily analytics");
            throw;
        }
    }

    private async Task ProcessWeeklySnapshots(IAnalyticsProcessingService processingService, DateTime today, CancellationToken cancellationToken)
    {
        try
        {
            // Calculate last Monday (start of previous week)
            var daysToSubtract = ((int)today.DayOfWeek - 1 + 7) % 7 + 7; // Last Monday
            var lastWeekStart = today.AddDays(-daysToSubtract);

            _logger.LogInformation("Generating weekly snapshots for week starting {WeekStart}", lastWeekStart);

            await processingService.GenerateWeeklySnapshotsAsync(lastWeekStart, cancellationToken);

            _logger.LogInformation("Completed weekly snapshots generation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate weekly snapshots");
            // Don't rethrow - continue with other tasks
        }
    }

    private async Task ProcessMonthlySnapshots(IAnalyticsProcessingService processingService, DateTime today, CancellationToken cancellationToken)
    {
        try
        {
            // Process previous month
            var lastMonth = today.AddMonths(-1);

            _logger.LogInformation("Generating monthly snapshots for {Year}-{Month:D2}", lastMonth.Year, lastMonth.Month);

            await processingService.GenerateMonthlySnapshotsAsync(lastMonth.Year, lastMonth.Month, cancellationToken);

            _logger.LogInformation("Completed monthly snapshots generation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate monthly snapshots");
            // Don't rethrow - continue with other tasks
        }
    }

    private async Task ProcessMaintenanceTasks(IAnalyticsProcessingService processingService, DateTime now, CancellationToken cancellationToken)
    {
        try
        {
            var timeSinceLastMaintenance = now - _lastMaintenance;

            if (timeSinceLastMaintenance >= _maintenanceInterval)
            {
                _logger.LogInformation("Starting analytics maintenance tasks");

                // Data cleanup (run during off-peak hours)
                if (now.Hour >= 2 && now.Hour <= 4)
                {
                    await processingService.PerformDataCleanupAsync(cancellationToken);
                }

                // Get and log system status
                var status = await processingService.GetProcessingStatusAsync(cancellationToken);

                _logger.LogInformation("Analytics system status: {Status}, Active jobs: {ActiveJobs}, Failed jobs: {FailedJobs}",
                    status.SystemStatus, status.ActiveProcessingJobs, status.FailedProcessingJobs);

                if (!status.IsHealthy)
                {
                    _logger.LogWarning("Analytics system health is degraded. Recent errors: {Errors}",
                        string.Join("; ", status.RecentErrors.Take(3)));
                }

                _lastMaintenance = now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process maintenance tasks");
            // Don't rethrow - this shouldn't stop the service
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analytics background service is stopping");

        try
        {
            // Allow some time for current operations to complete
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            await base.StopAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Analytics background service stop operation was cancelled");
        }

        _logger.LogInformation("Analytics background service stopped");
    }
}
