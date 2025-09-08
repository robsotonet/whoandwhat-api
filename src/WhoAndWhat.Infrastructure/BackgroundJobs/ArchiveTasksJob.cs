using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job for automated task archiving
/// Runs periodically to archive completed and canceled tasks based on configured criteria
/// </summary>
public class ArchiveTasksJob
{
    private readonly ITaskArchiveService _archiveService;
    private readonly ILogger<ArchiveTasksJob> _logger;
    private readonly ArchiveSettings _archiveSettings;

    public ArchiveTasksJob(
        ITaskArchiveService archiveService,
        ILogger<ArchiveTasksJob> logger,
        IOptions<ArchiveSettings> archiveSettings)
    {
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _archiveSettings = archiveSettings.Value ?? throw new ArgumentNullException(nameof(archiveSettings));
    }

    /// <summary>
    /// Executes the automatic archiving job
    /// This method is called by Hangfire on a scheduled basis
    /// </summary>
    [JobDisplayName("Archive Completed Tasks")]
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)] // Prevent overlapping executions for up to 1 hour
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting automated task archiving job");

            if (!_archiveSettings.Enabled)
            {
                _logger.LogInformation("Task archiving is disabled, skipping job execution");
                return;
            }

            // Create default archive criteria from settings
            var archiveCriteria = _archiveSettings.ToArchiveCriteria(userId: null);

            // Validate criteria before proceeding
            var validation = _archiveService.ValidateArchiveCriteria(archiveCriteria);
            if (!validation.IsValid)
            {
                _logger.LogError("Invalid archive criteria: {Errors}", string.Join(", ", validation.Errors));
                return;
            }

            if (validation.Warnings.Any())
            {
                _logger.LogWarning("Archive criteria warnings: {Warnings}", string.Join(", ", validation.Warnings));
            }

            // Execute the archiving operation
            var result = await _archiveService.ArchiveTasksAsync(archiveCriteria, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Automated archiving completed successfully. " +
                    "Tasks archived: {TasksArchived}, " +
                    "Projects archived: {ProjectsArchived}, " +
                    "Duration: {Duration}ms, " +
                    "Success rate: {SuccessRate:F1}%",
                    result.TasksArchived,
                    result.ProjectsArchived,
                    result.Duration.TotalMilliseconds,
                    result.SuccessRate);

                // Schedule cleanup job if we archived a significant number of tasks
                if (result.TasksArchived >= _archiveSettings.CleanupThreshold)
                {
                    ScheduleCleanupJob();
                }
            }
            else
            {
                _logger.LogError(
                    "Automated archiving failed: {ErrorMessage}. " +
                    "Tasks failed: {TasksFailed}, Duration: {Duration}ms",
                    result.ErrorMessage,
                    result.TasksFailed,
                    result.Duration.TotalMilliseconds);

                if (result.Errors.Any())
                {
                    _logger.LogError("Detailed errors: {Errors}", string.Join("; ", result.Errors));
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Automated archiving job was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during automated archiving job: {Error}", ex.Message);
            throw; // Re-throw to let Hangfire handle retry logic
        }
    }

    /// <summary>
    /// Archives tasks for a specific user
    /// Can be triggered manually or scheduled for specific users
    /// </summary>
    [JobDisplayName("Archive User Tasks ({0})")]
    [DisableConcurrentExecution(timeoutInSeconds: 30 * 60)] // 30 minutes timeout per user
    public async Task ArchiveUserTasksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting task archiving for user {UserId}", userId);

            var result = await _archiveService.ArchiveUserTasksAsync(userId, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "User task archiving completed for {UserId}. " +
                    "Tasks archived: {TasksArchived}, Duration: {Duration}ms",
                    userId,
                    result.TasksArchived,
                    result.Duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogError(
                    "User task archiving failed for {UserId}: {ErrorMessage}",
                    userId,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user task archiving for {UserId}: {Error}", userId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Cleanup expired archives based on retention policy
    /// </summary>
    [JobDisplayName("Cleanup Expired Archives")]
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60)] // 2 hours timeout
    public async Task CleanupExpiredArchivesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting cleanup of expired archives");

            if (!_archiveSettings.CleanupEnabled)
            {
                _logger.LogInformation("Archive cleanup is disabled, skipping cleanup job");
                return;
            }

            var result = await _archiveService.CleanupExpiredArchivesAsync(
                _archiveSettings.RetentionPeriod, 
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Archive cleanup completed successfully. " +
                    "Tasks deleted: {TasksDeleted}, " +
                    "Projects deleted: {ProjectsDeleted}, " +
                    "Space freed: {SpaceFreed} bytes, " +
                    "Duration: {Duration}ms",
                    result.TasksDeleted,
                    result.ProjectsDeleted,
                    result.SpaceFreed,
                    result.Duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogError("Archive cleanup failed with errors: {Errors}", 
                    string.Join("; ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during archive cleanup: {Error}", ex.Message);
            throw;
        }
    }

    private void ScheduleCleanupJob()
    {
        try
        {
            // Schedule cleanup job to run in a few hours to avoid overlapping with archiving
            var delay = TimeSpan.FromHours(2);
            BackgroundJob.Schedule<ArchiveTasksJob>(
                job => job.CleanupExpiredArchivesAsync(CancellationToken.None),
                delay);

            _logger.LogInformation("Scheduled cleanup job to run in {Delay} hours", delay.TotalHours);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to schedule cleanup job: {Error}", ex.Message);
        }
    }
}