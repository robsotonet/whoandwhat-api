using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Represents a user's scheduling preferences for smart scheduling optimization
/// </summary>
public class UserSchedulingPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Working Hours
    public TimeSpan WorkStartTime { get; set; } = TimeSpan.FromHours(9); // 9:00 AM
    public TimeSpan WorkEndTime { get; set; } = TimeSpan.FromHours(17); // 5:00 PM
    public string WorkingDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday"; // Comma-separated
    public TimeSpan LunchBreakStart { get; set; } = TimeSpan.FromHours(12); // 12:00 PM
    public TimeSpan LunchBreakDuration { get; set; } = TimeSpan.FromMinutes(60); // 1 hour
    public bool FlexibleSchedule { get; set; } = true;

    // Task Preferences
    public int MaxTasksPerTimeBlock { get; set; } = 3;
    public TimeSpan MinimumTaskDuration { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan MaximumTaskDuration { get; set; } = TimeSpan.FromHours(4);
    public string PreferredTaskCategories { get; set; } = string.Empty; // Comma-separated
    public bool AllowOverlappingTasks { get; set; } = false;
    public bool PreferMorningTasks { get; set; } = true;

    // Break and Buffer Preferences
    public bool RequireBufferTime { get; set; } = true;
    public TimeSpan BufferDuration { get; set; } = TimeSpan.FromMinutes(15);
    public string PreferredBreakTimes { get; set; } = "10:30,15:30"; // Comma-separated time spans

    // Productivity Patterns
    public int ProductivityPattern { get; set; } = 1; // Maps to ProductivityPatterns enum
    public bool MinimizeContextSwitching { get; set; } = true;
    public bool RespectDeadlines { get; set; } = true;
    public bool OptimizeForEnergyLevels { get; set; } = true;

    // Optimization Weights (0.0 to 1.0)
    public double ProductivityWeight { get; set; } = 0.7;
    public double BalanceWeight { get; set; } = 0.3;
    public double EfficiencyWeight { get; set; } = 0.8;

    // Machine Learning Data
    public DateTime? LastAnalyzed { get; set; }
    public string? HistoricalPatternData { get; set; } // JSON storage for ML patterns
    public double? ProductivityScore { get; set; }

    // Navigation Properties
    public ICollection<ScheduledItem> ScheduledItems { get; set; } = new List<ScheduledItem>();
    public ICollection<TimeBlock> TimeBlocks { get; set; } = new List<TimeBlock>();
    public ICollection<SchedulingPattern> SchedulingPatterns { get; set; } = new List<SchedulingPattern>();

    // Domain Methods

    /// <summary>
    /// Gets the user's working days as a list of DayOfWeek
    /// </summary>
    public List<DayOfWeek> GetWorkingDays()
    {
        if (string.IsNullOrEmpty(WorkingDays))
            return new List<DayOfWeek>();

        return WorkingDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(day => Enum.Parse<DayOfWeek>(day.Trim()))
            .ToList();
    }

    /// <summary>
    /// Sets the user's working days from a list of DayOfWeek
    /// </summary>
    public void SetWorkingDays(List<DayOfWeek> workingDays)
    {
        WorkingDays = string.Join(",", workingDays.Select(d => d.ToString()));
        MarkAsModified();
    }

    /// <summary>
    /// Gets the user's preferred break times as a list of TimeSpan
    /// </summary>
    public List<TimeSpan> GetPreferredBreakTimes()
    {
        if (string.IsNullOrEmpty(PreferredBreakTimes))
            return new List<TimeSpan>();

        return PreferredBreakTimes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(time => TimeSpan.Parse(time.Trim()))
            .ToList();
    }

    /// <summary>
    /// Sets the user's preferred break times from a list of TimeSpan
    /// </summary>
    public void SetPreferredBreakTimes(List<TimeSpan> breakTimes)
    {
        PreferredBreakTimes = string.Join(",", breakTimes.Select(t => t.ToString(@"hh\:mm")));
        MarkAsModified();
    }

    /// <summary>
    /// Gets the user's preferred task categories as a list of strings
    /// </summary>
    public List<string> GetPreferredTaskCategories()
    {
        if (string.IsNullOrEmpty(PreferredTaskCategories))
            return new List<string>();

        return PreferredTaskCategories
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(cat => cat.Trim())
            .ToList();
    }

    /// <summary>
    /// Sets the user's preferred task categories from a list of strings
    /// </summary>
    public void SetPreferredTaskCategories(List<string> categories)
    {
        PreferredTaskCategories = string.Join(",", categories);
        MarkAsModified();
    }

    /// <summary>
    /// Checks if the given time falls within working hours
    /// </summary>
    public bool IsWithinWorkingHours(TimeSpan time)
    {
        if (FlexibleSchedule)
        {
            // Allow some flexibility (+/- 2 hours)
            var flexStart = WorkStartTime.Subtract(TimeSpan.FromHours(2));
            var flexEnd = WorkEndTime.Add(TimeSpan.FromHours(2));
            return time >= flexStart && time <= flexEnd;
        }

        return time >= WorkStartTime && time <= WorkEndTime;
    }

    /// <summary>
    /// Checks if the given day is a working day
    /// </summary>
    public bool IsWorkingDay(DayOfWeek dayOfWeek)
    {
        return GetWorkingDays().Contains(dayOfWeek);
    }

    /// <summary>
    /// Gets the total daily working hours
    /// </summary>
    public TimeSpan GetDailyWorkingHours()
    {
        var totalTime = WorkEndTime - WorkStartTime;
        return totalTime.Subtract(LunchBreakDuration);
    }

    /// <summary>
    /// Updates the productivity score based on analysis
    /// </summary>
    public void UpdateProductivityScore(double score)
    {
        ProductivityScore = Math.Max(0.0, Math.Min(1.0, score));
        LastAnalyzed = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Updates historical pattern data for machine learning
    /// </summary>
    public void UpdateHistoricalPatterns(string patternData)
    {
        HistoricalPatternData = patternData;
        LastAnalyzed = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Checks if preferences need to be re-analyzed based on time threshold
    /// </summary>
    public bool NeedsReanalysis(TimeSpan threshold = default)
    {
        if (threshold == default)
            threshold = TimeSpan.FromDays(7); // Default: weekly analysis

        return !LastAnalyzed.HasValue ||
               DateTime.UtcNow - LastAnalyzed.Value > threshold;
    }

    /// <summary>
    /// Creates default scheduling preferences for a new user
    /// </summary>
    public static UserSchedulingPreference CreateDefault(Guid userId)
    {
        return new UserSchedulingPreference
        {
            UserId = userId,
            WorkStartTime = TimeSpan.FromHours(9),
            WorkEndTime = TimeSpan.FromHours(17),
            WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday",
            LunchBreakStart = TimeSpan.FromHours(12),
            LunchBreakDuration = TimeSpan.FromMinutes(60),
            FlexibleSchedule = true,
            MaxTasksPerTimeBlock = 3,
            MinimumTaskDuration = TimeSpan.FromMinutes(15),
            MaximumTaskDuration = TimeSpan.FromHours(4),
            PreferredTaskCategories = "ToDo,Project",
            AllowOverlappingTasks = false,
            PreferMorningTasks = true,
            RequireBufferTime = true,
            BufferDuration = TimeSpan.FromMinutes(15),
            PreferredBreakTimes = "10:30,15:30",
            ProductivityPattern = 1, // MorningPerson
            MinimizeContextSwitching = true,
            RespectDeadlines = true,
            OptimizeForEnergyLevels = true,
            ProductivityWeight = 0.7,
            BalanceWeight = 0.3,
            EfficiencyWeight = 0.8
        };
    }
}
