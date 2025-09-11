using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Entity representing point-in-time analytics snapshots for historical analysis
/// </summary>
public class AnalyticsSnapshot : BaseEntity
{
    public Guid UserId { get; private set; }
    public DateTime SnapshotDate { get; private set; }
    public SnapshotType SnapshotType { get; private set; }
    public Dictionary<string, object> MetricsData { get; private set; }
    public string? Notes { get; private set; }
    public Dictionary<string, object> ComparisonData { get; private set; }
    public double OverallScore { get; private set; }
    public string? TrendAnalysis { get; private set; }

    // Protected constructor for EF Core
    protected AnalyticsSnapshot()
    {
        MetricsData = new Dictionary<string, object>();
        ComparisonData = new Dictionary<string, object>();
    }

    private AnalyticsSnapshot(Guid userId, DateTime snapshotDate, SnapshotType snapshotType) : this()
    {
        UserId = userId;
        SnapshotDate = snapshotDate.Date;
        SnapshotType = snapshotType;
        OverallScore = 0.0;
    }

    /// <summary>
    /// Creates a new analytics snapshot
    /// </summary>
    public static AnalyticsSnapshot Create(Guid userId, DateTime snapshotDate, SnapshotType snapshotType,
        Dictionary<string, object> metricsData)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (metricsData == null || metricsData.Count == 0)
        {
            throw new ArgumentException("Metrics data cannot be null or empty", nameof(metricsData));
        }

        var snapshot = new AnalyticsSnapshot(userId, snapshotDate, snapshotType);
        snapshot.MetricsData = metricsData;
        snapshot.CalculateOverallScore();

        snapshot.AddDomainEvent(new AnalyticsSnapshotCreatedEvent(snapshot));
        return snapshot;
    }

    /// <summary>
    /// Updates metrics data in the snapshot
    /// </summary>
    public void UpdateMetricsData(Dictionary<string, object> metricsData)
    {
        MetricsData = metricsData ?? new Dictionary<string, object>();
        CalculateOverallScore();
        MarkAsModified();
    }

    /// <summary>
    /// Adds comparison data with previous snapshot
    /// </summary>
    public void AddComparisonData(AnalyticsSnapshot? previousSnapshot)
    {
        if (previousSnapshot == null)
        {
            ComparisonData["hasPrevious"] = false;
            return;
        }

        ComparisonData["hasPrevious"] = true;
        ComparisonData["previousDate"] = previousSnapshot.SnapshotDate;
        ComparisonData["previousScore"] = previousSnapshot.OverallScore;
        ComparisonData["scoreChange"] = OverallScore - previousSnapshot.OverallScore;
        ComparisonData["percentChange"] = previousSnapshot.OverallScore > 0
            ? (OverallScore - previousSnapshot.OverallScore) / previousSnapshot.OverallScore * 100
            : 0.0;

        // Compare specific metrics
        foreach (var metric in MetricsData)
        {
            if (previousSnapshot.MetricsData.TryGetValue(metric.Key, out var previousValue))
            {
                ComparisonData[$"{metric.Key}_previous"] = previousValue;
                ComparisonData[$"{metric.Key}_change"] = CalculateChange(metric.Value, previousValue);
            }
        }

        GenerateTrendAnalysis(previousSnapshot);
        MarkAsModified();
    }

    /// <summary>
    /// Adds notes to the snapshot
    /// </summary>
    public void AddNotes(string notes)
    {
        Notes = notes;
        MarkAsModified();
    }

    /// <summary>
    /// Gets metric value by key
    /// </summary>
    public T? GetMetric<T>(string key)
    {
        if (MetricsData.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Gets comparison data value by key
    /// </summary>
    public T? GetComparisonData<T>(string key)
    {
        if (ComparisonData.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Gets percentage change from previous snapshot
    /// </summary>
    public double GetPercentageChange()
    {
        return GetComparisonData<double>("percentChange");
    }

    /// <summary>
    /// Gets score change from previous snapshot
    /// </summary>
    public double GetScoreChange()
    {
        return GetComparisonData<double>("scoreChange");
    }

    /// <summary>
    /// Determines if this snapshot shows improvement
    /// </summary>
    public bool ShowsImprovement()
    {
        var scoreChange = GetScoreChange();
        return scoreChange > 0;
    }

    /// <summary>
    /// Gets key insights from the snapshot
    /// </summary>
    public List<string> GetKeyInsights()
    {
        var insights = new List<string>();

        // Overall performance insight
        if (OverallScore >= 0.8)
        {
            insights.Add("Excellent overall productivity!");
        }
        else if (OverallScore >= 0.6)
        {
            insights.Add("Good productivity with room for improvement.");
        }
        else if (OverallScore >= 0.4)
        {
            insights.Add("Moderate productivity - consider optimizing workflows.");
        }
        else
        {
            insights.Add("Focus needed on improving productivity habits.");
        }

        // Trend insight
        if (!string.IsNullOrEmpty(TrendAnalysis))
        {
            insights.Add(TrendAnalysis);
        }

        // Comparison insights
        var percentChange = GetPercentageChange();
        if (Math.Abs(percentChange) >= 10)
        {
            var direction = percentChange > 0 ? "improved" : "decreased";
            insights.Add($"Productivity {direction} by {Math.Abs(percentChange):F1}% since last snapshot.");
        }

        // Specific metric insights
        AddSpecificMetricInsights(insights);

        return insights;
    }

    /// <summary>
    /// Gets summary statistics from the snapshot
    /// </summary>
    public Dictionary<string, object> GetSummaryStatistics()
    {
        var summary = new Dictionary<string, object>
        {
            ["snapshotDate"] = SnapshotDate,
            ["snapshotType"] = SnapshotType.ToString(),
            ["overallScore"] = OverallScore,
            ["hasComparison"] = GetComparisonData<bool>("hasPrevious"),
            ["totalMetrics"] = MetricsData.Count
        };

        if (GetComparisonData<bool>("hasPrevious"))
        {
            summary["scoreChange"] = GetScoreChange();
            summary["percentChange"] = GetPercentageChange();
            summary["trend"] = GetScoreChange() > 0 ? "Improving" : GetScoreChange() < 0 ? "Declining" : "Stable";
        }

        return summary;
    }

    /// <summary>
    /// Exports snapshot data for external analysis
    /// </summary>
    public Dictionary<string, object> ExportData()
    {
        return new Dictionary<string, object>
        {
            ["id"] = Id,
            ["userId"] = UserId,
            ["snapshotDate"] = SnapshotDate,
            ["snapshotType"] = SnapshotType.ToString(),
            ["overallScore"] = OverallScore,
            ["metricsData"] = MetricsData,
            ["comparisonData"] = ComparisonData,
            ["notes"] = Notes ?? string.Empty,
            ["trendAnalysis"] = TrendAnalysis ?? string.Empty,
            ["createdAt"] = CreatedAt
        };
    }

    private void CalculateOverallScore()
    {
        // Calculate weighted score based on key metrics
        var score = 0.0;
        var totalWeight = 0.0;

        // Common metrics with weights
        var metricWeights = new Dictionary<string, double>
        {
            ["completionRate"] = 0.3,
            ["efficiencyScore"] = 0.25,
            ["streakLength"] = 0.2,
            ["consistencyRate"] = 0.15,
            ["overdueRate"] = -0.1 // Negative weight for overdue tasks
        };

        foreach (var weight in metricWeights)
        {
            if (MetricsData.TryGetValue(weight.Key, out var value) && value is double doubleValue)
            {
                score += doubleValue * weight.Value;
                totalWeight += Math.Abs(weight.Value);
            }
        }

        // Normalize score
        OverallScore = totalWeight > 0 ? Math.Max(0.0, Math.Min(1.0, score / totalWeight)) : 0.0;
    }

    private double CalculateChange(object current, object previous)
    {
        try
        {
            if (current is double currentDouble && previous is double previousDouble)
            {
                return currentDouble - previousDouble;
            }
            if (current is int currentInt && previous is int previousInt)
            {
                return currentInt - previousInt;
            }
            // Add more type conversions as needed
        }
        catch
        {
            // Return 0 if unable to calculate change
        }
        return 0.0;
    }

    private void GenerateTrendAnalysis(AnalyticsSnapshot previousSnapshot)
    {
        var scoreChange = OverallScore - previousSnapshot.OverallScore;
        var daysBetween = (SnapshotDate - previousSnapshot.SnapshotDate).Days;

        TrendAnalysis = (scoreChange, daysBetween) switch
        {
            ( > 0.1, <= 7) => "Rapid improvement in the last week!",
            ( > 0.05, <= 30) => "Steady improvement this month.",
            ( < -0.1, <= 7) => "Significant decline in the last week.",
            ( < -0.05, <= 30) => "Productivity has declined this month.",
            _ => "Productivity remains relatively stable."
        };
    }

    private void AddSpecificMetricInsights(List<string> insights)
    {
        // Task completion insights
        var completionRate = GetMetric<double>("completionRate");
        if (completionRate >= 90)
        {
            insights.Add("Outstanding task completion rate!");
        }
        else if (completionRate <= 50)
        {
            insights.Add("Consider reducing task load to improve completion rate.");
        }

        // Efficiency insights
        var efficiencyScore = GetMetric<double>("efficiencyScore");
        if (efficiencyScore >= 0.8)
        {
            insights.Add("High efficiency in task execution.");
        }
        else if (efficiencyScore <= 0.4)
        {
            insights.Add("Focus on task prioritization to improve efficiency.");
        }

        // Streak insights
        var streakLength = GetMetric<int>("currentStreak");
        if (streakLength >= 30)
        {
            insights.Add($"Impressive {streakLength}-day productivity streak!");
        }
        else if (streakLength == 0)
        {
            insights.Add("Time to start building a new productivity streak!");
        }
    }

    public override bool CanSoftDelete()
    {
        // Snapshot data should not be soft deleted to maintain historical analysis
        return false;
    }
}

/// <summary>
/// Enumeration of snapshot types for different analysis periods
/// </summary>
public enum SnapshotType
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Annual = 4,
    Milestone = 5 // For special achievements or events
}

/// <summary>
/// Domain event raised when analytics snapshot is created
/// </summary>
public record AnalyticsSnapshotCreatedEvent(AnalyticsSnapshot Snapshot) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}
