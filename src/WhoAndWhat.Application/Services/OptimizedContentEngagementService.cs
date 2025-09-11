using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Services;

/// <summary>
/// Complete implementation of OptimizeContentForEngagementAsync method
/// This shows how the placeholder method should be implemented
/// </summary>
public static class OptimizedContentEngagementService
{
    /// <summary>
    /// Optimizes motivational content for better engagement based on historical data
    /// </summary>
    /// <param name="contentRepository">Repository for motivational content</param>
    /// <param name="deliveryRepository">Repository for content delivery logs</param>
    /// <param name="preferencesRepository">Repository for user preferences</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of content items optimized</returns>
    public static async Task<int> OptimizeContentForEngagementAsync(
        IRepository<MotivationalContent> contentRepository,
        IRepository<ContentDeliveryLog> deliveryRepository,
        IRepository<UserContentPreferences> preferencesRepository,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting comprehensive content optimization process");
            
            var optimizedCount = 0;
            var cutoffDate = DateTime.UtcNow.AddDays(-30); // Analyze last 30 days
            
            // Step 1: Analyze engagement patterns for all active content
            var activeContent = await contentRepository.FindAsync(
                c => c.IsActive && !c.IsDeleted, cancellationToken);
            
            logger.LogInformation("Analyzing engagement patterns for {Count} active content items", activeContent.Count());
            
            foreach (var content in activeContent)
            {
                var wasOptimized = await OptimizeIndividualContentAsync(
                    content, contentRepository, deliveryRepository, cutoffDate, logger, cancellationToken);
                
                if (wasOptimized)
                {
                    optimizedCount++;
                }
            }
            
            // Step 2: Optimize underperforming content targeting
            optimizedCount += await OptimizeTargetingConditionsAsync(
                activeContent.ToList(), deliveryRepository, cutoffDate, logger, cancellationToken);
            
            // Step 3: Optimize delivery timing based on user engagement patterns
            optimizedCount += await OptimizeDeliveryTimingAsync(
                deliveryRepository, preferencesRepository, cutoffDate, logger, cancellationToken);
            
            // Step 4: Update A/B test configurations based on results
            optimizedCount += await OptimizeABTestConfigurationsAsync(
                activeContent.ToList(), deliveryRepository, cutoffDate, logger, cancellationToken);
            
            logger.LogInformation("Content optimization completed - {Count} optimizations applied", optimizedCount);
            return optimizedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during content optimization process");
            return 0;
        }
    }
    
    /// <summary>
    /// Optimizes individual content item based on engagement metrics
    /// </summary>
    private static async Task<bool> OptimizeIndividualContentAsync(
        MotivationalContent content,
        IRepository<MotivationalContent> contentRepository,
        IRepository<ContentDeliveryLog> deliveryRepository,
        DateTime cutoffDate,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get engagement data for this content
            var deliveryLogs = await deliveryRepository.FindAsync(
                log => log.MotivationalContentId == content.Id && log.DeliveredAt >= cutoffDate,
                cancellationToken);
            
            if (!deliveryLogs.Any())
            {
                return false; // No data to optimize
            }
            
            var totalDeliveries = deliveryLogs.Count();
            var engagedDeliveries = deliveryLogs.Count(log => log.EngagementType.HasValue);
            var engagementRate = totalDeliveries > 0 ? (double)engagedDeliveries / totalDeliveries : 0.0;
            
            var wasOptimized = false;
            
            // Optimization Logic 1: Adjust priority based on engagement
            var optimalPriority = CalculateOptimalPriority(engagementRate, content.Priority);
            if (Math.Abs(optimalPriority - content.Priority) > 5)
            {
                var oldPriority = content.Priority;
                content.SetPriority(optimalPriority);
                wasOptimized = true;
                logger.LogDebug("Updated priority for content {ContentId}: {OldPriority} → {NewPriority} (Engagement: {Rate:P1})",
                    content.Id, oldPriority, optimalPriority, engagementRate);
            }
            
            // Optimization Logic 2: Adjust targeting based on user segment performance
            var segmentPerformance = AnalyzeSegmentPerformance(deliveryLogs);
            if (ShouldUpdateTargeting(segmentPerformance, content.TargetConditions))
            {
                var optimizedTargeting = OptimizeTargeting(segmentPerformance, content.TargetConditions);
                content.SetTargetConditions(optimizedTargeting);
                wasOptimized = true;
                logger.LogDebug("Updated targeting conditions for content {ContentId} based on segment performance", content.Id);
            }
            
            // Optimization Logic 3: Disable consistently poor performers
            if (engagementRate < 0.1 && totalDeliveries > 50) // Less than 10% engagement with significant data
            {
                content.Deactivate();
                wasOptimized = true;
                logger.LogInformation("Deactivated low-performing content {ContentId} (Engagement: {Rate:P1})", 
                    content.Id, engagementRate);
            }
            
            if (wasOptimized)
            {
                await contentRepository.UpdateAsync(content, cancellationToken);
            }
            
            return wasOptimized;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to optimize content {ContentId}", content.Id);
            return false;
        }
    }
    
    /// <summary>
    /// Optimizes targeting conditions across all content
    /// </summary>
    private static async Task<int> OptimizeTargetingConditionsAsync(
        ICollection<MotivationalContent> activeContent,
        IRepository<ContentDeliveryLog> deliveryRepository,
        DateTime cutoffDate,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Optimizing targeting conditions across content library");
        
        var optimizedCount = 0;
        
        // Analyze targeting effectiveness across experience levels
        var experienceLevelPerformance = await AnalyzeExperienceLevelPerformanceAsync(
            deliveryRepository, cutoffDate, cancellationToken);
        
        foreach (var content in activeContent)
        {
            if (content.TargetConditions.ContainsKey("experienceLevel"))
            {
                var currentLevel = content.TargetConditions["experienceLevel"];
                if (ShouldAdjustExperienceTargeting(currentLevel, experienceLevelPerformance))
                {
                    var optimizedConditions = new Dictionary<string, object>(content.TargetConditions);
                    optimizedConditions["experienceLevel"] = GetOptimalExperienceLevel(experienceLevelPerformance);
                    
                    content.SetTargetConditions(optimizedConditions);
                    optimizedCount++;
                    
                    logger.LogDebug("Optimized experience level targeting for content {ContentId}", content.Id);
                }
            }
        }
        
        return optimizedCount;
    }
    
    /// <summary>
    /// Optimizes delivery timing based on user engagement patterns
    /// </summary>
    private static async Task<int> OptimizeDeliveryTimingAsync(
        IRepository<ContentDeliveryLog> deliveryRepository,
        IRepository<UserContentPreferences> preferencesRepository,
        DateTime cutoffDate,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Optimizing content delivery timing patterns");
        
        // Analyze when users are most likely to engage
        var deliveryLogs = await deliveryRepository.FindAsync(
            log => log.DeliveredAt >= cutoffDate && log.EngagementType.HasValue,
            cancellationToken);
        
        if (!deliveryLogs.Any())
        {
            return 0;
        }
        
        // Group by hour of day and calculate engagement rates
        var hourlyEngagement = deliveryLogs
            .GroupBy(log => log.DeliveredAt.Hour)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    TotalDeliveries = g.Count(),
                    EngagementRate = g.Count(log => log.EngagementType.HasValue) / (double)g.Count()
                });
        
        // Identify optimal delivery hours
        var optimalHours = hourlyEngagement
            .Where(h => h.Value.EngagementRate > 0.3 && h.Value.TotalDeliveries > 10)
            .OrderByDescending(h => h.Value.EngagementRate)
            .Take(6) // Top 6 hours
            .Select(h => h.Key)
            .ToList();
        
        if (!optimalHours.Any())
        {
            return 0;
        }
        
        // Update user preferences with optimal delivery times
        var allPreferences = await preferencesRepository.GetAllAsync(cancellationToken);
        var optimizedCount = 0;
        
        foreach (var preferences in allPreferences)
        {
            var currentOptimalHours = GetCurrentOptimalHours(preferences);
            if (!ListsAreEquivalent(currentOptimalHours, optimalHours))
            {
                UpdateOptimalDeliveryHours(preferences, optimalHours);
                await preferencesRepository.UpdateAsync(preferences, cancellationToken);
                optimizedCount++;
            }
        }
        
        logger.LogInformation("Updated delivery timing for {Count} user preferences. Optimal hours: {Hours}",
            optimizedCount, string.Join(", ", optimalHours.OrderBy(h => h)));
        
        return optimizedCount;
    }
    
    /// <summary>
    /// Optimizes A/B test configurations based on statistical results
    /// </summary>
    private static async Task<int> OptimizeABTestConfigurationsAsync(
        ICollection<MotivationalContent> activeContent,
        IRepository<ContentDeliveryLog> deliveryRepository,
        DateTime cutoffDate,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Optimizing A/B test configurations");
        
        var optimizedCount = 0;
        var abTestContents = activeContent.Where(c => c.IsABTestEnabled).ToList();
        
        foreach (var content in abTestContents)
        {
            var testLogs = await deliveryRepository.FindAsync(
                log => log.MotivationalContentId == content.Id && 
                       log.DeliveredAt >= cutoffDate && 
                       !string.IsNullOrEmpty(log.ABTestGroup),
                cancellationToken);
                
            if (testLogs.Count() < 100) // Need minimum sample size
            {
                continue;
            }
            
            var groupResults = testLogs
                .GroupBy(log => log.ABTestGroup)
                .ToDictionary<IGrouping<string?, ContentDeliveryLog>, string, object>(
                    g => g.Key ?? "unknown",
                    g => new
                    {
                        Deliveries = g.Count(),
                        Engagements = g.Count(log => log.EngagementType.HasValue),
                        EngagementRate = g.Count(log => log.EngagementType.HasValue) / (double)g.Count()
                    });
            
            // Find statistically significant winner
            var winner = FindStatisticalWinner(groupResults);
            if (winner != null)
            {
                // Update content to use winning variant - configure A/B test with winner
                content.ConfigureABTest(winner, new Dictionary<string, object>
                {
                    ["winningGroup"] = winner,
                    ["optimizedAt"] = DateTime.UtcNow
                });
                optimizedCount++;
                
                logger.LogInformation("A/B test winner identified for content {ContentId}: Group {Winner}",
                    content.Id, winner);
            }
        }
        
        return optimizedCount;
    }
    
    // Helper Methods
    private static int CalculateOptimalPriority(double engagementRate, int currentPriority)
    {
        // Higher engagement = higher priority
        var priorityAdjustment = (int)((engagementRate - 0.3) * 100); // Baseline: 30% engagement
        var newPriority = Math.Max(0, Math.Min(200, currentPriority + priorityAdjustment));
        return newPriority;
    }
    
    private static Dictionary<string, double> AnalyzeSegmentPerformance(IEnumerable<ContentDeliveryLog> logs)
    {
        return logs
            .GroupBy(log => log.DeliveryContext?.GetValueOrDefault("userSegment", "unknown")?.ToString() ?? "unknown")
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToDictionary(
                g => g.Key!,
                g => g.Count(log => log.EngagementType.HasValue) / (double)g.Count()
            );
    }
    
    private static bool ShouldUpdateTargeting(Dictionary<string, double> performance, Dictionary<string, object> currentTargeting)
    {
        // Update if there's a segment performing significantly better
        var maxPerformance = performance.Values.DefaultIfEmpty(0).Max();
        var avgPerformance = performance.Values.DefaultIfEmpty(0).Average();
        return maxPerformance > avgPerformance * 1.5; // 50% better than average
    }
    
    private static Dictionary<string, object> OptimizeTargeting(
        Dictionary<string, double> segmentPerformance, 
        Dictionary<string, object> currentTargeting)
    {
        var optimized = new Dictionary<string, object>(currentTargeting);
        
        // Focus on high-performing segments
        var topSegments = segmentPerformance
            .OrderByDescending(p => p.Value)
            .Take(3)
            .Select(p => p.Key)
            .ToArray();
            
        if (topSegments.Any())
        {
            optimized["preferredSegments"] = topSegments;
        }
        
        return optimized;
    }
    
    private static async Task<Dictionary<UserExperienceLevel, double>> AnalyzeExperienceLevelPerformanceAsync(
        IRepository<ContentDeliveryLog> deliveryRepository,
        DateTime cutoffDate,
        CancellationToken cancellationToken)
    {
        var logs = await deliveryRepository.FindAsync(
            log => log.DeliveredAt >= cutoffDate, cancellationToken);
            
        return logs
            .Where(log => log.DeliveryContext?.ContainsKey("experienceLevel") == true)
            .Where(log => log.DeliveryContext!["experienceLevel"]?.ToString() != null)
            .GroupBy(log => Enum.Parse<UserExperienceLevel>(log.DeliveryContext!["experienceLevel"]!.ToString()!))
            .ToDictionary(
                g => g.Key,
                g => g.Count(log => log.EngagementType.HasValue) / (double)g.Count()
            );
    }
    
    private static bool ShouldAdjustExperienceTargeting(object currentLevel, Dictionary<UserExperienceLevel, double> performance)
    {
        if (!Enum.TryParse<UserExperienceLevel>(currentLevel.ToString(), out var level))
        {
            return false;
        }
        
        var currentPerformance = performance.GetValueOrDefault(level, 0);
        var bestPerformance = performance.Values.DefaultIfEmpty(0).Max();
        
        return bestPerformance > currentPerformance * 1.3; // 30% better performance available
    }
    
    private static UserExperienceLevel GetOptimalExperienceLevel(Dictionary<UserExperienceLevel, double> performance)
    {
        return performance
            .OrderByDescending(p => p.Value)
            .FirstOrDefault().Key;
    }
    
    private static bool ListsAreEquivalent<T>(IEnumerable<T> list1, IEnumerable<T> list2)
    {
        var set1 = new HashSet<T>(list1);
        var set2 = new HashSet<T>(list2);
        return set1.SetEquals(set2);
    }
    
    private static string? FindStatisticalWinner(Dictionary<string, object> groupResults)
    {
        if (groupResults.Count < 2)
        {
            return null;
        }
        
        // Convert to strongly typed results for analysis
        var typedResults = groupResults.ToDictionary(
            kvp => kvp.Key,
            kvp => {
                var result = kvp.Value as dynamic;
                return new { 
                    Deliveries = (int)result.Deliveries,
                    EngagementRate = (double)result.EngagementRate
                };
            });
        
        var bestGroup = typedResults
            .Where(g => g.Value.Deliveries > 50) // Minimum sample size
            .OrderByDescending(g => g.Value.EngagementRate)
            .FirstOrDefault();
            
        if (bestGroup.Key == null)
        {
            return null;
        }
        
        // Simple statistical significance check (in production, use proper statistical tests)
        var bestRate = bestGroup.Value.EngagementRate;
        var otherRates = typedResults
            .Where(g => g.Key != bestGroup.Key && g.Value.Deliveries > 50)
            .Select(g => g.Value.EngagementRate);
            
        var avgOtherRate = otherRates.DefaultIfEmpty(0).Average();
        
        // Winner needs to be at least 20% better than others
        return bestRate > avgOtherRate * 1.2 ? bestGroup.Key : null;
    }
    
    private static List<int> GetCurrentOptimalHours(UserContentPreferences preferences)
    {
        // Extract hours from current preferred delivery times
        return preferences.PreferredDeliveryTimes.Values
            .Select(timeSpan => timeSpan.Hours)
            .Distinct()
            .OrderBy(h => h)
            .ToList();
    }
    
    private static void UpdateOptimalDeliveryHours(UserContentPreferences preferences, List<int> optimalHours)
    {
        // Convert hours to TimeSpan dictionary
        var newDeliveryTimes = new Dictionary<string, TimeSpan>();
        
        for (int i = 0; i < optimalHours.Count; i++)
        {
            var hour = optimalHours[i];
            var name = i switch
            {
                0 => "morning",
                1 => "afternoon", 
                2 => "evening",
                _ => $"slot_{i + 1}"
            };
            newDeliveryTimes[name] = new TimeSpan(hour, 0, 0);
        }
        
        preferences.SetPreferredDeliveryTimes(newDeliveryTimes);
    }
}