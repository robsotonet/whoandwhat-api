using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Services.Analytics;

/// <summary>
/// Interface for aggregating and summarizing analytics metrics
/// </summary>
public interface IMetricsAggregationService
{
    /// <summary>
    /// Aggregates daily metrics into weekly summaries
    /// </summary>
    public Task<WeeklyMetricsSummary> AggregateWeeklyMetricsAsync(Guid userId, DateTime weekStartDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates daily/weekly metrics into monthly summaries
    /// </summary>
    public Task<MonthlyMetricsSummary> AggregateMonthlyMetricsAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates monthly metrics into quarterly summaries
    /// </summary>
    public Task<QuarterlyMetricsSummary> AggregateQuarterlyMetricsAsync(Guid userId, int year, int quarter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates all metrics into annual summaries
    /// </summary>
    public Task<AnnualMetricsSummary> AggregateAnnualMetricsAsync(Guid userId, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates analytics snapshots for different time periods
    /// </summary>
    public Task<AnalyticsSnapshot> CreateSnapshotAsync(Guid userId, DateTime date, SnapshotType snapshotType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates metrics across multiple users for comparative analysis
    /// </summary>
    public Task<ComparativeMetrics> AggregateComparativeMetricsAsync(List<Guid> userIds, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates rolling averages for smooth trend analysis
    /// </summary>
    public Task<RollingAverages> CalculateRollingAveragesAsync(Guid userId, int windowDays, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifies patterns and anomalies in user metrics
    /// </summary>
    public Task<PatternAnalysis> AnalyzePatternsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates key performance indicators (KPIs) for dashboards
    /// </summary>
    public Task<DashboardKPIs> CalculateKPIsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates executive summary of user's productivity
    /// </summary>
    public Task<ExecutiveSummary> GenerateExecutiveSummaryAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Weekly metrics summary
/// </summary>
public record WeeklyMetricsSummary
{
    public Guid UserId { get; init; }
    public DateTime WeekStartDate { get; init; }
    public DateTime WeekEndDate { get; init; }
    public int TotalTasksCompleted { get; init; }
    public int TotalTasksCreated { get; init; }
    public double WeeklyCompletionRate { get; init; }
    public int ProductiveDays { get; init; }
    public double AverageEfficiencyScore { get; init; }
    public int TotalProductiveHours { get; init; }
    public Dictionary<string, int> CategoryBreakdown { get; init; } = new();
    public Dictionary<string, int> PriorityBreakdown { get; init; } = new();
    public List<string> TopAchievements { get; init; } = new();
    public string WeeklyTrend { get; init; } = string.Empty;
    public double WeekOverWeekChange { get; init; }
}

/// <summary>
/// Monthly metrics summary
/// </summary>
public record MonthlyMetricsSummary
{
    public Guid UserId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public int TotalTasksCompleted { get; init; }
    public int TotalTasksCreated { get; init; }
    public double MonthlyCompletionRate { get; init; }
    public int ProductiveDays { get; init; }
    public double AverageEfficiencyScore { get; init; }
    public int TotalProductiveHours { get; init; }
    public Dictionary<string, CategoryPerformance> CategoryPerformance { get; init; } = new();
    public Dictionary<string, PriorityPerformance> PriorityPerformance { get; init; } = new();
    public List<WeeklyMetricsSummary> WeeklySummaries { get; init; } = new();
    public string MonthlyTrend { get; init; } = string.Empty;
    public double MonthOverMonthChange { get; init; }
    public List<string> MonthlyHighlights { get; init; } = new();
}

/// <summary>
/// Quarterly metrics summary
/// </summary>
public record QuarterlyMetricsSummary
{
    public Guid UserId { get; init; }
    public int Year { get; init; }
    public int Quarter { get; init; }
    public int TotalTasksCompleted { get; init; }
    public int TotalTasksCreated { get; init; }
    public double QuarterlyCompletionRate { get; init; }
    public int ProductiveDays { get; init; }
    public double AverageEfficiencyScore { get; init; }
    public List<MonthlyMetricsSummary> MonthlySummaries { get; init; } = new();
    public string QuarterlyTrend { get; init; } = string.Empty;
    public double QuarterOverQuarterChange { get; init; }
    public Dictionary<string, object> QuarterlyGoals { get; init; } = new();
    public List<string> QuarterlyAchievements { get; init; } = new();
}

/// <summary>
/// Annual metrics summary
/// </summary>
public record AnnualMetricsSummary
{
    public Guid UserId { get; init; }
    public int Year { get; init; }
    public int TotalTasksCompleted { get; init; }
    public int TotalTasksCreated { get; init; }
    public double AnnualCompletionRate { get; init; }
    public int ProductiveDays { get; init; }
    public double AverageEfficiencyScore { get; init; }
    public List<QuarterlyMetricsSummary> QuarterlySummaries { get; init; } = new();
    public string AnnualTrend { get; init; } = string.Empty;
    public double YearOverYearChange { get; init; }
    public Dictionary<string, object> AnnualGoals { get; init; } = new();
    public List<string> YearHighlights { get; init; } = new();
    public string ProductivityGrade { get; init; } = string.Empty;
}

/// <summary>
/// Comparative metrics across multiple users
/// </summary>
public record ComparativeMetrics
{
    public List<Guid> UserIds { get; init; } = new();
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public Dictionary<Guid, UserMetricsSummary> UserSummaries { get; init; } = new();
    public UserMetricsSummary AverageMetrics { get; init; } = new();
    public List<UserRanking> Rankings { get; init; } = new();
    public Dictionary<string, object> GroupInsights { get; init; } = new();
}

/// <summary>
/// User metrics summary for comparative analysis
/// </summary>
public record UserMetricsSummary
{
    public Guid UserId { get; init; }
    public int TasksCompleted { get; init; }
    public double CompletionRate { get; init; }
    public double EfficiencyScore { get; init; }
    public int StreakDays { get; init; }
    public int ProductiveDays { get; init; }
    public Dictionary<string, double> CategoryScores { get; init; } = new();
}

/// <summary>
/// User ranking information
/// </summary>
public record UserRanking
{
    public Guid UserId { get; init; }
    public string MetricName { get; init; } = string.Empty;
    public double MetricValue { get; init; }
    public int Rank { get; init; }
    public int TotalUsers { get; init; }
    public double Percentile { get; init; }
}

/// <summary>
/// Rolling averages for trend smoothing
/// </summary>
public record RollingAverages
{
    public Guid UserId { get; init; }
    public int WindowDays { get; init; }
    public Dictionary<DateTime, double> CompletionRateAverage { get; init; } = new();
    public Dictionary<DateTime, double> EfficiencyScoreAverage { get; init; } = new();
    public Dictionary<DateTime, double> TasksPerDayAverage { get; init; } = new();
    public Dictionary<DateTime, double> ProductiveHoursAverage { get; init; } = new();
    public string TrendDirection { get; init; } = string.Empty;
    public double TrendStrength { get; init; }
}

/// <summary>
/// Pattern analysis results
/// </summary>
public record PatternAnalysis
{
    public Guid UserId { get; init; }
    public DateTime AnalysisStartDate { get; init; }
    public DateTime AnalysisEndDate { get; init; }
    public List<ProductivityPattern> IdentifiedPatterns { get; init; } = new();
    public List<Anomaly> DetectedAnomalies { get; init; } = new();
    public Dictionary<string, double> CyclicalTrends { get; init; } = new();
    public Dictionary<string, object> BehavioralInsights { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
}

/// <summary>
/// Identified productivity pattern
/// </summary>
public record ProductivityPattern
{
    public string PatternType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public List<DateTime> OccurrenceDates { get; init; } = new();
    public Dictionary<string, object> PatternData { get; init; } = new();
}

/// <summary>
/// Detected anomaly in metrics
/// </summary>
public record Anomaly
{
    public DateTime Date { get; init; }
    public string AnomalyType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double Severity { get; init; }
    public Dictionary<string, object> Context { get; init; } = new();
}

/// <summary>
/// Dashboard Key Performance Indicators
/// </summary>
public record DashboardKPIs
{
    public Guid UserId { get; init; }
    public DateTime CalculationDate { get; init; }
    public Dictionary<string, KPIValue> CurrentPeriodKPIs { get; init; } = new();
    public Dictionary<string, KPIValue> PreviousPeriodKPIs { get; init; } = new();
    public Dictionary<string, double> PeriodOverPeriodChanges { get; init; } = new();
    public List<string> KPIAlerts { get; init; } = new();
    public Dictionary<string, string> KPITrends { get; init; } = new();
}

/// <summary>
/// KPI value with context
/// </summary>
public record KPIValue
{
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string DisplayFormat { get; init; } = string.Empty;
    public string HealthStatus { get; init; } = string.Empty; // Green, Yellow, Red
    public double Target { get; init; }
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Executive summary for high-level reporting
/// </summary>
public record ExecutiveSummary
{
    public Guid UserId { get; init; }
    public DateTime ReportDate { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public string OverallPerformanceGrade { get; init; } = string.Empty;
    public List<string> KeyAchievements { get; init; } = new();
    public List<string> AreasForImprovement { get; init; } = new();
    public Dictionary<string, double> HighLevelMetrics { get; init; } = new();
    public string ProductivityTrend { get; init; } = string.Empty;
    public List<string> StrategicRecommendations { get; init; } = new();
    public Dictionary<string, object> GoalProgress { get; init; } = new();
}
