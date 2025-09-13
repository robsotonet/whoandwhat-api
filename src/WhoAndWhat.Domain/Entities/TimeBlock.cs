using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Represents a time block for focused work and productivity optimization
/// </summary>
public class TimeBlock : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Time Block Details
    public string Title { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }

    // Purpose and Classification
    public int Purpose { get; set; } // Maps to TimeBlockPurpose enum
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; } = (int)Domain.ValueObjects.Priority.Medium;

    // Productivity Properties
    public double ProductivityScore { get; set; } = 0.0;
    public double EstimatedEffectiveness { get; set; } = 0.0;
    public string SuggestedActivities { get; set; } = string.Empty; // Comma-separated
    public string Reasoning { get; set; } = string.Empty;

    // Configuration
    public bool IsRecurring { get; set; } = false;
    public string? RecurrencePattern { get; set; } // JSON for recurrence rules
    public bool IsFlexible { get; set; } = true;
    public bool AllowInterruptions { get; set; } = false;

    // Tracking and Analytics
    public bool IsActive { get; set; } = false;
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? ActualDuration { get; set; }
    public double? ActualProductivityScore { get; set; }
    public int InterruptionCount { get; set; } = 0;

    // Machine Learning Data
    public string? PerformanceMetrics { get; set; } // JSON storage for ML analysis
    public DateTime? LastAnalyzed { get; set; }

    // Relationships
    public Guid? SchedulingPreferenceId { get; set; }
    public UserSchedulingPreference? SchedulingPreference { get; set; }

    public ICollection<ScheduledItem> ScheduledItems { get; set; } = new List<ScheduledItem>();

    // Domain Properties

    /// <summary>
    /// Gets whether the time block is currently in progress
    /// </summary>
    public bool IsInProgress => DateTime.UtcNow >= StartTime && DateTime.UtcNow <= EndTime && IsActive;

    /// <summary>
    /// Gets whether the time block is upcoming
    /// </summary>
    public bool IsUpcoming => StartTime > DateTime.UtcNow;

    /// <summary>
    /// Gets whether the time block is overdue (past end time and not completed)
    /// </summary>
    public bool IsOverdue => DateTime.UtcNow > EndTime && !IsCompleted;

    /// <summary>
    /// Gets the efficiency ratio (actual vs estimated duration)
    /// </summary>
    public double? EfficiencyRatio => ActualDuration.HasValue && Duration > TimeSpan.Zero
        ? ActualDuration.Value.TotalMinutes / Duration.TotalMinutes
        : null;

    /// <summary>
    /// Gets whether this is a deep work time block
    /// </summary>
    public bool IsDeepWork => Purpose == (int)TimeBlockPurpose.DeepWork ||
                              Purpose == (int)TimeBlockPurpose.Creative ||
                              Purpose == (int)TimeBlockPurpose.Learning;

    // Domain Methods

    /// <summary>
    /// Gets the suggested activities as a list of strings
    /// </summary>
    public List<string> GetSuggestedActivities()
    {
        if (string.IsNullOrEmpty(SuggestedActivities))
        {
            return new List<string>();
        }

        return SuggestedActivities
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(activity => activity.Trim())
            .ToList();
    }

    /// <summary>
    /// Sets the suggested activities from a list of strings
    /// </summary>
    public void SetSuggestedActivities(List<string> activities)
    {
        SuggestedActivities = string.Join(",", activities.Where(a => !string.IsNullOrWhiteSpace(a)));
        MarkAsModified();
    }

    /// <summary>
    /// Starts the time block and marks it as active
    /// </summary>
    public void Start()
    {
        if (IsActive || IsCompleted)
        {
            return;
        }

        IsActive = true;

        // Adjust start time if starting early/late
        if (Math.Abs((DateTime.UtcNow - StartTime).TotalMinutes) > 15)
        {
            StartTime = DateTime.UtcNow;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Completes the time block and records actual metrics
    /// </summary>
    public void Complete(double? productivityScore = null)
    {
        if (IsCompleted)
        {
            return;
        }

        IsActive = false;
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;

        // Calculate actual duration
        if (StartTime <= DateTime.UtcNow)
        {
            ActualDuration = DateTime.UtcNow - StartTime;
        }

        // Record productivity score if provided
        if (productivityScore.HasValue)
        {
            ActualProductivityScore = Math.Max(0.0, Math.Min(1.0, productivityScore.Value));
        }

        UpdatePerformanceMetrics();
        MarkAsModified();
    }

    /// <summary>
    /// Pauses the time block (for breaks or interruptions)
    /// </summary>
    public void Pause()
    {
        if (!IsActive || IsCompleted)
        {
            return;
        }

        IsActive = false;
        MarkAsModified();
    }

    /// <summary>
    /// Resumes a paused time block
    /// </summary>
    public void Resume()
    {
        if (IsActive || IsCompleted || IsOverdue)
        {
            return;
        }

        IsActive = true;
        MarkAsModified();
    }

    /// <summary>
    /// Records an interruption during the time block
    /// </summary>
    public void RecordInterruption()
    {
        InterruptionCount++;
        MarkAsModified();
    }

    /// <summary>
    /// Extends the time block duration
    /// </summary>
    public void ExtendDuration(TimeSpan extension)
    {
        if (extension <= TimeSpan.Zero)
        {
            throw new ArgumentException("Extension must be positive");
        }

        EndTime = EndTime.Add(extension);
        Duration = Duration.Add(extension);
        MarkAsModified();
    }

    /// <summary>
    /// Reschedules the time block to a new time slot
    /// </summary>
    public void Reschedule(DateTime newStartTime, DateTime newEndTime)
    {
        if (newStartTime >= newEndTime)
        {
            throw new ArgumentException("Start time must be before end time");
        }

        if (IsActive)
        {
            throw new InvalidOperationException("Cannot reschedule an active time block");
        }

        StartTime = newStartTime;
        EndTime = newEndTime;
        Duration = newEndTime - newStartTime;
        MarkAsModified();
    }

    /// <summary>
    /// Optimizes the time block based on historical performance
    /// </summary>
    public void OptimizeForPerformance()
    {
        var metrics = GetPerformanceMetrics();

        // Adjust duration based on historical data
        if (metrics.ContainsKey("averageEfficiency"))
        {
            var efficiency = (double)metrics["averageEfficiency"];
            if (efficiency < 0.8) // If typically running over
            {
                var suggestedExtension = TimeSpan.FromMinutes(Duration.TotalMinutes * 0.2);
                ExtendDuration(suggestedExtension);
            }
        }

        // Adjust productivity score based on historical performance
        if (metrics.ContainsKey("averageProductivity"))
        {
            EstimatedEffectiveness = (double)metrics["averageProductivity"];
        }

        LastAnalyzed = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Checks if this time block conflicts with another
    /// </summary>
    public bool ConflictsWith(TimeBlock other)
    {
        if (other == null || Id == other.Id)
        {
            return false;
        }

        return StartTime < other.EndTime && other.StartTime < EndTime;
    }

    /// <summary>
    /// Gets the optimal time for this type of time block based on research
    /// </summary>
    public TimeSpan GetOptimalStartTime()
    {
        return Purpose switch
        {
            (int)TimeBlockPurpose.DeepWork => TimeSpan.FromHours(9), // Morning for complex work
            (int)TimeBlockPurpose.Creative => TimeSpan.FromHours(16), // Afternoon for creativity
            (int)TimeBlockPurpose.Administrative => TimeSpan.FromHours(14), // After lunch
            (int)TimeBlockPurpose.Communication => TimeSpan.FromHours(10), // Mid-morning
            (int)TimeBlockPurpose.Learning => TimeSpan.FromHours(11), // Late morning
            (int)TimeBlockPurpose.Planning => TimeSpan.FromHours(8), // Early morning
            (int)TimeBlockPurpose.Break => TimeSpan.FromHours(15), // Mid-afternoon
            _ => TimeSpan.FromHours(10) // Default
        };
    }

    /// <summary>
    /// Updates performance metrics for machine learning
    /// </summary>
    private void UpdatePerformanceMetrics()
    {
        var metrics = GetPerformanceMetrics();

        var sessionData = new
        {
            Date = DateTime.UtcNow.Date,
            Purpose = Purpose,
            EstimatedDuration = Duration.TotalMinutes,
            ActualDuration = ActualDuration?.TotalMinutes,
            EstimatedProductivity = ProductivityScore,
            ActualProductivity = ActualProductivityScore,
            InterruptionCount = InterruptionCount,
            EfficiencyRatio = EfficiencyRatio,
            DayOfWeek = DateTime.UtcNow.DayOfWeek.ToString(),
            StartHour = StartTime.Hour
        };

        metrics["sessions"] = metrics.ContainsKey("sessions")
            ? ((List<object>)metrics["sessions"]).Concat(new[] { sessionData }).ToList()
            : new List<object> { sessionData };

        // Keep only last 30 sessions to prevent data bloat
        if (((List<object>)metrics["sessions"]).Count > 30)
        {
            ((List<object>)metrics["sessions"]).RemoveRange(0, ((List<object>)metrics["sessions"]).Count - 30);
        }

        PerformanceMetrics = System.Text.Json.JsonSerializer.Serialize(metrics);
    }

    /// <summary>
    /// Gets performance metrics as a dictionary
    /// </summary>
    private Dictionary<string, object> GetPerformanceMetrics()
    {
        if (string.IsNullOrEmpty(PerformanceMetrics))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(PerformanceMetrics)
                ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Creates a deep work time block
    /// </summary>
    public static TimeBlock CreateDeepWork(Guid userId, DateTime startTime, TimeSpan duration, string title = "Deep Work")
    {
        return new TimeBlock
        {
            UserId = userId,
            Title = title,
            Description = "Focused time for complex, cognitively demanding work",
            StartTime = startTime,
            EndTime = startTime.Add(duration),
            Duration = duration,
            Purpose = (int)TimeBlockPurpose.DeepWork,
            Category = "Productivity",
            Priority = (int)Domain.ValueObjects.Priority.High,
            ProductivityScore = 0.9,
            EstimatedEffectiveness = 0.85,
            SuggestedActivities = "Complex problem solving,Writing,Analysis,Design",
            Reasoning = "Optimized for high-cognitive demand tasks when mental energy is peak",
            IsFlexible = false,
            AllowInterruptions = false
        };
    }

    /// <summary>
    /// Creates a break time block
    /// </summary>
    public static TimeBlock CreateBreak(Guid userId, DateTime startTime, TimeSpan duration, string title = "Break")
    {
        return new TimeBlock
        {
            UserId = userId,
            Title = title,
            Description = "Scheduled break for mental recovery and productivity maintenance",
            StartTime = startTime,
            EndTime = startTime.Add(duration),
            Duration = duration,
            Purpose = (int)TimeBlockPurpose.Break,
            Category = "Wellness",
            Priority = (int)Domain.ValueObjects.Priority.Medium,
            ProductivityScore = 0.7,
            EstimatedEffectiveness = 0.8,
            SuggestedActivities = "Walk,Stretch,Hydrate,Mindfulness",
            Reasoning = "Scheduled break to maintain productivity and prevent burnout",
            IsFlexible = true,
            AllowInterruptions = true
        };
    }
}

/// <summary>
/// Enum for time block purposes to match the DTO definition
/// </summary>
public enum TimeBlockPurpose
{
    DeepWork = 0,
    Administrative = 1,
    Creative = 2,
    Planning = 3,
    Communication = 4,
    Learning = 5,
    Break = 6,
    Buffer = 7
}
