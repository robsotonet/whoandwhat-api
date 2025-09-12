using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GetProductivityStreak;

/// <summary>
/// Query to get user's productivity streak information
/// </summary>
public sealed record GetProductivityStreakQuery(
    Guid UserId) : IRequest<Result<GetProductivityStreakResponse>>;

/// <summary>
/// Response containing productivity streak details
/// </summary>
public sealed record GetProductivityStreakResponse(
    int CurrentStreak,
    int LongestStreak,
    int BestMonthlyStreak,
    DateTime? LastCompletionDate,
    DateTime? StreakStartDate,
    List<StreakMilestone> Milestones,
    StreakStats WeeklyStats,
    StreakStats MonthlyStats,
    List<DailyStreakPoint> Last30Days
);

/// <summary>
/// Streak milestone achievement
/// </summary>
public sealed record StreakMilestone(
    int Days,
    string Title,
    string Description,
    bool IsAchieved,
    DateTime? AchievedDate
);

/// <summary>
/// Streak statistics for a time period
/// </summary>
public sealed record StreakStats(
    int TotalDays,
    int ActiveDays,
    int CompletedTasks,
    double ConsistencyRate,
    double AverageTasksPerDay
);

/// <summary>
/// Daily streak data point
/// </summary>
public sealed record DailyStreakPoint(
    DateTime Date,
    bool HasActivity,
    int CompletedTasks,
    bool IsPartOfCurrentStreak
);