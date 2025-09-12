using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.DTOs;

/// <summary>
/// Statistics about a user's task completion and activity
/// </summary>
public class TaskStatistics
{
    /// <summary>
    /// User ID these statistics belong to
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Total number of tasks created by the user
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    /// Number of active tasks (pending or in progress)
    /// </summary>
    public int ActiveTasks { get; set; }

    /// <summary>
    /// Number of completed tasks
    /// </summary>
    public int CompletedTasks { get; set; }

    /// <summary>
    /// Number of archived tasks
    /// </summary>
    public int ArchivedTasks { get; set; }

    /// <summary>
    /// Number of overdue tasks
    /// </summary>
    public int OverdueTasks { get; set; }

    /// <summary>
    /// Number of tasks due today
    /// </summary>
    public int TasksDueToday { get; set; }

    /// <summary>
    /// Number of tasks due this week
    /// </summary>
    public int TasksDueThisWeek { get; set; }

    /// <summary>
    /// Task completion percentage (0-100)
    /// </summary>
    public decimal CompletionPercentage { get; set; }

    /// <summary>
    /// Average time to complete tasks
    /// </summary>
    public TimeSpan AverageCompletionTime { get; set; }

    /// <summary>
    /// Statistics by category
    /// </summary>
    public Dictionary<AppTaskCategory, CategoryStatistics> CategoryStats { get; set; } = new();

    /// <summary>
    /// Statistics by priority
    /// </summary>
    public Dictionary<Priority, PriorityStatistics> PriorityStats { get; set; } = new();

    /// <summary>
    /// Average days to complete tasks
    /// </summary>
    public double? AverageDaysToComplete { get; set; }

    /// <summary>
    /// Current productivity streak (days with task completions)
    /// </summary>
    public int ProductivityStreak { get; set; }

    /// <summary>
    /// Longest productivity streak achieved
    /// </summary>
    public int LongestProductivityStreak { get; set; }

    /// <summary>
    /// When these statistics were generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Period covered by these statistics
    /// </summary>
    public TimeSpan Period { get; set; }
}

/// <summary>
/// Task statistics for a specific category
/// </summary>
public class CategoryStatistics
{
    /// <summary>
    /// The task category
    /// </summary>
    public AppTaskCategory Category { get; set; } = null!;

    /// <summary>
    /// Total tasks in this category
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    /// Completed tasks in this category
    /// </summary>
    public int CompletedTasks { get; set; }

    /// <summary>
    /// Active tasks in this category
    /// </summary>
    public int ActiveTasks { get; set; }

    /// <summary>
    /// Completion percentage for this category
    /// </summary>
    public decimal CompletionPercentage { get; set; }

    /// <summary>
    /// Average days to complete tasks in this category
    /// </summary>
    public double? AverageDaysToComplete { get; set; }
}

/// <summary>
/// Task statistics for a specific priority level
/// </summary>
public class PriorityStatistics
{
    /// <summary>
    /// The priority level
    /// </summary>
    public Priority Priority { get; set; } = null!;

    /// <summary>
    /// Total tasks at this priority
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    /// Completed tasks at this priority
    /// </summary>
    public int CompletedTasks { get; set; }

    /// <summary>
    /// Active tasks at this priority
    /// </summary>
    public int ActiveTasks { get; set; }

    /// <summary>
    /// Overdue tasks at this priority
    /// </summary>
    public int OverdueTasks { get; set; }

    /// <summary>
    /// Average days to complete tasks at this priority
    /// </summary>
    public double? AverageDaysToComplete { get; set; }
}

/// <summary>
/// Task completion trends over time
/// </summary>
public class TaskCompletionTrends
{
    /// <summary>
    /// User ID these trends belong to
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Daily completion data
    /// </summary>
    public List<DailyCompletionData> DailyData { get; set; } = new();

    /// <summary>
    /// Weekly completion data
    /// </summary>
    public List<WeeklyCompletionData> WeeklyData { get; set; } = new();

    /// <summary>
    /// Monthly completion data
    /// </summary>
    public List<MonthlyCompletionData> MonthlyData { get; set; } = new();

    /// <summary>
    /// Period covered by these trends
    /// </summary>
    public TimeSpan Period { get; set; }

    /// <summary>
    /// When these trends were generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Daily task completion data
/// </summary>
public class DailyCompletionData
{
    /// <summary>
    /// The date
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Number of tasks completed
    /// </summary>
    public int CompletedTasks { get; set; }

    /// <summary>
    /// Number of tasks created
    /// </summary>
    public int CreatedTasks { get; set; }

    /// <summary>
    /// Number of active tasks at end of day
    /// </summary>
    public int ActiveTasks { get; set; }
}

/// <summary>
/// Weekly task completion data
/// </summary>
public class WeeklyCompletionData
{
    /// <summary>
    /// Week start date (Monday)
    /// </summary>
    public DateTime WeekStart { get; set; }

    /// <summary>
    /// Number of tasks completed in the week
    /// </summary>
    public int CompletedTasks { get; set; }

    /// <summary>
    /// Number of tasks created in the week
    /// </summary>
    public int CreatedTasks { get; set; }

    /// <summary>
    /// Average active tasks during the week
    /// </summary>
    public double AverageActiveTasks { get; set; }
}

/// <summary>
/// Monthly task completion data
/// </summary>
public class MonthlyCompletionData
{
    /// <summary>
    /// Year and month
    /// </summary>
    public DateTime Month { get; set; }

    /// <summary>
    /// Number of tasks completed in the month
    /// </summary>
    public int CompletedTasks { get; set; }

    /// <summary>
    /// Number of tasks created in the month
    /// </summary>
    public int CreatedTasks { get; set; }

    /// <summary>
    /// Average active tasks during the month
    /// </summary>
    public double AverageActiveTasks { get; set; }

    /// <summary>
    /// Productivity score for the month (0-100)
    /// </summary>
    public decimal ProductivityScore { get; set; }
}
