using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GetProductivityStreak;

/// <summary>
/// Handler for retrieving user's productivity streak information
/// </summary>
public sealed class GetProductivityStreakQueryHandler
    : IRequestHandler<GetProductivityStreakQuery, Result<GetProductivityStreakResponse>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<GetProductivityStreakQueryHandler> _logger;

    public GetProductivityStreakQueryHandler(
        IAppTaskRepository taskRepository,
        ILogger<GetProductivityStreakQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result<GetProductivityStreakResponse>> Handle(
        GetProductivityStreakQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting productivity streak for user {UserId}", request.UserId);

            // Get all completed tasks for the user
            var filter = TaskFilter.ForCompletedTasks();
            filter.PageSize = 10000; // Get all completed tasks
            var (allTasks, _) = await _taskRepository.GetTasksByUserIdAsync(request.UserId, filter, cancellationToken);
            var completedTasks = allTasks
                .Where(t => t.Status == (int)AppTaskStatus.Completed)
                .OrderBy(t => t.UpdatedAt)
                .ToList();

            if (!completedTasks.Any())
            {
                return Result<GetProductivityStreakResponse>.Success(CreateEmptyResponse());
            }

            var today = DateTime.UtcNow.Date;
            var activeDates = completedTasks
                .Select(t => t.UpdatedAt.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // Calculate streaks
            var currentStreak = CalculateCurrentStreak(activeDates, today);
            var longestStreak = CalculateLongestStreak(activeDates);
            var bestMonthlyStreak = CalculateBestMonthlyStreak(activeDates);

            // Get streak dates
            var lastCompletionDate = completedTasks.Any() ? completedTasks.Max(t => t.UpdatedAt) : (DateTime?)null;
            var streakStartDate = currentStreak > 0 ? today.AddDays(-(currentStreak - 1)) : (DateTime?)null;

            // Calculate milestones
            var milestones = CalculateMilestones(longestStreak, activeDates);

            // Calculate weekly and monthly stats
            var weeklyStats = CalculateWeeklyStats(completedTasks, today);
            var monthlyStats = CalculateMonthlyStats(completedTasks, today);

            // Generate last 30 days data
            var last30Days = GenerateLast30DaysData(completedTasks, activeDates, today, currentStreak);

            var response = new GetProductivityStreakResponse(
                CurrentStreak: currentStreak,
                LongestStreak: longestStreak,
                BestMonthlyStreak: bestMonthlyStreak,
                LastCompletionDate: lastCompletionDate,
                StreakStartDate: streakStartDate,
                Milestones: milestones,
                WeeklyStats: weeklyStats,
                MonthlyStats: monthlyStats,
                Last30Days: last30Days
            );

            _logger.LogInformation("Successfully retrieved productivity streak for user {UserId}", request.UserId);
            return Result<GetProductivityStreakResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting productivity streak for user {UserId}", request.UserId);
            return Result<GetProductivityStreakResponse>.Failure($"Failed to retrieve productivity streak: {ex.Message}");
        }
    }

    private int CalculateCurrentStreak(List<DateTime> activeDates, DateTime today)
    {
        if (!activeDates.Any())
        {
            return 0;
        }

        var streak = 0;
        var currentDate = today;

        // Check if there's activity today or yesterday (allow for timezone differences)
        var hasRecentActivity = activeDates.Any(d => d >= today.AddDays(-1));
        if (!hasRecentActivity)
        {
            return 0;
        }

        // Count consecutive days backwards
        while (activeDates.Contains(currentDate))
        {
            streak++;
            currentDate = currentDate.AddDays(-1);
        }

        return streak;
    }

    private int CalculateLongestStreak(List<DateTime> activeDates)
    {
        if (!activeDates.Any())
        {
            return 0;
        }

        int longestStreak = 1;
        int currentStreak = 1;

        for (int i = 1; i < activeDates.Count; i++)
        {
            if (activeDates[i] == activeDates[i - 1].AddDays(1))
            {
                currentStreak++;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }
            else
            {
                currentStreak = 1;
            }
        }

        return longestStreak;
    }

    private int CalculateBestMonthlyStreak(List<DateTime> activeDates)
    {
        if (!activeDates.Any())
        {
            return 0;
        }

        var monthlyStreaks = new Dictionary<string, int>();

        foreach (var dateGroup in activeDates.GroupBy(d => $"{d.Year}-{d.Month:D2}"))
        {
            var monthDates = dateGroup.OrderBy(d => d).ToList();
            monthlyStreaks[dateGroup.Key] = CalculateLongestStreak(monthDates);
        }

        return monthlyStreaks.Values.DefaultIfEmpty(0).Max();
    }

    private List<StreakMilestone> CalculateMilestones(int longestStreak, List<DateTime> activeDates)
    {
        var milestoneTargets = new[] { 3, 7, 14, 30, 60, 100, 365 };
        var milestones = new List<StreakMilestone>();

        foreach (var target in milestoneTargets)
        {
            var isAchieved = longestStreak >= target;
            DateTime? achievedDate = null;

            if (isAchieved)
            {
                // Find the first date when this streak was achieved
                // This is a simplified calculation - in reality you'd track this more precisely
                var streakStartIndex = FindStreakOfLength(activeDates, target);
                if (streakStartIndex >= 0)
                {
                    achievedDate = activeDates[streakStartIndex + target - 1];
                }
            }

            milestones.Add(new StreakMilestone(
                Days: target,
                Title: GetMilestoneTitle(target),
                Description: GetMilestoneDescription(target),
                IsAchieved: isAchieved,
                AchievedDate: achievedDate
            ));
        }

        return milestones;
    }

    private int FindStreakOfLength(List<DateTime> activeDates, int targetLength)
    {
        if (activeDates.Count < targetLength)
        {
            return -1;
        }

        int currentStreak = 1;
        int streakStart = 0;

        for (int i = 1; i < activeDates.Count; i++)
        {
            if (activeDates[i] == activeDates[i - 1].AddDays(1))
            {
                currentStreak++;
                if (currentStreak >= targetLength)
                {
                    return i - targetLength + 1;
                }
            }
            else
            {
                currentStreak = 1;
                streakStart = i;
            }
        }

        return -1;
    }

    private StreakStats CalculateWeeklyStats(List<Domain.Entities.AppTask> completedTasks, DateTime today)
    {
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var weekTasks = completedTasks.Where(t => t.UpdatedAt >= weekStart).ToList();
        var activeDays = weekTasks.Select(t => t.UpdatedAt.Date).Distinct().Count();

        return new StreakStats(
            TotalDays: 7,
            ActiveDays: activeDays,
            CompletedTasks: weekTasks.Count,
            ConsistencyRate: Math.Round((double)activeDays / 7 * 100, 1),
            AverageTasksPerDay: Math.Round((double)weekTasks.Count / 7, 1)
        );
    }

    private StreakStats CalculateMonthlyStats(List<Domain.Entities.AppTask> completedTasks, DateTime today)
    {
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var monthTasks = completedTasks.Where(t => t.UpdatedAt >= monthStart).ToList();
        var activeDays = monthTasks.Select(t => t.UpdatedAt.Date).Distinct().Count();

        return new StreakStats(
            TotalDays: daysInMonth,
            ActiveDays: activeDays,
            CompletedTasks: monthTasks.Count,
            ConsistencyRate: Math.Round((double)activeDays / daysInMonth * 100, 1),
            AverageTasksPerDay: Math.Round((double)monthTasks.Count / daysInMonth, 1)
        );
    }

    private List<DailyStreakPoint> GenerateLast30DaysData(
        List<Domain.Entities.AppTask> completedTasks,
        List<DateTime> activeDates,
        DateTime today,
        int currentStreak)
    {
        var last30Days = new List<DailyStreakPoint>();

        for (int i = 29; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var hasActivity = activeDates.Contains(date);
            var completedCount = completedTasks.Count(t => t.UpdatedAt.Date == date);
            var isPartOfCurrentStreak = i < currentStreak;

            last30Days.Add(new DailyStreakPoint(
                Date: date,
                HasActivity: hasActivity,
                CompletedTasks: completedCount,
                IsPartOfCurrentStreak: isPartOfCurrentStreak
            ));
        }

        return last30Days;
    }

    private GetProductivityStreakResponse CreateEmptyResponse()
    {
        return new GetProductivityStreakResponse(
            CurrentStreak: 0,
            LongestStreak: 0,
            BestMonthlyStreak: 0,
            LastCompletionDate: null,
            StreakStartDate: null,
            Milestones: CalculateMilestones(0, new List<DateTime>()),
            WeeklyStats: new StreakStats(7, 0, 0, 0.0, 0.0),
            MonthlyStats: new StreakStats(DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month), 0, 0, 0.0, 0.0),
            Last30Days: GenerateLast30DaysData(new List<Domain.Entities.AppTask>(), new List<DateTime>(), DateTime.UtcNow.Date, 0)
        );
    }

    private string GetMilestoneTitle(int days)
    {
        return days switch
        {
            3 => "Getting Started",
            7 => "Week Warrior",
            14 => "Two Week Champion",
            30 => "Monthly Master",
            60 => "Consistency King",
            100 => "Century Achiever",
            365 => "Year-Long Legend",
            _ => $"{days} Day Streak"
        };
    }

    private string GetMilestoneDescription(int days)
    {
        return days switch
        {
            3 => "Complete tasks for 3 consecutive days",
            7 => "Maintain productivity for a full week",
            14 => "Two weeks of consistent task completion",
            30 => "A full month of daily productivity",
            60 => "Two months of unwavering consistency",
            100 => "Reached the prestigious 100-day milestone",
            365 => "An entire year of daily achievement",
            _ => $"Complete tasks for {days} consecutive days"
        };
    }
}
