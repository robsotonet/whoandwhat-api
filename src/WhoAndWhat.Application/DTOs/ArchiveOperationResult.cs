namespace WhoAndWhat.Application.DTOs;

/// <summary>
/// Result of an archive operation containing statistics and outcome details
/// </summary>
public record ArchiveOperationResult
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of tasks successfully archived
    /// </summary>
    public int TasksArchived { get; init; }

    /// <summary>
    /// Number of projects successfully archived
    /// </summary>
    public int ProjectsArchived { get; init; }

    /// <summary>
    /// Number of tasks that failed to archive
    /// </summary>
    public int TasksFailed { get; init; }

    /// <summary>
    /// Number of tasks that were skipped (e.g., not meeting criteria)
    /// </summary>
    public int TasksSkipped { get; init; }

    /// <summary>
    /// Total duration of the archive operation
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Timestamp when operation started
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Timestamp when operation completed
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// List of IDs of tasks that were archived
    /// </summary>
    public List<Guid> ArchivedTaskIds { get; init; } = new();

    /// <summary>
    /// List of IDs of projects that were archived
    /// </summary>
    public List<Guid> ArchivedProjectIds { get; init; } = new();

    /// <summary>
    /// Detailed error information for failed operations
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Additional metadata about the operation
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Total number of items processed
    /// </summary>
    public int TotalProcessed => TasksArchived + TasksFailed + TasksSkipped + ProjectsArchived;

    /// <summary>
    /// Success rate as a percentage
    /// </summary>
    public double SuccessRate => TotalProcessed > 0 ? (double)(TasksArchived + ProjectsArchived) / TotalProcessed * 100 : 0;

    /// <summary>
    /// Creates a successful archive operation result
    /// </summary>
    public static ArchiveOperationResult Success(int tasksArchived, int projectsArchived, TimeSpan duration, List<Guid>? archivedTaskIds = null, List<Guid>? archivedProjectIds = null)
    {
        return new ArchiveOperationResult
        {
            IsSuccess = true,
            TasksArchived = tasksArchived,
            ProjectsArchived = projectsArchived,
            Duration = duration,
            StartedAt = DateTime.UtcNow - duration,
            CompletedAt = DateTime.UtcNow,
            ArchivedTaskIds = archivedTaskIds ?? new List<Guid>(),
            ArchivedProjectIds = archivedProjectIds ?? new List<Guid>()
        };
    }

    /// <summary>
    /// Creates a failed archive operation result
    /// </summary>
    public static ArchiveOperationResult Failure(string errorMessage, int tasksProcessed = 0, int tasksFailed = 0, TimeSpan? duration = null)
    {
        var operationDuration = duration ?? TimeSpan.Zero;
        return new ArchiveOperationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            TasksFailed = tasksFailed,
            TasksSkipped = tasksProcessed - tasksFailed,
            Duration = operationDuration,
            StartedAt = DateTime.UtcNow - operationDuration,
            CompletedAt = DateTime.UtcNow,
            Errors = new List<string> { errorMessage }
        };
    }

    /// <summary>
    /// Creates a partial success result (some items succeeded, some failed)
    /// </summary>
    public static ArchiveOperationResult Partial(int tasksArchived, int tasksFailed, int tasksSkipped, TimeSpan duration, List<string> errors)
    {
        return new ArchiveOperationResult
        {
            IsSuccess = tasksArchived > 0,
            TasksArchived = tasksArchived,
            TasksFailed = tasksFailed,
            TasksSkipped = tasksSkipped,
            Duration = duration,
            StartedAt = DateTime.UtcNow - duration,
            CompletedAt = DateTime.UtcNow,
            Errors = errors
        };
    }
}

/// <summary>
/// Preview of what would be archived without executing the operation
/// </summary>
public record ArchiveOperationPreview
{
    /// <summary>
    /// Number of tasks that would be archived
    /// </summary>
    public int TasksToArchive { get; init; }

    /// <summary>
    /// Number of projects that would be archived
    /// </summary>
    public int ProjectsToArchive { get; init; }

    /// <summary>
    /// Estimated size of data to be archived (in bytes)
    /// </summary>
    public long EstimatedDataSize { get; init; }

    /// <summary>
    /// Breakdown by task category
    /// </summary>
    public Dictionary<string, int> TasksByCategory { get; init; } = new();

    /// <summary>
    /// Breakdown by task status
    /// </summary>
    public Dictionary<string, int> TasksByStatus { get; init; } = new();

    /// <summary>
    /// Breakdown by priority level
    /// </summary>
    public Dictionary<string, int> TasksByPriority { get; init; } = new();

    /// <summary>
    /// Date range of tasks to be archived
    /// </summary>
    public DateOnly? OldestTaskDate { get; init; }

    /// <summary>
    /// Date of the newest task to be archived
    /// </summary>
    public DateOnly? NewestTaskDate { get; init; }

    /// <summary>
    /// List of warnings about the archive operation
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Whether the operation is safe to proceed
    /// </summary>
    public bool IsSafeToExecute => !Warnings.Any(w => w.Contains("CRITICAL"));
}

/// <summary>
/// Result of restoring an archived task
/// </summary>
public record RestoreOperationResult
{
    /// <summary>
    /// Whether the restore was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if restore failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// ID of the restored task
    /// </summary>
    public Guid? RestoredTaskId { get; init; }

    /// <summary>
    /// Number of related items also restored (subtasks, etc.)
    /// </summary>
    public int RelatedItemsRestored { get; init; }

    /// <summary>
    /// Creates a successful restore result
    /// </summary>
    public static RestoreOperationResult Success(Guid restoredTaskId, int relatedItemsRestored = 0)
    {
        return new RestoreOperationResult
        {
            IsSuccess = true,
            RestoredTaskId = restoredTaskId,
            RelatedItemsRestored = relatedItemsRestored
        };
    }

    /// <summary>
    /// Creates a failed restore result
    /// </summary>
    public static RestoreOperationResult Failure(string errorMessage)
    {
        return new RestoreOperationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Result of cleanup operations for expired archives
/// </summary>
public record CleanupOperationResult
{
    /// <summary>
    /// Whether cleanup was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Number of archived tasks permanently deleted
    /// </summary>
    public int TasksDeleted { get; init; }

    /// <summary>
    /// Number of archived projects permanently deleted
    /// </summary>
    public int ProjectsDeleted { get; init; }

    /// <summary>
    /// Amount of storage space freed (in bytes)
    /// </summary>
    public long SpaceFreed { get; init; }

    /// <summary>
    /// Duration of cleanup operation
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Any errors encountered during cleanup
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Creates a successful cleanup result
    /// </summary>
    public static CleanupOperationResult Success(int tasksDeleted, int projectsDeleted, long spaceFreed, TimeSpan duration)
    {
        return new CleanupOperationResult
        {
            IsSuccess = true,
            TasksDeleted = tasksDeleted,
            ProjectsDeleted = projectsDeleted,
            SpaceFreed = spaceFreed,
            Duration = duration
        };
    }
}

/// <summary>
/// Validation result for archive criteria
/// </summary>
public record ArchiveValidationResult
{
    /// <summary>
    /// Whether the criteria are valid
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// List of validation warnings
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Creates a valid result
    /// </summary>
    public static ArchiveValidationResult Valid() => new() { IsValid = true };

    /// <summary>
    /// Creates an invalid result with errors
    /// </summary>
    public static ArchiveValidationResult Invalid(List<string> errors, List<string>? warnings = null)
    {
        return new ArchiveValidationResult
        {
            IsValid = false,
            Errors = errors,
            Warnings = warnings ?? new List<string>()
        };
    }
}