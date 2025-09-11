using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Services.Analytics;

/// <summary>
/// Service for calculating various analytics and metrics
/// </summary>
public class AnalyticsCalculationService : IAnalyticsCalculationService
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly ILogger<AnalyticsCalculationService> _logger;

    public AnalyticsCalculationService(
        IAppTaskRepository taskRepository,
        ILogger<AnalyticsCalculationService> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<TaskMetrics> CalculateDailyMetricsAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calculating daily metrics for user {UserId} on {Date}", userId, date.Date);

            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            // Get all tasks for the user
            var allTasks = await _taskRepository.GetTasksByUserIdAsync(userId, cancellationToken);

            // Filter tasks relevant to the specific date
            var tasksCreatedOnDate = allTasks.Where(t => t.CreatedAt.Date == dayStart).ToList();
            var tasksCompletedOnDate = allTasks.Where(t =>
                t.Status == (int)AppTaskStatus.Completed &&
                t.UpdatedAt.Date == dayStart).ToList();
            var overdueTasksOnDate = allTasks.Where(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value.Date < dayStart &&
                t.Status != (int)AppTaskStatus.Completed).ToList();
            var totalActiveTasksOnDate = allTasks.Where(t =>
                t.CreatedAt.Date <= dayStart &&
                !t.IsDeleted &&
                t.Status != (int)AppTaskStatus.Completed).ToList();

            // Create metrics instance
            var metrics = TaskMetrics.Create(userId, date);

            // Update basic counts
            metrics.UpdateTaskCounts(
                completed: tasksCompletedOnDate.Count,
                overdue: overdueTasksOnDate.Count,
                total: totalActiveTasksOnDate.Count,
                created: tasksCreatedOnDate.Count);

            // Calculate category breakdown
            var categoryBreakdown = CalculateCategoryBreakdown(tasksCompletedOnDate);
            metrics.UpdateCategoryBreakdown(categoryBreakdown);

            // Calculate priority breakdown
            var priorityBreakdown = CalculatePriorityBreakdown(tasksCompletedOnDate);
            metrics.UpdatePriorityBreakdown(priorityBreakdown);

            // Calculate average completion time
            var avgCompletionTime = CalculateAverageCompletionTime(tasksCompletedOnDate);
            metrics.UpdateAverageCompletionTime(avgCompletionTime);

            // Estimate productive hours (simplified calculation)
            var productiveHours = Math.Min(tasksCompletedOnDate.Count * 0.5 + tasksCreatedOnDate.Count * 0.25, 8);
            metrics.UpdateProductiveHours((int)Math.Ceiling(productiveHours));

            _logger.LogInformation("Calculated daily metrics: {Completed} completed, {Overdue} overdue, {Total} total tasks",
                tasksCompletedOnDate.Count, overdueTasksOnDate.Count, totalActiveTasksOnDate.Count);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating daily metrics for user {UserId} on {Date}", userId, date);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> CalculateWeeklyMetricsAsync(Guid userId, DateTime weekStartDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calculating weekly metrics for user {UserId} starting {WeekStart}", userId, weekStartDate.Date);

            var weekEnd = weekStartDate.AddDays(7);
            var weeklyMetrics = new Dictionary<string, object>();

            // Calculate daily metrics for each day of the week
            var dailyMetrics = new List<TaskMetrics>();
            for (int i = 0; i < 7; i++)
            {
                var currentDate = weekStartDate.AddDays(i);
                var dayMetrics = await CalculateDailyMetricsAsync(userId, currentDate, cancellationToken);
                dailyMetrics.Add(dayMetrics);
            }

            // Aggregate weekly data
            weeklyMetrics["totalCompletedTasks"] = dailyMetrics.Sum(m => m.CompletedTasks);
            weeklyMetrics["totalCreatedTasks"] = dailyMetrics.Sum(m => m.CreatedTasks);
            weeklyMetrics["averageOverdueTasks"] = dailyMetrics.Average(m => m.OverdueTasks);
            weeklyMetrics["totalProductiveHours"] = dailyMetrics.Sum(m => m.ProductiveHours);
            weeklyMetrics["averageEfficiencyScore"] = dailyMetrics.Average(m => m.EfficiencyScore);
            weeklyMetrics["productiveDays"] = dailyMetrics.Count(m => m.IsProductiveDay());
            weeklyMetrics["weeklyCompletionRate"] = CalculateWeeklyCompletionRate(dailyMetrics);

            // Week-over-week comparison would require previous week data
            weeklyMetrics["weekStart"] = weekStartDate.Date;
            weeklyMetrics["weekEnd"] = weekEnd.Date;

            return weeklyMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating weekly metrics for user {UserId}", userId);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> CalculateMonthlyMetricsAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calculating monthly metrics for user {UserId} for {Year}-{Month}", userId, year, month);

            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var monthlyMetrics = new Dictionary<string, object>();

            var allTasks = await _taskRepository.GetTasksByUserIdAsync(userId, cancellationToken);

            // Filter tasks for the specific month
            var monthTasks = allTasks.Where(t =>
                t.CreatedAt >= monthStart && t.CreatedAt < monthEnd).ToList();
            var completedInMonth = monthTasks.Where(t =>
                t.Status == (int)AppTaskStatus.Completed).ToList();

            monthlyMetrics["monthStart"] = monthStart.Date;
            monthlyMetrics["monthEnd"] = monthEnd.Date;
            monthlyMetrics["totalTasksCreated"] = monthTasks.Count;
            monthlyMetrics["totalTasksCompleted"] = completedInMonth.Count;
            monthlyMetrics["monthlyCompletionRate"] = monthTasks.Count > 0
                ? (double)completedInMonth.Count / monthTasks.Count * 100
                : 0.0;

            // Calculate category performance for the month
            monthlyMetrics["categoryPerformance"] = await CalculateCategoryPerformanceAsync(
                userId, monthStart, monthEnd, cancellationToken);

            // Calculate priority performance for the month
            monthlyMetrics["priorityPerformance"] = await CalculatePriorityPerformanceAsync(
                userId, monthStart, monthEnd, cancellationToken);

            // Calculate efficiency score for the month
            monthlyMetrics["monthlyEfficiencyScore"] = await CalculateEfficiencyScoreAsync(
                userId, monthStart, monthEnd, cancellationToken);

            return monthlyMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating monthly metrics for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserAnalytics> CalculateUserAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calculating user analytics for user {UserId}", userId);

            var allTasks = await _taskRepository.GetTasksByUserIdAsync(userId, cancellationToken);
            if (!allTasks.Any())
            {
                // Return basic analytics for user with no tasks
                return UserAnalytics.Create(userId, DateTime.UtcNow);
            }

            var firstTask = allTasks.OrderBy(t => t.CreatedAt).First();
            var analytics = UserAnalytics.Create(userId, firstTask.CreatedAt);

            // Calculate basic statistics
            var completedTasks = allTasks.Where(t => t.Status == (int)AppTaskStatus.Completed).ToList();
            var overdueTasks = allTasks.Where(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value < DateTime.UtcNow &&
                t.Status != (int)AppTaskStatus.Completed).ToList();

            analytics.UpdateTaskStats(
                completedDelta: completedTasks.Count,
                createdDelta: allTasks.Count,
                overdueDelta: overdueTasks.Count);

            // Calculate category stats
            var categoryStats = CalculateCategoryBreakdown(completedTasks);
            analytics.UpdateCategoryStats(categoryStats);

            // Calculate priority stats
            var priorityStats = CalculatePriorityBreakdown(completedTasks);
            analytics.UpdatePriorityStats(priorityStats);

            // Calculate monthly trends (last 12 months)
            var monthlyTrends = await CalculateProductivityTrendsAsync(userId, 12, cancellationToken);
            analytics.UpdateMonthlyTrends(monthlyTrends);

            // Update personalization data based on user behavior
            await UpdatePersonalizationData(analytics, allTasks);

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating user analytics for user {UserId}", userId);
            throw;
        }
    }

    public async Task<double> CalculateEfficiencyScoreAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = await _taskRepository.GetTasksByUserIdAsync(userId, cancellationToken);
            var periodTasks = tasks.Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate).ToList();

            if (!periodTasks.Any())
            {
                return 0.0;
            }

            var completedTasks = periodTasks.Where(t => t.Status == (int)AppTaskStatus.Completed).ToList();
            var overdueTasks = periodTasks.Where(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value < DateTime.UtcNow &&
                t.Status != (int)AppTaskStatus.Completed).ToList();

            var completionRate = (double)completedTasks.Count / periodTasks.Count;
            var overdueRate = (double)overdueTasks.Count / periodTasks.Count;

            // Efficiency combines completion rate with overdue penalty
            var efficiency = completionRate * (1.0 - overdueRate * 0.5);
            return Math.Max(0.0, Math.Min(1.0, efficiency));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating efficiency score for user {UserId}", userId);
            return 0.0;
        }
    }

    public async Task<Dictionary<string, double>> CalculateProductivityTrendsAsync(Guid userId, int monthsBack, CancellationToken cancellationToken = default)
    {
        try
        {
            var trends = new Dictionary<string, double>();
            var endDate = DateTime.UtcNow.Date;

            for (int i = 0; i < monthsBack; i++)
            {
                var monthStart = endDate.AddMonths(-i).AddDays(-endDate.Day + 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var monthKey = monthStart.ToString("yyyy-MM");

                var efficiency = await CalculateEfficiencyScoreAsync(userId, monthStart, monthEnd, cancellationToken);
                trends[monthKey] = efficiency;
            }

            return trends;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating productivity trends for user {UserId}", userId);
            return new Dictionary<string, double>();
        }
    }

    public async Task<Dictionary<string, CategoryPerformance>> CalculateCategoryPerformanceAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = await _taskRepository.GetTasksByUserIdAsync(userId, cancellationToken);
            var periodTasks = tasks.Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate).ToList();

            var categoryPerformance = new Dictionary<string, CategoryPerformance>();

            var categoriesWithTasks = periodTasks.GroupBy(t => AppTaskCategory.FromValue(t.Category).Name);

            foreach (var categoryGroup in categoriesWithTasks)
            {
                var categoryTasks = categoryGroup.ToList();
                var completedTasks = categoryTasks.Where(t => t.Status == (int)AppTaskStatus.Completed).ToList();
                var overdueTasks = categoryTasks.Where(t =>
                    t.DueDate.HasValue &&
                    t.DueDate.Value < DateTime.UtcNow &&
                    t.Status != (int)AppTaskStatus.Completed).ToList();

                var performance = new CategoryPerformance
                {
                    CategoryName = categoryGroup.Key,
                    TotalTasks = categoryTasks.Count,
                    CompletedTasks = completedTasks.Count,
                    OverdueTasks = overdueTasks.Count,
                    CompletionRate = categoryTasks.Count > 0 ? (double)completedTasks.Count / categoryTasks.Count * 100 : 0.0,
                    AverageCompletionTimeHours = CalculateAverageCompletionTime(completedTasks),
                    EfficiencyScore = categoryTasks.Count > 0 ?
                        (double)completedTasks.Count / categoryTasks.Count * (1.0 - (double)overdueTasks.Count / categoryTasks.Count * 0.5) : 0.0,
                    Trend = "Stable" // This would require historical comparison
                };

                categoryPerformance[categoryGroup.Key] = performance;
            }

            return categoryPerformance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating category performance for user {UserId}", userId);
            return new Dictionary<string, CategoryPerformance>();
        }
    }

    public async Task<Dictionary<string, PriorityPerformance>> CalculatePriorityPerformanceAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = await _taskRepository.GetTasksByUserIdAsync(userId, cancellationToken);
            var periodTasks = tasks.Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate).ToList();

            var priorityPerformance = new Dictionary<string, PriorityPerformance>();

            var prioritiesWithTasks = periodTasks.GroupBy(t => Priority.FromValue(t.Priority).Name);

            foreach (var priorityGroup in prioritiesWithTasks)
            {
                var priorityTasks = priorityGroup.ToList();
                var completedTasks = priorityTasks.Where(t => t.Status == (int)AppTaskStatus.Completed).ToList();
                var overdueTasks = priorityTasks.Where(t =>
                    t.DueDate.HasValue &&
                    t.DueDate.Value < DateTime.UtcNow &&
                    t.Status != (int)AppTaskStatus.Completed).ToList();
                var onTimeTasks = completedTasks.Where(t =>
                    !t.DueDate.HasValue || t.UpdatedAt <= t.DueDate.Value).ToList();

                var performance = new PriorityPerformance
                {
                    PriorityName = priorityGroup.Key,
                    TotalTasks = priorityTasks.Count,
                    CompletedTasks = completedTasks.Count,
                    OverdueTasks = overdueTasks.Count,
                    CompletionRate = priorityTasks.Count > 0 ? (double)completedTasks.Count / priorityTasks.Count * 100 : 0.0,
                    AverageCompletionTimeHours = CalculateAverageCompletionTime(completedTasks),
                    OnTimeCompletionRate = completedTasks.Count > 0 ? (double)onTimeTasks.Count / completedTasks.Count * 100 : 0.0,
                    Trend = "Stable" // This would require historical comparison
                };

                priorityPerformance[priorityGroup.Key] = performance;
            }

            return priorityPerformance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating priority performance for user {UserId}", userId);
            return new Dictionary<string, PriorityPerformance>();
        }
    }

    public async Task<Dictionary<string, object>> CalculateTimeBasedPatternsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = await _taskRepository.GetTasksByUserIdAsync(userId, cancellationToken);
            var periodTasks = tasks.Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate).ToList();
            var completedTasks = periodTasks.Where(t => t.Status == (int)AppTaskStatus.Completed).ToList();

            var patterns = new Dictionary<string, object>();

            // Hourly patterns
            var hourlyCompletions = completedTasks
                .GroupBy(t => t.UpdatedAt.Hour)
                .ToDictionary(g => $"hour_{g.Key:D2}", g => g.Count());
            patterns["hourlyCompletions"] = hourlyCompletions;

            // Daily patterns (day of week)
            var dailyCompletions = completedTasks
                .GroupBy(t => t.UpdatedAt.DayOfWeek)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());
            patterns["dailyCompletions"] = dailyCompletions;

            // Most productive hour
            var mostProductiveHour = hourlyCompletions.Count > 0
                ? hourlyCompletions.OrderByDescending(h => h.Value).First().Key
                : "unknown";
            patterns["mostProductiveHour"] = mostProductiveHour;

            // Most productive day
            var mostProductiveDay = dailyCompletions.Count > 0
                ? dailyCompletions.OrderByDescending(d => d.Value).First().Key
                : "unknown";
            patterns["mostProductiveDay"] = mostProductiveDay;

            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating time-based patterns for user {UserId}", userId);
            return new Dictionary<string, object>();
        }
    }

    public async Task<ProductivityPrediction> PredictProductivityAsync(Guid userId, DateTime targetDate, CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple prediction based on recent trends
            var recentTrends = await CalculateProductivityTrendsAsync(userId, 6, cancellationToken);
            var avgEfficiency = recentTrends.Values.Any() ? recentTrends.Values.Average() : 0.5;

            var userAnalytics = await CalculateUserAnalyticsAsync(userId, cancellationToken);
            var avgTasksPerDay = userAnalytics.GetAverageTasksPerDay();

            var prediction = new ProductivityPrediction
            {
                TargetDate = targetDate,
                PredictedEfficiencyScore = avgEfficiency,
                PredictedTasksCompleted = (int)Math.Ceiling(avgTasksPerDay),
                ConfidenceLevel = recentTrends.Count >= 3 ? 0.75 : 0.5,
                InfluencingFactors = GetInfluencingFactors(userAnalytics),
                RecommendedAction = GetRecommendedAction(avgEfficiency, avgTasksPerDay)
            };

            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting productivity for user {UserId}", userId);
            return new ProductivityPrediction { TargetDate = targetDate, ConfidenceLevel = 0.0 };
        }
    }

    #region Private Helper Methods

    private Dictionary<string, int> CalculateCategoryBreakdown(List<AppTask> tasks)
    {
        return tasks
            .GroupBy(t => AppTaskCategory.FromValue(t.Category).Name)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private Dictionary<string, int> CalculatePriorityBreakdown(List<AppTask> tasks)
    {
        return tasks
            .GroupBy(t => Priority.FromValue(t.Priority).Name)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private double CalculateAverageCompletionTime(List<AppTask> completedTasks)
    {
        if (!completedTasks.Any())
        {
            return 0.0;
        }

        var completionTimes = completedTasks
            .Select(t => (t.UpdatedAt - t.CreatedAt).TotalHours)
            .Where(hours => hours > 0);

        return completionTimes.Any() ? completionTimes.Average() : 0.0;
    }

    private double CalculateWeeklyCompletionRate(List<TaskMetrics> dailyMetrics)
    {
        var totalCompleted = dailyMetrics.Sum(m => m.CompletedTasks);
        var totalTasks = dailyMetrics.Sum(m => m.TotalTasks);
        return totalTasks > 0 ? (double)totalCompleted / totalTasks * 100 : 0.0;
    }

    private Task UpdatePersonalizationData(UserAnalytics analytics, List<AppTask> allTasks)
    {
        // Most active time of day
        var mostActiveHour = allTasks
            .GroupBy(t => t.CreatedAt.Hour)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? 9; // Default to 9 AM
        analytics.UpdatePersonalizationData("preferredHour", mostActiveHour);

        // Preferred task categories
        var preferredCategory = analytics.GetMostProductiveCategory() ?? "ToDo";
        analytics.UpdatePersonalizationData("preferredCategory", preferredCategory);

        // Average task completion time preference
        var avgCompletionTime = CalculateAverageCompletionTime(
            allTasks.Where(t => t.Status == (int)AppTaskStatus.Completed).ToList());
        analytics.UpdatePersonalizationData("avgCompletionTimeHours", avgCompletionTime);
        return Task.CompletedTask;
    }

    private List<string> GetInfluencingFactors(UserAnalytics analytics)
    {
        var factors = new List<string>();

        if (analytics.CurrentStreakDays > 7)
        {
            factors.Add("Strong current productivity streak");
        }

        if (analytics.GetCompletionRate() > 80)
        {
            factors.Add("High historical completion rate");
        }

        if (analytics.GetOverdueRate() > 20)
        {
            factors.Add("Tendency towards overdue tasks");
        }

        var mostProductiveCategory = analytics.GetMostProductiveCategory();
        if (!string.IsNullOrEmpty(mostProductiveCategory))
        {
            factors.Add($"Strong performance in {mostProductiveCategory} tasks");
        }

        return factors;
    }

    private string GetRecommendedAction(double avgEfficiency, double avgTasksPerDay)
    {
        return (avgEfficiency, avgTasksPerDay) switch
        {
            ( < 0.5, _) => "Focus on completing existing tasks rather than creating new ones",
            (_, < 1.0) => "Consider setting more daily goals to increase productivity",
            ( > 0.8, > 5.0) => "Excellent productivity! Consider taking on more challenging tasks",
            _ => "Maintain your current productivity patterns"
        };
    }

    #endregion
}
