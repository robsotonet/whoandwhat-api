using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Service interface for task archiving operations
/// Handles the lifecycle of moving completed tasks to archive storage for performance optimization
/// </summary>
public interface ITaskArchiveService
{
    /// <summary>
    /// Archives tasks based on the specified criteria
    /// Moves eligible tasks from active storage to archive tables
    /// </summary>
    /// <param name="criteria">Archive criteria defining which tasks to archive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Archive operation result with statistics</returns>
    public Task<ArchiveOperationResult> ArchiveTasksAsync(ArchiveCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives tasks for a specific user based on default criteria
    /// </summary>
    /// <param name="userId">User ID whose tasks should be archived</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Archive operation result</returns>
    public Task<ArchiveOperationResult> ArchiveUserTasksAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a dry run of the archive operation without making changes
    /// Useful for validation and reporting what would be archived
    /// </summary>
    /// <param name="criteria">Archive criteria to test</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Archive preview with statistics</returns>
    public Task<ArchiveOperationPreview> PreviewArchiveAsync(ArchiveCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores an archived task back to active status
    /// </summary>
    /// <param name="archivedTaskId">ID of the archived task</param>
    /// <param name="userId">User ID for authorization</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Restore operation result</returns>
    public Task<RestoreOperationResult> RestoreArchivedTaskAsync(Guid archivedTaskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets archived tasks for a specific user with filtering and pagination
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="filter">Optional filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of archived tasks</returns>
    public Task<PagedResult<ArchivedTaskDto>> GetArchivedTasksAsync(Guid userId, ArchivedTaskFilter? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets archive statistics for a specific user or system-wide
    /// </summary>
    /// <param name="userId">User ID (null for system-wide statistics)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Archive statistics</returns>
    public Task<ArchiveStatistics> GetArchiveStatisticsAsync(Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a specific project and all its associated tasks
    /// </summary>
    /// <param name="projectId">Project ID to archive</param>
    /// <param name="userId">User ID for authorization</param>
    /// <param name="reason">Reason for archiving</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Archive operation result</returns>
    public Task<ArchiveOperationResult> ArchiveProjectAsync(Guid projectId, Guid userId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes archived data older than specified retention period
    /// This is irreversible and should be used carefully
    /// </summary>
    /// <param name="retentionPeriod">Data older than this period will be deleted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cleanup operation result</returns>
    public Task<CleanupOperationResult> CleanupExpiredArchivesAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates archive criteria and returns validation results
    /// </summary>
    /// <param name="criteria">Criteria to validate</param>
    /// <returns>Validation result</returns>
    public ArchiveValidationResult ValidateArchiveCriteria(ArchiveCriteria criteria);
}