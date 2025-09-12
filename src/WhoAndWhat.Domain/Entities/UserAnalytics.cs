using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Entity representing comprehensive user analytics aggregated over time
/// </summary>
public class UserAnalytics : BaseEntity
{
    public Guid UserId { get; private set; }
    public int TotalTasksCompleted { get; private set; }
    public int TotalTasksCreated { get; private set; }
    public int TotalOverdueTasks { get; private set; }
    public double TotalProductiveHours { get; private set; }
    public int CurrentStreakDays { get; private set; }
    public int LongestStreakDays { get; private set; }
    public double OverallEfficiencyScore { get; private set; }
    public Dictionary<string, int> CategoryCompletionStats { get; private set; }
    public Dictionary<string, int> PriorityCompletionStats { get; private set; }
    public Dictionary<string, double> MonthlyProductivityTrends { get; private set; }
    public DateTime LastActivityDate { get; private set; }
    public DateTime FirstTaskDate { get; private set; }
    public int ProductiveDaysCount { get; private set; }
    public Dictionary<string, object> PersonalizationData { get; private set; }

    // Protected constructor for EF Core
    protected UserAnalytics()
    {
        CategoryCompletionStats = new Dictionary<string, int>();
        PriorityCompletionStats = new Dictionary<string, int>();
        MonthlyProductivityTrends = new Dictionary<string, double>();
        PersonalizationData = new Dictionary<string, object>();
    }

    private UserAnalytics(Guid userId, DateTime firstTaskDate) : this()
    {
        UserId = userId;
        FirstTaskDate = firstTaskDate.Date;
        LastActivityDate = firstTaskDate.Date;
        TotalTasksCompleted = 0;
        TotalTasksCreated = 0;
        TotalOverdueTasks = 0;
        TotalProductiveHours = 0.0;
        CurrentStreakDays = 0;
        LongestStreakDays = 0;
        OverallEfficiencyScore = 0.0;
        ProductiveDaysCount = 0;
    }

    /// <summary>
    /// Creates new user analytics profile
    /// </summary>
    public static UserAnalytics Create(Guid userId, DateTime firstTaskDate)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        var analytics = new UserAnalytics(userId, firstTaskDate);
        analytics.AddDomainEvent(new UserAnalyticsCreatedEvent(analytics));
        return analytics;
    }

    /// <summary>
    /// Updates task completion statistics
    /// </summary>
    public void UpdateTaskStats(int completedDelta, int createdDelta, int overdueDelta)
    {
        TotalTasksCompleted = Math.Max(0, TotalTasksCompleted + completedDelta);
        TotalTasksCreated = Math.Max(0, TotalTasksCreated + createdDelta);
        TotalOverdueTasks = Math.Max(0, TotalOverdueTasks + overdueDelta);

        RecalculateOverallEfficiency();
        MarkAsModified();
    }

    /// <summary>
    /// Updates productivity streak information
    /// </summary>
    public void UpdateStreakInfo(int currentStreak, int longestStreak)
    {
        CurrentStreakDays = Math.Max(0, currentStreak);
        if (longestStreak > LongestStreakDays)
        {
            var oldBest = LongestStreakDays;
            LongestStreakDays = longestStreak;
            AddDomainEvent(new PersonalBestStreakUpdatedEvent(this, oldBest, longestStreak));
        }
        MarkAsModified();
    }

    /// <summary>
    /// Updates category completion statistics
    /// </summary>
    public void UpdateCategoryStats(Dictionary<string, int> categoryStats)
    {
        CategoryCompletionStats = categoryStats ?? new Dictionary<string, int>();
        MarkAsModified();
    }

    /// <summary>
    /// Updates priority completion statistics
    /// </summary>
    public void UpdatePriorityStats(Dictionary<string, int> priorityStats)
    {
        PriorityCompletionStats = priorityStats ?? new Dictionary<string, int>();
        MarkAsModified();
    }

    /// <summary>
    /// Updates monthly productivity trends
    /// </summary>
    public void UpdateMonthlyTrends(Dictionary<string, double> monthlyTrends)
    {
        MonthlyProductivityTrends = monthlyTrends ?? new Dictionary<string, double>();
        MarkAsModified();
    }

    /// <summary>
    /// Records productive hours for recalculation
    /// </summary>
    public void AddProductiveHours(double hours, DateTime activityDate)
    {
        TotalProductiveHours += Math.Max(0.0, hours);

        if (activityDate > LastActivityDate)
        {
            LastActivityDate = activityDate.Date;
        }

        RecalculateOverallEfficiency();
        MarkAsModified();
    }

    /// <summary>
    /// Increments productive days counter
    /// </summary>
    public void IncrementProductiveDays()
    {
        ProductiveDaysCount++;
        MarkAsModified();
    }

    /// <summary>
    /// Updates personalization data used for content recommendations
    /// </summary>
    public void UpdatePersonalizationData(string key, object value)
    {
        PersonalizationData[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Gets personalization data value
    /// </summary>
    public T? GetPersonalizationData<T>(string key)
    {
        if (PersonalizationData.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Gets overall completion rate percentage
    /// </summary>
    public double GetCompletionRate()
    {
        return TotalTasksCreated > 0
            ? (double)TotalTasksCompleted / TotalTasksCreated * 100
            : 0.0;
    }

    /// <summary>
    /// Gets overall overdue rate percentage
    /// </summary>
    public double GetOverdueRate()
    {
        return TotalTasksCreated > 0
            ? (double)TotalOverdueTasks / TotalTasksCreated * 100
            : 0.0;
    }

    /// <summary>
    /// Gets average tasks completed per day
    /// </summary>
    public double GetAverageTasksPerDay()
    {
        var totalDays = GetTotalActiveDays();
        return totalDays > 0 ? (double)TotalTasksCompleted / totalDays : 0.0;
    }

    /// <summary>
    /// Gets average productive hours per day
    /// </summary>
    public double GetAverageProductiveHoursPerDay()
    {
        var totalDays = GetTotalActiveDays();
        return totalDays > 0 ? TotalProductiveHours / totalDays : 0.0;
    }

    /// <summary>
    /// Gets total active days since first task
    /// </summary>
    public int GetTotalActiveDays()
    {
        return (LastActivityDate.Date - FirstTaskDate.Date).Days + 1;
    }

    /// <summary>
    /// Gets productivity consistency percentage
    /// </summary>
    public double GetProductivityConsistency()
    {
        var totalDays = GetTotalActiveDays();
        return totalDays > 0 ? (double)ProductiveDaysCount / totalDays * 100 : 0.0;
    }

    /// <summary>
    /// Gets the most productive category
    /// </summary>
    public string? GetMostProductiveCategory()
    {
        return CategoryCompletionStats.Count > 0
            ? CategoryCompletionStats.OrderByDescending(c => c.Value).First().Key
            : null;
    }

    /// <summary>
    /// Gets the most common priority level
    /// </summary>
    public string? GetMostCommonPriority()
    {
        return PriorityCompletionStats.Count > 0
            ? PriorityCompletionStats.OrderByDescending(p => p.Value).First().Key
            : null;
    }

    /// <summary>
    /// Gets productivity trend for the last few months
    /// </summary>
    public string GetRecentProductivityTrend()
    {
        if (MonthlyProductivityTrends.Count < 2)
        {
            return "Insufficient data";
        }

        var recentMonths = MonthlyProductivityTrends
            .OrderBy(m => m.Key)
            .TakeLast(3)
            .Select(m => m.Value)
            .ToList();

        if (recentMonths.Count < 2)
        {
            return "Insufficient data";
        }

        var lastMonth = recentMonths.Last();
        var previousMonth = recentMonths[recentMonths.Count - 2];

        if (lastMonth > previousMonth * 1.1)
        {
            return "Improving";
        }

        if (lastMonth < previousMonth * 0.9)
        {
            return "Declining";
        }

        return "Stable";
    }

    /// <summary>
    /// Gets user's experience level based on analytics
    /// </summary>
    public UserExperienceLevel GetExperienceLevel()
    {
        var totalDays = GetTotalActiveDays();
        var completionRate = GetCompletionRate();
        var consistency = GetProductivityConsistency();

        return (totalDays, completionRate, consistency) switch
        {
            ( >= 365, >= 80, >= 70) => UserExperienceLevel.Expert,
            ( >= 180, >= 70, >= 60) => UserExperienceLevel.Advanced,
            ( >= 90, >= 60, >= 50) => UserExperienceLevel.Intermediate,
            ( >= 30, >= 50, >= 40) => UserExperienceLevel.Developing,
            _ => UserExperienceLevel.Beginner
        };
    }

    /// <summary>
    /// Gets personalized insights based on user data
    /// </summary>
    public List<string> GetPersonalizedInsights()
    {
        var insights = new List<string>();

        // Completion rate insights
        var completionRate = GetCompletionRate();
        if (completionRate >= 90)
        {
            insights.Add("You have exceptional task completion skills!");
        }
        else if (completionRate < 50)
        {
            insights.Add("Consider focusing on fewer tasks to improve completion rate.");
        }

        // Streak insights
        if (CurrentStreakDays >= 30)
        {
            insights.Add($"Amazing! You've maintained productivity for {CurrentStreakDays} days!");
        }
        else if (CurrentStreakDays == 0)
        {
            insights.Add("Start building your productivity streak today!");
        }

        // Category insights
        var mostProductiveCategory = GetMostProductiveCategory();
        if (!string.IsNullOrEmpty(mostProductiveCategory))
        {
            insights.Add($"You excel at {mostProductiveCategory} tasks!");
        }

        // Trend insights
        var trend = GetRecentProductivityTrend();
        if (trend == "Improving")
        {
            insights.Add("Your productivity is trending upward!");
        }
        else if (trend == "Declining")
        {
            insights.Add("Consider reviewing your workflow to boost productivity.");
        }

        return insights;
    }

    /// <summary>
    /// Calculates overall user score for gamification
    /// </summary>
    public int CalculateUserScore()
    {
        var baseScore = TotalTasksCompleted * 10;
        var streakBonus = LongestStreakDays * 50;
        var efficiencyBonus = (int)(OverallEfficiencyScore * 1000);
        var consistencyBonus = (int)(GetProductivityConsistency() * 10);

        return baseScore + streakBonus + efficiencyBonus + consistencyBonus;
    }

    private void RecalculateOverallEfficiency()
    {
        var completionFactor = GetCompletionRate() / 100.0;
        var overdueFactorPenalty = GetOverdueRate() / 100.0;
        var consistencyFactor = GetProductivityConsistency() / 100.0;

        OverallEfficiencyScore = Math.Max(0.0, Math.Min(1.0,
            (completionFactor * 0.5 + consistencyFactor * 0.3) * (1.0 - overdueFactorPenalty * 0.3)));
    }

    public override bool CanSoftDelete()
    {
        // User analytics should not be soft deleted to maintain historical data
        return false;
    }
}

/// <summary>
/// Enumeration of user experience levels based on analytics
/// </summary>
public enum UserExperienceLevel
{
    Beginner = 0,
    Developing = 1,
    Intermediate = 2,
    Advanced = 3,
    Expert = 4
}

/// <summary>
/// Domain event raised when user analytics are created
/// </summary>
public record UserAnalyticsCreatedEvent(UserAnalytics Analytics) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when personal best streak is updated
/// </summary>
public record PersonalBestStreakUpdatedEvent(UserAnalytics Analytics, int OldBest, int NewBest) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}
