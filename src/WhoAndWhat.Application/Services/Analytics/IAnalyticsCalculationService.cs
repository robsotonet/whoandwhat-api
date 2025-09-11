using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Services.Analytics;

/// <summary>
/// Interface for analytics calculation services
/// </summary>
public interface IAnalyticsCalculationService
{
    /// <summary>
    /// Calculates daily task metrics for a user
    /// </summary>
    public Task<TaskMetrics> CalculateDailyMetricsAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates weekly aggregated metrics for a user
    /// </summary>
    public Task<Dictionary<string, object>> CalculateWeeklyMetricsAsync(Guid userId, DateTime weekStartDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates monthly aggregated metrics for a user
    /// </summary>
    public Task<Dictionary<string, object>> CalculateMonthlyMetricsAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates user's overall analytics summary
    /// </summary>
    public Task<UserAnalytics> CalculateUserAnalyticsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates efficiency score based on task completion data
    /// </summary>
    public Task<double> CalculateEfficiencyScoreAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates productivity trends over time
    /// </summary>
    public Task<Dictionary<string, double>> CalculateProductivityTrendsAsync(Guid userId, int monthsBack, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates category performance breakdown
    /// </summary>
    public Task<Dictionary<string, CategoryPerformance>> CalculateCategoryPerformanceAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates priority distribution and performance
    /// </summary>
    public Task<Dictionary<string, PriorityPerformance>> CalculatePriorityPerformanceAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates time-based productivity patterns (hourly, daily)
    /// </summary>
    public Task<Dictionary<string, object>> CalculateTimeBasedPatternsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Predicts future productivity based on historical data
    /// </summary>
    public Task<ProductivityPrediction> PredictProductivityAsync(Guid userId, DateTime targetDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Category performance data structure
/// </summary>
public record CategoryPerformance
{
    public string CategoryName { get; init; } = string.Empty;
    public int TotalTasks { get; init; }
    public int CompletedTasks { get; init; }
    public int OverdueTasks { get; init; }
    public double CompletionRate { get; init; }
    public double AverageCompletionTimeHours { get; init; }
    public double EfficiencyScore { get; init; }
    public string Trend { get; init; } = string.Empty;
}

/// <summary>
/// Priority performance data structure
/// </summary>
public record PriorityPerformance
{
    public string PriorityName { get; init; } = string.Empty;
    public int TotalTasks { get; init; }
    public int CompletedTasks { get; init; }
    public int OverdueTasks { get; init; }
    public double CompletionRate { get; init; }
    public double AverageCompletionTimeHours { get; init; }
    public double OnTimeCompletionRate { get; init; }
    public string Trend { get; init; } = string.Empty;
}

/// <summary>
/// Productivity prediction data structure
/// </summary>
public record ProductivityPrediction
{
    public DateTime TargetDate { get; init; }
    public double PredictedEfficiencyScore { get; init; }
    public int PredictedTasksCompleted { get; init; }
    public double ConfidenceLevel { get; init; }
    public List<string> InfluencingFactors { get; init; } = new();
    public Dictionary<string, double> CategoryPredictions { get; init; } = new();
    public string RecommendedAction { get; init; } = string.Empty;
}
