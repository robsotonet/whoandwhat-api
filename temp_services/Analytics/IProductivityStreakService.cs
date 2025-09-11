using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Services.Analytics;

/// <summary>
/// Interface for productivity streak management services
/// </summary>
public interface IProductivityStreakService
{
    /// <summary>
    /// Gets current active streaks for a user
    /// </summary>
    public Task<List<ProductivityStreak>> GetActiveStreaksAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all streaks for a user (active and completed)
    /// </summary>
    public Task<List<ProductivityStreak>> GetAllStreaksAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new productivity streak
    /// </summary>
    public Task<ProductivityStreak> StartStreakAsync(Guid userId, StreakType streakType, DateTime startDate,
        Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates streak based on daily activity
    /// </summary>
    public Task<StreakUpdateResult> UpdateStreaksAsync(Guid userId, DateTime activityDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually extends a streak
    /// </summary>
    public Task<ProductivityStreak> ExtendStreakAsync(Guid streakId, DateTime activityDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Breaks a streak manually
    /// </summary>
    public Task<ProductivityStreak> BreakStreakAsync(Guid streakId, DateTime endDate, string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates streak eligibility based on daily metrics
    /// </summary>
    public Task<StreakEligibility> CheckStreakEligibilityAsync(Guid userId, DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets streak statistics and achievements for a user
    /// </summary>
    public Task<StreakStatistics> GetStreakStatisticsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets streak recommendations for motivation
    /// </summary>
    public Task<List<StreakRecommendation>> GetStreakRecommendationsAsync(Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Repairs broken streaks if user had qualifying activity
    /// </summary>
    public Task<List<ProductivityStreak>> RepairStreaksAsync(Guid userId, DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets streak leaderboard data
    /// </summary>
    public Task<List<StreakLeaderboardEntry>> GetStreakLeaderboardAsync(StreakType streakType, int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates motivational streak insights
    /// </summary>
    public Task<StreakInsights> GetStreakInsightsAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of streak update operation
/// </summary>
public record StreakUpdateResult
{
    public List<ProductivityStreak> ExtendedStreaks { get; init; } = new();
    public List<ProductivityStreak> BrokenStreaks { get; init; } = new();
    public List<ProductivityStreak> NewStreaks { get; init; } = new();
    public bool HasAnyChanges => ExtendedStreaks.Any() || BrokenStreaks.Any() || NewStreaks.Any();
    public List<string> Messages { get; init; } = new();
}

/// <summary>
/// Streak eligibility information
/// </summary>
public record StreakEligibility
{
    public DateTime Date { get; init; }
    public bool IsEligible { get; init; }
    public List<string> Reasons { get; init; } = new();
    public Dictionary<string, object> Metrics { get; init; } = new();
    public double EligibilityScore { get; init; }
    public string Recommendation { get; init; } = string.Empty;
}

/// <summary>
/// Comprehensive streak statistics
/// </summary>
public record StreakStatistics
{
    public Guid UserId { get; init; }
    public int TotalStreaks { get; init; }
    public int ActiveStreaks { get; init; }
    public int LongestStreakDays { get; init; }
    public ProductivityStreak? LongestStreak { get; init; }
    public int CurrentBestStreakDays { get; init; }
    public ProductivityStreak? CurrentBestStreak { get; init; }
    public Dictionary<string, int> StreaksByType { get; init; } = new();
    public Dictionary<string, int> AchievementLevels { get; init; } = new();
    public double AverageStreakLength { get; init; }
    public int TotalStreakDays { get; init; }
    public DateTime? LastStreakDate { get; init; }
    public int DaysSinceLastStreak { get; init; }
}

/// <summary>
/// Streak recommendation for user motivation
/// </summary>
public record StreakRecommendation
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public StreakType RecommendedType { get; init; } = StreakType.Daily;
    public int EstimatedDifficulty { get; init; } // 1-5 scale
    public List<string> Tips { get; init; } = new();
    public string MotivationalMessage { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Leaderboard entry for streak competition
/// </summary>
public record StreakLeaderboardEntry
{
    public Guid UserId { get; init; }
    public string? UserDisplayName { get; init; }
    public int StreakLength { get; init; }
    public StreakType StreakType { get; init; } = StreakType.Daily;
    public DateTime StartDate { get; init; }
    public bool IsActive { get; init; }
    public int Rank { get; init; }
    public StreakAchievementLevel AchievementLevel { get; init; }
}

/// <summary>
/// Motivational insights about user's streaks
/// </summary>
public record StreakInsights
{
    public Guid UserId { get; init; }
    public List<string> Achievements { get; init; } = new();
    public List<string> Motivations { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public Dictionary<string, object> PersonalBests { get; init; } = new();
    public string PrimaryStrengthArea { get; init; } = string.Empty;
    public string ImprovementArea { get; init; } = string.Empty;
    public int StreakPotentialScore { get; init; } // 0-100 scale
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
