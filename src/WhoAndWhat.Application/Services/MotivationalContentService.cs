using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Repositories.Analytics;

namespace WhoAndWhat.Application.Services;

/// <summary>
/// Service for managing and delivering personalized motivational content
/// </summary>
public class MotivationalContentService : IMotivationalContentService
{
    private readonly IRepository<MotivationalContent> _contentRepository;
    private readonly IRepository<ContentDeliveryLog> _deliveryLogRepository;
    private readonly IRepository<UserContentPreferences> _preferencesRepository;
    private readonly IRepository<UserAnalytics> _analyticsRepository;
    private readonly IRepository<ProductivityStreak> _streakRepository;
    private readonly ILogger<MotivationalContentService> _logger;

    // A/B test groups and their weights
    private readonly Dictionary<string, double> _defaultABTestWeights = new()
    {
        ["control"] = 0.5,
        ["variant_a"] = 0.25,
        ["variant_b"] = 0.25
    };

    public MotivationalContentService(
        IRepository<MotivationalContent> contentRepository,
        IRepository<ContentDeliveryLog> deliveryLogRepository,
        IRepository<UserContentPreferences> preferencesRepository,
        IRepository<UserAnalytics> analyticsRepository,
        IRepository<ProductivityStreak> streakRepository,
        ILogger<MotivationalContentService> logger)
    {
        _contentRepository = contentRepository ?? throw new ArgumentNullException(nameof(contentRepository));
        _deliveryLogRepository = deliveryLogRepository ?? throw new ArgumentNullException(nameof(deliveryLogRepository));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _analyticsRepository = analyticsRepository ?? throw new ArgumentNullException(nameof(analyticsRepository));
        _streakRepository = streakRepository ?? throw new ArgumentNullException(nameof(streakRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PersonalizedContentResult?> GetPersonalizedContentAsync(Guid userId, 
        ContentSelectionContext? contentContext = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting personalized content for user {UserId}", userId);

            // Get user preferences
            var preferences = await GetOrCreateUserPreferencesAsync(userId, cancellationToken);
            if (!preferences.IsContentEnabled)
            {
                _logger.LogDebug("Content disabled for user {UserId}", userId);
                return null;
            }

            // Check content limits
            if (await HasReachedContentLimitsAsync(userId, ContentLimitTimeWindow.Daily, cancellationToken))
            {
                _logger.LogDebug("User {UserId} has reached daily content limits", userId);
                return null;
            }

            // Get user analytics for personalization
            var userAnalytics = await _analyticsRepository.GetByConditionAsync(
                ua => ua.UserId == userId, cancellationToken);
            
            var userStreak = await _streakRepository.GetByConditionAsync(
                ps => ps.UserId == userId && ps.IsActive, cancellationToken);

            // Build user context for content matching
            var userContext = BuildUserContext(userAnalytics, userStreak, preferences);

            // Get recent content to avoid repetition
            var recentContentIds = new HashSet<Guid>();
            if (contentContext?.ExcludeRecentContent == true)
            {
                var recentWindow = contentContext.RecentContentWindow ?? TimeSpan.FromHours(24);
                var recentLogs = await _deliveryLogRepository.GetAllByConditionAsync(
                    dl => dl.UserId == userId && dl.DeliveredAt > DateTime.UtcNow.Subtract(recentWindow),
                    cancellationToken);
                
                recentContentIds = recentLogs.Select(dl => dl.MotivationalContentId).ToHashSet();
            }

            // Get all active content
            var allContent = await _contentRepository.GetAllByConditionAsync(
                c => c.IsCurrentlyActive() && !recentContentIds.Contains(c.Id),
                cancellationToken);

            // Filter and score content
            var scoredContent = new List<(MotivationalContent Content, double Score, string ReasonCode)>();

            foreach (var content in allContent)
            {
                var (score, reasonCode) = CalculateContentScore(content, userContext, contentContext);
                if (score > 0.1) // Minimum threshold
                {
                    scoredContent.Add((content, score, reasonCode));
                }
            }

            // Select best content
            var selectedContent = scoredContent
                .OrderByDescending(sc => sc.Score)
                .FirstOrDefault();

            if (selectedContent.Content == null)
            {
                _logger.LogDebug("No suitable content found for user {UserId}", userId);
                return null;
            }

            // Assign A/B test group if enabled
            string? abTestGroup = null;
            if (selectedContent.Content.IsABTestEnabled)
            {
                abTestGroup = await AssignABTestGroupAsync(userId, selectedContent.Content.ABTestGroup, cancellationToken);
            }

            // Build result
            var result = new PersonalizedContentResult
            {
                Content = selectedContent.Content.ToDisplay(),
                PersonalizationScore = selectedContent.Score,
                ReasonCode = selectedContent.ReasonCode,
                ABTestGroup = abTestGroup,
                RecommendedChannel = GetRecommendedChannel(preferences, contentContext),
                OptimalDeliveryTime = preferences.GetNextPreferredDeliveryTime(),
                PersonalizationFactors = ExtractPersonalizationFactors(userContext, selectedContent.Content)
            };

            _logger.LogDebug("Selected content {ContentId} for user {UserId} with score {Score}",
                selectedContent.Content.Id, userId, selectedContent.Score);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting personalized content for user {UserId}", userId);
            return null;
        }
    }

    public async Task<IEnumerable<PersonalizedContentResult>> GetPersonalizedContentBatchAsync(Guid userId, 
        int maxItems, 
        ContentSelectionContext? contentContext = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = new List<PersonalizedContentResult>();
            var excludedIds = new HashSet<Guid>();

            // Get multiple content items, avoiding duplicates
            for (int i = 0; i < maxItems; i++)
            {
                // Create context that excludes previously selected items
                var batchContext = contentContext ?? new ContentSelectionContext();
                batchContext.AdditionalContext["excludedIds"] = excludedIds.ToList();

                var content = await GetPersonalizedContentAsync(userId, batchContext, cancellationToken);
                if (content == null)
                {
                    break; // No more suitable content
                }

                results.Add(content);
                excludedIds.Add(content.Content.Id);
            }

            _logger.LogDebug("Retrieved {Count} personalized content items for user {UserId}", results.Count, userId);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting personalized content batch for user {UserId}", userId);
            return Enumerable.Empty<PersonalizedContentResult>();
        }
    }

    public async Task<bool> LogContentEngagementAsync(Guid userId, 
        Guid contentId, 
        ContentEngagementType engagementType,
        Guid? deliveryLogId = null,
        Dictionary<string, object>? engagementMetadata = null,
        TimeSpan? viewDuration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ContentDeliveryLog? deliveryLog;

            if (deliveryLogId.HasValue)
            {
                // Update existing delivery log
                deliveryLog = await _deliveryLogRepository.GetByIdAsync(deliveryLogId.Value, cancellationToken);
                if (deliveryLog == null)
                {
                    _logger.LogWarning("Delivery log {DeliveryLogId} not found for engagement", deliveryLogId.Value);
                    return false;
                }
            }
            else
            {
                // Find most recent delivery log for this user and content
                deliveryLog = await _deliveryLogRepository.GetByConditionAsync(
                    dl => dl.UserId == userId && dl.MotivationalContentId == contentId,
                    cancellationToken);

                if (deliveryLog == null)
                {
                    _logger.LogWarning("No delivery log found for user {UserId} and content {ContentId}", userId, contentId);
                    return false;
                }
            }

            // Record engagement
            deliveryLog.RecordEngagement(engagementType, engagementMetadata, viewDuration);
            await _deliveryLogRepository.UpdateAsync(deliveryLog, cancellationToken);

            // Update user preferences based on engagement
            await UpdateUserEngagementHistoryAsync(userId, contentId, engagementType, cancellationToken);

            _logger.LogDebug("Recorded {EngagementType} engagement for user {UserId} on content {ContentId}",
                engagementType, userId, contentId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging content engagement for user {UserId} and content {ContentId}",
                userId, contentId);
            return false;
        }
    }

    public async Task<Guid> RecordContentDeliveryAsync(Guid userId, 
        Guid contentId, 
        ContentDeliveryChannel deliveryChannel,
        Dictionary<string, object>? deliveryContext = null,
        double? personalizationScore = null,
        string? abTestGroup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deliveryLog = ContentDeliveryLog.Create(userId, contentId, deliveryChannel, abTestGroup, deliveryContext);

            if (personalizationScore.HasValue)
            {
                deliveryLog.MarkAsPersonalized(personalizationScore.Value);
            }

            await _deliveryLogRepository.AddAsync(deliveryLog, cancellationToken);

            // Update user's last content delivery time
            var preferences = await GetOrCreateUserPreferencesAsync(userId, cancellationToken);
            preferences.RecordContentDelivery(DateTime.UtcNow);
            await _preferencesRepository.UpdateAsync(preferences, cancellationToken);

            _logger.LogDebug("Recorded content delivery {DeliveryId} for user {UserId} and content {ContentId}",
                deliveryLog.Id, userId, contentId);

            return deliveryLog.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording content delivery for user {UserId} and content {ContentId}",
                userId, contentId);
            return Guid.Empty;
        }
    }

    public async Task<string> AssignABTestGroupAsync(Guid userId, 
        string testName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use consistent hashing to assign users to groups
            var userHash = ComputeUserHash(userId, testName);
            var groups = _defaultABTestWeights.Keys.ToArray();
            var weights = _defaultABTestWeights.Values.ToArray();

            var selectedGroup = SelectGroupByWeight(userHash, groups, weights);

            _logger.LogDebug("Assigned user {UserId} to A/B test group '{Group}' for test '{TestName}'",
                userId, selectedGroup, testName);

            return selectedGroup;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning A/B test group for user {UserId} and test {TestName}",
                userId, testName);
            return "control"; // Default fallback
        }
    }

    public async Task<ABTestMetrics> GetABTestMetricsAsync(string testName, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deliveryLogs = await _deliveryLogRepository.GetAllByConditionAsync(
                dl => dl.ABTestGroup != null && 
                      dl.ABTestGroup.Contains(testName) && 
                      dl.DeliveredAt >= startDate && 
                      dl.DeliveredAt <= endDate,
                cancellationToken);

            var groupMetrics = new Dictionary<string, ABTestGroupMetrics>();

            foreach (var group in deliveryLogs.GroupBy(dl => dl.ABTestGroup))
            {
                if (group.Key == null) continue;

                var logs = group.ToList();
                var engagedLogs = logs.Where(l => l.EngagementType.HasValue).ToList();

                var metrics = new ABTestGroupMetrics
                {
                    GroupName = group.Key,
                    TotalDeliveries = logs.Count,
                    UniqueUsers = logs.Select(l => l.UserId).Distinct().Count(),
                    TotalEngagements = engagedLogs.Count,
                    EngagementRate = logs.Count > 0 ? (double)engagedLogs.Count / logs.Count : 0,
                    AverageEngagementScore = engagedLogs.Count > 0 ? engagedLogs.Average(l => l.GetEngagementScore()) : 0,
                    AverageEngagementLatency = engagedLogs.Count > 0 
                        ? TimeSpan.FromTicks((long)engagedLogs.Where(l => l.GetEngagementLatency().HasValue)
                            .Average(l => l.GetEngagementLatency()!.Value.Ticks))
                        : TimeSpan.Zero,
                    EngagementTypeBreakdown = engagedLogs
                        .GroupBy(l => l.EngagementType!.Value.ToString())
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                groupMetrics[group.Key] = metrics;
            }

            // Determine winning group and statistical significance
            var winningGroup = groupMetrics.Count > 0 
                ? groupMetrics.OrderByDescending(kvp => kvp.Value.EngagementRate).First()
                : default;

            var statisticalSignificance = CalculateStatisticalSignificance(groupMetrics);

            var abTestMetrics = new ABTestMetrics
            {
                TestName = testName,
                StartDate = startDate,
                EndDate = endDate,
                GroupMetrics = groupMetrics,
                WinningGroup = winningGroup.Key,
                StatisticalSignificance = statisticalSignificance,
                Recommendation = GenerateABTestRecommendation(groupMetrics, statisticalSignificance)
            };

            _logger.LogDebug("Generated A/B test metrics for test '{TestName}' with {GroupCount} groups",
                testName, groupMetrics.Count);

            return abTestMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting A/B test metrics for test {TestName}", testName);
            return new ABTestMetrics { TestName = testName, StartDate = startDate, EndDate = endDate };
        }
    }

    public async Task<ContentPerformanceMetrics> GetContentPerformanceAsync(Guid contentId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deliveryLogs = await _deliveryLogRepository.GetAllByConditionAsync(
                dl => dl.MotivationalContentId == contentId && 
                      dl.DeliveredAt >= startDate && 
                      dl.DeliveredAt <= endDate,
                cancellationToken);

            var engagedLogs = deliveryLogs.Where(l => l.EngagementType.HasValue).ToList();
            var personalizedLogs = deliveryLogs.Where(l => l.WasPersonalized).ToList();

            var metrics = new ContentPerformanceMetrics
            {
                ContentId = contentId,
                StartDate = startDate,
                EndDate = endDate,
                TotalDeliveries = deliveryLogs.Count(),
                UniqueUsers = deliveryLogs.Select(l => l.UserId).Distinct().Count(),
                TotalEngagements = engagedLogs.Count,
                EngagementRate = deliveryLogs.Any() ? (double)engagedLogs.Count / deliveryLogs.Count() : 0,
                AverageEngagementScore = engagedLogs.Any() ? engagedLogs.Average(l => l.GetEngagementScore()) : 0,
                AveragePersonalizationScore = personalizedLogs.Any() 
                    ? personalizedLogs.Where(l => l.PersonalizationScore.HasValue)
                        .Average(l => l.PersonalizationScore!.Value) 
                    : 0,
                AverageEngagementLatency = engagedLogs.Any() 
                    ? TimeSpan.FromTicks((long)engagedLogs.Where(l => l.GetEngagementLatency().HasValue)
                        .Average(l => l.GetEngagementLatency()!.Value.Ticks))
                    : TimeSpan.Zero,
                ChannelPerformance = deliveryLogs
                    .GroupBy(l => l.DeliveryChannel.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                EngagementTypeBreakdown = engagedLogs
                    .GroupBy(l => l.EngagementType!.Value.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                Trend = AnalyzeContentTrend(deliveryLogs.ToList())
            };

            _logger.LogDebug("Generated content performance metrics for content {ContentId}", contentId);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content performance for content {ContentId}", contentId);
            return new ContentPerformanceMetrics { ContentId = contentId, StartDate = startDate, EndDate = endDate };
        }
    }

    public async Task<bool> UpdateUserPreferencesFromEngagementAsync(Guid userId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var preferences = await GetOrCreateUserPreferencesAsync(userId, cancellationToken);
            
            // Get recent engagement history
            var recentLogs = await _deliveryLogRepository.GetAllByConditionAsync(
                dl => dl.UserId == userId && 
                      dl.DeliveredAt > DateTime.UtcNow.AddDays(-30) &&
                      dl.EngagementType.HasValue,
                cancellationToken);

            if (!recentLogs.Any())
            {
                return false; // No engagement data to learn from
            }

            // Analyze engagement patterns by content type
            var contentTypeEngagement = new Dictionary<MotivationalContentType, List<double>>();
            
            foreach (var log in recentLogs)
            {
                var content = await _contentRepository.GetByIdAsync(log.MotivationalContentId, cancellationToken);
                if (content != null)
                {
                    if (!contentTypeEngagement.ContainsKey(content.ContentType))
                    {
                        contentTypeEngagement[content.ContentType] = new List<double>();
                    }
                    contentTypeEngagement[content.ContentType].Add(log.GetEngagementScore());
                }
            }

            // Update engagement history in preferences
            foreach (var kvp in contentTypeEngagement)
            {
                var averageScore = kvp.Value.Average();
                preferences.UpdateEngagementHistory($"score_{kvp.Key}", averageScore);
            }

            await _preferencesRepository.UpdateAsync(preferences, cancellationToken);

            _logger.LogDebug("Updated user preferences from engagement for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user preferences from engagement for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> HasReachedContentLimitsAsync(Guid userId, 
        ContentLimitTimeWindow timeWindow, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var preferences = await GetOrCreateUserPreferencesAsync(userId, cancellationToken);
            
            var windowStart = timeWindow switch
            {
                ContentLimitTimeWindow.Daily => DateTime.UtcNow.Date,
                ContentLimitTimeWindow.Weekly => DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek),
                ContentLimitTimeWindow.Monthly => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
                _ => DateTime.UtcNow.Date
            };

            var deliveryCount = await _deliveryLogRepository.CountByConditionAsync(
                dl => dl.UserId == userId && dl.DeliveredAt >= windowStart,
                cancellationToken);

            var limit = timeWindow switch
            {
                ContentLimitTimeWindow.Daily => preferences.MaxDailyContent,
                ContentLimitTimeWindow.Weekly => preferences.MaxWeeklyContent,
                ContentLimitTimeWindow.Monthly => preferences.MaxWeeklyContent * 4, // Approximate monthly limit
                _ => preferences.MaxDailyContent
            };

            return deliveryCount >= limit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking content limits for user {UserId}", userId);
            return false; // Allow content delivery on error
        }
    }

    public async Task<double> GetContentRecommendationScoreAsync(Guid userId, 
        Guid contentId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await _contentRepository.GetByIdAsync(contentId, cancellationToken);
            if (content == null)
            {
                return 0.0;
            }

            var preferences = await GetOrCreateUserPreferencesAsync(userId, cancellationToken);
            var userAnalytics = await _analyticsRepository.GetByConditionAsync(
                ua => ua.UserId == userId, cancellationToken);

            var userContext = BuildUserContext(userAnalytics, null, preferences);
            var (score, _) = CalculateContentScore(content, userContext, null);

            return score;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content recommendation score for user {UserId} and content {ContentId}",
                userId, contentId);
            return 0.0;
        }
    }

    public async Task<int> OptimizeContentForEngagementAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // This is a placeholder for content optimization algorithms
            // In a full implementation, this would analyze historical engagement data
            // to automatically optimize content targeting, timing, and personalization
            
            _logger.LogInformation("Starting content optimization process");
            
            // Placeholder implementation - would include:
            // 1. Analyze historical engagement patterns
            // 2. Identify high/low performing content
            // 3. Adjust targeting conditions for better performance
            // 4. Update A/B test configurations based on results
            
            await Task.CompletedTask; // Placeholder
            
            _logger.LogInformation("Content optimization completed - 0 items optimized (placeholder implementation)");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during content optimization");
            return 0;
        }
    }

    // Private helper methods

    private async Task<UserContentPreferences> GetOrCreateUserPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preferences = await _preferencesRepository.GetByConditionAsync(
            p => p.UserId == userId, cancellationToken);

        if (preferences == null)
        {
            preferences = UserContentPreferences.CreateDefault(userId);
            await _preferencesRepository.AddAsync(preferences, cancellationToken);
        }

        return preferences;
    }

    private Dictionary<string, object> BuildUserContext(UserAnalytics? analytics, ProductivityStreak? streak, UserContentPreferences preferences)
    {
        var context = new Dictionary<string, object>();

        if (analytics != null)
        {
            context["completionRate"] = analytics.GetCompletionRate();
            context["experienceLevel"] = analytics.GetExperienceLevel();
            context["overallEfficiency"] = analytics.OverallEfficiencyScore;
            context["mostProductiveCategory"] = analytics.GetMostProductiveCategory() ?? "Unknown";
            context["productivityTrend"] = analytics.GetRecentProductivityTrend();
        }

        if (streak != null)
        {
            context["currentStreak"] = streak.StreakLength;
            context["achievementLevel"] = streak.GetAchievementLevel();
        }
        else
        {
            context["currentStreak"] = 0;
            context["achievementLevel"] = StreakAchievementLevel.Starter;
        }

        context["contentFrequency"] = preferences.PreferredFrequency;
        context["preferredTypes"] = preferences.PreferredContentTypes.ToList();
        context["preferredCategories"] = preferences.PreferredCategories.ToList();

        return context;
    }

    private (double Score, string ReasonCode) CalculateContentScore(
        MotivationalContent content, 
        Dictionary<string, object> userContext, 
        ContentSelectionContext? selectionContext)
    {
        double score = 0.5; // Base score
        var reasons = new List<string>();

        // Check if content matches user conditions
        if (!content.MatchesUserConditions(userContext))
        {
            return (0.0, "No user match");
        }

        // Content type preference
        if (userContext.TryGetValue("preferredTypes", out var preferredTypesObj) && 
            preferredTypesObj is List<MotivationalContentType> preferredTypes)
        {
            if (preferredTypes.Contains(content.ContentType))
            {
                score += 0.2;
                reasons.Add("Preferred type");
            }
        }

        // Category preference
        if (userContext.TryGetValue("preferredCategories", out var preferredCategoriesObj) &&
            preferredCategoriesObj is List<ContentCategory> preferredCategories)
        {
            if (preferredCategories.Contains(content.Category))
            {
                score += 0.15;
                reasons.Add("Preferred category");
            }
        }

        // Context-specific boost
        if (selectionContext?.PreferredType.HasValue == true && 
            content.ContentType == selectionContext.PreferredType.Value)
        {
            score += 0.1;
            reasons.Add("Context match");
        }

        // Priority boost
        score += content.Priority * 0.01; // Small boost for higher priority content

        // Streak-specific logic
        if (userContext.TryGetValue("currentStreak", out var streakObj) && streakObj is int streak)
        {
            if (content.ContentType == MotivationalContentType.Streak && streak > 0)
            {
                score += 0.15;
                reasons.Add("Active streak");
            }
            else if (content.ContentType == MotivationalContentType.Encouragement && streak == 0)
            {
                score += 0.1;
                reasons.Add("No streak encouragement");
            }
        }

        // Experience level matching
        if (userContext.TryGetValue("experienceLevel", out var levelObj) && levelObj is UserExperienceLevel level)
        {
            var targetLevel = content.GetTargetCondition<UserExperienceLevel>("experiencelevel");
            if (targetLevel != UserExperienceLevel.Beginner && level >= targetLevel)
            {
                score += 0.1;
                reasons.Add("Experience match");
            }
        }

        return (Math.Max(0.0, Math.Min(1.0, score)), string.Join(", ", reasons));
    }

    private ContentDeliveryChannel GetRecommendedChannel(UserContentPreferences preferences, ContentSelectionContext? context)
    {
        if (context?.DeliveryChannel.HasValue == true && preferences.PreferredChannels.Contains(context.DeliveryChannel.Value))
        {
            return context.DeliveryChannel.Value;
        }

        return preferences.PreferredChannels.FirstOrDefault();
    }

    private Dictionary<string, object> ExtractPersonalizationFactors(Dictionary<string, object> userContext, MotivationalContent content)
    {
        var factors = new Dictionary<string, object>();

        foreach (var kvp in userContext)
        {
            factors[$"user_{kvp.Key}"] = kvp.Value;
        }

        factors["content_type"] = content.ContentType.ToString();
        factors["content_category"] = content.Category.ToString();
        factors["content_priority"] = content.Priority;

        return factors;
    }

    private async Task UpdateUserEngagementHistoryAsync(Guid userId, Guid contentId, ContentEngagementType engagementType, CancellationToken cancellationToken)
    {
        try
        {
            var preferences = await GetOrCreateUserPreferencesAsync(userId, cancellationToken);
            var content = await _contentRepository.GetByIdAsync(contentId, cancellationToken);

            if (content != null)
            {
                var engagementScore = GetEngagementScore(engagementType);
                var currentScore = preferences.GetEngagementHistory<double>($"score_{content.ContentType}") ?? 0.5;
                
                // Weighted average with more weight on recent engagement
                var newScore = (currentScore * 0.7) + (engagementScore * 0.3);
                preferences.UpdateEngagementHistory($"score_{content.ContentType}", newScore);
                
                await _preferencesRepository.UpdateAsync(preferences, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user engagement history for user {UserId}", userId);
        }
    }

    private double GetEngagementScore(ContentEngagementType engagementType)
    {
        return engagementType switch
        {
            ContentEngagementType.Dismissed => 0.1,
            ContentEngagementType.Viewed => 0.3,
            ContentEngagementType.Clicked => 0.6,
            ContentEngagementType.Shared => 0.8,
            ContentEngagementType.ActionTaken => 1.0,
            _ => 0.0
        };
    }

    private int ComputeUserHash(Guid userId, string testName)
    {
        var input = $"{userId}_{testName}";
        return Math.Abs(input.GetHashCode());
    }

    private string SelectGroupByWeight(int hash, string[] groups, double[] weights)
    {
        var normalizedHash = (hash % 1000) / 1000.0;
        var cumulativeWeight = 0.0;

        for (int i = 0; i < groups.Length; i++)
        {
            cumulativeWeight += weights[i];
            if (normalizedHash <= cumulativeWeight)
            {
                return groups[i];
            }
        }

        return groups.Last(); // Fallback
    }

    private double? CalculateStatisticalSignificance(Dictionary<string, ABTestGroupMetrics> groupMetrics)
    {
        // Simplified statistical significance calculation
        // In a full implementation, this would use proper statistical tests (Chi-square, t-test, etc.)
        
        if (groupMetrics.Count < 2)
        {
            return null;
        }

        var groups = groupMetrics.Values.ToList();
        var maxRate = groups.Max(g => g.EngagementRate);
        var minRate = groups.Min(g => g.EngagementRate);
        var avgDeliveries = groups.Average(g => g.TotalDeliveries);

        // Very simplified significance approximation
        if (avgDeliveries > 100 && (maxRate - minRate) > 0.05)
        {
            return 0.95; // Assume significant if large sample and meaningful difference
        }

        return avgDeliveries > 50 ? 0.8 : 0.5;
    }

    private string GenerateABTestRecommendation(Dictionary<string, ABTestGroupMetrics> groupMetrics, double? significance)
    {
        if (!groupMetrics.Any())
        {
            return "Insufficient data for recommendation";
        }

        var winner = groupMetrics.OrderByDescending(kvp => kvp.Value.EngagementRate).First();
        var isSignificant = significance >= 0.95;

        if (isSignificant)
        {
            return $"Recommend implementing '{winner.Key}' variant - statistically significant improvement";
        }
        else
        {
            return $"Continue testing - '{winner.Key}' shows promise but needs more data for statistical significance";
        }
    }

    private ContentPerformanceTrend AnalyzeContentTrend(List<ContentDeliveryLog> logs)
    {
        if (logs.Count < 10)
        {
            return new ContentPerformanceTrend
            {
                TrendDirection = "Stable",
                TrendMagnitude = 0.0,
                TrendConfidence = "Low"
            };
        }

        // Simple trend analysis based on chronological engagement rates
        var sortedLogs = logs.OrderBy(l => l.DeliveredAt).ToList();
        var midPoint = sortedLogs.Count / 2;
        
        var firstHalf = sortedLogs.Take(midPoint);
        var secondHalf = sortedLogs.Skip(midPoint);

        var firstHalfEngagement = firstHalf.Count(l => l.EngagementType.HasValue) / (double)firstHalf.Count();
        var secondHalfEngagement = secondHalf.Count(l => l.EngagementType.HasValue) / (double)secondHalf.Count();

        var change = secondHalfEngagement - firstHalfEngagement;
        var magnitude = Math.Abs(change);

        return new ContentPerformanceTrend
        {
            TrendDirection = change > 0.05 ? "Improving" : change < -0.05 ? "Declining" : "Stable",
            TrendMagnitude = magnitude,
            TrendConfidence = magnitude > 0.1 ? "High" : magnitude > 0.05 ? "Medium" : "Low"
        };
    }
}