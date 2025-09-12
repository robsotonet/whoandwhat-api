using AppTask = WhoAndWhat.Domain.Entities.AppTask;

namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Value object defining criteria for when tasks should be archived
/// Encapsulates business rules for automatic archiving decisions
/// </summary>
public record ArchiveCriteria : IEquatable<ArchiveCriteria>
{
    /// <summary>
    /// Minimum age for completed tasks to be eligible for archiving
    /// </summary>
    public TimeSpan MinimumCompletedAge { get; init; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Minimum age for canceled/abandoned tasks to be archived
    /// </summary>
    public TimeSpan MinimumCanceledAge { get; init; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Whether to include tasks that are part of active projects
    /// </summary>
    public bool IncludeActiveProjectTasks { get; init; } = false;

    /// <summary>
    /// Whether to include tasks that have subtasks
    /// </summary>
    public bool IncludeParentTasks { get; init; } = true;

    /// <summary>
    /// Maximum number of tasks to archive in a single operation (for performance)
    /// </summary>
    public int MaxArchiveBatchSize { get; init; } = 1000;

    /// <summary>
    /// Task statuses that are eligible for archiving
    /// </summary>
    public AppTaskStatus[] ArchivableStatuses { get; init; } =
    {
        AppTaskStatus.Completed,
        AppTaskStatus.Archived
    };

    /// <summary>
    /// User ID filter - null means archive for all users
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Priority threshold - archive only tasks below this priority
    /// </summary>
    public Priority? MaxPriorityToArchive { get; init; }

    public static ArchiveCriteria Default => new();

    public static ArchiveCriteria ForUser(Guid userId) => new() { UserId = userId };

    public static ArchiveCriteria Aggressive => new()
    {
        MinimumCompletedAge = TimeSpan.FromDays(30),
        MinimumCanceledAge = TimeSpan.FromDays(7),
        IncludeActiveProjectTasks = true,
        MaxArchiveBatchSize = 2000
    };

    public static ArchiveCriteria Conservative => new()
    {
        MinimumCompletedAge = TimeSpan.FromDays(180),
        MinimumCanceledAge = TimeSpan.FromDays(60),
        IncludeActiveProjectTasks = false,
        IncludeParentTasks = false,
        MaxArchiveBatchSize = 500
    };

    public bool ShouldArchiveTask(AppTask task, DateTime currentTime)
    {
        // Check if task status is archivable
        if (!ArchivableStatuses.Contains((AppTaskStatus)task.Status))
        {
            return false;
        }

        // Check user filter
        if (UserId.HasValue && task.UserId != UserId.Value)
        {
            return false;
        }

        // Check priority threshold
        if (MaxPriorityToArchive != null && task.Priority > (int)MaxPriorityToArchive.Value)
        {
            return false;
        }

        // Check age requirements based on last updated time
        // Since Task entity doesn't have CompletedAt, we use UpdatedAt for completed tasks
        var taskAge = currentTime - task.UpdatedAt;
        var requiredAge = task.Status == (int)AppTaskStatus.Completed
            ? MinimumCompletedAge
            : MinimumCanceledAge;

        if (taskAge < requiredAge)
        {
            return false;
        }

        // Check project constraints
        if (!IncludeActiveProjectTasks && task.ProjectId.HasValue)
        {
            // Would need to check if project is active - this would be resolved in the service layer
            return false;
        }

        // Check parent task constraints
        if (!IncludeParentTasks && task.Subtasks.Any(s => s.Status != (int)AppTaskStatus.Completed))
        {
            return false;
        }

        return true;
    }

    public ArchiveCriteria WithUserId(Guid userId) => this with { UserId = userId };

    public ArchiveCriteria WithBatchSize(int batchSize) => this with { MaxArchiveBatchSize = batchSize };

    /// <summary>
    /// Custom equality implementation to handle array comparison
    /// </summary>
    public virtual bool Equals(ArchiveCriteria? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return MinimumCompletedAge == other.MinimumCompletedAge &&
               MinimumCanceledAge == other.MinimumCanceledAge &&
               IncludeActiveProjectTasks == other.IncludeActiveProjectTasks &&
               IncludeParentTasks == other.IncludeParentTasks &&
               MaxArchiveBatchSize == other.MaxArchiveBatchSize &&
               UserId == other.UserId &&
               Equals(MaxPriorityToArchive, other.MaxPriorityToArchive) &&
               ArchivableStatuses.SequenceEqual(other.ArchivableStatuses);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MinimumCompletedAge);
        hash.Add(MinimumCanceledAge);
        hash.Add(IncludeActiveProjectTasks);
        hash.Add(IncludeParentTasks);
        hash.Add(MaxArchiveBatchSize);
        hash.Add(UserId);
        hash.Add(MaxPriorityToArchive);

        // Hash array contents
        foreach (var status in ArchivableStatuses)
        {
            hash.Add(status);
        }

        return hash.ToHashCode();
    }
}
