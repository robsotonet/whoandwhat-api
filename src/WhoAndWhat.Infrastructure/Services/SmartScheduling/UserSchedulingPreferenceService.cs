using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Infrastructure.Services.SmartScheduling;

/// <summary>
/// Service for managing user scheduling preferences and learning from user patterns
/// Implements machine learning algorithms to adapt to user behavior and optimize scheduling
/// </summary>
public sealed class UserSchedulingPreferenceService : IUserSchedulingPreferenceService
{
    private readonly ILogger<UserSchedulingPreferenceService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IUserSchedulingPreferenceRepository _preferenceRepository;
    private readonly ISchedulingPatternRepository _patternRepository;
    private readonly IAppTaskRepository _taskRepository;

    // Learning constants for preference adaptation
    private const double LEARNING_RATE = 0.1;
    private const double PATTERN_CONFIDENCE_THRESHOLD = 0.7;
    private const int MIN_PATTERN_OCCURRENCES = 5;
    private const int ANALYSIS_DAYS_DEFAULT = 30;
    private const double PRODUCTIVITY_CORRELATION_THRESHOLD = 0.6;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(4);

    public UserSchedulingPreferenceService(
        ILogger<UserSchedulingPreferenceService> logger,
        IMemoryCache cache,
        IUserSchedulingPreferenceRepository preferenceRepository,
        ISchedulingPatternRepository patternRepository,
        IAppTaskRepository taskRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _preferenceRepository = preferenceRepository ?? throw new ArgumentNullException(nameof(preferenceRepository));
        _patternRepository = patternRepository ?? throw new ArgumentNullException(nameof(patternRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
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
            var preferenceEntity = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);

            if (preferenceEntity == null)
            {
                _logger.LogInformation("No preferences found for user {UserId}, initializing defaults", userId);
                return await InitializeDefaultPreferencesAsync(userId, "UTC", cancellationToken);
            }

            var preferences = MapToDto(preferenceEntity);
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
            var existingPreferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);
            if (existingPreferences == null)
            {
                // Create new preferences if none exist
                var newEntity = MapToEntity(userId, preferences);
                await _preferenceRepository.AddAsync(newEntity, cancellationToken);
                await _preferenceRepository.SaveChangesAsync(cancellationToken);

                var newPreferences = MapToDto(newEntity);
                var cacheKey = $"scheduling_preferences_{userId}";
                _cache.Set(cacheKey, newPreferences, CacheExpiry);

                _logger.LogInformation("Created new preferences for user {UserId}", userId);
                return newPreferences;
            }

            // Update existing preferences
            UpdateEntityFromDto(existingPreferences, preferences);
            await _preferenceRepository.UpdateAsync(existingPreferences, cancellationToken);
            await _preferenceRepository.SaveChangesAsync(cancellationToken);

            var updatedPreferences = MapToDto(existingPreferences);
            var cacheKey2 = $"scheduling_preferences_{userId}";
            _cache.Set(cacheKey2, updatedPreferences, CacheExpiry);

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
            // Get user's completed tasks in the date range
            var tasks = await _taskRepository.GetCompletedTasksByDateRangeAsync(userId, startDate, endDate, cancellationToken);
            var patterns = new List<DetectedPattern>();

            // Analyze productivity patterns by time of day
            var productivityByHour = AnalyzeProductivityByTimeOfDay(tasks);
            if (productivityByHour.Any())
            {
                patterns.Add(new DetectedPattern(
                    "ProductivityByTimeOfDay",
                    "Time-based productivity patterns detected",
                    productivityByHour.Max(x => x.Value),
                    productivityByHour.Select(kv => new PatternInsight(
                        $"Hour {kv.Key}",
                        kv.Value,
                        kv.Value > 0.7 ? "High productivity period" : "Lower productivity period"
                    )).ToList(),
                    DateTime.UtcNow
                ));
            }

            // Analyze category-based patterns
            var categoryPatterns = AnalyzeCategoryPatterns(tasks);
            if (categoryPatterns.Any())
            {
                patterns.Add(new DetectedPattern(
                    "CategoryProductivity",
                    "Task category productivity patterns",
                    categoryPatterns.Max(x => x.Value),
                    categoryPatterns.Select(kv => new PatternInsight(
                        $"Category {kv.Key}",
                        kv.Value,
                        $"Average completion time: {kv.Value:F1} hours"
                    )).ToList(),
                    DateTime.UtcNow
                ));
            }

            // Analyze weekly patterns
            var weeklyPatterns = AnalyzeWeeklyPatterns(tasks);
            if (weeklyPatterns.Any())
            {
                patterns.Add(new DetectedPattern(
                    "WeeklyProductivity",
                    "Day of week productivity patterns",
                    weeklyPatterns.Max(x => x.Value),
                    weeklyPatterns.Select(kv => new PatternInsight(
                        kv.Key.ToString(),
                        kv.Value,
                        $"Productivity score: {kv.Value:F2}"
                    )).ToList(),
                    DateTime.UtcNow
                ));
            }

            var insights = GeneratePatternInsights(patterns);
            var recommendations = GenerateRecommendations(patterns, userId);

            return new UserSchedulingPatternsResponse(
                userId,
                new AnalysisTimeframe(startDate, endDate, TimeframePeriod.Daily),
                patterns,
                insights,
                recommendations,
                DateTime.UtcNow
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
        _logger.LogInformation("Getting cached scheduling patterns for user {UserId}", userId);

        try
        {
            // Try to get recent patterns from cache or database
            var recentPatterns = await _patternRepository.GetActivePatternsByUserAsync(userId, cancellationToken);

            if (!recentPatterns.Any())
            {
                // No patterns found, analyze recent data
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-ANALYSIS_DAYS_DEFAULT);
                return await AnalyzeSchedulingPatternsAsync(userId, startDate, endDate, cancellationToken);
            }

            // Convert stored patterns to response format
            var patterns = recentPatterns.Select(p => new DetectedPattern(
                p.PatternType,
                p.Description,
                p.SuccessRate,
                new List<PatternInsight>
                {
                    new PatternInsight(
                        p.PatternType,
                        p.SuccessRate,
                        $"Confidence: {p.ConfidenceScore:F2}, Reinforcements: {p.ReinforcementCount}"
                    )
                },
                p.LastReinforced ?? p.CreatedAt
            )).ToList();

            return new UserSchedulingPatternsResponse(
                userId,
                new AnalysisTimeframe(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, TimeframePeriod.Monthly),
                patterns,
                new List<string> { "Using cached pattern data" },
                new List<string> { "Keep building consistent scheduling habits" },
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting patterns for user {UserId}", userId);
            throw;
        }
    }

    public async Task RecordSchedulingActivityAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recording scheduling activity for user {UserId} with {Count} items", userId, scheduledItems.Count);

        try
        {
            foreach (var item in scheduledItems)
            {
                // Create or update scheduling patterns based on this activity
                var patterns = await DetectPatternsFromActivity(userId, item, cancellationToken);

                foreach (var pattern in patterns)
                {
                    var existingPattern = await _patternRepository.GetOptimizationEligiblePatternsAsync(userId, cancellationToken);
                    var matching = existingPattern.FirstOrDefault(p => p.PatternType == pattern.PatternType &&
                                                                        p.Context.ContainsKey("category") &&
                                                                        p.Context["category"].ToString() == pattern.Context["category"].ToString());

                    if (matching != null)
                    {
                        // Reinforce existing pattern
                        matching.Reinforce(0.8); // Default reinforcement score
                        await _patternRepository.UpdateAsync(matching, cancellationToken);
                    }
                    else
                    {
                        // Add new pattern
                        await _patternRepository.AddAsync(pattern, cancellationToken);
                    }
                }
            }

            await _patternRepository.SaveChangesAsync(cancellationToken);
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
        _logger.LogInformation("Recording schedule feedback for user {UserId}, schedule {ScheduleId}", userId, scheduleId);

        try
        {
            // Use feedback to adjust user preferences and patterns
            var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);
            if (preferences != null)
            {
                // Adjust optimization weights based on feedback
                AdjustPreferencesFromFeedback(preferences, feedback);
                await _preferenceRepository.UpdateAsync(preferences, cancellationToken);
            }

            // Reinforce or penalize patterns based on feedback quality
            var patterns = await _patternRepository.GetActivePatternsByUserAsync(userId, cancellationToken);
            foreach (var pattern in patterns)
            {
                var adjustmentScore = CalculateFeedbackAdjustment(feedback);
                if (adjustmentScore > 0.5)
                {
                    pattern.Reinforce(adjustmentScore);
                }
                else if (adjustmentScore < -0.3)
                {
                    pattern.RecordViolation();
                }
                await _patternRepository.UpdateAsync(pattern, cancellationToken);
            }

            await _patternRepository.SaveChangesAsync(cancellationToken);
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
            var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);
            if (preferences == null)
            {
                return await InitializeDefaultPreferencesAsync(userId, "UTC", cancellationToken);
            }

            // Analyze recent patterns to learn preferences
            var patterns = await _patternRepository.GetReliablePatternsByUserAsync(userId, cancellationToken);
            var recentTasks = await _taskRepository.GetTasksByUserAsync(userId, 1, 100, cancellationToken);

            // Learn optimal working hours
            var optimalHours = LearnOptimalWorkingHours(recentTasks.Items);
            if (optimalHours.start.HasValue && optimalHours.end.HasValue)
            {
                preferences.WorkingStartTime = optimalHours.start.Value;
                preferences.WorkingEndTime = optimalHours.end.Value;
            }

            // Learn break preferences
            var breakPatterns = LearnBreakPatterns(patterns);
            if (breakPatterns.Any())
            {
                preferences.SetPreferredBreakTimes(breakPatterns);
            }

            // Update productivity scores based on recent performance
            var productivityInsights = await GetProductivityInsightsAsync(userId,
                new AnalysisTimeframe(DateTime.UtcNow.AddDays(-14), DateTime.UtcNow, TimeframePeriod.Daily),
                cancellationToken);

            preferences.UpdateProductivityScore(productivityInsights.AverageProductivityScore);

            await _preferenceRepository.UpdateAsync(preferences, cancellationToken);
            await _preferenceRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully learned and updated preferences for user {UserId}", userId);
            return MapToDto(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error learning preferences for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ProductivityInsightsResponse> GetProductivityInsightsAsync(
        Guid userId,
        AnalysisTimeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting productivity insights for user {UserId}", userId);

        try
        {
            var tasks = await _taskRepository.GetCompletedTasksByDateRangeAsync(userId, timeframe.StartDate, timeframe.EndDate, cancellationToken);

            var productivityByHour = AnalyzeProductivityByTimeOfDay(tasks);
            var productivityByCategory = AnalyzeCategoryPatterns(tasks);
            var trends = AnalyzeProductivityTrends(tasks, timeframe.Period);

            var averageProductivity = tasks.Any() ?
                tasks.Where(t => t.ProductivityScore.HasValue).Average(t => t.ProductivityScore.Value) : 0.0;

            var insights = GenerateProductivityInsights(productivityByHour, productivityByCategory, trends);
            var recommendations = GenerateProductivityRecommendations(productivityByHour, productivityByCategory, trends);

            return new ProductivityInsightsResponse(
                userId,
                timeframe,
                averageProductivity,
                productivityByHour.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                productivityByCategory.ToDictionary(kv => kv.Key, kv => kv.Value),
                trends,
                insights,
                recommendations,
                DateTime.UtcNow
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
        _logger.LogInformation("Predicting optimal times for user {UserId}, category {Category}", userId, taskCategory);

        try
        {
            var patterns = await _patternRepository.GetPatternsByCategoriesAsync(userId, new List<string> { taskCategory }, cancellationToken);
            var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);

            var optimalSlots = new List<OptimalTimeSlot>();

            // Analyze patterns for this category
            foreach (var pattern in patterns.Where(p => p.SuccessRate > PRODUCTIVITY_CORRELATION_THRESHOLD))
            {
                if (pattern.Context.ContainsKey("optimal_hours"))
                {
                    var hours = pattern.Context["optimal_hours"].ToString()?.Split(',');
                    if (hours != null)
                    {
                        foreach (var hour in hours)
                        {
                            if (int.TryParse(hour, out int hourValue))
                            {
                                var startTime = TimeSpan.FromHours(hourValue);
                                var endTime = TimeSpan.FromHours(hourValue + 1);

                                optimalSlots.Add(new OptimalTimeSlot(
                                    startTime,
                                    endTime,
                                    pattern.SuccessRate,
                                    $"Historical data shows high productivity for {taskCategory} at this time",
                                    new List<string>
                                    {
                                        $"Success rate: {pattern.SuccessRate:F2}",
                                        $"Pattern confidence: {pattern.ConfidenceScore:F2}",
                                        $"Based on {pattern.ReinforcementCount} observations"
                                    }
                                ));
                            }
                        }
                    }
                }
            }

            // If no specific patterns, use general productivity insights
            if (!optimalSlots.Any() && preferences != null)
            {
                var workingStart = preferences.WorkingStartTime;
                var workingEnd = preferences.WorkingEndTime;

                // Suggest peak productivity hours (typically mid-morning and early afternoon)
                optimalSlots.AddRange(new[]
                {
                    new OptimalTimeSlot(
                        TimeSpan.FromHours(Math.Max(9, workingStart.Hours)),
                        TimeSpan.FromHours(Math.Max(11, workingStart.Hours + 2)),
                        0.8,
                        "Mid-morning productivity peak",
                        new List<string> { "General productivity pattern", "Cognitive performance peak" }
                    ),
                    new OptimalTimeSlot(
                        TimeSpan.FromHours(Math.Min(14, workingEnd.Hours - 3)),
                        TimeSpan.FromHours(Math.Min(16, workingEnd.Hours - 1)),
                        0.7,
                        "Early afternoon focus period",
                        new List<string> { "Post-lunch recovery period", "Good for focused work" }
                    )
                });
            }

            return optimalSlots.OrderByDescending(s => s.OptimalityScore).ToList();
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
        _logger.LogInformation("Getting energy level predictions for user {UserId} on {Date}", userId, date);

        try
        {
            var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);
            var patterns = await _patternRepository.GetActivePatternsByUserAsync(userId, cancellationToken);

            var predictions = new List<EnergyLevelPrediction>();

            // Generate hourly predictions for the working day
            var startHour = preferences?.WorkingStartTime.Hours ?? 9;
            var endHour = preferences?.WorkingEndTime.Hours ?? 17;

            for (int hour = startHour; hour <= endHour; hour++)
            {
                var timeSpan = TimeSpan.FromHours(hour);
                var energyLevel = PredictEnergyLevelForTime(hour, patterns, preferences);
                var levelType = GetEnergyLevelType(energyLevel);

                predictions.Add(new EnergyLevelPrediction(
                    timeSpan,
                    energyLevel,
                    levelType,
                    0.7, // Default confidence
                    new List<string>
                    {
                        "Based on historical patterns",
                        $"Time of day: {hour}:00",
                        $"Pattern analysis confidence"
                    }
                ));
            }

            return predictions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting energy predictions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<SmartSchedulingPreferences> InitializeDefaultPreferencesAsync(
        Guid userId,
        string timezone,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing default preferences for user {UserId}", userId);

        try
        {
            var defaultPreferences = new UserSchedulingPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TimeZone = timezone,
                WorkingStartTime = TimeSpan.FromHours(9),
                WorkingEndTime = TimeSpan.FromHours(17),
                WorkingDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                PreferredBreakDuration = TimeSpan.FromMinutes(15),
                MaxConsecutiveWorkHours = 3,
                ProductivityOptimizationWeight = 0.4,
                DeadlineOptimizationWeight = 0.3,
                BalanceOptimizationWeight = 0.2,
                FlexibilityOptimizationWeight = 0.1,
                EnergyLevelPattern = "Standard",
                PreferredTaskDuration = TimeSpan.FromMinutes(60),
                BufferTimeBetweenTasks = TimeSpan.FromMinutes(10),
                AllowEarlyMorning = false,
                AllowLateEvening = false,
                PreferBatchingSimilarTasks = true,
                EnableSmartBreaks = true,
                ProductivityScore = 0.7,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _preferenceRepository.AddAsync(defaultPreferences, cancellationToken);
            await _preferenceRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully initialized default preferences for user {UserId}", userId);
            return MapToDto(defaultPreferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing preferences for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if all required dependencies are available
            var testUserId = Guid.Empty;
            await _preferenceRepository.GetByUserIdAsync(testUserId, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Private helper methods

    private SmartSchedulingPreferences MapToDto(UserSchedulingPreference entity)
    {
        return new SmartSchedulingPreferences
        {
            UserId = entity.UserId,
            TimeZone = entity.TimeZone,
            WorkingHours = new WorkingHoursPreference
            {
                StartTime = entity.WorkingStartTime,
                EndTime = entity.WorkingEndTime,
                WorkingDays = entity.GetWorkingDays(),
                FlexibleHours = entity.FlexibleWorkingHours,
                BreakDuration = entity.PreferredBreakDuration,
                MaxConsecutiveHours = entity.MaxConsecutiveWorkHours
            },
            OptimizationWeights = new OptimizationWeights
            {
                ProductivityWeight = entity.ProductivityOptimizationWeight,
                DeadlineWeight = entity.DeadlineOptimizationWeight,
                BalanceWeight = entity.BalanceOptimizationWeight,
                FlexibilityWeight = entity.FlexibilityOptimizationWeight
            },
            TaskPreferences = new TaskSchedulingPreferences
            {
                PreferredDuration = entity.PreferredTaskDuration,
                BufferTime = entity.BufferTimeBetweenTasks,
                BatchSimilarTasks = entity.PreferBatchingSimilarTasks,
                AllowEarlyMorning = entity.AllowEarlyMorning,
                AllowLateEvening = entity.AllowLateEvening
            },
            ProductivityInsights = new ProductivityPatterns
            {
                EnergyPattern = entity.EnergyLevelPattern,
                HighProductivityPeriods = new List<TimeSlot>(),
                LowProductivityPeriods = new List<TimeSlot>(),
                OptimalTaskDurations = new Dictionary<string, TimeSpan>(),
                PreferredBreakTimes = new List<TimeSpan>()
            },
            LearningSettings = new LearningPreferences
            {
                EnablePatternLearning = true,
                AdaptToChanges = true,
                LearningRate = LEARNING_RATE,
                ConfidenceThreshold = PATTERN_CONFIDENCE_THRESHOLD
            }
        };
    }

    private UserSchedulingPreference MapToEntity(Guid userId, SmartSchedulingPreferences dto)
    {
        return new UserSchedulingPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TimeZone = dto.TimeZone,
            WorkingStartTime = dto.WorkingHours.StartTime,
            WorkingEndTime = dto.WorkingHours.EndTime,
            WorkingDays = dto.WorkingHours.WorkingDays,
            FlexibleWorkingHours = dto.WorkingHours.FlexibleHours,
            PreferredBreakDuration = dto.WorkingHours.BreakDuration,
            MaxConsecutiveWorkHours = dto.WorkingHours.MaxConsecutiveHours,
            ProductivityOptimizationWeight = dto.OptimizationWeights.ProductivityWeight,
            DeadlineOptimizationWeight = dto.OptimizationWeights.DeadlineWeight,
            BalanceOptimizationWeight = dto.OptimizationWeights.BalanceWeight,
            FlexibilityOptimizationWeight = dto.OptimizationWeights.FlexibilityWeight,
            EnergyLevelPattern = dto.ProductivityInsights.EnergyPattern,
            PreferredTaskDuration = dto.TaskPreferences.PreferredDuration,
            BufferTimeBetweenTasks = dto.TaskPreferences.BufferTime,
            AllowEarlyMorning = dto.TaskPreferences.AllowEarlyMorning,
            AllowLateEvening = dto.TaskPreferences.AllowLateEvening,
            PreferBatchingSimilarTasks = dto.TaskPreferences.BatchSimilarTasks,
            EnableSmartBreaks = true,
            ProductivityScore = 0.7,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private void UpdateEntityFromDto(UserSchedulingPreference entity, SmartSchedulingPreferences dto)
    {
        entity.TimeZone = dto.TimeZone;
        entity.WorkingStartTime = dto.WorkingHours.StartTime;
        entity.WorkingEndTime = dto.WorkingHours.EndTime;
        entity.WorkingDays = dto.WorkingHours.WorkingDays;
        entity.FlexibleWorkingHours = dto.WorkingHours.FlexibleHours;
        entity.PreferredBreakDuration = dto.WorkingHours.BreakDuration;
        entity.MaxConsecutiveWorkHours = dto.WorkingHours.MaxConsecutiveHours;
        entity.ProductivityOptimizationWeight = dto.OptimizationWeights.ProductivityWeight;
        entity.DeadlineOptimizationWeight = dto.OptimizationWeights.DeadlineWeight;
        entity.BalanceOptimizationWeight = dto.OptimizationWeights.BalanceWeight;
        entity.FlexibilityOptimizationWeight = dto.OptimizationWeights.FlexibilityWeight;
        entity.EnergyLevelPattern = dto.ProductivityInsights.EnergyPattern;
        entity.PreferredTaskDuration = dto.TaskPreferences.PreferredDuration;
        entity.BufferTimeBetweenTasks = dto.TaskPreferences.BufferTime;
        entity.AllowEarlyMorning = dto.TaskPreferences.AllowEarlyMorning;
        entity.AllowLateEvening = dto.TaskPreferences.AllowLateEvening;
        entity.PreferBatchingSimilarTasks = dto.TaskPreferences.BatchSimilarTasks;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    private Dictionary<int, double> AnalyzeProductivityByTimeOfDay(IEnumerable<AppTask> tasks)
    {
        var productivityByHour = new Dictionary<int, double>();

        var tasksByHour = tasks
            .Where(t => t.CompletedAt.HasValue && t.ProductivityScore.HasValue)
            .GroupBy(t => t.CompletedAt!.Value.Hour)
            .ToList();

        foreach (var hourGroup in tasksByHour)
        {
            var averageProductivity = hourGroup.Average(t => t.ProductivityScore!.Value);
            productivityByHour[hourGroup.Key] = averageProductivity;
        }

        return productivityByHour;
    }

    private Dictionary<string, double> AnalyzeCategoryPatterns(IEnumerable<AppTask> tasks)
    {
        return tasks
            .Where(t => t.CompletedAt.HasValue && t.ProductivityScore.HasValue)
            .GroupBy(t => t.Category.ToString())
            .ToDictionary(g => g.Key, g => g.Average(t => t.ProductivityScore!.Value));
    }

    private Dictionary<DayOfWeek, double> AnalyzeWeeklyPatterns(IEnumerable<AppTask> tasks)
    {
        return tasks
            .Where(t => t.CompletedAt.HasValue && t.ProductivityScore.HasValue)
            .GroupBy(t => t.CompletedAt!.Value.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Average(t => t.ProductivityScore!.Value));
    }

    private List<string> GeneratePatternInsights(List<DetectedPattern> patterns)
    {
        var insights = new List<string>();

        var productivityPattern = patterns.FirstOrDefault(p => p.PatternType == "ProductivityByTimeOfDay");
        if (productivityPattern != null)
        {
            var bestTime = productivityPattern.Insights.OrderByDescending(i => i.Confidence).FirstOrDefault();
            if (bestTime != null)
            {
                insights.Add($"Your most productive time appears to be {bestTime.InsightName} with {bestTime.Confidence:F1} productivity score");
            }
        }

        return insights;
    }

    private List<string> GenerateRecommendations(List<DetectedPattern> patterns, Guid userId)
    {
        var recommendations = new List<string>
        {
            "Schedule your most important tasks during your high-productivity periods",
            "Consider batching similar tasks together for improved efficiency",
            "Take regular breaks to maintain consistent productivity levels"
        };

        return recommendations;
    }

    private Task<List<Domain.Entities.SchedulingPattern>> DetectPatternsFromActivity(Guid userId, SmartScheduledItem item, CancellationToken cancellationToken)
    {
        var patterns = new List<Domain.Entities.SchedulingPattern>();

        // Create a pattern for task category and time
        var categoryTimePattern = new Domain.Entities.SchedulingPattern
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PatternType = "CategoryTime",
            Description = $"Task category {item.TaskCategory} scheduled at {item.StartTime:HH:mm}",
            Context = new Dictionary<string, object>
            {
                ["category"] = item.TaskCategory ?? "Unknown",
                ["hour"] = item.StartTime.Hour,
                ["day_of_week"] = item.StartTime.DayOfWeek.ToString()
            },
            ConfidenceScore = 0.5, // Initial confidence
            SuccessRate = 0.7, // Assume moderate success initially
            ReinforcementCount = 1,
            ViolationCount = 0,
            CreatedAt = DateTime.UtcNow,
            LastReinforced = DateTime.UtcNow,
            IsActive = true
        };

        patterns.Add(categoryTimePattern);
        return Task.FromResult(patterns);
    }

    private void AdjustPreferencesFromFeedback(UserSchedulingPreference preferences, ScheduleFeedback feedback)
    {
        // Adjust optimization weights based on feedback
        var overallRating = feedback.OverallRating;
        var adjustment = (overallRating - 3.0) * 0.1; // Normalize to -0.2 to +0.2

        if (feedback.ProductivityRating > 3.5)
        {
            preferences.ProductivityOptimizationWeight = Math.Min(1.0, preferences.ProductivityOptimizationWeight + adjustment * 0.5);
        }

        if (feedback.BalanceRating > 3.5)
        {
            preferences.BalanceOptimizationWeight = Math.Min(1.0, preferences.BalanceOptimizationWeight + adjustment * 0.5);
        }

        if (feedback.FlexibilityRating > 3.5)
        {
            preferences.FlexibilityOptimizationWeight = Math.Min(1.0, preferences.FlexibilityOptimizationWeight + adjustment * 0.5);
        }

        preferences.UpdatedAt = DateTime.UtcNow;
    }

    private double CalculateFeedbackAdjustment(ScheduleFeedback feedback)
    {
        // Calculate weighted feedback score
        var weightedScore = (feedback.OverallRating * 0.4 +
                           feedback.ProductivityRating * 0.3 +
                           feedback.BalanceRating * 0.2 +
                           feedback.FlexibilityRating * 0.1) / 4.0;

        return (weightedScore - 2.5) * 0.4; // Normalize to -1.0 to +1.0 range
    }

    private (TimeSpan? start, TimeSpan? end) LearnOptimalWorkingHours(IEnumerable<AppTask> tasks)
    {
        var completedTasks = tasks.Where(t => t.CompletedAt.HasValue && t.ProductivityScore.HasValue && t.ProductivityScore > 0.6).ToList();

        if (!completedTasks.Any())
        {
            return (null, null);
        }

        var productiveTimes = completedTasks.Select(t => t.CompletedAt!.Value.TimeOfDay).OrderBy(t => t).ToList();
        var earliestProductive = productiveTimes.First();
        var latestProductive = productiveTimes.Last();

        return (earliestProductive, latestProductive);
    }

    private List<TimeSpan> LearnBreakPatterns(IEnumerable<Domain.Entities.SchedulingPattern> patterns)
    {
        // Analyze patterns to determine optimal break times
        var breakTimes = new List<TimeSpan>
        {
            TimeSpan.FromHours(10.5), // Mid-morning break
            TimeSpan.FromHours(15)    // Afternoon break
        };

        return breakTimes;
    }

    private List<ProductivityTrend> AnalyzeProductivityTrends(IEnumerable<AppTask> tasks, TimeframePeriod period)
    {
        var trends = new List<ProductivityTrend>();

        var completedTasks = tasks.Where(t => t.CompletedAt.HasValue && t.ProductivityScore.HasValue).ToList();
        if (!completedTasks.Any())
        {
            return trends;
        }

        // Analyze overall productivity trend
        var orderedTasks = completedTasks.OrderBy(t => t.CompletedAt).ToList();
        var firstHalf = orderedTasks.Take(orderedTasks.Count / 2);
        var secondHalf = orderedTasks.Skip(orderedTasks.Count / 2);

        var firstHalfAvg = firstHalf.Average(t => t.ProductivityScore!.Value);
        var secondHalfAvg = secondHalf.Average(t => t.ProductivityScore!.Value);
        var changePercentage = ((secondHalfAvg - firstHalfAvg) / firstHalfAvg) * 100;

        var direction = Math.Abs(changePercentage) < 5 ? TrendDirection.Stable :
                       changePercentage > 0 ? TrendDirection.Improving :
                       TrendDirection.Declining;

        trends.Add(new ProductivityTrend(
            "Overall Productivity",
            direction,
            changePercentage,
            period,
            new List<TrendDataPoint>(),
            $"Productivity has {direction.ToString().ToLower()} by {Math.Abs(changePercentage):F1}%"
        ));

        return trends;
    }

    private List<string> GenerateProductivityInsights(Dictionary<int, double> productivityByHour, Dictionary<string, double> productivityByCategory, List<ProductivityTrend> trends)
    {
        var insights = new List<string>();

        if (productivityByHour.Any())
        {
            var bestHour = productivityByHour.OrderByDescending(kv => kv.Value).First();
            insights.Add($"Your most productive hour is {bestHour.Key}:00 with {bestHour.Value:F2} average productivity");
        }

        if (productivityByCategory.Any())
        {
            var bestCategory = productivityByCategory.OrderByDescending(kv => kv.Value).First();
            insights.Add($"You're most productive with {bestCategory.Key} tasks ({bestCategory.Value:F2} average score)");
        }

        return insights;
    }

    private List<string> GenerateProductivityRecommendations(Dictionary<int, double> productivityByHour, Dictionary<string, double> productivityByCategory, List<ProductivityTrend> trends)
    {
        var recommendations = new List<string>
        {
            "Schedule your most challenging tasks during peak productivity hours",
            "Consider time-blocking similar task types together",
            "Take breaks when productivity typically dips"
        };

        if (trends.Any(t => t.Direction == TrendDirection.Declining))
        {
            recommendations.Add("Your productivity has been declining - consider adjusting your schedule or taking more breaks");
        }

        return recommendations;
    }

    private double PredictEnergyLevelForTime(int hour, IEnumerable<Domain.Entities.SchedulingPattern> patterns, UserSchedulingPreference? preferences)
    {
        // Basic energy prediction based on typical human circadian rhythms
        var baseEnergy = hour switch
        {
            < 8 => 0.3,   // Early morning
            >= 8 and < 10 => 0.8, // Morning peak
            >= 10 and < 12 => 0.9, // Late morning peak
            >= 12 and < 14 => 0.6, // Post-lunch dip
            >= 14 and < 16 => 0.7, // Afternoon recovery
            >= 16 and < 18 => 0.8, // Evening peak
            _ => 0.4      // Late evening
        };

        // Adjust based on user patterns if available
        var relevantPatterns = patterns.Where(p => p.Context.ContainsKey("hour") &&
                                                 int.TryParse(p.Context["hour"].ToString(), out int patternHour) &&
                                                 patternHour == hour);

        if (relevantPatterns.Any())
        {
            var patternAdjustment = relevantPatterns.Average(p => p.SuccessRate) - 0.7;
            baseEnergy = Math.Max(0.1, Math.Min(1.0, baseEnergy + patternAdjustment * 0.3));
        }

        return baseEnergy;
    }

    private EnergyLevelType GetEnergyLevelType(double energyLevel)
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
}
