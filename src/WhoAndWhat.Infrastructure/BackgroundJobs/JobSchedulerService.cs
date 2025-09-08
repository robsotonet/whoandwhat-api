using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that sets up recurring Hangfire jobs for the application
/// </summary>
public class JobSchedulerService : IHostedService
{
    private readonly ILogger<JobSchedulerService> _logger;
    private readonly ArchiveSettings _archiveSettings;
    private readonly IRecurringJobManager _recurringJobManager;

    public JobSchedulerService(
        ILogger<JobSchedulerService> logger,
        IOptions<ArchiveSettings> archiveSettings,
        IRecurringJobManager recurringJobManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _archiveSettings = archiveSettings.Value ?? throw new ArgumentNullException(nameof(archiveSettings));
        _recurringJobManager = recurringJobManager ?? throw new ArgumentNullException(nameof(recurringJobManager));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Setting up recurring background jobs");

            SetupArchivingJobs();
            SetupMaintenanceJobs();

            _logger.LogInformation("Successfully configured all recurring background jobs");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup recurring background jobs: {Error}", ex.Message);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping job scheduler service");
        return Task.CompletedTask;
    }

    private void SetupArchivingJobs()
    {
        if (!_archiveSettings.Enabled)
        {
            _logger.LogInformation("Task archiving is disabled, skipping archiving job setup");
            return;
        }

        // Setup automatic task archiving job
        // Run daily at 2 AM UTC by default, or use configured schedule
        var archivingSchedule = _archiveSettings.ScheduleCron ?? "0 2 * * *"; // Daily at 2 AM UTC
        
        _recurringJobManager.AddOrUpdate<ArchiveTasksJob>(
            "archive-tasks",
            job => job.ExecuteAsync(CancellationToken.None),
            archivingSchedule,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        _logger.LogInformation("Configured automatic task archiving job with schedule: {Schedule}", archivingSchedule);

        // Setup cleanup job if enabled
        if (_archiveSettings.CleanupEnabled)
        {
            // Run weekly on Sunday at 3 AM UTC by default
            var cleanupSchedule = _archiveSettings.CleanupScheduleCron ?? "0 3 * * 0"; // Weekly on Sunday at 3 AM UTC
            
            _recurringJobManager.AddOrUpdate<ArchiveTasksJob>(
                "cleanup-expired-archives",
                job => job.CleanupExpiredArchivesAsync(CancellationToken.None),
                cleanupSchedule,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });

            _logger.LogInformation("Configured archive cleanup job with schedule: {Schedule}", cleanupSchedule);
        }
    }

    private void SetupMaintenanceJobs()
    {
        // Setup database maintenance job (runs monthly)
        _recurringJobManager.AddOrUpdate(
            "database-maintenance",
            () => PerformDatabaseMaintenance(),
            "0 4 1 * *", // Monthly on the 1st at 4 AM UTC
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        _logger.LogInformation("Configured database maintenance job");
    }

    [JobDisplayName("Database Maintenance")]
    [DisableConcurrentExecution(timeoutInSeconds: 4 * 60 * 60)] // 4 hours timeout
    public void PerformDatabaseMaintenance()
    {
        try
        {
            _logger.LogInformation("Starting database maintenance tasks");

            // Here you could add various maintenance tasks:
            // - Update statistics
            // - Rebuild indexes
            // - Vacuum operations
            // - Clear old logs
            // etc.

            _logger.LogInformation("Database maintenance completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database maintenance failed: {Error}", ex.Message);
            throw;
        }
    }
}