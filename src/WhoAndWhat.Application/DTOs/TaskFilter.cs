using WhoAndWhat.Domain.ValueObjects;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Application.DTOs;

/// <summary>
/// Advanced filtering criteria for Task queries
/// </summary>
public class TaskFilter
{
    /// <summary>
    /// Filter by specific task status
    /// </summary>
    public DomainTaskStatus? Status { get; set; }

    /// <summary>
    /// Filter by multiple task statuses
    /// </summary>
    public IEnumerable<DomainTaskStatus>? Statuses { get; set; }

    /// <summary>
    /// Exclude completed tasks from results
    /// </summary>
    public bool ExcludeCompleted { get; set; } = false;

    /// <summary>
    /// Exclude archived tasks from results
    /// </summary>
    public bool ExcludeArchived { get; set; } = true;

    /// <summary>
    /// Include soft-deleted tasks in results
    /// </summary>
    public bool IncludeDeleted { get; set; } = false;

    /// <summary>
    /// Filter by specific task category
    /// </summary>
    public AppTaskCategory? Category { get; set; }

    /// <summary>
    /// Filter by multiple task categories
    /// </summary>
    public IEnumerable<AppTaskCategory>? Categories { get; set; }

    /// <summary>
    /// Filter by specific priority
    /// </summary>
    public Priority? Priority { get; set; }

    /// <summary>
    /// Filter by minimum priority level
    /// </summary>
    public Priority? MinPriority { get; set; }

    /// <summary>
    /// Filter by maximum priority level
    /// </summary>
    public Priority? MaxPriority { get; set; }

    /// <summary>
    /// Filter tasks due from this date
    /// </summary>
    public DateTime? DueDateFrom { get; set; }

    /// <summary>
    /// Filter tasks due until this date
    /// </summary>
    public DateTime? DueDateTo { get; set; }

    /// <summary>
    /// Filter tasks created after this date
    /// </summary>
    public DateTime? CreatedAfter { get; set; }

    /// <summary>
    /// Filter tasks created before this date
    /// </summary>
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Filter tasks updated after this date
    /// </summary>
    public DateTime? UpdatedAfter { get; set; }

    /// <summary>
    /// Filter tasks updated before this date
    /// </summary>
    public DateTime? UpdatedBefore { get; set; }

    /// <summary>
    /// Include only overdue tasks
    /// </summary>
    public bool OverdueOnly { get; set; } = false;

    /// <summary>
    /// Include only tasks due today
    /// </summary>
    public bool DueTodayOnly { get; set; } = false;

    /// <summary>
    /// Include only parent tasks (tasks that are not subtasks)
    /// </summary>
    public bool ParentTasksOnly { get; set; } = false;

    /// <summary>
    /// Include only subtasks (tasks that belong to a project)
    /// </summary>
    public bool SubtasksOnly { get; set; } = false;

    /// <summary>
    /// Filter tasks by specific project
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Include only standalone tasks (not part of any project)
    /// </summary>
    public bool StandaloneTasksOnly { get; set; } = false;

    /// <summary>
    /// Filter tasks by title containing this text
    /// </summary>
    public string? TitleContains { get; set; }

    /// <summary>
    /// Filter tasks by description containing this text
    /// </summary>
    public string? DescriptionContains { get; set; }

    /// <summary>
    /// Page number for pagination (1-based)
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// How to sort the results
    /// </summary>
    public TaskSortBy SortBy { get; set; } = TaskSortBy.UpdatedAt;

    /// <summary>
    /// Sort in descending order
    /// </summary>
    public bool SortDescending { get; set; } = true;

    /// <summary>
    /// Include subtasks in the results
    /// </summary>
    public bool IncludeSubtasks { get; set; } = false;

    /// <summary>
    /// Include related contacts in the results
    /// </summary>
    public bool IncludeContacts { get; set; } = false;

    /// <summary>
    /// Include project information in the results
    /// </summary>
    public bool IncludeProject { get; set; } = false;

    /// <summary>
    /// Gets the skip count for pagination
    /// </summary>
    public int Skip => (PageNumber - 1) * PageSize;

    /// <summary>
    /// Validates the filter parameters
    /// </summary>
    /// <returns>True if the filter is valid</returns>
    public bool IsValid()
    {
        // Page validation
        if (PageNumber < 1 || PageSize < 1 || PageSize > 1000)
        {
            return false;
        }

        // Date range validation
        if (DueDateFrom.HasValue && DueDateTo.HasValue && DueDateFrom > DueDateTo)
        {
            return false;
        }

        if (CreatedAfter.HasValue && CreatedBefore.HasValue && CreatedAfter > CreatedBefore)
        {
            return false;
        }

        if (UpdatedAfter.HasValue && UpdatedBefore.HasValue && UpdatedAfter > UpdatedBefore)
        {
            return false;
        }

        // Priority validation
        if (MinPriority != null && MaxPriority != null && MinPriority > MaxPriority)
        {
            return false;
        }

        // Mutual exclusion validation
        if (ParentTasksOnly && SubtasksOnly)
        {
            return false;
        }

        if (StandaloneTasksOnly && ProjectId.HasValue)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a filter for overdue tasks only
    /// </summary>
    public static TaskFilter ForOverdueTasks()
    {
        return new TaskFilter
        {
            OverdueOnly = true,
            ExcludeCompleted = true,
            ExcludeArchived = true,
            SortBy = TaskSortBy.DueDate,
            SortDescending = false
        };
    }

    /// <summary>
    /// Creates a filter for tasks due today
    /// </summary>
    public static TaskFilter ForTasksDueToday()
    {
        return new TaskFilter
        {
            DueTodayOnly = true,
            ExcludeCompleted = true,
            ExcludeArchived = true,
            SortBy = TaskSortBy.Priority,
            SortDescending = true
        };
    }

    /// <summary>
    /// Creates a filter for active tasks (not completed or archived)
    /// </summary>
    public static TaskFilter ForActiveTasks()
    {
        return new TaskFilter
        {
            ExcludeCompleted = true,
            ExcludeArchived = true,
            SortBy = TaskSortBy.Priority,
            SortDescending = true
        };
    }

    /// <summary>
    /// Creates a filter for completed tasks
    /// </summary>
    public static TaskFilter ForCompletedTasks()
    {
        return new TaskFilter
        {
            Status = DomainTaskStatus.Completed,
            ExcludeArchived = true,
            SortBy = TaskSortBy.UpdatedAt,
            SortDescending = true
        };
    }
}

/// <summary>
/// Sorting options for task queries
/// </summary>
public enum TaskSortBy
{
    /// <summary>
    /// Sort by creation date
    /// </summary>
    CreatedAt,

    /// <summary>
    /// Sort by last update date
    /// </summary>
    UpdatedAt,

    /// <summary>
    /// Sort by due date
    /// </summary>
    DueDate,

    /// <summary>
    /// Sort by priority
    /// </summary>
    Priority,

    /// <summary>
    /// Sort by title alphabetically
    /// </summary>
    Title,

    /// <summary>
    /// Sort by task status
    /// </summary>
    Status,

    /// <summary>
    /// Sort by task category
    /// </summary>
    Category
}
