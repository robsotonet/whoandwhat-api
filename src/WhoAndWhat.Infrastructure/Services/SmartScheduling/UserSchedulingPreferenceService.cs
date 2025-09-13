using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Repositories;

namespace WhoAndWhat.Infrastructure.Services.SmartScheduling;

/// <summary>
/// Service for managing user scheduling preferences and learning from user patterns
/// </summary>
public sealed class UserSchedulingPreferenceService : IUserSchedulingPreferenceService
{
    private readonly ILogger<UserSchedulingPreferenceService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IUserRepository _userRepository;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(4);

    public UserSchedulingPreferenceService(
        ILogger<UserSchedulingPreferenceService> logger,
        IMemoryCache cache,
        IUserRepository userRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<SmartSchedulingPreferences> GetUserPreferencesAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting scheduling preferences for user {UserId}", userId);

        var cacheKey = $"scheduling_preferences_{userId}";
        
        if (_cache.TryGetValue(cacheKey, out SmartSchedulingPreferences? cachedPreferences))
        {
            return cachedPreferences!;
        }

        try
        {
            // In a real implementation, this would fetch from database
            var preferences = await LoadUserPreferencesFromStorageAsync(userId, cancellationToken);
            
            if (preferences == null)
            {
                _logger.LogInformation("No preferences found for user {UserId}, initializing defaults", userId);
                preferences = await InitializeDefaultPreferencesAsync(userId, "UTC", cancellationToken);
            }

            _cache.Set(cacheKey, preferences, CacheExpiry);
            return preferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduling preferences for user {UserId}", userId);
            throw;
        }
    }

    public async Task<SmartSchedulingPreferences> UpdatePreferencesAsync(
        Guid userId, 
        SmartSchedulingPreferences preferences, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating scheduling preferences for user {UserId}", userId);

        try
        {
            var updatedPreferences = preferences with { UserId = userId };
            
            // Save to storage (database)
            await SaveUserPreferencesToStorageAsync(updatedPreferences, cancellationToken);
            
            // Update cache
            var cacheKey = $"scheduling_preferences_{userId}";
            _cache.Set(cacheKey, updatedPreferences, CacheExpiry);
            
            _logger.LogInformation("Successfully updated scheduling preferences for user {UserId}", userId);
            return updatedPreferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating scheduling preferences for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserSchedulingPatternsResponse> AnalyzeSchedulingPatternsAsync(
        Guid userId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing scheduling patterns for user {UserId} from {StartDate} to {EndDate}", 
            userId, startDate, endDate);

        try
        {
            var analysisPeriod = endDate - startDate;
            
            // Analyze historical scheduling data
            var historicalData = await GetHistoricalSchedulingDataAsync(userId, startDate, endDate, cancellationToken);
            
            // Detect productivity patterns
            var detectedPatterns = DetectProductivityPatterns(historicalData);
            
            // Identify scheduling patterns
            var schedulingPatterns = IdentifySchedulingPatterns(historicalData);
            
            // Generate insights
            var insights = GenerateSchedulingInsights(historicalData, schedulingPatterns);
            
            return new UserSchedulingPatternsResponse(
                UserId: userId,
                AnalysisPeriod: analysisPeriod,
                DetectedPatterns: detectedPatterns,
                Patterns: schedulingPatterns,
                Insights: insights,
                AnalyzedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scheduling patterns for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserSchedulingPatternsResponse> GetUserSchedulingPatternsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"scheduling_patterns_{userId}";
        
        if (_cache.TryGetValue(cacheKey, out UserSchedulingPatternsResponse? cachedPatterns))
        {
            return cachedPatterns!;
        }

        // If not cached, analyze last 30 days
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-30);
        
        var patterns = await AnalyzeSchedulingPatternsAsync(userId, startDate, endDate, cancellationToken);
        
        _cache.Set(cacheKey, patterns, TimeSpan.FromHours(12)); // Cache for 12 hours
        return patterns;
    }

    public async Task RecordSchedulingActivityAsync(
        Guid userId, 
        List<SmartScheduledItem> scheduledItems, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recording scheduling activity for user {UserId} with {ItemCount} items", 
            userId, scheduledItems.Count);

        try
        {
            // In a real implementation, this would save to a scheduling activity log
            var activityLog = new SchedulingActivityLog(
                UserId: userId,
                Timestamp: DateTime.UtcNow,
                ScheduledItems: scheduledItems,
                SessionType: "Smart Scheduling",
                Metadata: new Dictionary<string, object>
                {
                    ["item_count"] = scheduledItems.Count,
                    ["categories"] = scheduledItems.Select(i => i.Category).Distinct().ToList(),
                    ["total_duration"] = scheduledItems.Sum(i => i.EstimatedDuration.TotalMinutes)
                }
            );

            await SaveActivityLogAsync(activityLog, cancellationToken);
            
            // Invalidate patterns cache to trigger reanalysis
            var cacheKey = $"scheduling_patterns_{userId}";
            _cache.Remove(cacheKey);
            
            _logger.LogInformation("Successfully recorded scheduling activity for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording scheduling activity for user {UserId}", userId);
            throw;
        }
    }

    public async Task RecordScheduleFeedbackAsync(
        Guid userId, 
        Guid scheduleId, 
        ScheduleFeedback feedback, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recording schedule feedback for user {UserId}, schedule {ScheduleId}", 
            userId, scheduleId);

        try
        {
            var feedbackRecord = new ScheduleFeedbackRecord(
                Id: Guid.NewGuid(),
                UserId: userId,
                ScheduleId: scheduleId,
                Feedback: feedback,
                RecordedAt: DateTime.UtcNow
            );

            await SaveFeedbackRecordAsync(feedbackRecord, cancellationToken);
            
            // Use feedback to improve future scheduling
            await ApplyFeedbackToPreferencesAsync(userId, feedback, cancellationToken);
            
            _logger.LogInformation("Successfully recorded schedule feedback for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording schedule feedback for user {UserId}", userId);
            throw;
        }
    }

    public async Task<SmartSchedulingPreferences> LearnAndUpdatePreferencesAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Learning and updating preferences for user {UserId}", userId);

        try
        {
            var currentPreferences = await GetUserPreferencesAsync(userId, cancellationToken);
            var patterns = await GetUserSchedulingPatternsAsync(userId, cancellationToken);
            
            // Learn from patterns and update preferences
            var updatedPreferences = ApplyLearningsToPreferences(currentPreferences, patterns);
            
            if (!PreferencesAreEqual(currentPreferences, updatedPreferences))
            {
                await UpdatePreferencesAsync(userId, updatedPreferences, cancellationToken);
                _logger.LogInformation("Updated preferences for user {UserId} based on learned patterns", userId);
            }
            
            return updatedPreferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error learning and updating preferences for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ProductivityInsightsResponse> GetProductivityInsightsAsync(
        Guid userId, 
        AnalysisTimeframe timeframe, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting productivity insights for user {UserId} for period {Period}", 
            userId, timeframe.Period);

        try
        {
            var historicalData = await GetHistoricalSchedulingDataAsync(
                userId, 
                timeframe.StartDate, 
                timeframe.EndDate, 
                cancellationToken);

            var productivityByTime = CalculateProductivityByTimeOfDay(historicalData);
            var productivityByCategory = CalculateProductivityByCategory(historicalData);
            var trends = IdentifyProductivityTrends(historicalData, timeframe.Period);
            
            var avgScore = productivityByTime.Values.Average();
            
            var insights = GenerateProductivityInsights(productivityByTime, productivityByCategory, trends);
            var recommendations = GenerateProductivityRecommendations(insights, trends);

            return new ProductivityInsightsResponse(
                UserId: userId,
                Timeframe: timeframe,
                AverageProductivityScore: avgScore,
                ProductivityByTimeOfDay: productivityByTime,
                ProductivityByTaskCategory: productivityByCategory,
                Trends: trends,
                Insights: insights,
                Recommendations: recommendations,
                GeneratedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting productivity insights for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<OptimalTimeSlot>> PredictOptimalTimesAsync(
        Guid userId, 
        string taskCategory, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Predicting optimal times for user {UserId}, category {Category}", 
            userId, taskCategory);

        try
        {
            var patterns = await GetUserSchedulingPatternsAsync(userId, cancellationToken);
            var preferences = await GetUserPreferencesAsync(userId, cancellationToken);
            
            var optimalSlots = new List<OptimalTimeSlot>();
            
            // Analyze historical performance by category and time
            var categoryPatterns = patterns.Patterns
                .Where(p => p.AssociatedCategories.Contains(taskCategory))
                .OrderByDescending(p => p.ProductivityCorrelation)
                .Take(3);

            foreach (var pattern in categoryPatterns)
            {
                foreach (var preferredTime in pattern.PreferredTimes)
                {
                    var slot = new OptimalTimeSlot(
                        StartTime: preferredTime,
                        EndTime: preferredTime.Add(TimeSpan.FromHours(1)),
                        OptimalityScore: pattern.ProductivityCorrelation,
                        Reasoning: $"Based on {pattern.PatternName} pattern with {pattern.Frequency:P0} frequency",
                        SupportingFactors: new List<string>
                        {
                            $"High productivity correlation ({pattern.ProductivityCorrelation:P0})",
                            $"Consistent pattern ({pattern.Frequency:P0} of the time)",
                            "Historical performance data"
                        }
                    );
                    
                    optimalSlots.Add(slot);
                }
            }

            // If no specific category patterns, use general productivity patterns
            if (!optimalSlots.Any())
            {
                optimalSlots.AddRange(GetDefaultOptimalSlots(preferences));
            }

            return optimalSlots
                .OrderByDescending(s => s.OptimalityScore)
                .Take(5)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting optimal times for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<EnergyLevelPrediction>> GetEnergyLevelPredictionsAsync(
        Guid userId, 
        DateTime date, 
        CancellationToken cancellationToken = default)
    {
        var preferences = await GetUserPreferencesAsync(userId, cancellationToken);
        var patterns = await GetUserSchedulingPatternsAsync(userId, cancellationToken);
        
        var predictions = new List<EnergyLevelPrediction>();
        
        // Generate hourly predictions for the day
        for (int hour = 6; hour <= 22; hour++)
        {
            var time = TimeSpan.FromHours(hour);
            var energyLevel = PredictEnergyLevelForTime(time, preferences, patterns);
            var levelType = ClassifyEnergyLevel(energyLevel);
            var confidence = CalculateConfidence(time, preferences, patterns);
            var factors = GetInfluencingFactors(time, preferences, patterns);
            
            predictions.Add(new EnergyLevelPrediction(
                Time: time,
                EnergyLevel: energyLevel,
                LevelType: levelType,
                Confidence: confidence,
                InfluencingFactors: factors
            ));
        }

        return predictions;
    }

    public async Task<SmartSchedulingPreferences> InitializeDefaultPreferencesAsync(
        Guid userId, 
        string timezone, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing default preferences for user {UserId}", userId);

        var defaultPreferences = new SmartSchedulingPreferences(
            UserId: userId,
            PreferredWorkingHours: new WorkingHours(
                StartTime: TimeSpan.FromHours(9),
                EndTime: TimeSpan.FromHours(17),
                WorkingDays: new List<DayOfWeek> 
                { 
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
                    DayOfWeek.Thursday, DayOfWeek.Friday 
                },
                LunchBreakStart: TimeSpan.FromHours(12),
                LunchBreakDuration: TimeSpan.FromHours(1),
                FlexibleSchedule: true
            ),
            PreferredBreakTimes: new List<TimeSpan> 
            { 
                TimeSpan.FromHours(10.5), // 10:30 AM
                TimeSpan.FromHours(15)    // 3:00 PM
            },
            MaxTasksPerTimeBlock: 3,
            MinimumTaskDuration: TimeSpan.FromMinutes(30),
            MaximumTaskDuration: TimeSpan.FromHours(4),
            PreferredTaskCategories: new List<string> { "Development", "Planning", "Communication" },
            ProductivityPattern: ProductivityPatterns.MorningPerson,
            AllowOverlappingTasks: false,
            PreferMorningTasks: true,
            RequireBufferTime: true,
            BufferDuration: TimeSpan.FromMinutes(15),
            CustomConstraints: new List<SchedulingConstraint>()
        );

        await UpdatePreferencesAsync(userId, defaultPreferences, cancellationToken);
        return defaultPreferences;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(true);
    }

    // Private helper methods

    private async Task<SmartSchedulingPreferences?> LoadUserPreferencesFromStorageAsync(
        Guid userId, 
        CancellationToken cancellationToken)
    {
        // In a real implementation, this would query the database
        // For now, return null to trigger default initialization
        return await Task.FromResult<SmartSchedulingPreferences?>(null);
    }

    private async Task SaveUserPreferencesToStorageAsync(
        SmartSchedulingPreferences preferences, 
        CancellationToken cancellationToken)
    {
        // In a real implementation, this would save to database
        await Task.CompletedTask;
    }

    private async Task<List<SchedulingDataPoint>> GetHistoricalSchedulingDataAsync(
        Guid userId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken)
    {
        // In a real implementation, this would query historical scheduling data
        // For now, return simulated data
        var dataPoints = new List<SchedulingDataPoint>();
        var random = new Random();
        
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                // Simulate some historical data points
                dataPoints.Add(new SchedulingDataPoint(
                    Date: date,
                    TimeSlot: TimeSpan.FromHours(9),
                    TaskCategory: "Development",
                    ProductivityScore: 0.8 + (random.NextDouble() * 0.2),
                    Duration: TimeSpan.FromHours(2),
                    CompletionRate: 0.9
                ));

                dataPoints.Add(new SchedulingDataPoint(
                    Date: date,
                    TimeSlot: TimeSpan.FromHours(14),
                    TaskCategory: "Administrative",
                    ProductivityScore: 0.6 + (random.NextDouble() * 0.2),
                    Duration: TimeSpan.FromHours(1),
                    CompletionRate: 0.95
                ));
            }
        }

        return dataPoints;
    }

    private ProductivityPatterns DetectProductivityPatterns(List<SchedulingDataPoint> historicalData)
    {
        var morningScore = historicalData
            .Where(d => d.TimeSlot.Hours <= 12)
            .Average(d => d.ProductivityScore);

        var afternoonScore = historicalData
            .Where(d => d.TimeSlot.Hours > 12 && d.TimeSlot.Hours <= 17)
            .Average(d => d.ProductivityScore);

        var eveningScore = historicalData
            .Where(d => d.TimeSlot.Hours > 17)
            .DefaultIfEmpty()
            .Average(d => d?.ProductivityScore ?? 0);

        if (morningScore > afternoonScore && morningScore > eveningScore)
            return ProductivityPatterns.MorningPerson;
        else if (afternoonScore > morningScore && afternoonScore > eveningScore)
            return ProductivityPatterns.MidDay;
        else if (eveningScore > morningScore && eveningScore > afternoonScore)
            return ProductivityPatterns.NightOwl;
        else
            return ProductivityPatterns.Consistent;
    }

    private List<SchedulingPattern> IdentifySchedulingPatterns(List<SchedulingDataPoint> historicalData)
    {
        var patterns = new List<SchedulingPattern>();

        // Group by category and time
        var categoryGroups = historicalData.GroupBy(d => d.TaskCategory);

        foreach (var group in categoryGroups)
        {
            var categoryData = group.ToList();
            var preferredTimes = categoryData
                .GroupBy(d => TimeSpan.FromHours(d.TimeSlot.Hours))
                .OrderByDescending(g => g.Average(d => d.ProductivityScore))
                .Take(2)
                .Select(g => g.Key)
                .ToList();

            var frequency = (double)categoryData.Count / Math.Max(1, historicalData.Count);
            var avgProductivity = categoryData.Average(d => d.ProductivityScore);

            patterns.Add(new SchedulingPattern(
                PatternName: $"Optimal {group.Key} Time",
                Description: $"Best times for {group.Key} tasks based on historical performance",
                Frequency: frequency,
                PreferredTimes: preferredTimes,
                AssociatedCategories: new List<string> { group.Key },
                ProductivityCorrelation: avgProductivity
            ));
        }

        return patterns;
    }

    private List<string> GenerateSchedulingInsights(
        List<SchedulingDataPoint> historicalData, 
        List<SchedulingPattern> patterns)
    {
        var insights = new List<string>();

        var bestPattern = patterns.OrderByDescending(p => p.ProductivityCorrelation).FirstOrDefault();
        if (bestPattern != null)
        {
            insights.Add($"Your most productive pattern is '{bestPattern.PatternName}' with {bestPattern.ProductivityCorrelation:P0} average performance");
        }

        var morningTasks = historicalData.Count(d => d.TimeSlot.Hours <= 12);
        var afternoonTasks = historicalData.Count(d => d.TimeSlot.Hours > 12);

        if (morningTasks > afternoonTasks * 1.5)
        {
            insights.Add("You tend to schedule more tasks in the morning, which aligns with higher morning productivity");
        }

        var avgDuration = historicalData.Average(d => d.Duration.TotalMinutes);
        if (avgDuration > 120)
        {
            insights.Add("Your tasks tend to be long-duration, consider breaking them into smaller blocks for better focus");
        }

        return insights;
    }

    private async Task SaveActivityLogAsync(SchedulingActivityLog activityLog, CancellationToken cancellationToken)
    {
        // In real implementation, save to database
        await Task.CompletedTask;
    }

    private async Task SaveFeedbackRecordAsync(ScheduleFeedbackRecord feedbackRecord, CancellationToken cancellationToken)
    {
        // In real implementation, save to database
        await Task.CompletedTask;
    }

    private async Task ApplyFeedbackToPreferencesAsync(
        Guid userId, 
        ScheduleFeedback feedback, 
        CancellationToken cancellationToken)
    {
        // Use feedback to adjust preferences
        if (feedback.OverallRating < 3.0)
        {
            // Poor feedback might indicate need to adjust preferences
            var currentPreferences = await GetUserPreferencesAsync(userId, cancellationToken);
            
            // Simple adjustment: if balance rating is low, increase buffer time
            if (feedback.BalanceRating < 3.0 && currentPreferences.BufferDuration < TimeSpan.FromMinutes(20))
            {
                var adjustedPreferences = currentPreferences with
                {
                    BufferDuration = currentPreferences.BufferDuration.Add(TimeSpan.FromMinutes(5))
                };
                
                await UpdatePreferencesAsync(userId, adjustedPreferences, cancellationToken);
            }
        }
    }

    private SmartSchedulingPreferences ApplyLearningsToPreferences(
        SmartSchedulingPreferences currentPreferences, 
        UserSchedulingPatternsResponse patterns)
    {
        var updatedPreferences = currentPreferences;

        // Update productivity pattern based on detected patterns
        if (patterns.DetectedPatterns != currentPreferences.ProductivityPattern)
        {
            updatedPreferences = updatedPreferences with
            {
                ProductivityPattern = patterns.DetectedPatterns
            };
        }

        // Update preferred task categories based on high-performing patterns
        var topPerformingCategories = patterns.Patterns
            .Where(p => p.ProductivityCorrelation > 0.8)
            .SelectMany(p => p.AssociatedCategories)
            .Distinct()
            .Take(5)
            .ToList();

        if (topPerformingCategories.Any())
        {
            updatedPreferences = updatedPreferences with
            {
                PreferredTaskCategories = topPerformingCategories
            };
        }

        return updatedPreferences;
    }

    private bool PreferencesAreEqual(SmartSchedulingPreferences pref1, SmartSchedulingPreferences pref2)
    {
        return pref1.ProductivityPattern == pref2.ProductivityPattern &&
               pref1.PreferredTaskCategories.SequenceEqual(pref2.PreferredTaskCategories) &&
               pref1.BufferDuration == pref2.BufferDuration;
    }

    private Dictionary<string, double> CalculateProductivityByTimeOfDay(List<SchedulingDataPoint> historicalData)
    {
        return historicalData
            .GroupBy(d => d.TimeSlot.Hours)
            .ToDictionary(
                g => $"{g.Key}:00",
                g => g.Average(d => d.ProductivityScore)
            );
    }

    private Dictionary<string, double> CalculateProductivityByCategory(List<SchedulingDataPoint> historicalData)
    {
        return historicalData
            .GroupBy(d => d.TaskCategory)
            .ToDictionary(
                g => g.Key,
                g => g.Average(d => d.ProductivityScore)
            );
    }

    private List<ProductivityTrend> IdentifyProductivityTrends(
        List<SchedulingDataPoint> historicalData, 
        TimeframePeriod period)
    {
        var trends = new List<ProductivityTrend>();

        // Overall productivity trend
        var chronologicalData = historicalData.OrderBy(d => d.Date).ToList();
        if (chronologicalData.Count >= 2)
        {
            var firstHalf = chronologicalData.Take(chronologicalData.Count / 2).Average(d => d.ProductivityScore);
            var secondHalf = chronologicalData.Skip(chronologicalData.Count / 2).Average(d => d.ProductivityScore);
            
            var change = ((secondHalf - firstHalf) / firstHalf) * 100;
            var direction = change > 5 ? TrendDirection.Improving :
                           change < -5 ? TrendDirection.Declining : TrendDirection.Stable;

            var dataPoints = chronologicalData
                .GroupBy(d => d.Date.Date)
                .Select(g => new TrendDataPoint(
                    Date: g.Key,
                    Value: g.Average(d => d.ProductivityScore),
                    Category: "Overall",
                    Metadata: new Dictionary<string, object> { ["task_count"] = g.Count() }
                ))
                .ToList();

            trends.Add(new ProductivityTrend(
                TrendName: "Overall Productivity",
                Direction: direction,
                ChangePercentage: change,
                Period: period,
                DataPoints: dataPoints,
                Description: $"Overall productivity is {direction.ToString().ToLower()}"
            ));
        }

        return trends;
    }

    private List<string> GenerateProductivityInsights(
        Dictionary<string, double> productivityByTime,
        Dictionary<string, double> productivityByCategory,
        List<ProductivityTrend> trends)
    {
        var insights = new List<string>();

        var bestTime = productivityByTime.OrderByDescending(kvp => kvp.Value).First();
        insights.Add($"Your peak productivity time is {bestTime.Key} with {bestTime.Value:P0} average performance");

        var bestCategory = productivityByCategory.OrderByDescending(kvp => kvp.Value).First();
        insights.Add($"You perform best on {bestCategory.Key} tasks ({bestCategory.Value:P0} success rate)");

        var improvingTrends = trends.Where(t => t.Direction == TrendDirection.Improving).ToList();
        if (improvingTrends.Any())
        {
            insights.Add($"{improvingTrends.Count} area(s) showing improvement over time");
        }

        return insights;
    }

    private List<string> GenerateProductivityRecommendations(
        List<string> insights,
        List<ProductivityTrend> trends)
    {
        var recommendations = new List<string>();

        recommendations.Add("Schedule your most important tasks during your peak productivity hours");
        
        if (trends.Any(t => t.Direction == TrendDirection.Declining))
        {
            recommendations.Add("Consider reviewing your task allocation - some areas show declining performance");
        }

        recommendations.Add("Focus on categories where you consistently perform well");
        recommendations.Add("Use time blocking to maintain focus during high-productivity periods");

        return recommendations;
    }

    private List<OptimalTimeSlot> GetDefaultOptimalSlots(SmartSchedulingPreferences preferences)
    {
        var slots = new List<OptimalTimeSlot>();

        if (preferences.PreferMorningTasks)
        {
            slots.Add(new OptimalTimeSlot(
                StartTime: TimeSpan.FromHours(9),
                EndTime: TimeSpan.FromHours(11),
                OptimalityScore: 0.8,
                Reasoning: "Morning preference based on user settings",
                SupportingFactors: new List<string> { "User prefers morning tasks", "Typical peak productivity hours" }
            ));
        }

        slots.Add(new OptimalTimeSlot(
            StartTime: TimeSpan.FromHours(14),
            EndTime: TimeSpan.FromHours(16),
            OptimalityScore: 0.7,
            Reasoning: "Afternoon productivity window",
            SupportingFactors: new List<string> { "Post-lunch productivity boost", "Good for focused work" }
        ));

        return slots;
    }

    private double PredictEnergyLevelForTime(
        TimeSpan time, 
        SmartSchedulingPreferences preferences, 
        UserSchedulingPatternsResponse patterns)
    {
        var hour = time.Hours;

        return preferences.ProductivityPattern switch
        {
            ProductivityPatterns.MorningPerson => hour <= 12 ? 0.9 : Math.Max(0.4, 0.9 - (hour - 12) * 0.1),
            ProductivityPatterns.NightOwl => hour >= 14 ? Math.Min(0.9, 0.5 + (hour - 14) * 0.1) : 0.5,
            ProductivityPatterns.MidDay => hour >= 10 && hour <= 15 ? 0.9 : 0.6,
            ProductivityPatterns.EarlyBird => hour <= 10 ? 0.95 : 0.6,
            ProductivityPatterns.AfternoonPeak => hour >= 13 && hour <= 17 ? 0.9 : 0.6,
            _ => 0.7 // Consistent energy
        };
    }

    private EnergyLevelType ClassifyEnergyLevel(double energyLevel)
    {
        return energyLevel switch
        {
            >= 0.9 => EnergyLevelType.Peak,
            >= 0.8 => EnergyLevelType.VeryHigh,
            >= 0.7 => EnergyLevelType.High,
            >= 0.5 => EnergyLevelType.Moderate,
            >= 0.3 => EnergyLevelType.Low,
            _ => EnergyLevelType.VeryLow
        };
    }

    private double CalculateConfidence(
        TimeSpan time, 
        SmartSchedulingPreferences preferences, 
        UserSchedulingPatternsResponse patterns)
    {
        // Higher confidence during working hours
        var workingHours = preferences.PreferredWorkingHours;
        if (time >= workingHours.StartTime && time <= workingHours.EndTime)
        {
            return 0.8;
        }
        
        return 0.6; // Lower confidence outside working hours
    }

    private List<string> GetInfluencingFactors(
        TimeSpan time, 
        SmartSchedulingPreferences preferences, 
        UserSchedulingPatternsResponse patterns)
    {
        var factors = new List<string>();

        factors.Add($"Productivity pattern: {preferences.ProductivityPattern}");
        
        if (time >= preferences.PreferredWorkingHours.StartTime && 
            time <= preferences.PreferredWorkingHours.EndTime)
        {
            factors.Add("Within preferred working hours");
        }

        if (preferences.PreferredBreakTimes.Any(bt => Math.Abs((bt - time).TotalMinutes) <= 30))
        {
            factors.Add("Near preferred break time");
        }

        return factors;
    }

    // Helper records for internal data structures

    private sealed record SchedulingDataPoint(
        DateTime Date,
        TimeSpan TimeSlot,
        string TaskCategory,
        double ProductivityScore,
        TimeSpan Duration,
        double CompletionRate
    );

    private sealed record SchedulingActivityLog(
        Guid UserId,
        DateTime Timestamp,
        List<SmartScheduledItem> ScheduledItems,
        string SessionType,
        Dictionary<string, object> Metadata
    );

    private sealed record ScheduleFeedbackRecord(
        Guid Id,
        Guid UserId,
        Guid ScheduleId,
        ScheduleFeedback Feedback,
        DateTime RecordedAt
    );
}