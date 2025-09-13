using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Service for managing user scheduling preferences and learning from user patterns using machine learning
/// </summary>
public class UserSchedulingPreferenceService : IUserSchedulingPreferenceService, IDisposable
{
    private readonly ILogger<UserSchedulingPreferenceService> _logger;
    private readonly SmartSchedulingSettings _settings;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ConcurrentDictionary<Guid, SmartSchedulingPreferences> _preferencesCache;
    private readonly ConcurrentDictionary<Guid, UserSchedulingPatternsResponse> _patternsCache;
    private readonly ConcurrentDictionary<Guid, List<SchedulingActivityRecord>> _activityRecords;
    private readonly SemaphoreSlim _cacheSemaphore;
    private bool _disposed;

    public UserSchedulingPreferenceService(
        IOptions<SmartSchedulingSettings> settings,
        IAppTaskRepository taskRepository,
        ILogger<UserSchedulingPreferenceService> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _preferencesCache = new ConcurrentDictionary<Guid, SmartSchedulingPreferences>();
        _patternsCache = new ConcurrentDictionary<Guid, UserSchedulingPatternsResponse>();
        _activityRecords = new ConcurrentDictionary<Guid, List<SchedulingActivityRecord>>();
        _cacheSemaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<SmartSchedulingPreferences> GetUserPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting scheduling preferences for user {UserId}", userId);

            // Check cache first
            if (_preferencesCache.TryGetValue(userId, out var cachedPreferences))
            {
                return cachedPreferences;
            }

            // Load from database or initialize defaults
            var preferences = await LoadUserPreferencesFromStorage(userId, cancellationToken) ??
                            await InitializeDefaultPreferencesAsync(userId, "UTC", cancellationToken);

            // Cache the preferences
            _preferencesCache[userId] = preferences;

            return preferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduling preferences for user {UserId}", userId);
            return await InitializeDefaultPreferencesAsync(userId, "UTC", cancellationToken);
        }
    }

    public async Task<SmartSchedulingPreferences> UpdatePreferencesAsync(
        Guid userId, 
        SmartSchedulingPreferences preferences, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating scheduling preferences for user {UserId}", userId);

            // Validate preferences
            var validatedPreferences = ValidatePreferences(preferences);

            // Save to storage (would implement actual database persistence here)
            await SaveUserPreferencesToStorage(userId, validatedPreferences, cancellationToken);

            // Update cache
            _preferencesCache[userId] = validatedPreferences;

            _logger.LogInformation("Successfully updated scheduling preferences for user {UserId}", userId);
            return validatedPreferences;
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
        try
        {
            _logger.LogInformation("Analyzing scheduling patterns for user {UserId} from {StartDate} to {EndDate}",
                userId, startDate, endDate);

            // Get user's task history
            var taskHistory = await GetUserTaskHistory(userId, startDate, endDate, cancellationToken);
            
            // Get scheduling activity records
            var activityRecords = GetUserActivityRecords(userId);

            // Analyze patterns
            var detectedPatterns = AnalyzeUserPatterns(taskHistory, activityRecords);
            var productivityPatterns = DetectProductivityPatterns(taskHistory, activityRecords);
            var insights = GenerateSchedulingInsights(detectedPatterns, productivityPatterns);

            var analysisResult = new UserSchedulingPatternsResponse(
                userId,
                endDate - startDate,
                productivityPatterns,
                detectedPatterns,
                insights,
                DateTime.UtcNow
            );

            // Cache the results
            _patternsCache[userId] = analysisResult;

            _logger.LogInformation("Successfully analyzed scheduling patterns for user {UserId}", userId);
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scheduling patterns for user {UserId}", userId);
            return CreateDefaultPatternsResponse(userId);
        }
    }

    public async Task<UserSchedulingPatternsResponse> GetUserSchedulingPatternsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache first
            if (_patternsCache.TryGetValue(userId, out var cachedPatterns))
            {
                // Check if patterns are still fresh (within last 7 days)
                if (DateTime.UtcNow - cachedPatterns.AnalyzedAt < TimeSpan.FromDays(7))
                {
                    return cachedPatterns;
                }
            }

            // Analyze patterns for the last 30 days
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-30);
            
            return await AnalyzeSchedulingPatternsAsync(userId, startDate, endDate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduling patterns for user {UserId}", userId);
            return CreateDefaultPatternsResponse(userId);
        }
    }

    public async Task RecordSchedulingActivityAsync(
        Guid userId, 
        List<SmartScheduledItem> scheduledItems, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Recording scheduling activity for user {UserId} with {ItemCount} items",
                userId, scheduledItems.Count);

            var activityRecord = new SchedulingActivityRecord(
                Guid.NewGuid(),
                userId,
                DateTime.UtcNow,
                scheduledItems,
                CalculateScheduleQualityScore(scheduledItems),
                ExtractSchedulingContext(scheduledItems)
            );

            // Add to activity records
            var userRecords = _activityRecords.GetOrAdd(userId, _ => new List<SchedulingActivityRecord>());
            userRecords.Add(activityRecord);

            // Keep only the last 100 records per user
            if (userRecords.Count > 100)
            {
                userRecords.RemoveRange(0, userRecords.Count - 100);
            }

            // Persist to storage (would implement actual database persistence here)
            await PersistSchedulingActivity(activityRecord, cancellationToken);

            _logger.LogDebug("Successfully recorded scheduling activity for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording scheduling activity for user {UserId}", userId);
        }
    }

    public async Task RecordScheduleFeedbackAsync(
        Guid userId, 
        Guid scheduleId, 
        ScheduleFeedback feedback, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Recording schedule feedback for user {UserId}, schedule {ScheduleId}", userId, scheduleId);

            var feedbackRecord = new ScheduleFeedbackRecord(
                Guid.NewGuid(),
                userId,
                scheduleId,
                feedback,
                DateTime.UtcNow
            );

            // Persist feedback (would implement actual database persistence here)
            await PersistScheduleFeedback(feedbackRecord, cancellationToken);

            // Use feedback to improve future scheduling
            await IncorporateFeedbackIntoLearning(userId, feedbackRecord, cancellationToken);

            _logger.LogInformation("Successfully recorded schedule feedback for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording schedule feedback for user {UserId}", userId);
        }
    }

    public async Task<SmartSchedulingPreferences> LearnAndUpdatePreferencesAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Learning and updating preferences for user {UserId}", userId);

            var currentPreferences = await GetUserPreferencesAsync(userId, cancellationToken);
            var patterns = await GetUserSchedulingPatternsAsync(userId, cancellationToken);
            var activityRecords = GetUserActivityRecords(userId);

            // Apply machine learning to update preferences
            var updatedPreferences = ApplyMachineLearning(currentPreferences, patterns, activityRecords);

            // Only update if significant changes detected
            if (HasSignificantChanges(currentPreferences, updatedPreferences))
            {
                updatedPreferences = await UpdatePreferencesAsync(userId, updatedPreferences, cancellationToken);
                _logger.LogInformation("Updated preferences for user {UserId} based on learned patterns", userId);
            }
            else
            {
                _logger.LogDebug("No significant preference changes detected for user {UserId}", userId);
            }

            return updatedPreferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error learning and updating preferences for user {UserId}", userId);
            return await GetUserPreferencesAsync(userId, cancellationToken);
        }
    }

    public async Task<ProductivityInsightsResponse> GetProductivityInsightsAsync(
        Guid userId, 
        AnalysisTimeframe timeframe, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting productivity insights for user {UserId} from {StartDate} to {EndDate}",
                userId, timeframe.StartDate, timeframe.EndDate);

            var taskHistory = await GetUserTaskHistory(userId, timeframe.StartDate, timeframe.EndDate, cancellationToken);
            var activityRecords = GetUserActivityRecords(userId).Where(r => 
                r.RecordedAt >= timeframe.StartDate && r.RecordedAt <= timeframe.EndDate).ToList();

            var insights = AnalyzeProductivityInsights(userId, taskHistory, activityRecords, timeframe);

            _logger.LogInformation("Generated productivity insights for user {UserId}", userId);
            return insights;
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
        try
        {
            _logger.LogDebug("Predicting optimal times for user {UserId}, category {TaskCategory}", userId, taskCategory);

            var patterns = await GetUserSchedulingPatternsAsync(userId, cancellationToken);
            var preferences = await GetUserPreferencesAsync(userId, cancellationToken);

            var optimalSlots = PredictOptimalTimesForCategory(patterns, preferences, taskCategory);

            return optimalSlots;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting optimal times for user {UserId}", userId);
            return new List<OptimalTimeSlot>();
        }
    }

    public async Task<List<EnergyLevelPrediction>> GetEnergyLevelPredictionsAsync(
        Guid userId, 
        DateTime date, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting energy level predictions for user {UserId} on {Date}", userId, date);

            var preferences = await GetUserPreferencesAsync(userId, cancellationToken);
            var patterns = await GetUserSchedulingPatternsAsync(userId, cancellationToken);

            var energyPredictions = PredictEnergyLevels(preferences, patterns, date);

            return energyPredictions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting energy level predictions for user {UserId}", userId);
            return new List<EnergyLevelPrediction>();
        }
    }

    public async Task<SmartSchedulingPreferences> InitializeDefaultPreferencesAsync(
        Guid userId, 
        string timezone, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing default preferences for user {UserId}", userId);

            var defaultPreferences = new SmartSchedulingPreferences(
                userId,
                new WorkingHours(
                    TimeSpan.FromHours(9),   // 9 AM start
                    TimeSpan.FromHours(17),  // 5 PM end
                    new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                    TimeSpan.FromHours(12),  // Lunch at noon
                    TimeSpan.FromMinutes(60), // 1 hour lunch
                    false
                ),
                new List<TimeSpan> { TimeSpan.FromHours(10.5), TimeSpan.FromHours(15) }, // 10:30 AM and 3 PM breaks
                3,                        // Max tasks per time block
                TimeSpan.FromMinutes(15), // Minimum task duration
                TimeSpan.FromHours(4),    // Maximum task duration
                new List<string> { "Work", "Personal", "Administrative" }, // Preferred categories
                ProductivityPatterns.Consistent, // Default pattern
                false,                    // Don't allow overlapping tasks
                true,                     // Prefer morning tasks
                true,                     // Require buffer time
                TimeSpan.FromMinutes(15), // 15-minute buffer
                new List<ScheduleConstraint>() // No custom constraints initially
            );

            // Save to cache and storage
            _preferencesCache[userId] = defaultPreferences;
            await SaveUserPreferencesToStorage(userId, defaultPreferences, cancellationToken);

            _logger.LogInformation("Successfully initialized default preferences for user {UserId}", userId);
            return defaultPreferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default preferences for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if service is properly configured and dependencies are available
            return _settings.EnablePatternLearning && await _taskRepository.GetCountAsync() >= 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user preference service availability");
            return false;
        }
    }

    // Private helper methods

    private async Task<SmartSchedulingPreferences?> LoadUserPreferencesFromStorage(Guid userId, CancellationToken cancellationToken)
    {
        // In a real implementation, this would load from database
        // For now, return null to trigger default initialization
        return null;
    }

    private async Task SaveUserPreferencesToStorage(Guid userId, SmartSchedulingPreferences preferences, CancellationToken cancellationToken)
    {
        // In a real implementation, this would save to database
        _logger.LogDebug("Saving preferences to storage for user {UserId}", userId);
        await Task.CompletedTask;
    }

    private SmartSchedulingPreferences ValidatePreferences(SmartSchedulingPreferences preferences)
    {
        var validatedPreferences = preferences;

        // Validate working hours
        if (preferences.PreferredWorkingHours.StartTime >= preferences.PreferredWorkingHours.EndTime)
        {
            validatedPreferences = validatedPreferences with
            {
                PreferredWorkingHours = preferences.PreferredWorkingHours with
                {
                    EndTime = preferences.PreferredWorkingHours.StartTime.Add(TimeSpan.FromHours(8))
                }
            };
        }

        // Validate task durations
        if (preferences.MinimumTaskDuration >= preferences.MaximumTaskDuration)
        {
            validatedPreferences = validatedPreferences with
            {
                MaximumTaskDuration = preferences.MinimumTaskDuration.Add(TimeSpan.FromHours(1))
            };
        }

        return validatedPreferences;
    }

    private async Task<List<Domain.Entities.AppTask>> GetUserTaskHistory(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        try
        {
            var searchCriteria = new Domain.Common.AppTaskSearchCriteria
            {
                UserId = userId,
                CreatedAfter = startDate,
                CreatedBefore = endDate,
                IncludeArchived = true
            };

            var pagedTasks = await _taskRepository.SearchAsync(searchCriteria, 1, 1000, "CreatedAt", false);
            return pagedTasks.Items.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task history for user {UserId}", userId);
            return new List<Domain.Entities.AppTask>();
        }
    }

    private List<SchedulingActivityRecord> GetUserActivityRecords(Guid userId)
    {
        return _activityRecords.GetValueOrDefault(userId, new List<SchedulingActivityRecord>());
    }

    private List<SchedulingPattern> AnalyzeUserPatterns(List<Domain.Entities.AppTask> taskHistory, List<SchedulingActivityRecord> activityRecords)
    {
        var patterns = new List<SchedulingPattern>();

        // Analyze task creation patterns
        if (taskHistory.Any())
        {
            var creationTimeGroups = taskHistory.GroupBy(t => t.CreatedAt.Hour).OrderByDescending(g => g.Count());
            var mostActiveHour = creationTimeGroups.First();
            
            patterns.Add(new SchedulingPattern(
                "TaskCreationPattern",
                $"Most active task creation time: {mostActiveHour.Key}:00",
                mostActiveHour.Count() / (double)taskHistory.Count,
                new List<TimeSpan> { TimeSpan.FromHours(mostActiveHour.Key) },
                new List<string>(),
                0.7
            ));
        }

        // Analyze category preferences
        if (taskHistory.Any())
        {
            var categoryGroups = taskHistory.GroupBy(t => t.Category.ToString()).OrderByDescending(g => g.Count());
            var preferredCategory = categoryGroups.First();
            
            patterns.Add(new SchedulingPattern(
                "CategoryPreference",
                $"Preferred task category: {preferredCategory.Key}",
                preferredCategory.Count() / (double)taskHistory.Count,
                new List<TimeSpan>(),
                new List<string> { preferredCategory.Key },
                0.8
            ));
        }

        return patterns;
    }

    private ProductivityPatterns DetectProductivityPatterns(List<Domain.Entities.AppTask> taskHistory, List<SchedulingActivityRecord> activityRecords)
    {
        // Analyze when users are most productive based on task completion patterns
        if (taskHistory.Any())
        {
            var completedTasks = taskHistory.Where(t => t.Status == Domain.ValueObjects.AppTaskStatus.Completed);
            if (completedTasks.Any())
            {
                var completionHours = completedTasks.Select(t => t.UpdatedAt?.Hour ?? 12).ToList();
                var averageHour = completionHours.Average();
                
                return averageHour switch
                {
                    < 12 => ProductivityPatterns.MorningPerson,
                    >= 12 and <= 16 => ProductivityPatterns.AfternoonPeak,
                    > 16 => ProductivityPatterns.NightOwl,
                    _ => ProductivityPatterns.Consistent
                };
            }
        }

        return ProductivityPatterns.Consistent;
    }

    private List<string> GenerateSchedulingInsights(List<SchedulingPattern> patterns, ProductivityPatterns productivityPattern)
    {
        var insights = new List<string>();

        insights.Add($"Your productivity pattern is: {productivityPattern}");
        
        foreach (var pattern in patterns)
        {
            insights.Add($"Pattern detected: {pattern.Description} (frequency: {pattern.Frequency:P0})");
        }

        // Add actionable insights based on patterns
        if (productivityPattern == ProductivityPatterns.MorningPerson)
        {
            insights.Add("Schedule your most important tasks in the morning for optimal productivity");
        }
        else if (productivityPattern == ProductivityPatterns.AfternoonPeak)
        {
            insights.Add("Your peak productivity is in the afternoon - save challenging tasks for 1-4 PM");
        }

        return insights;
    }

    private UserSchedulingPatternsResponse CreateDefaultPatternsResponse(Guid userId)
    {
        return new UserSchedulingPatternsResponse(
            userId,
            TimeSpan.FromDays(30),
            ProductivityPatterns.Consistent,
            new List<SchedulingPattern>(),
            new List<string> { "No scheduling patterns detected yet. Complete more tasks to build your pattern profile." },
            DateTime.UtcNow
        );
    }

    private double CalculateScheduleQualityScore(List<SmartScheduledItem> scheduledItems)
    {
        if (!scheduledItems.Any()) return 0.0;

        // Simple quality calculation based on various factors
        var score = 0.8; // Base score

        // Bonus for buffer time between tasks
        for (int i = 1; i < scheduledItems.Count; i++)
        {
            var gap = scheduledItems[i].StartTime - scheduledItems[i - 1].EndTime;
            if (gap >= TimeSpan.FromMinutes(15))
            {
                score += 0.05;
            }
        }

        // Bonus for priority alignment (high priority tasks scheduled earlier)
        var highPriorityItems = scheduledItems.Where(i => i.Priority == Domain.ValueObjects.Priority.High);
        if (highPriorityItems.Any())
        {
            var averageHour = highPriorityItems.Average(i => i.StartTime.Hour);
            if (averageHour <= 12) // Morning
            {
                score += 0.1;
            }
        }

        return Math.Min(1.0, score);
    }

    private Dictionary<string, object> ExtractSchedulingContext(List<SmartScheduledItem> scheduledItems)
    {
        return new Dictionary<string, object>
        {
            ["TotalItems"] = scheduledItems.Count,
            ["Categories"] = scheduledItems.Select(i => i.Category).Distinct().ToList(),
            ["AverageDuration"] = scheduledItems.Any() ? scheduledItems.Average(i => i.EstimatedDuration.TotalMinutes) : 0,
            ["TimeRange"] = scheduledItems.Any() ? 
                (scheduledItems.Max(i => i.EndTime) - scheduledItems.Min(i => i.StartTime)).TotalHours : 0
        };
    }

    private async Task PersistSchedulingActivity(SchedulingActivityRecord record, CancellationToken cancellationToken)
    {
        // In a real implementation, this would save to database
        _logger.LogDebug("Persisting scheduling activity for user {UserId}", record.UserId);
        await Task.CompletedTask;
    }

    private async Task PersistScheduleFeedback(ScheduleFeedbackRecord record, CancellationToken cancellationToken)
    {
        // In a real implementation, this would save to database
        _logger.LogDebug("Persisting schedule feedback for user {UserId}", record.UserId);
        await Task.CompletedTask;
    }

    private async Task IncorporateFeedbackIntoLearning(Guid userId, ScheduleFeedbackRecord feedback, CancellationToken cancellationToken)
    {
        // Use feedback to adjust learning algorithms
        // This would involve more sophisticated ML algorithms in a real implementation
        _logger.LogDebug("Incorporating feedback into learning for user {UserId}", userId);
        await Task.CompletedTask;
    }

    private SmartSchedulingPreferences ApplyMachineLearning(
        SmartSchedulingPreferences currentPreferences, 
        UserSchedulingPatternsResponse patterns, 
        List<SchedulingActivityRecord> activityRecords)
    {
        var updatedPreferences = currentPreferences;

        // Update productivity pattern based on learned patterns
        updatedPreferences = updatedPreferences with { ProductivityPattern = patterns.DetectedPatterns };

        // Adjust working hours based on activity patterns
        if (activityRecords.Any())
        {
            var mostActiveHours = activityRecords
                .SelectMany(r => r.ScheduledItems.Select(i => i.StartTime.Hour))
                .GroupBy(h => h)
                .OrderByDescending(g => g.Count())
                .Take(2)
                .Select(g => g.Key)
                .ToList();

            if (mostActiveHours.Any())
            {
                var earliestActive = TimeSpan.FromHours(mostActiveHours.Min());
                var latestActive = TimeSpan.FromHours(mostActiveHours.Max() + 8); // 8-hour work day

                updatedPreferences = updatedPreferences with
                {
                    PreferredWorkingHours = updatedPreferences.PreferredWorkingHours with
                    {
                        StartTime = earliestActive,
                        EndTime = latestActive
                    }
                };
            }
        }

        return updatedPreferences;
    }

    private bool HasSignificantChanges(SmartSchedulingPreferences current, SmartSchedulingPreferences updated)
    {
        // Check if there are significant differences between preferences
        var workingHoursChanged = Math.Abs((current.PreferredWorkingHours.StartTime - updated.PreferredWorkingHours.StartTime).TotalMinutes) > 30;
        var productivityPatternChanged = current.ProductivityPattern != updated.ProductivityPattern;
        
        return workingHoursChanged || productivityPatternChanged;
    }

    private ProductivityInsightsResponse AnalyzeProductivityInsights(
        Guid userId, 
        List<Domain.Entities.AppTask> taskHistory, 
        List<SchedulingActivityRecord> activityRecords, 
        AnalysisTimeframe timeframe)
    {
        var productivityByTimeOfDay = new Dictionary<string, double>();
        var productivityByCategory = new Dictionary<string, double>();
        var trends = new List<ProductivityTrend>();

        // Analyze productivity by time of day
        if (taskHistory.Any())
        {
            var completedTasks = taskHistory.Where(t => t.Status == Domain.ValueObjects.AppTaskStatus.Completed);
            if (completedTasks.Any())
            {
                var hourlyProductivity = completedTasks
                    .GroupBy(t => t.UpdatedAt?.Hour ?? 12)
                    .ToDictionary(g => $"{g.Key}:00", g => g.Count() / (double)completedTasks.Count());
                
                productivityByTimeOfDay = hourlyProductivity;
            }

            // Analyze productivity by category
            var categoryProductivity = taskHistory
                .Where(t => t.Status == Domain.ValueObjects.AppTaskStatus.Completed)
                .GroupBy(t => t.Category.ToString())
                .ToDictionary(g => g.Key, g => g.Count() / (double)taskHistory.Count());
            
            productivityByCategory = categoryProductivity;
        }

        var averageProductivity = activityRecords.Any() ? activityRecords.Average(r => r.QualityScore) : 0.75;

        var insights = new List<string>
        {
            $"Your average productivity score is {averageProductivity:P0}",
            "Most productive time periods identified",
            "Task completion patterns analyzed"
        };

        var recommendations = new List<string>
        {
            "Schedule demanding tasks during your peak hours",
            "Consider grouping similar tasks together",
            "Add buffer time between different types of work"
        };

        return new ProductivityInsightsResponse(
            userId,
            timeframe,
            averageProductivity,
            productivityByTimeOfDay,
            productivityByCategory,
            trends,
            insights,
            recommendations,
            DateTime.UtcNow
        );
    }

    private List<OptimalTimeSlot> PredictOptimalTimesForCategory(
        UserSchedulingPatternsResponse patterns, 
        SmartSchedulingPreferences preferences, 
        string taskCategory)
    {
        var optimalSlots = new List<OptimalTimeSlot>();

        // Predict based on productivity patterns
        switch (preferences.ProductivityPattern)
        {
            case ProductivityPatterns.MorningPerson:
                optimalSlots.Add(new OptimalTimeSlot(
                    TimeSpan.FromHours(8),
                    TimeSpan.FromHours(11),
                    0.9,
                    "Morning peak productivity period",
                    new List<string> { "High energy", "Few distractions", "Fresh mind" }
                ));
                break;

            case ProductivityPatterns.AfternoonPeak:
                optimalSlots.Add(new OptimalTimeSlot(
                    TimeSpan.FromHours(13),
                    TimeSpan.FromHours(16),
                    0.85,
                    "Afternoon peak productivity period",
                    new List<string> { "Post-lunch energy boost", "Focused attention" }
                ));
                break;

            default:
                optimalSlots.Add(new OptimalTimeSlot(
                    TimeSpan.FromHours(10),
                    TimeSpan.FromHours(12),
                    0.8,
                    "Mid-morning productivity period",
                    new List<string> { "Good energy levels", "Minimal interruptions" }
                ));
                break;
        }

        return optimalSlots;
    }

    private List<EnergyLevelPrediction> PredictEnergyLevels(
        SmartSchedulingPreferences preferences, 
        UserSchedulingPatternsResponse patterns, 
        DateTime date)
    {
        var predictions = new List<EnergyLevelPrediction>();

        // Generate hourly predictions for working hours
        var workingHours = preferences.PreferredWorkingHours;
        var startHour = (int)workingHours.StartTime.TotalHours;
        var endHour = (int)workingHours.EndTime.TotalHours;

        for (int hour = startHour; hour <= endHour; hour++)
        {
            var energyLevel = PredictEnergyForHour(hour, preferences.ProductivityPattern);
            var levelType = energyLevel switch
            {
                >= 0.9 => EnergyLevelType.Peak,
                >= 0.8 => EnergyLevelType.VeryHigh,
                >= 0.7 => EnergyLevelType.High,
                >= 0.5 => EnergyLevelType.Moderate,
                >= 0.3 => EnergyLevelType.Low,
                _ => EnergyLevelType.VeryLow
            };

            predictions.Add(new EnergyLevelPrediction(
                TimeSpan.FromHours(hour),
                energyLevel,
                levelType,
                0.8, // Confidence score
                new List<string> { "Historical patterns", "Circadian rhythms", "User preferences" }
            ));
        }

        return predictions;
    }

    private double PredictEnergyForHour(int hour, ProductivityPatterns pattern)
    {
        return pattern switch
        {
            ProductivityPatterns.MorningPerson => hour <= 11 ? 1.0 - (hour - 8) * 0.05 : 0.6,
            ProductivityPatterns.AfternoonPeak => hour >= 13 && hour <= 16 ? 1.0 : 0.7,
            ProductivityPatterns.NightOwl => hour >= 14 ? 0.8 + (hour - 14) * 0.02 : 0.5,
            _ => hour >= 10 && hour <= 15 ? 0.8 : 0.6
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cacheSemaphore?.Dispose();
        _preferencesCache.Clear();
        _patternsCache.Clear();
        _activityRecords.Clear();

        _disposed = true;
    }
}

// Supporting record types

internal record SchedulingActivityRecord(
    Guid Id,
    Guid UserId,
    DateTime RecordedAt,
    List<SmartScheduledItem> ScheduledItems,
    double QualityScore,
    Dictionary<string, object> Context
);

internal record ScheduleFeedbackRecord(
    Guid Id,
    Guid UserId,
    Guid ScheduleId,
    ScheduleFeedback Feedback,
    DateTime RecordedAt
);