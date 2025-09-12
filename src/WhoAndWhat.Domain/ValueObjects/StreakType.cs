namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Value object representing different types of productivity streaks
/// </summary>
public record StreakType
{
    public static readonly StreakType Daily = new("Daily", "Daily productivity streak", 1);
    public static readonly StreakType Weekly = new("Weekly", "Weekly productivity streak", 7);
    public static readonly StreakType Monthly = new("Monthly", "Monthly productivity streak", 30);

    private static readonly IReadOnlyList<StreakType> AllTypes = new List<StreakType>
    {
        Daily, Weekly, Monthly
    };

    private static readonly IReadOnlyDictionary<string, StreakType> TypeByName =
        AllTypes.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public string Description { get; }
    public int IntervalDays { get; }

    private StreakType(string name, string description, int intervalDays)
    {
        Name = name;
        Description = description;
        IntervalDays = intervalDays;
    }

    /// <summary>
    /// Gets all available streak types
    /// </summary>
    public static IEnumerable<StreakType> GetAll() => AllTypes;

    /// <summary>
    /// Creates StreakType from name string
    /// </summary>
    public static StreakType FromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Streak type name cannot be null or empty", nameof(name));
        }

        if (TypeByName.TryGetValue(name.Trim(), out var streakType))
        {
            return streakType;
        }

        throw new ArgumentException($"Invalid streak type name: {name}", nameof(name));
    }

    /// <summary>
    /// Tries to create StreakType from name string
    /// </summary>
    public static bool TryFromName(string name, out StreakType? streakType)
    {
        streakType = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return TypeByName.TryGetValue(name.Trim(), out streakType);
    }

    /// <summary>
    /// Gets the display name for UI
    /// </summary>
    public string GetDisplayName() => Name;

    /// <summary>
    /// Gets the icon name for the streak type
    /// </summary>
    public string GetIconName() => this switch
    {
        _ when this == Daily => "calendar-day",
        _ when this == Weekly => "calendar-week",
        _ when this == Monthly => "calendar-month",
        _ => "calendar"
    };

    /// <summary>
    /// Gets the color code for the streak type
    /// </summary>
    public string GetColorCode() => this switch
    {
        _ when this == Daily => "#28a745",    // Green
        _ when this == Weekly => "#007bff",   // Blue  
        _ when this == Monthly => "#6f42c1",  // Purple
        _ => "#6c757d"                        // Gray
    };

    /// <summary>
    /// Gets the minimum days required to consider a streak significant
    /// </summary>
    public int GetMinimumSignificantLength() => this switch
    {
        _ when this == Daily => 3,
        _ when this == Weekly => 2,
        _ when this == Monthly => 2,
        _ => 3
    };

    /// <summary>
    /// Gets the maximum realistic streak length
    /// </summary>
    public int GetMaximumRealisticLength() => this switch
    {
        _ when this == Daily => 365,
        _ when this == Weekly => 52,
        _ when this == Monthly => 12,
        _ => 365
    };

    public override string ToString() => Name;
}

/// <summary>
/// Enumeration of streak achievement levels
/// </summary>
public enum StreakAchievementLevel
{
    Starter = 0,
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
    Expert = 4,
    Master = 5,
    Legendary = 6
}
