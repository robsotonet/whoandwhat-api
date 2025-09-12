using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Dashboard;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GetOverdueTasks;

/// <summary>
/// Query to get user's overdue tasks with analytics
/// </summary>
public sealed record GetOverdueTasksQuery(
    Guid UserId,
    int? Limit = null,
    string? CategoryFilter = null,
    string? PriorityFilter = null) : IRequest<Result<GetOverdueTasksResponse>>;

/// <summary>
/// Response containing overdue tasks and analytics
/// </summary>
public sealed record GetOverdueTasksResponse(
    List<OverdueTaskDto> Tasks,
    OverdueTasksSummary Summary,
    OverdueTasksAnalytics Analytics
);


/// <summary>
/// Summary of overdue tasks
/// </summary>
public sealed record OverdueTasksSummary(
    int TotalOverdue,
    int CriticalPriorityCount,
    int HighPriorityCount,
    int MediumPriorityCount,
    int LowPriorityCount,
    double AverageDaysOverdue,
    int MostOverdueDays,
    string MostOverdueCategory,
    DateTime? OldestOverdueDate
);

/// <summary>
/// Analytics for overdue tasks patterns
/// </summary>
public sealed record OverdueTasksAnalytics(
    Dictionary<string, int> CategoryBreakdown,
    Dictionary<string, int> PriorityBreakdown,
    List<OverdueTrendPoint> TrendData,
    List<string> RecommendedActions,
    double OverdueRate,
    int TasksOverdueThisWeek,
    int TasksOverdueThisMonth
);

/// <summary>
/// Data point for overdue tasks trend analysis
/// </summary>
public sealed record OverdueTrendPoint(
    DateTime Date,
    int NewOverdueTasks,
    int ResolvedOverdueTasks,
    int TotalOverdue
);