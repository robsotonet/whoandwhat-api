namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for the archive system
/// Controls when and how tasks and projects are archived
/// </summary>
public class ArchiveSettings
{
    /// <summary>
    /// Configuration section key
    /// </summary>
    public const string SectionName = "Archive";

    /// <summary>
    /// Whether archive operations are enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Minimum age in days for completed tasks to be archived
    /// Default: 90 days
    /// </summary>
    public int CompletedTaskArchiveAgeDays { get; set; } = 90;

    /// <summary>
    /// Minimum age in days for canceled/abandoned tasks to be archived
    /// Default: 30 days
    /// </summary>
    public int CanceledTaskArchiveAgeDays { get; set; } = 30;

    /// <summary>
    /// Maximum number of tasks to archive in a single batch operation
    /// Default: 1000 tasks
    /// </summary>
    public int MaxArchiveBatchSize { get; set; } = 1000;

    /// <summary>
    /// Whether to include tasks that are part of active projects in archiving
    /// Default: false (preserve active project tasks)
    /// </summary>
    public bool IncludeActiveProjectTasks { get; set; } = false;

    /// <summary>
    /// Whether to include parent tasks that have incomplete subtasks
    /// Default: true (allow archiving parent tasks)
    /// </summary>
    public bool IncludeParentTasks { get; set; } = true;

    /// <summary>
    /// Archive job schedule in cron format
    /// Default: "0 2 * * *" (daily at 2 AM)
    /// </summary>
    public string? ScheduleCron { get; set; }

    /// <summary>
    /// Whether cleanup of expired archives is enabled
    /// Default: true
    /// </summary>
    public bool CleanupEnabled { get; set; } = true;

    /// <summary>
    /// Cleanup job schedule in cron format
    /// Default: "0 3 * * 0" (weekly on Sunday at 3 AM)
    /// </summary>
    public string? CleanupScheduleCron { get; set; }

    /// <summary>
    /// Threshold number of archived tasks that triggers cleanup scheduling
    /// Default: 1000 tasks
    /// </summary>
    public int CleanupThreshold { get; set; } = 1000;

    /// <summary>
    /// Number of days to keep archived data before permanent deletion
    /// Default: 2555 days (7 years) for compliance
    /// </summary>
    public int ArchiveRetentionDays { get; set; } = 2555;

    /// <summary>
    /// Gets the retention period as TimeSpan
    /// </summary>
    public TimeSpan RetentionPeriod => TimeSpan.FromDays(ArchiveRetentionDays);

    /// <summary>
    /// Maximum priority level to archive (Low=0, Medium=1, High=2, Urgent=3)
    /// Default: null (archive all priority levels)
    /// </summary>
    public int? MaxPriorityToArchive { get; set; }

    /// <summary>
    /// Whether to compress JSON data in archive entries
    /// Default: true for storage efficiency
    /// </summary>
    public bool CompressJsonData { get; set; } = true;

    /// <summary>
    /// Whether to send notifications when archiving operations complete
    /// Default: false (silent operations)
    /// </summary>
    public bool SendNotifications { get; set; } = false;

    /// <summary>
    /// Email addresses to notify about archive operations (comma-separated)
    /// </summary>
    public string? NotificationEmails { get; set; }

    /// <summary>
    /// Whether to perform dry run validation before actual archiving
    /// Default: true (safer operations)
    /// </summary>
    public bool EnableDryRun { get; set; } = true;

    /// <summary>
    /// Timeout in minutes for archive operations
    /// Default: 60 minutes
    /// </summary>
    public int ArchiveTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Gets the archive criteria based on current settings
    /// </summary>
    /// <param name="userId">Optional user ID filter</param>
    /// <returns>Configured archive criteria</returns>
    public Domain.ValueObjects.ArchiveCriteria ToArchiveCriteria(Guid? userId = null)
    {
        return new Domain.ValueObjects.ArchiveCriteria
        {
            MinimumCompletedAge = TimeSpan.FromDays(CompletedTaskArchiveAgeDays),
            MinimumCanceledAge = TimeSpan.FromDays(CanceledTaskArchiveAgeDays),
            MaxArchiveBatchSize = MaxArchiveBatchSize,
            UserId = userId,
            ArchivableStatuses = GetArchivableStatuses().ToArray(),
            MaxPriorityToArchive = MaxPriorityToArchive.HasValue
                ? (Domain.ValueObjects.Priority)MaxPriorityToArchive.Value
                : null,
            IncludeActiveProjectTasks = IncludeActiveProjectTasks,
            IncludeParentTasks = IncludeParentTasks
        };
    }

    /// <summary>
    /// Gets the default set of archivable task statuses
    /// </summary>
    public ISet<Domain.ValueObjects.AppTaskStatus> GetArchivableStatuses()
    {
        return new HashSet<Domain.ValueObjects.AppTaskStatus>
        {
            Domain.ValueObjects.AppTaskStatus.Completed,
            Domain.ValueObjects.AppTaskStatus.Archived
        };
    }

    /// <summary>
    /// Validates the archive settings for consistency
    /// </summary>
    /// <returns>True if settings are valid</returns>
    public bool IsValid()
    {
        return CompletedTaskArchiveAgeDays > 0 &&
               CanceledTaskArchiveAgeDays > 0 &&
               MaxArchiveBatchSize > 0 &&
               ArchiveRetentionDays > 0 &&
               ArchiveTimeoutMinutes > 0 &&
               (!MaxPriorityToArchive.HasValue ||
                (MaxPriorityToArchive.Value >= 0 && MaxPriorityToArchive.Value <= 3));
    }

    /// <summary>
    /// Gets configuration for aggressive archiving (shorter retention periods)
    /// </summary>
    public static ArchiveSettings Aggressive => new()
    {
        CompletedTaskArchiveAgeDays = 30,
        CanceledTaskArchiveAgeDays = 7,
        MaxArchiveBatchSize = 2000,
        IncludeActiveProjectTasks = true,
        IncludeParentTasks = true
    };

    /// <summary>
    /// Gets configuration for conservative archiving (longer retention periods)
    /// </summary>
    public static ArchiveSettings Conservative => new()
    {
        CompletedTaskArchiveAgeDays = 180,
        CanceledTaskArchiveAgeDays = 60,
        MaxArchiveBatchSize = 500,
        IncludeActiveProjectTasks = false,
        IncludeParentTasks = false,
        EnableDryRun = true
    };
}
