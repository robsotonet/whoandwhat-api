using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GetCompletionStats;

/// <summary>
/// Handler for retrieving comprehensive task completion statistics
/// </summary>
public sealed class GetCompletionStatsQueryHandler
    : IRequestHandler<GetCompletionStatsQuery, Result<GetCompletionStatsResponse>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<GetCompletionStatsQueryHandler> _logger;

    public GetCompletionStatsQueryHandler(
        IAppTaskRepository taskRepository,
        ILogger<GetCompletionStatsQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result<GetCompletionStatsResponse>> Handle(
        GetCompletionStatsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting completion stats for user {UserId}, period: {Period}",
                request.UserId, request.Period);

            // Get date range based on period
            var (startDate, endDate) = GetDateRange(request.Period);

            // Get all tasks for the user
            var filter = new TaskFilter
            {
                CreatedAfter = startDate,
                CreatedBefore = endDate,
                PageSize = 10000
            };
            var (allTasksResult, _) = await _taskRepository.GetTasksByUserIdAsync(request.UserId, filter, cancellationToken);
            var periodTasks = allTasksResult.ToList();

            var completedTasks = periodTasks
                .Where(t => t.Status == (int)AppTaskStatus.Completed)
                .ToList();

            // Calculate overview statistics
            var overview = CalculateOverview(periodTasks, completedTasks);

            // Calculate trends
            var trends = CalculateTrends(periodTasks, startDate, endDate);

            // Calculate breakdown statistics
            var breakdown = CalculateBreakdown(periodTasks, completedTasks);

            // Calculate comparison with previous period
            var comparison = CalculateComparison(allTasksResult.ToList(), startDate, endDate, request.Period);

            // Generate insights and recommendations
            var insights = GenerateInsights(periodTasks, completedTasks, overview, breakdown);

            var response = new GetCompletionStatsResponse(
                Overview: overview,
                Trends: trends,
                Breakdown: breakdown,
                Comparison: comparison,
                Insights: insights
            );

            _logger.LogInformation("Successfully retrieved completion stats for user {UserId}", request.UserId);
            return Result<GetCompletionStatsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completion stats for user {UserId}", request.UserId);
            return Result<GetCompletionStatsResponse>.Failure($"Failed to retrieve completion stats: {ex.Message}");
        }
    }

    private (DateTime startDate, DateTime endDate) GetDateRange(string period)
    {
        var now = DateTime.UtcNow;

        return period.ToLowerInvariant() switch
        {
            "day" => (now.Date, now.Date.AddDays(1).AddTicks(-1)),
            "week" => (now.Date.AddDays(-(int)now.DayOfWeek), now.Date.AddDays(7 - (int)now.DayOfWeek).AddTicks(-1)),
            "month" => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1)),
            "quarter" => GetQuarterRange(now),
            "year" => (new DateTime(now.Year, 1, 1), new DateTime(now.Year, 12, 31, 23, 59, 59)),
            _ => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1))
        };
    }

    private (DateTime startDate, DateTime endDate) GetQuarterRange(DateTime now)
    {
        var quarter = (now.Month - 1) / 3 + 1;
        var startMonth = (quarter - 1) * 3 + 1;
        var startDate = new DateTime(now.Year, startMonth, 1);
        var endDate = startDate.AddMonths(3).AddTicks(-1);
        return (startDate, endDate);
    }

    private CompletionOverview CalculateOverview(
        List<Domain.Entities.AppTask> periodTasks,
        List<Domain.Entities.AppTask> completedTasks)
    {
        var totalCreated = periodTasks.Count;
        var totalCompleted = completedTasks.Count;
        var inProgress = periodTasks.Count(t => t.Status == (int)AppTaskStatus.InProgress);
        var pending = periodTasks.Count(t => t.Status == (int)AppTaskStatus.Pending);

        var completionRate = totalCreated > 0 ? (double)totalCompleted / totalCreated * 100 : 0.0;

        // On-time completion rate
        var tasksWithDueDates = completedTasks.Where(t => t.DueDate.HasValue).ToList();
        var onTimeTasks = tasksWithDueDates.Count(t => t.UpdatedAt <= t.DueDate);
        var onTimeRate = tasksWithDueDates.Count > 0 ? (double)onTimeTasks / tasksWithDueDates.Count * 100 : 0.0;

        // Average completion time
        var completionTimes = completedTasks
            .Select(t => t.UpdatedAt - t.CreatedAt)
            .ToList();

        var averageCompletionTime = completionTimes.Any()
            ? TimeSpan.FromTicks((long)completionTimes.Average(ts => ts.Ticks))
            : TimeSpan.Zero;

        // Ahead of schedule vs late
        var aheadOfSchedule = tasksWithDueDates.Count(t => t.UpdatedAt < t.DueDate);
        var late = tasksWithDueDates.Count(t => t.UpdatedAt > t.DueDate);

        return new CompletionOverview(
            TotalTasksCreated: totalCreated,
            TotalTasksCompleted: totalCompleted,
            TasksInProgress: inProgress,
            TasksPending: pending,
            CompletionRate: Math.Round(completionRate, 1),
            OnTimeCompletionRate: Math.Round(onTimeRate, 1),
            AverageCompletionTime: averageCompletionTime,
            TasksCompletedAheadOfSchedule: aheadOfSchedule,
            TasksCompletedLate: late
        );
    }

    private CompletionTrends CalculateTrends(
        List<Domain.Entities.AppTask> allTasks,
        DateTime startDate,
        DateTime endDate)
    {
        // Generate daily data
        var dailyData = new List<DailyCompletionPoint>();
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var dayTasks = allTasks.Where(t => t.CreatedAt.Date == date).ToList();
            var dayCompleted = allTasks.Count(t => t.UpdatedAt.Date == date);
            var completionRate = dayTasks.Count > 0 ? (double)dayCompleted / dayTasks.Count * 100 : 0.0;

            var dayCompletionTimes = allTasks
                .Where(t => t.UpdatedAt.Date == date)
                .Select(t => t.UpdatedAt - t.CreatedAt)
                .ToList();

            var avgTime = dayCompletionTimes.Any()
                ? TimeSpan.FromTicks((long)dayCompletionTimes.Average(ts => ts.Ticks))
                : TimeSpan.Zero;

            dailyData.Add(new DailyCompletionPoint(
                Date: date,
                TasksCompleted: dayCompleted,
                TasksCreated: dayTasks.Count,
                CompletionRate: Math.Round(completionRate, 1),
                AverageTimeToComplete: avgTime
            ));
        }

        // Generate weekly data (simplified - last 4 weeks)
        var weeklyData = new List<WeeklyCompletionPoint>();
        for (int i = 3; i >= 0; i--)
        {
            var weekStart = DateTime.UtcNow.Date.AddDays(-7 * (i + 1) + -(int)DateTime.UtcNow.DayOfWeek);
            var weekEnd = weekStart.AddDays(7);

            var weekTasks = allTasks.Where(t => t.CreatedAt >= weekStart && t.CreatedAt < weekEnd).ToList();
            var weekCompleted = allTasks.Count(t => t.UpdatedAt >= weekStart && t.UpdatedAt < weekEnd);
            var weekRate = weekTasks.Count > 0 ? (double)weekCompleted / weekTasks.Count * 100 : 0.0;

            weeklyData.Add(new WeeklyCompletionPoint(
                WeekStarting: weekStart,
                TasksCompleted: weekCompleted,
                TasksCreated: weekTasks.Count,
                CompletionRate: Math.Round(weekRate, 1),
                AverageTasksPerDay: Math.Round((double)weekCompleted / 7, 1)
            ));
        }

        // Generate monthly data (simplified - last 6 months)
        var monthlyData = new List<MonthlyCompletionPoint>();
        for (int i = 5; i >= 0; i--)
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);

            var monthTasks = allTasks.Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd).ToList();
            var monthCompleted = allTasks.Count(t => t.UpdatedAt >= monthStart && t.UpdatedAt < monthEnd);
            var monthRate = monthTasks.Count > 0 ? (double)monthCompleted / monthTasks.Count * 100 : 0.0;

            monthlyData.Add(new MonthlyCompletionPoint(
                Month: monthStart,
                TasksCompleted: monthCompleted,
                TasksCreated: monthTasks.Count,
                CompletionRate: Math.Round(monthRate, 1),
                WorkingDays: DateTime.DaysInMonth(monthStart.Year, monthStart.Month)
            ));
        }

        // Calculate velocity
        var recentCompleted = allTasks.Count(t => t.UpdatedAt >= DateTime.UtcNow.AddDays(-30));
        var velocity = new CompletionVelocity(
            TasksPerDay: Math.Round((double)recentCompleted / 30, 1),
            TasksPerWeek: Math.Round((double)recentCompleted / 30 * 7, 1),
            TasksPerMonth: recentCompleted,
            VelocityTrend: monthlyData.Count > 1 && monthlyData.Last().TasksCompleted > monthlyData.First().TasksCompleted ? "Increasing" : "Stable",
            PredictedMonthlyCompletion: Math.Round((double)recentCompleted / 30 * DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month), 0)
        );

        return new CompletionTrends(
            DailyData: dailyData,
            WeeklyData: weeklyData,
            MonthlyData: monthlyData,
            Velocity: velocity
        );
    }

    private CompletionBreakdown CalculateBreakdown(
        List<Domain.Entities.AppTask> periodTasks,
        List<Domain.Entities.AppTask> completedTasks)
    {
        // By category
        var byCategory = new Dictionary<string, CompletionCategoryStats>();
        foreach (AppTaskCategory category in AppTaskCategory.GetAll())
        {
            var categoryTasks = periodTasks.Where(t => t.Category == (int)category).ToList();
            var categoryCompleted = completedTasks.Where(t => t.Category == (int)category).ToList();
            var categoryOverdue = categoryTasks.Count(t => t.DueDate.HasValue &&
                                                         t.DueDate.Value.Date < DateTime.UtcNow.Date &&
                                                         t.Status != (int)AppTaskStatus.Completed);

            if (categoryTasks.Any())
            {
                var completionTimes = categoryCompleted
                                        .Select(t => t.UpdatedAt - t.CreatedAt)
                    .ToList();

                var avgTime = completionTimes.Any()
                    ? TimeSpan.FromTicks((long)completionTimes.Average(ts => ts.Ticks))
                    : TimeSpan.Zero;

                byCategory[category.ToString()] = new CompletionCategoryStats(
                    TotalTasks: categoryTasks.Count,
                    CompletedTasks: categoryCompleted.Count,
                    CompletionRate: Math.Round((double)categoryCompleted.Count / categoryTasks.Count * 100, 1),
                    AverageTimeToComplete: avgTime,
                    OverdueTasks: categoryOverdue
                );
            }
        }

        // By priority
        var byPriority = new Dictionary<string, CompletionPriorityStats>();
        foreach (Priority priority in Priority.GetAll())
        {
            var priorityTasks = periodTasks.Where(t => t.Priority == (int)priority).ToList();
            var priorityCompleted = completedTasks.Where(t => t.Priority == (int)priority).ToList();
            var priorityOnTime = priorityCompleted.Count(t =>
                t.DueDate.HasValue && t.UpdatedAt <= t.DueDate);

            if (priorityTasks.Any())
            {
                var completionTimes = priorityCompleted
                                        .Select(t => t.UpdatedAt - t.CreatedAt)
                    .ToList();

                var avgTime = completionTimes.Any()
                    ? TimeSpan.FromTicks((long)completionTimes.Average(ts => ts.Ticks))
                    : TimeSpan.Zero;

                var onTimeRate = priorityCompleted.Count(t => t.DueDate.HasValue) > 0 ?
                    (double)priorityOnTime / priorityCompleted.Count(t => t.DueDate.HasValue) * 100 : 0.0;

                byPriority[priority.ToString()] = new CompletionPriorityStats(
                    TotalTasks: priorityTasks.Count,
                    CompletedTasks: priorityCompleted.Count,
                    CompletionRate: Math.Round((double)priorityCompleted.Count / priorityTasks.Count * 100, 1),
                    OnTimeRate: Math.Round(onTimeRate, 1),
                    AverageTimeToComplete: avgTime
                );
            }
        }

        // By hour of day
        var byHourOfDay = completedTasks
                        .GroupBy(t => t.UpdatedAt.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        // By day of week
        var byDayOfWeek = completedTasks
                        .GroupBy(t => t.UpdatedAt.DayOfWeek.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // By time range
        var byTimeRange = new Dictionary<string, CompletionTimeRangeStats>
        {
            ["Same Day"] = new(completedTasks.Count(t => (t.UpdatedAt - t.CreatedAt).Days == 0), 0, "Same Day"),
            ["1-3 Days"] = new(completedTasks.Count(t => (t.UpdatedAt - t.CreatedAt).Days is > 0 and <= 3), 0, "1-3 Days"),
            ["4-7 Days"] = new(completedTasks.Count(t => (t.UpdatedAt - t.CreatedAt).Days is > 3 and <= 7), 0, "4-7 Days"),
            ["1-2 Weeks"] = new(completedTasks.Count(t => (t.UpdatedAt - t.CreatedAt).Days is > 7 and <= 14), 0, "1-2 Weeks"),
            ["2+ Weeks"] = new(completedTasks.Count(t => (t.UpdatedAt - t.CreatedAt).Days > 14), 0, "2+ Weeks")
        };

        // Calculate percentages for time ranges
        var totalWithTimeData = byTimeRange.Values.Sum(s => s.TasksCompleted);
        if (totalWithTimeData > 0)
        {
            byTimeRange = byTimeRange.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value with { Percentage = Math.Round((double)kvp.Value.TasksCompleted / totalWithTimeData * 100, 1) }
            );
        }

        return new CompletionBreakdown(
            ByCategory: byCategory,
            ByPriority: byPriority,
            ByHourOfDay: byHourOfDay,
            ByDayOfWeek: byDayOfWeek,
            ByTimeRange: byTimeRange
        );
    }

    private CompletionComparison CalculateComparison(
        List<Domain.Entities.AppTask> allTasks,
        DateTime startDate,
        DateTime endDate,
        string period)
    {
        // Calculate previous period
        var periodLength = endDate - startDate;
        var previousStartDate = startDate - periodLength;
        var previousEndDate = startDate.AddTicks(-1);

        var currentPeriodTasks = allTasks.Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate).ToList();
        var previousPeriodTasks = allTasks.Where(t => t.CreatedAt >= previousStartDate && t.CreatedAt <= previousEndDate).ToList();

        var currentCompleted = currentPeriodTasks.Count(t => t.Status == (int)AppTaskStatus.Completed);
        var previousCompleted = previousPeriodTasks.Count(t => t.Status == (int)AppTaskStatus.Completed);

        var currentRate = currentPeriodTasks.Count > 0 ? (double)currentCompleted / currentPeriodTasks.Count * 100 : 0.0;
        var previousRate = previousPeriodTasks.Count > 0 ? (double)previousCompleted / previousPeriodTasks.Count * 100 : 0.0;

        var changePercentage = previousRate > 0 ? (currentRate - previousRate) / previousRate * 100 : 0.0;
        var trend = changePercentage > 5 ? "Improving" : changePercentage < -5 ? "Declining" : "Stable";

        // Find best and worst performing days/categories
        var dailyCompletions = allTasks
            .Where(t => t.UpdatedAt >= startDate && t.UpdatedAt <= endDate)
            .GroupBy(t => t.UpdatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var bestDay = dailyCompletions.Any() ? dailyCompletions.Values.Max() : 0;
        var worstDay = dailyCompletions.Any() ? dailyCompletions.Values.Min() : 0;

        var categoryCompletions = currentPeriodTasks
            .Where(t => t.Status == (int)AppTaskStatus.Completed)
            .GroupBy(t => ((AppTaskCategory)t.Category).ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var bestCategory = categoryCompletions.Any() ?
            categoryCompletions.OrderByDescending(kvp => kvp.Value).First().Key : "None";
        var worstCategory = categoryCompletions.Any() ?
            categoryCompletions.OrderBy(kvp => kvp.Value).First().Key : "None";

        return new CompletionComparison(
            CurrentPeriodRate: Math.Round(currentRate, 1),
            PreviousPeriodRate: Math.Round(previousRate, 1),
            ChangePercentage: Math.Round(changePercentage, 1),
            Trend: trend,
            BestDay: bestDay,
            WorstDay: worstDay,
            BestCategory: bestCategory,
            WorstCategory: worstCategory
        );
    }

    private List<CompletionInsight> GenerateInsights(
        List<Domain.Entities.AppTask> periodTasks,
        List<Domain.Entities.AppTask> completedTasks,
        CompletionOverview overview,
        CompletionBreakdown breakdown)
    {
        var insights = new List<CompletionInsight>();

        // Completion rate insight
        if (overview.CompletionRate < 50)
        {
            insights.Add(new CompletionInsight(
                Type: "Performance",
                Title: "Low Completion Rate",
                Description: $"Your completion rate is {overview.CompletionRate}%, which is below the recommended 70%+",
                Recommendation: "Consider breaking large tasks into smaller, more manageable subtasks",
                Severity: "Medium",
                Data: new Dictionary<string, object> { ["rate"] = overview.CompletionRate }
            ));
        }

        // On-time completion insight
        if (overview.OnTimeCompletionRate < 70)
        {
            insights.Add(new CompletionInsight(
                Type: "Timing",
                Title: "Tasks Often Completed Late",
                Description: $"Only {overview.OnTimeCompletionRate}% of tasks are completed on time",
                Recommendation: "Review your time estimates and consider setting more realistic deadlines",
                Severity: "Medium",
                Data: new Dictionary<string, object> { ["onTimeRate"] = overview.OnTimeCompletionRate }
            ));
        }

        // Category performance insight
        var weakestCategory = breakdown.ByCategory
            .OrderBy(kvp => kvp.Value.CompletionRate)
            .FirstOrDefault();

        if (weakestCategory.Value != null && weakestCategory.Value.CompletionRate < 60)
        {
            insights.Add(new CompletionInsight(
                Type: "Category",
                Title: $"Low Performance in {weakestCategory.Key}",
                Description: $"{weakestCategory.Key} tasks have a completion rate of only {weakestCategory.Value.CompletionRate}%",
                Recommendation: $"Focus on improving your {weakestCategory.Key} task management strategy",
                Severity: "Low",
                Data: new Dictionary<string, object>
                {
                    ["category"] = weakestCategory.Key,
                    ["rate"] = weakestCategory.Value.CompletionRate
                }
            ));
        }

        // Productivity pattern insight
        var mostProductiveHour = breakdown.ByHourOfDay.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
        if (mostProductiveHour.Value > 0)
        {
            insights.Add(new CompletionInsight(
                Type: "Pattern",
                Title: "Peak Productivity Time",
                Description: $"You're most productive at {mostProductiveHour.Key}:00 with {mostProductiveHour.Value} tasks completed",
                Recommendation: $"Schedule your most important tasks around {mostProductiveHour.Key}:00",
                Severity: "Info",
                Data: new Dictionary<string, object>
                {
                    ["hour"] = mostProductiveHour.Key,
                    ["count"] = mostProductiveHour.Value
                }
            ));
        }

        return insights;
    }
}
