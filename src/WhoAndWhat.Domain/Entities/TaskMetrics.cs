using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Entity representing task completion and performance metrics for analytics
/// </summary>
public class TaskMetrics : BaseEntity
{
    public Guid UserId { get; private set; }
    public DateTime Date { get; private set; }
    public int CompletedTasks { get; private set; }
    public int OverdueTasks { get; private set; }
    public int TotalTasks { get; private set; }
    public int CreatedTasks { get; private set; }
    public double AverageCompletionTimeHours { get; private set; }
    public Dictionary<string, int> CategoryBreakdown { get; private set; }
    public Dictionary<string, int> PriorityBreakdown { get; private set; }
    public int ProductiveHours { get; private set; }
    public double EfficiencyScore { get; private set; }

    // Protected constructor for EF Core
    protected TaskMetrics()
    {
        CategoryBreakdown = new Dictionary<string, int>();
        PriorityBreakdown = new Dictionary<string, int>();
    }

    private TaskMetrics(Guid userId, DateTime date) : this()
    {
        UserId = userId;
        Date = date.Date; // Ensure we store only the date part
        CompletedTasks = 0;
        OverdueTasks = 0;
        TotalTasks = 0;
        CreatedTasks = 0;
        AverageCompletionTimeHours = 0.0;
        ProductiveHours = 0;
        EfficiencyScore = 0.0;
    }

    /// <summary>
    /// Creates a new TaskMetrics instance for the specified user and date
    /// </summary>
    public static TaskMetrics Create(Guid userId, DateTime date)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        var metrics = new TaskMetrics(userId, date);
        metrics.AddDomainEvent(new TaskMetricsCreatedEvent(metrics));
        return metrics;
    }

    /// <summary>
    /// Updates task completion metrics
    /// </summary>
    public void UpdateTaskCounts(int completed, int overdue, int total, int created)
    {
        CompletedTasks = Math.Max(0, completed);
        OverdueTasks = Math.Max(0, overdue);
        TotalTasks = Math.Max(0, total);
        CreatedTasks = Math.Max(0, created);

        CalculateEfficiencyScore();
        MarkAsModified();
    }

    /// <summary>
    /// Updates category breakdown metrics
    /// </summary>
    public void UpdateCategoryBreakdown(Dictionary<string, int> categoryBreakdown)
    {
        CategoryBreakdown = categoryBreakdown ?? new Dictionary<string, int>();
        MarkAsModified();
    }

    /// <summary>
    /// Updates priority breakdown metrics
    /// </summary>
    public void UpdatePriorityBreakdown(Dictionary<string, int> priorityBreakdown)
    {
        PriorityBreakdown = priorityBreakdown ?? new Dictionary<string, int>();
        MarkAsModified();
    }

    /// <summary>
    /// Updates average completion time
    /// </summary>
    public void UpdateAverageCompletionTime(double averageHours)
    {
        AverageCompletionTimeHours = Math.Max(0.0, averageHours);
        CalculateEfficiencyScore();
        MarkAsModified();
    }

    /// <summary>
    /// Updates productive hours for the day
    /// </summary>
    public void UpdateProductiveHours(int hours)
    {
        ProductiveHours = Math.Max(0, Math.Min(24, hours)); // Cap at 24 hours
        CalculateEfficiencyScore();
        MarkAsModified();
    }

    /// <summary>
    /// Gets completion rate as percentage
    /// </summary>
    public double GetCompletionRate()
    {
        return TotalTasks > 0 ? (double)CompletedTasks / TotalTasks * 100 : 0.0;
    }

    /// <summary>
    /// Gets overdue rate as percentage
    /// </summary>
    public double GetOverdueRate()
    {
        return TotalTasks > 0 ? (double)OverdueTasks / TotalTasks * 100 : 0.0;
    }

    /// <summary>
    /// Gets tasks per productive hour ratio
    /// </summary>
    public double GetTasksPerHour()
    {
        return ProductiveHours > 0 ? (double)CompletedTasks / ProductiveHours : 0.0;
    }

    /// <summary>
    /// Determines if this was a productive day based on metrics
    /// </summary>
    public bool IsProductiveDay()
    {
        var completionRate = GetCompletionRate();
        var overdueRate = GetOverdueRate();

        return completionRate >= 70.0 && overdueRate <= 20.0 && CompletedTasks > 0;
    }

    /// <summary>
    /// Gets the most productive category for the day
    /// </summary>
    public string? GetMostProductiveCategory()
    {
        return CategoryBreakdown.Count > 0
            ? CategoryBreakdown.OrderByDescending(c => c.Value).First().Key
            : null;
    }

    /// <summary>
    /// Gets productivity trend compared to previous metrics
    /// </summary>
    public string GetProductivityTrend(TaskMetrics? previousMetrics)
    {
        if (previousMetrics == null)
        {
            return "Unknown";
        }

        var currentScore = EfficiencyScore;
        var previousScore = previousMetrics.EfficiencyScore;

        if (currentScore > previousScore * 1.1)
        {
            return "Improving";
        }

        if (currentScore < previousScore * 0.9)
        {
            return "Declining";
        }

        return "Stable";
    }

    private void CalculateEfficiencyScore()
    {
        // Efficiency score combines completion rate, speed, and consistency
        var completionFactor = GetCompletionRate() / 100.0;
        var speedFactor = ProductiveHours > 0 ? Math.Min(GetTasksPerHour() / 2.0, 1.0) : 0.0;
        var overdueFactorPenalty = GetOverdueRate() / 100.0;

        EfficiencyScore = Math.Max(0.0, Math.Min(1.0,
            (completionFactor * 0.6 + speedFactor * 0.4) * (1.0 - overdueFactorPenalty * 0.5)));
    }

    /// <summary>
    /// Validates that the metrics make logical sense
    /// </summary>
    public bool IsValid()
    {
        return UserId != Guid.Empty
            && CompletedTasks >= 0
            && OverdueTasks >= 0
            && TotalTasks >= CompletedTasks
            && TotalTasks >= OverdueTasks
            && ProductiveHours >= 0 && ProductiveHours <= 24
            && EfficiencyScore >= 0.0 && EfficiencyScore <= 1.0;
    }

    public override bool CanSoftDelete()
    {
        // Analytics data should generally not be soft deleted to maintain history
        return false;
    }
}

/// <summary>
/// Domain event raised when new task metrics are created
/// </summary>
public record TaskMetricsCreatedEvent(TaskMetrics TaskMetrics) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when task metrics are updated significantly
/// </summary>
public record TaskMetricsUpdatedEvent(TaskMetrics TaskMetrics, string UpdateType) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}
