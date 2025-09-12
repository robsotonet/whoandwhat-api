using WhoAndWhat.Domain.Events;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

/// <summary>
/// Entity representing productivity streaks for user motivation and analytics
/// </summary>
public class ProductivityStreak : BaseEntity
{
    public Guid UserId { get; private set; }
    public StreakType StreakType { get; private set; }
    public int StreakLength { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public Dictionary<string, object> StreakMetadata { get; private set; }
    public int? PreviousBestLength { get; private set; }
    public DateTime? LastActivityDate { get; private set; }

    // Protected constructor for EF Core
    protected ProductivityStreak()
    {
        StreakMetadata = new Dictionary<string, object>();
        StreakType = StreakType.Daily; // Default value for EF Core
    }

    private ProductivityStreak(Guid userId, StreakType streakType, DateTime startDate) : this()
    {
        UserId = userId;
        StreakType = streakType;
        StartDate = startDate.Date;
        StreakLength = 1;
        IsActive = true;
        LastActivityDate = startDate.Date;
    }

    /// <summary>
    /// Creates a new productivity streak
    /// </summary>
    public static ProductivityStreak Create(Guid userId, StreakType streakType, DateTime startDate,
        Dictionary<string, object>? metadata = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        var streak = new ProductivityStreak(userId, streakType, startDate);
        if (metadata != null)
        {
            streak.StreakMetadata = metadata;
        }

        streak.AddDomainEvent(new ProductivityStreakStartedEvent(streak));
        return streak;
    }

    /// <summary>
    /// Extends the streak by one day
    /// </summary>
    public void ExtendStreak(DateTime activityDate)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Cannot extend an inactive streak");
        }

        var expectedNextDate = GetExpectedNextActivityDate();
        if (activityDate.Date != expectedNextDate.Date)
        {
            throw new ArgumentException($"Activity date {activityDate.Date:yyyy-MM-dd} does not match expected next date {expectedNextDate.Date:yyyy-MM-dd}");
        }

        StreakLength++;
        LastActivityDate = activityDate.Date;
        MarkAsModified();

        // Check if this is a new personal best
        if (PreviousBestLength == null || StreakLength > PreviousBestLength)
        {
            var oldBest = PreviousBestLength;
            PreviousBestLength = StreakLength;
            AddDomainEvent(new PersonalBestStreakAchievedEvent(this, oldBest, StreakLength));
        }

        AddDomainEvent(new ProductivityStreakExtendedEvent(this, StreakLength));
    }

    /// <summary>
    /// Breaks the streak and marks it as inactive
    /// </summary>
    public void BreakStreak(DateTime endDate, string? reason = null)
    {
        if (!IsActive)
        {
            return; // Already broken
        }

        IsActive = false;
        EndDate = endDate.Date;

        if (!string.IsNullOrEmpty(reason))
        {
            StreakMetadata["breakReason"] = reason;
        }

        MarkAsModified();
        AddDomainEvent(new ProductivityStreakBrokenEvent(this, StreakLength, reason));
    }

    /// <summary>
    /// Gets the expected next activity date for the streak
    /// </summary>
    public DateTime GetExpectedNextActivityDate()
    {
        if (!IsActive || LastActivityDate == null)
        {
            return StartDate.AddDays(1);
        }

        return StreakType switch
        {
            _ when StreakType == ValueObjects.StreakType.Daily => LastActivityDate.Value.AddDays(1),
            _ when StreakType == ValueObjects.StreakType.Weekly => LastActivityDate.Value.AddDays(7),
            _ when StreakType == ValueObjects.StreakType.Monthly => LastActivityDate.Value.AddMonths(1),
            _ => LastActivityDate.Value.AddDays(1)
        };
    }

    /// <summary>
    /// Checks if the streak should be considered broken based on current date
    /// </summary>
    public bool ShouldBeConsideredBroken(DateTime currentDate)
    {
        if (!IsActive)
        {
            return false;
        }

        var expectedDate = GetExpectedNextActivityDate();
        var gracePeriod = StreakType switch
        {
            _ when StreakType == ValueObjects.StreakType.Daily => TimeSpan.FromDays(1),
            _ when StreakType == ValueObjects.StreakType.Weekly => TimeSpan.FromDays(2), // 2-day grace period for weekly
            _ when StreakType == ValueObjects.StreakType.Monthly => TimeSpan.FromDays(7), // 1-week grace period for monthly
            _ => TimeSpan.FromDays(1)
        };

        return currentDate > expectedDate.Add(gracePeriod);
    }

    /// <summary>
    /// Gets the streak duration in days
    /// </summary>
    public int GetStreakDurationDays()
    {
        var endDateForCalculation = EndDate ?? DateTime.UtcNow.Date;
        return (endDateForCalculation - StartDate).Days + 1;
    }

    /// <summary>
    /// Gets achievement level based on streak length
    /// </summary>
    public StreakAchievementLevel GetAchievementLevel()
    {
        return StreakLength switch
        {
            >= 100 => StreakAchievementLevel.Legendary,
            >= 50 => StreakAchievementLevel.Master,
            >= 30 => StreakAchievementLevel.Expert,
            >= 14 => StreakAchievementLevel.Advanced,
            >= 7 => StreakAchievementLevel.Intermediate,
            >= 3 => StreakAchievementLevel.Beginner,
            _ => StreakAchievementLevel.Starter
        };
    }

    /// <summary>
    /// Gets next achievement milestone
    /// </summary>
    public (StreakAchievementLevel Level, int DaysNeeded) GetNextMilestone()
    {
        var currentLevel = GetAchievementLevel();

        return currentLevel switch
        {
            StreakAchievementLevel.Starter => (StreakAchievementLevel.Beginner, 3 - StreakLength),
            StreakAchievementLevel.Beginner => (StreakAchievementLevel.Intermediate, 7 - StreakLength),
            StreakAchievementLevel.Intermediate => (StreakAchievementLevel.Advanced, 14 - StreakLength),
            StreakAchievementLevel.Advanced => (StreakAchievementLevel.Expert, 30 - StreakLength),
            StreakAchievementLevel.Expert => (StreakAchievementLevel.Master, 50 - StreakLength),
            StreakAchievementLevel.Master => (StreakAchievementLevel.Legendary, 100 - StreakLength),
            StreakAchievementLevel.Legendary => (StreakAchievementLevel.Legendary, 0),
            _ => (StreakAchievementLevel.Beginner, 1)
        };
    }

    /// <summary>
    /// Gets motivational message based on current streak
    /// </summary>
    public string GetMotivationalMessage()
    {
        var level = GetAchievementLevel();
        var (nextLevel, daysNeeded) = GetNextMilestone();

        return level switch
        {
            StreakAchievementLevel.Starter => $"Great start! Keep going for {daysNeeded} more days to reach {nextLevel}!",
            StreakAchievementLevel.Beginner => $"You're building momentum! {daysNeeded} more days to reach {nextLevel}!",
            StreakAchievementLevel.Intermediate => $"Fantastic! You're on fire! {daysNeeded} more days to reach {nextLevel}!",
            StreakAchievementLevel.Advanced => $"Incredible dedication! {daysNeeded} more days to reach {nextLevel}!",
            StreakAchievementLevel.Expert => $"You're a productivity expert! {daysNeeded} more days to reach {nextLevel}!",
            StreakAchievementLevel.Master => $"Productivity master! {daysNeeded} more days to become legendary!",
            StreakAchievementLevel.Legendary => "Legendary productivity streak! You're an inspiration!",
            _ => "Keep up the great work!"
        };
    }

    /// <summary>
    /// Updates streak metadata
    /// </summary>
    public void UpdateMetadata(string key, object value)
    {
        StreakMetadata[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// Gets metadata value by key
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (StreakMetadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Calculates streak score for gamification
    /// </summary>
    public int CalculateStreakScore()
    {
        var baseScore = StreakLength * 10;
        var levelMultiplier = GetAchievementLevel() switch
        {
            StreakAchievementLevel.Legendary => 5.0,
            StreakAchievementLevel.Master => 4.0,
            StreakAchievementLevel.Expert => 3.0,
            StreakAchievementLevel.Advanced => 2.5,
            StreakAchievementLevel.Intermediate => 2.0,
            StreakAchievementLevel.Beginner => 1.5,
            _ => 1.0
        };

        return (int)(baseScore * levelMultiplier);
    }

    public override bool CanSoftDelete()
    {
        // Streak data should generally not be soft deleted to maintain history
        return false;
    }
}

/// <summary>
/// Domain event raised when a new productivity streak is started
/// </summary>
public record ProductivityStreakStartedEvent(ProductivityStreak Streak) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when a productivity streak is extended
/// </summary>
public record ProductivityStreakExtendedEvent(ProductivityStreak Streak, int NewLength) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when a productivity streak is broken
/// </summary>
public record ProductivityStreakBrokenEvent(ProductivityStreak Streak, int FinalLength, string? Reason) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when a personal best streak is achieved
/// </summary>
public record PersonalBestStreakAchievedEvent(ProductivityStreak Streak, int? PreviousBest, int NewBest) : IDomainEvent
{
    public DateTime DateOccurred { get; } = DateTime.UtcNow;
}
