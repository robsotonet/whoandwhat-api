using Microsoft.Extensions.Logging;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Services.Analytics;

/// <summary>
/// Service for managing productivity streaks and related analytics
/// </summary>
public class ProductivityStreakService : IProductivityStreakService
{
    // Note: Repository access will be implemented through dependency injection in Infrastructure
    private readonly IAnalyticsCalculationService _calculationService;
    private readonly ILogger<ProductivityStreakService> _logger;

    public ProductivityStreakService(
        IAnalyticsCalculationService calculationService,
        ILogger<ProductivityStreakService> logger)
    {
        _calculationService = calculationService;
        _logger = logger;
    }

    public async Task<List<ProductivityStreak>> GetActiveStreaksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting active streaks for user {UserId}", userId);
            // TODO: Implement through repository injection
            await Task.CompletedTask;
            return new List<ProductivityStreak>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active streaks for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<ProductivityStreak>> GetAllStreaksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting all streaks for user {UserId}", userId);
            // TODO: Implement through repository injection
        await Task.CompletedTask;
        return new List<ProductivityStreak>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all streaks for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ProductivityStreak> StartStreakAsync(Guid userId, StreakType streakType, DateTime startDate,
        Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting new {StreakType} streak for user {UserId} on {StartDate}",
                streakType.Name, userId, startDate.Date);

            // Check if user already has an active streak of this type
            var existingActiveStreaks = await GetActiveStreaksAsync(userId, cancellationToken);
            var existingStreak = existingActiveStreaks.FirstOrDefault(s => s.StreakType.Name == streakType.Name);

            if (existingStreak != null)
            {
                _logger.LogWarning("User {UserId} already has an active {StreakType} streak", userId, streakType.Name);
                return existingStreak;
            }

            var streak = ProductivityStreak.Create(userId, streakType, startDate, metadata);
            // TODO: Implement through repository injection

            _logger.LogInformation("Created new productivity streak {StreakId} for user {UserId}", streak.Id, userId);
            return streak;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting streak for user {UserId}", userId);
            throw;
        }
    }

    public async Task<StreakUpdateResult> UpdateStreaksAsync(Guid userId, DateTime activityDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating streaks for user {UserId} on {ActivityDate}", userId, activityDate.Date);

            var result = new StreakUpdateResult();
            var messages = new List<string>();

            // Check eligibility for streak extension
            var eligibility = await CheckStreakEligibilityAsync(userId, activityDate, cancellationToken);
            if (!eligibility.IsEligible)
            {
                messages.Add($"Activity on {activityDate.Date:yyyy-MM-dd} does not qualify for streak extension: {string.Join(", ", eligibility.Reasons)}");
                return result with { Messages = messages };
            }

            // Get current active streaks
            var activeStreaks = await GetActiveStreaksAsync(userId, cancellationToken);
            var extendedStreaks = new List<ProductivityStreak>();
            var brokenStreaks = new List<ProductivityStreak>();
            var newStreaks = new List<ProductivityStreak>();

            // Process each streak type
            await ProcessDailyStreak(userId, activityDate, activeStreaks, extendedStreaks, newStreaks, brokenStreaks, messages, cancellationToken);
            await ProcessWeeklyStreak(userId, activityDate, activeStreaks, extendedStreaks, newStreaks, brokenStreaks, messages, cancellationToken);
            await ProcessMonthlyStreak(userId, activityDate, activeStreaks, extendedStreaks, newStreaks, brokenStreaks, messages, cancellationToken);

            // Save all changes
            foreach (var streak in extendedStreaks.Concat(brokenStreaks).Concat(newStreaks))
            {
                // TODO: Implement through repository injection
            }

            return new StreakUpdateResult
            {
                ExtendedStreaks = extendedStreaks,
                BrokenStreaks = brokenStreaks,
                NewStreaks = newStreaks,
                Messages = messages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating streaks for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ProductivityStreak> ExtendStreakAsync(Guid streakId, DateTime activityDate, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Implement through repository injection
        ProductivityStreak? streak = null;
            if (streak == null)
            {
                throw new InvalidOperationException($"Streak with ID {streakId} not found");
            }

            streak.ExtendStreak(activityDate);
            // TODO: Implement through repository injection

            _logger.LogInformation("Extended streak {StreakId} to length {Length}", streakId, streak.StreakLength);
            return streak;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending streak {StreakId}", streakId);
            throw;
        }
    }

    public async Task<ProductivityStreak> BreakStreakAsync(Guid streakId, DateTime endDate, string? reason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Implement through repository injection
        ProductivityStreak? streak = null;
            if (streak == null)
            {
                throw new InvalidOperationException($"Streak with ID {streakId} not found");
            }

            streak.BreakStreak(endDate, reason);
            // TODO: Implement through repository injection

            _logger.LogInformation("Broke streak {StreakId} with final length {Length}", streakId, streak.StreakLength);
            return streak;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error breaking streak {StreakId}", streakId);
            throw;
        }
    }

    public async Task<StreakEligibility> CheckStreakEligibilityAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            var dailyMetrics = await _calculationService.CalculateDailyMetricsAsync(userId, date, cancellationToken);
            var reasons = new List<string>();
            var metrics = new Dictionary<string, object>();
            var score = 0.0;

            // Basic productivity criteria
            var completionRate = dailyMetrics.GetCompletionRate();
            var isProductiveDay = dailyMetrics.IsProductiveDay();

            metrics["completionRate"] = completionRate;
            metrics["completedTasks"] = dailyMetrics.CompletedTasks;
            metrics["overdueTasks"] = dailyMetrics.OverdueTasks;
            metrics["isProductiveDay"] = isProductiveDay;
            metrics["efficiencyScore"] = dailyMetrics.EfficiencyScore;

            // Scoring criteria
            if (isProductiveDay)
            {
                score += 50;
                reasons.Add("Qualified as productive day");
            }

            if (dailyMetrics.CompletedTasks > 0)
            {
                score += 30;
                reasons.Add($"Completed {dailyMetrics.CompletedTasks} tasks");
            }

            if (completionRate >= 70)
            {
                score += 20;
                reasons.Add($"High completion rate ({completionRate:F1}%)");
            }

            var isEligible = score >= 50; // Minimum threshold
            var recommendation = GetEligibilityRecommendation(score, dailyMetrics);

            if (!isEligible)
            {
                if (dailyMetrics.CompletedTasks == 0)
                {
                    reasons.Add("No tasks completed");
                }

                if (completionRate < 50)
                {
                    reasons.Add("Low completion rate");
                }

                if (dailyMetrics.OverdueTasks > dailyMetrics.CompletedTasks)
                {
                    reasons.Add("More overdue than completed tasks");
                }
            }

            return new StreakEligibility
            {
                Date = date,
                IsEligible = isEligible,
                Reasons = reasons,
                Metrics = metrics,
                EligibilityScore = score,
                Recommendation = recommendation
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking streak eligibility for user {UserId}", userId);
            return new StreakEligibility { Date = date, IsEligible = false };
        }
    }

    public async Task<StreakStatistics> GetStreakStatisticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var allStreaks = await GetAllStreaksAsync(userId, cancellationToken);
            var activeStreaks = allStreaks.Where(s => s.IsActive).ToList();

            var longestStreak = allStreaks.OrderByDescending(s => s.StreakLength).FirstOrDefault();
            var currentBestStreak = activeStreaks.OrderByDescending(s => s.StreakLength).FirstOrDefault();

            var streaksByType = allStreaks
                .GroupBy(s => s.StreakType.Name)
                .ToDictionary(g => g.Key, g => g.Count());

            var achievementLevels = allStreaks
                .GroupBy(s => s.GetAchievementLevel())
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            var lastStreak = allStreaks
                .Where(s => !s.IsActive)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefault();

            return new StreakStatistics
            {
                UserId = userId,
                TotalStreaks = allStreaks.Count,
                ActiveStreaks = activeStreaks.Count,
                LongestStreakDays = longestStreak?.StreakLength ?? 0,
                LongestStreak = longestStreak,
                CurrentBestStreakDays = currentBestStreak?.StreakLength ?? 0,
                CurrentBestStreak = currentBestStreak,
                StreaksByType = streaksByType,
                AchievementLevels = achievementLevels,
                AverageStreakLength = allStreaks.Any() ? allStreaks.Average(s => s.StreakLength) : 0.0,
                TotalStreakDays = allStreaks.Sum(s => s.StreakLength),
                LastStreakDate = lastStreak?.EndDate,
                DaysSinceLastStreak = lastStreak?.EndDate != null
                    ? (DateTime.UtcNow.Date - lastStreak.EndDate.Value.Date).Days
                    : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting streak statistics for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<StreakRecommendation>> GetStreakRecommendationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await GetStreakStatisticsAsync(userId, cancellationToken);
            var userAnalytics = await _calculationService.CalculateUserAnalyticsAsync(userId, cancellationToken);
            var recommendations = new List<StreakRecommendation>();

            // Beginner recommendations
            if (statistics.TotalStreaks == 0)
            {
                recommendations.Add(new StreakRecommendation
                {
                    Title = "Start Your First Streak",
                    Description = "Begin with a simple daily productivity streak",
                    RecommendedType = StreakType.Daily,
                    EstimatedDifficulty = 1,
                    Tips = new List<string> { "Complete at least one task per day", "Set realistic daily goals", "Track your progress" },
                    MotivationalMessage = "Every expert was once a beginner!"
                });
            }

            // Recovery recommendations
            if (statistics.DaysSinceLastStreak > 7 && statistics.TotalStreaks > 0)
            {
                recommendations.Add(new StreakRecommendation
                {
                    Title = "Get Back on Track",
                    Description = $"It's been {statistics.DaysSinceLastStreak} days since your last streak",
                    RecommendedType = StreakType.Daily,
                    EstimatedDifficulty = 2,
                    Tips = new List<string> { "Start small with 1-2 tasks", "Focus on consistency", "Don't aim for perfection" },
                    MotivationalMessage = "The best time to restart is now!"
                });
            }

            // Challenge recommendations for experienced users
            if (statistics.LongestStreakDays >= 30)
            {
                recommendations.Add(new StreakRecommendation
                {
                    Title = "Challenge Yourself",
                    Description = "Try a weekly or monthly streak for bigger goals",
                    RecommendedType = StreakType.Weekly,
                    EstimatedDifficulty = 4,
                    Tips = new List<string> { "Plan weekly objectives", "Break down big goals", "Review progress mid-week" },
                    MotivationalMessage = "You've proven you can do it - time to level up!"
                });
            }

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting streak recommendations for user {UserId}", userId);
            return new List<StreakRecommendation>();
        }
    }

    public async Task<List<ProductivityStreak>> RepairStreaksAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Repairing streaks for user {UserId} from {StartDate} to {EndDate}", userId, startDate, endDate);

            var repairedStreaks = new List<ProductivityStreak>();
            var current = startDate;

            while (current <= endDate)
            {
                var eligibility = await CheckStreakEligibilityAsync(userId, current, cancellationToken);
                if (eligibility.IsEligible)
                {
                    var updateResult = await UpdateStreaksAsync(userId, current, cancellationToken);
                    repairedStreaks.AddRange(updateResult.ExtendedStreaks);
                    repairedStreaks.AddRange(updateResult.NewStreaks);
                }

                current = current.AddDays(1);
            }

            return repairedStreaks.Distinct().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error repairing streaks for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<StreakLeaderboardEntry>> GetStreakLeaderboardAsync(StreakType streakType, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically query across all users - simplified for now
            // TODO: Implement through repository injection
            var topStreaks = new List<(Guid UserId, int StreakLength, DateTime StartDate, bool IsActive)>();

            return topStreaks.Select((streak, index) => new StreakLeaderboardEntry
            {
                UserId = streak.UserId,
                UserDisplayName = $"User {streak.UserId.ToString()[..8]}", // Would normally get from user service
                StreakLength = streak.StreakLength,
                StreakType = streakType,
                StartDate = streak.StartDate,
                IsActive = streak.IsActive,
                Rank = index + 1,
                AchievementLevel = GetAchievementLevelForLength(streak.StreakLength)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting streak leaderboard for {StreakType}", streakType.Name);
            return new List<StreakLeaderboardEntry>();
        }
    }

    public async Task<StreakInsights> GetStreakInsightsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await GetStreakStatisticsAsync(userId, cancellationToken);
            var activeStreaks = await GetActiveStreaksAsync(userId, cancellationToken);

            var achievements = new List<string>();
            var motivations = new List<string>();
            var recommendations = new List<string>();
            var personalBests = new Dictionary<string, object>();

            // Generate achievements
            if (statistics.LongestStreakDays >= 100)
            {
                achievements.Add("Legendary streak achiever! 100+ day streak!");
            }
            else if (statistics.LongestStreakDays >= 30)
            {
                achievements.Add($"Impressive {statistics.LongestStreakDays}-day streak!");
            }

            if (statistics.TotalStreaks >= 10)
            {
                achievements.Add("Streak veteran with 10+ streaks!");
            }

            // Generate motivations
            if (activeStreaks.Any())
            {
                motivations.Add($"You have {activeStreaks.Count} active streak(s) - keep it up!");
            }
            else
            {
                motivations.Add("Ready to start a new streak? You've got this!");
            }

            // Generate recommendations
            var streakRecommendations = await GetStreakRecommendationsAsync(userId, cancellationToken);
            recommendations.AddRange(streakRecommendations.Take(3).Select(r => r.Description));

            // Personal bests
            personalBests["longestStreak"] = statistics.LongestStreakDays;
            personalBests["totalStreaks"] = statistics.TotalStreaks;
            personalBests["activeStreaks"] = statistics.ActiveStreaks;

            return new StreakInsights
            {
                UserId = userId,
                Achievements = achievements,
                Motivations = motivations,
                Recommendations = recommendations,
                PersonalBests = personalBests,
                PrimaryStrengthArea = GetPrimaryStrengthArea(statistics),
                ImprovementArea = GetImprovementArea(statistics),
                StreakPotentialScore = CalculateStreakPotential(statistics)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting streak insights for user {UserId}", userId);
            throw;
        }
    }

    #region Private Helper Methods

    private async Task ProcessDailyStreak(Guid userId, DateTime activityDate, List<ProductivityStreak> activeStreaks,
        List<ProductivityStreak> extendedStreaks, List<ProductivityStreak> newStreaks, List<ProductivityStreak> brokenStreaks,
        List<string> messages, CancellationToken cancellationToken)
    {
        var dailyStreak = activeStreaks.FirstOrDefault(s => s.StreakType.Name == StreakType.Daily.Name);

        if (dailyStreak == null)
        {
            // Start new daily streak
            var newStreak = await StartStreakAsync(userId, StreakType.Daily, activityDate, null, cancellationToken);
            newStreaks.Add(newStreak);
            messages.Add("Started new daily productivity streak!");
        }
        else
        {
            try
            {
                if (!dailyStreak.ShouldBeConsideredBroken(activityDate))
                {
                    dailyStreak.ExtendStreak(activityDate);
                    extendedStreaks.Add(dailyStreak);
                    messages.Add($"Extended daily streak to {dailyStreak.StreakLength} days!");
                }
                else
                {
                    dailyStreak.BreakStreak(activityDate.AddDays(-1), "Gap in daily activity");
                    brokenStreaks.Add(dailyStreak);

                    // Start new streak
                    var newStreak = await StartStreakAsync(userId, StreakType.Daily, activityDate, null, cancellationToken);
                    newStreaks.Add(newStreak);
                    messages.Add("Previous daily streak ended, started new streak!");
                }
            }
            catch (ArgumentException)
            {
                // Streak extension failed - likely due to gap
                messages.Add("Daily streak cannot be extended due to gap in activity");
            }
        }
    }

    private async Task ProcessWeeklyStreak(Guid userId, DateTime activityDate, List<ProductivityStreak> activeStreaks,
        List<ProductivityStreak> extendedStreaks, List<ProductivityStreak> newStreaks, List<ProductivityStreak> brokenStreaks,
        List<string> messages, CancellationToken cancellationToken)
    {
        var weeklyStreak = activeStreaks.FirstOrDefault(s => s.StreakType.Name == StreakType.Weekly.Name);
        var weeklyMetrics = await _calculationService.CalculateWeeklyMetricsAsync(userId, GetWeekStart(activityDate), cancellationToken);
        var weekQualifies = (int)weeklyMetrics["productiveDays"] >= 4; // At least 4 productive days in week

        if (!weekQualifies)
        {
            return;
        }

        if (weeklyStreak == null)
        {
            var newStreak = await StartStreakAsync(userId, StreakType.Weekly, GetWeekStart(activityDate), null, cancellationToken);
            newStreaks.Add(newStreak);
            messages.Add("Started new weekly productivity streak!");
        }
        else
        {
            try
            {
                if (!weeklyStreak.ShouldBeConsideredBroken(activityDate))
                {
                    weeklyStreak.ExtendStreak(GetWeekStart(activityDate));
                    extendedStreaks.Add(weeklyStreak);
                    messages.Add($"Extended weekly streak to {weeklyStreak.StreakLength} weeks!");
                }
            }
            catch (ArgumentException)
            {
                messages.Add("Weekly streak cannot be extended");
            }
        }
    }

    private async Task ProcessMonthlyStreak(Guid userId, DateTime activityDate, List<ProductivityStreak> activeStreaks,
        List<ProductivityStreak> extendedStreaks, List<ProductivityStreak> newStreaks, List<ProductivityStreak> brokenStreaks,
        List<string> messages, CancellationToken cancellationToken)
    {
        var monthlyStreak = activeStreaks.FirstOrDefault(s => s.StreakType.Name == StreakType.Monthly.Name);
        var monthStart = new DateTime(activityDate.Year, activityDate.Month, 1);
        var monthlyMetrics = await _calculationService.CalculateMonthlyMetricsAsync(userId, activityDate.Year, activityDate.Month, cancellationToken);
        var monthQualifies = (double)monthlyMetrics["monthlyCompletionRate"] >= 60.0;

        if (!monthQualifies || activityDate.Day < 28) // Only evaluate near month end
        {
            return;
        }

        if (monthlyStreak == null)
        {
            var newStreak = await StartStreakAsync(userId, StreakType.Monthly, monthStart, null, cancellationToken);
            newStreaks.Add(newStreak);
            messages.Add("Started new monthly productivity streak!");
        }
        else
        {
            try
            {
                if (!monthlyStreak.ShouldBeConsideredBroken(activityDate))
                {
                    monthlyStreak.ExtendStreak(monthStart);
                    extendedStreaks.Add(monthlyStreak);
                    messages.Add($"Extended monthly streak to {monthlyStreak.StreakLength} months!");
                }
            }
            catch (ArgumentException)
            {
                messages.Add("Monthly streak cannot be extended");
            }
        }
    }

    private DateTime GetWeekStart(DateTime date)
    {
        var daysToSubtract = (int)date.DayOfWeek;
        return date.AddDays(-daysToSubtract).Date;
    }

    private string GetEligibilityRecommendation(double score, TaskMetrics metrics)
    {
        return score switch
        {
            >= 80 => "Excellent productivity! Perfect for extending streaks.",
            >= 60 => "Good productivity day with room for improvement.",
            >= 40 => "Moderate productivity - consider completing more tasks.",
            _ => "Focus on completing at least one task to qualify for streaks."
        };
    }

    private string GetPrimaryStrengthArea(StreakStatistics stats)
    {
        if (stats.StreaksByType.Count == 0)
        {
            return "Getting started";
        }

        var topStreakType = stats.StreaksByType.OrderByDescending(s => s.Value).First().Key;
        return $"{topStreakType} productivity";
    }

    private string GetImprovementArea(StreakStatistics stats)
    {
        if (stats.ActiveStreaks == 0)
        {
            return "Starting new streaks";
        }

        if (stats.AverageStreakLength < 7)
        {
            return "Maintaining longer streaks";
        }

        if (stats.StreaksByType.Count == 1)
        {
            return "Diversifying streak types";
        }

        return "Consistency maintenance";
    }

    private int CalculateStreakPotential(StreakStatistics stats)
    {
        var potential = 0;

        // Base potential from experience
        potential += Math.Min(stats.TotalStreaks * 5, 30);

        // Bonus for current activity
        potential += stats.ActiveStreaks * 10;

        // Bonus for long streaks
        if (stats.LongestStreakDays >= 30)
        {
            potential += 25;
        }
        else if (stats.LongestStreakDays >= 7)
        {
            potential += 15;
        }

        // Consistency bonus
        if (stats.AverageStreakLength >= 14)
        {
            potential += 20;
        }

        // Recent activity penalty
        if (stats.DaysSinceLastStreak > 30)
        {
            potential = Math.Max(10, potential - 25);
        }

        return Math.Min(100, Math.Max(0, potential));
    }

    /// <summary>
    /// Helper method to calculate achievement level based on streak length
    /// </summary>
    private static StreakAchievementLevel GetAchievementLevelForLength(int length)
    {
        return length switch
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

    #endregion
}

