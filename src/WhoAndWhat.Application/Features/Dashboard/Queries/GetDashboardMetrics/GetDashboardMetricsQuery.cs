using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GetDashboardMetrics;

/// <summary>
/// Query to get comprehensive dashboard metrics for the user
/// </summary>
public sealed record GetDashboardMetricsQuery(
    Guid UserId) : IRequest<Result<GetDashboardMetricsResponse>>;

/// <summary>
/// Response containing comprehensive dashboard metrics
/// </summary>
public sealed record GetDashboardMetricsResponse(
    int CompletedTasksToday,
    int CompletedTasksThisWeek,
    int CompletedTasksThisMonth,
    int TotalActiveTasks,
    int OverdueTasks,
    int TasksCompletedOnTime,
    int TasksCompletedLate,
    double CompletionRate,
    double OnTimeCompletionRate,
    TaskCategoryStats CategoryBreakdown,
    TaskPriorityStats PriorityBreakdown,
    ProductivityTrends Trends
);

/// <summary>
/// Task statistics broken down by category
/// </summary>
public sealed record TaskCategoryStats(
    int TodoTasks,
    int IdeaTasks,
    int AppointmentTasks,
    int BillReminderTasks,
    int ProjectTasks
);

/// <summary>
/// Task statistics broken down by priority
/// </summary>
public sealed record TaskPriorityStats(
    int CriticalTasks,
    int HighTasks,
    int MediumTasks,
    int LowTasks,
    int NoneTasks
);

/// <summary>
/// Productivity trends and analytics
/// </summary>
public sealed record ProductivityTrends(
    double DailyAverageCompletions,
    double WeeklyAverageCompletions,
    int CurrentStreak,
    int LongestStreak,
    List<DailyProductivityPoint> Last7Days,
    List<WeeklyProductivityPoint> Last4Weeks
);

/// <summary>
/// Daily productivity data point
/// </summary>
public sealed record DailyProductivityPoint(
    DateTime Date,
    int CompletedTasks,
    int CreatedTasks,
    double CompletionRate
);

/// <summary>
/// Weekly productivity data point
/// </summary>
public sealed record WeeklyProductivityPoint(
    DateTime WeekStarting,
    int CompletedTasks,
    int CreatedTasks,
    double CompletionRate,
    double AverageTasksPerDay
);
