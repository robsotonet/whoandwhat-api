using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GetCompletionStats;

/// <summary>
/// Query to get comprehensive task completion statistics
/// </summary>
public sealed record GetCompletionStatsQuery(
    Guid UserId,
    string Period = "month") : IRequest<Result<GetCompletionStatsResponse>>;

/// <summary>
/// Response containing comprehensive completion statistics
/// </summary>
public sealed record GetCompletionStatsResponse(
    CompletionOverview Overview,
    CompletionTrends Trends,
    CompletionBreakdown Breakdown,
    CompletionComparison Comparison,
    List<CompletionInsight> Insights
);

/// <summary>
/// Overview of completion statistics
/// </summary>
public sealed record CompletionOverview(
    int TotalTasksCreated,
    int TotalTasksCompleted,
    int TasksInProgress,
    int TasksPending,
    double CompletionRate,
    double OnTimeCompletionRate,
    TimeSpan AverageCompletionTime,
    int TasksCompletedAheadOfSchedule,
    int TasksCompletedLate
);

/// <summary>
/// Completion trends over time
/// </summary>
public sealed record CompletionTrends(
    List<DailyCompletionPoint> DailyData,
    List<WeeklyCompletionPoint> WeeklyData,
    List<MonthlyCompletionPoint> MonthlyData,
    CompletionVelocity Velocity
);

/// <summary>
/// Completion breakdown by various dimensions
/// </summary>
public sealed record CompletionBreakdown(
    Dictionary<string, CompletionCategoryStats> ByCategory,
    Dictionary<string, CompletionPriorityStats> ByPriority,
    Dictionary<int, int> ByHourOfDay,
    Dictionary<string, int> ByDayOfWeek,
    Dictionary<string, CompletionTimeRangeStats> ByTimeRange
);

/// <summary>
/// Completion rate comparison with previous periods
/// </summary>
public sealed record CompletionComparison(
    double CurrentPeriodRate,
    double PreviousPeriodRate,
    double ChangePercentage,
    string Trend,
    int BestDay,
    int WorstDay,
    string BestCategory,
    string WorstCategory
);

/// <summary>
/// Daily completion data point
/// </summary>
public sealed record DailyCompletionPoint(
    DateTime Date,
    int TasksCompleted,
    int TasksCreated,
    double CompletionRate,
    TimeSpan AverageTimeToComplete
);

/// <summary>
/// Weekly completion data point
/// </summary>
public sealed record WeeklyCompletionPoint(
    DateTime WeekStarting,
    int TasksCompleted,
    int TasksCreated,
    double CompletionRate,
    double AverageTasksPerDay
);

/// <summary>
/// Monthly completion data point
/// </summary>
public sealed record MonthlyCompletionPoint(
    DateTime Month,
    int TasksCompleted,
    int TasksCreated,
    double CompletionRate,
    int WorkingDays
);

/// <summary>
/// Completion velocity metrics
/// </summary>
public sealed record CompletionVelocity(
    double TasksPerDay,
    double TasksPerWeek,
    double TasksPerMonth,
    string VelocityTrend,
    double PredictedMonthlyCompletion
);

/// <summary>
/// Category-specific completion statistics
/// </summary>
public sealed record CompletionCategoryStats(
    int TotalTasks,
    int CompletedTasks,
    double CompletionRate,
    TimeSpan AverageTimeToComplete,
    int OverdueTasks
);

/// <summary>
/// Priority-specific completion statistics
/// </summary>
public sealed record CompletionPriorityStats(
    int TotalTasks,
    int CompletedTasks,
    double CompletionRate,
    double OnTimeRate,
    TimeSpan AverageTimeToComplete
);

/// <summary>
/// Time range completion statistics
/// </summary>
public sealed record CompletionTimeRangeStats(
    int TasksCompleted,
    double Percentage,
    string TimeRange
);

/// <summary>
/// Completion insight with recommendations
/// </summary>
public sealed record CompletionInsight(
    string Type,
    string Title,
    string Description,
    string Recommendation,
    string Severity,
    Dictionary<string, object> Data
);
