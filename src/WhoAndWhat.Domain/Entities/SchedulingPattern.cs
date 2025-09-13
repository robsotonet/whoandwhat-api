using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Represents a detected scheduling pattern for a user, used for machine learning and optimization
/// </summary>
public class SchedulingPattern : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Pattern Identification
    public string PatternName { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string PatternType { get; set; } = string.Empty; // "Productivity", "Energy", "Category", "Time", etc.

    // Pattern Metrics
    public double Frequency { get; set; } = 0.0; // How often this pattern occurs (0.0 to 1.0)
    public double Confidence { get; set; } = 0.0; // Confidence in pattern detection (0.0 to 1.0)
    public double ProductivityCorrelation { get; set; } = 0.0; // Correlation with productivity (-1.0 to 1.0)
    public double SuccessRate { get; set; } = 0.0; // Success rate when pattern is followed (0.0 to 1.0)

    // Temporal Properties
    public string PreferredTimes { get; set; } = string.Empty; // Comma-separated TimeSpan values
    public string AssociatedDays { get; set; } = string.Empty; // Comma-separated DayOfWeek values
    public TimeSpan? OptimalDuration { get; set; }
    public int? PreferredSequence { get; set; } // Order preference in daily schedule

    // Context and Categories
    public string AssociatedCategories { get; set; } = string.Empty; // Comma-separated task categories
    public string AssociatedTags { get; set; } = string.Empty; // Comma-separated tags
    public string EnvironmentalFactors { get; set; } = string.Empty; // JSON for environmental conditions

    // Machine Learning Data
    public string AnalysisData { get; set; } = string.Empty; // JSON storage for ML analysis details
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastReinforcedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastViolatedAt { get; set; }
    public int ReinforcementCount { get; set; } = 1;
    public int ViolationCount { get; set; } = 0;

    // Pattern Status
    public bool IsActive { get; set; } = true;
    public bool IsReliable { get; set; } = false; // True if pattern has high confidence and success rate
    public bool ShouldApplyToOptimization { get; set; } = true;

    // Relationships
    public Guid? SchedulingPreferenceId { get; set; }
    public UserSchedulingPreference? SchedulingPreference { get; set; }

    // Domain Properties

    /// <summary>
    /// Gets the pattern strength based on frequency, confidence, and success rate
    /// </summary>
    public double PatternStrength => (Frequency + Confidence + SuccessRate) / 3.0;

    /// <summary>
    /// Gets whether the pattern is statistically significant
    /// </summary>
    public bool IsStatisticallySignificant =>
        ReinforcementCount >= 5 &&
        Confidence >= 0.7 &&
        Frequency >= 0.3;

    /// <summary>
    /// Gets whether the pattern should be trusted for optimization
    /// </summary>
    public bool IsTrustworthy =>
        IsStatisticallySignificant &&
        SuccessRate >= 0.6 &&
        ViolationCount < ReinforcementCount * 0.3;

    /// <summary>
    /// Gets whether the pattern needs more data
    /// </summary>
    public bool NeedsMoreData => ReinforcementCount < 10 || Confidence < 0.6;

    // Domain Methods

    /// <summary>
    /// Gets the preferred times as a list of TimeSpan
    /// </summary>
    public List<TimeSpan> GetPreferredTimes()
    {
        if (string.IsNullOrEmpty(PreferredTimes))
        {
            return new List<TimeSpan>();
        }

        return PreferredTimes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(time => TimeSpan.Parse(time.Trim()))
            .ToList();
    }

    /// <summary>
    /// Sets the preferred times from a list of TimeSpan
    /// </summary>
    public void SetPreferredTimes(List<TimeSpan> times)
    {
        PreferredTimes = string.Join(",", times.Select(t => t.ToString(@"hh\:mm")));
        MarkAsModified();
    }

    /// <summary>
    /// Gets the associated days as a list of DayOfWeek
    /// </summary>
    public List<DayOfWeek> GetAssociatedDays()
    {
        if (string.IsNullOrEmpty(AssociatedDays))
        {
            return new List<DayOfWeek>();
        }

        return AssociatedDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(day => Enum.Parse<DayOfWeek>(day.Trim()))
            .ToList();
    }

    /// <summary>
    /// Sets the associated days from a list of DayOfWeek
    /// </summary>
    public void SetAssociatedDays(List<DayOfWeek> days)
    {
        AssociatedDays = string.Join(",", days.Select(d => d.ToString()));
        MarkAsModified();
    }

    /// <summary>
    /// Gets the associated categories as a list of strings
    /// </summary>
    public List<string> GetAssociatedCategories()
    {
        if (string.IsNullOrEmpty(AssociatedCategories))
        {
            return new List<string>();
        }

        return AssociatedCategories
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(cat => cat.Trim())
            .ToList();
    }

    /// <summary>
    /// Sets the associated categories from a list of strings
    /// </summary>
    public void SetAssociatedCategories(List<string> categories)
    {
        AssociatedCategories = string.Join(",", categories.Where(c => !string.IsNullOrWhiteSpace(c)));
        MarkAsModified();
    }

    /// <summary>
    /// Gets the associated tags as a list of strings
    /// </summary>
    public List<string> GetAssociatedTags()
    {
        if (string.IsNullOrEmpty(AssociatedTags))
        {
            return new List<string>();
        }

        return AssociatedTags
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToList();
    }

    /// <summary>
    /// Sets the associated tags from a list of strings
    /// </summary>
    public void SetAssociatedTags(List<string> tags)
    {
        AssociatedTags = string.Join(",", tags.Where(t => !string.IsNullOrWhiteSpace(t)));
        MarkAsModified();
    }

    /// <summary>
    /// Reinforces the pattern when it's followed successfully
    /// </summary>
    public void Reinforce(double productivityScore = 0.0)
    {
        ReinforcementCount++;
        LastReinforcedAt = DateTime.UtcNow;

        // Update metrics based on reinforcement
        Frequency = Math.Min(1.0, Frequency + 0.05);
        Confidence = Math.Min(1.0, Confidence + 0.02);

        if (productivityScore > 0)
        {
            // Update productivity correlation using exponential moving average
            var alpha = 0.1; // Smoothing factor
            ProductivityCorrelation = (alpha * productivityScore) + ((1 - alpha) * ProductivityCorrelation);
        }

        // Update success rate
        SuccessRate = (double)ReinforcementCount / (ReinforcementCount + ViolationCount);

        // Mark as reliable if it meets criteria
        if (IsStatisticallySignificant && SuccessRate >= 0.7)
        {
            IsReliable = true;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Records a violation when the pattern is not followed or fails
    /// </summary>
    public void RecordViolation()
    {
        ViolationCount++;
        LastViolatedAt = DateTime.UtcNow;

        // Decrease confidence and frequency slightly
        Confidence = Math.Max(0.0, Confidence - 0.05);
        Frequency = Math.Max(0.0, Frequency - 0.02);

        // Update success rate
        SuccessRate = (double)ReinforcementCount / (ReinforcementCount + ViolationCount);

        // Mark as unreliable if too many violations
        if (ViolationCount > ReinforcementCount * 0.5)
        {
            IsReliable = false;
        }

        // Deactivate if pattern becomes too unreliable
        if (SuccessRate < 0.3 && ReinforcementCount > 10)
        {
            IsActive = false;
            ShouldApplyToOptimization = false;
        }

        MarkAsModified();
    }

    /// <summary>
    /// Updates the pattern's analysis data with new machine learning insights
    /// </summary>
    public void UpdateAnalysisData(object analysisData)
    {
        AnalysisData = System.Text.Json.JsonSerializer.Serialize(analysisData);
        MarkAsModified();
    }

    /// <summary>
    /// Gets the analysis data as a dictionary
    /// </summary>
    public Dictionary<string, object> GetAnalysisData()
    {
        if (string.IsNullOrEmpty(AnalysisData))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(AnalysisData)
                ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Checks if the pattern applies to a given time and context
    /// </summary>
    public bool AppliesTo(DateTime time, string category = "", List<string>? tags = null)
    {
        if (!IsActive || !ShouldApplyToOptimization)
        {
            return false;
        }

        // Check day of week
        var associatedDays = GetAssociatedDays();
        if (associatedDays.Any() && !associatedDays.Contains(time.DayOfWeek))
        {
            return false;
        }

        // Check time of day
        var preferredTimes = GetPreferredTimes();
        if (preferredTimes.Any())
        {
            var timeOfDay = time.TimeOfDay;
            var tolerance = TimeSpan.FromMinutes(30); // 30-minute tolerance

            var timeMatches = preferredTimes.Any(pt =>
                Math.Abs((timeOfDay - pt).TotalMinutes) <= tolerance.TotalMinutes);

            if (!timeMatches)
            {
                return false;
            }
        }

        // Check category
        if (!string.IsNullOrEmpty(category))
        {
            var associatedCategories = GetAssociatedCategories();
            if (associatedCategories.Any() && !associatedCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check tags
        if (tags != null && tags.Any())
        {
            var associatedTags = GetAssociatedTags();
            if (associatedTags.Any() && !tags.Intersect(associatedTags, StringComparer.OrdinalIgnoreCase).Any())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the optimization weight for this pattern
    /// </summary>
    public double GetOptimizationWeight()
    {
        if (!ShouldApplyToOptimization)
        {
            return 0.0;
        }

        return PatternStrength * (IsReliable ? 1.2 : 0.8);
    }

    /// <summary>
    /// Creates a productivity pattern based on historical data
    /// </summary>
    public static SchedulingPattern CreateProductivityPattern(Guid userId, string patternName,
        List<TimeSpan> productiveTimes, List<string> categories, double averageProductivity)
    {
        return new SchedulingPattern
        {
            UserId = userId,
            PatternName = patternName,
            Description = $"High productivity pattern: {patternName}",
            PatternType = "Productivity",
            Frequency = 0.6,
            Confidence = 0.7,
            ProductivityCorrelation = averageProductivity,
            SuccessRate = 0.8,
            PreferredTimes = string.Join(",", productiveTimes.Select(t => t.ToString(@"hh\:mm"))),
            AssociatedCategories = string.Join(",", categories),
            OptimalDuration = TimeSpan.FromHours(2),
            IsActive = true,
            IsReliable = true,
            ShouldApplyToOptimization = true,
            ReinforcementCount = 10
        };
    }

    /// <summary>
    /// Creates an energy-based pattern
    /// </summary>
    public static SchedulingPattern CreateEnergyPattern(Guid userId, string energyLevel,
        List<TimeSpan> energyTimes, List<DayOfWeek> energyDays)
    {
        return new SchedulingPattern
        {
            UserId = userId,
            PatternName = $"{energyLevel} Energy Period",
            Description = $"Period of {energyLevel.ToLower()} energy levels for optimal task scheduling",
            PatternType = "Energy",
            Frequency = 0.8,
            Confidence = 0.9,
            ProductivityCorrelation = energyLevel.ToLower() == "high" ? 0.8 : 0.4,
            SuccessRate = 0.85,
            PreferredTimes = string.Join(",", energyTimes.Select(t => t.ToString(@"hh\:mm"))),
            AssociatedDays = string.Join(",", energyDays.Select(d => d.ToString())),
            IsActive = true,
            IsReliable = true,
            ShouldApplyToOptimization = true,
            ReinforcementCount = 15
        };
    }
}
