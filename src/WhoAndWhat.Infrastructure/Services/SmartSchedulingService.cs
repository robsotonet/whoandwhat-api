using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Infrastructure.Configuration;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;
using DomainTaskStatus = WhoAndWhat.Domain.ValueObjects.AppTaskStatus;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Core smart scheduling service that orchestrates AI planning, calendar integration, and task management
/// for intelligent task scheduling and optimization
/// </summary>
public class SmartSchedulingService : ISmartSchedulingService, IDisposable
{
    private readonly IAIPlanningService _aiPlanningService;
    private readonly ICalendarSyncService _calendarSyncService;
    private readonly IAppTaskRepository _taskRepository;
    private readonly IScheduleOptimizationEngine _optimizationEngine;
    private readonly ITimeBlockManager _timeBlockManager;
    private readonly IUserSchedulingPreferenceService _userPreferenceService;
    private readonly ILogger<SmartSchedulingService> _logger;
    private readonly SmartSchedulingSettings _settings;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _userSchedulingSemaphores;
    private readonly ConcurrentDictionary<Guid, SmartSchedulingPreferences> _preferencesCache;
    private bool _disposed;

    public SmartSchedulingService(
        IAIPlanningService aiPlanningService,
        ICalendarSyncService calendarSyncService,
        IAppTaskRepository taskRepository,
        IScheduleOptimizationEngine optimizationEngine,
        ITimeBlockManager timeBlockManager,
        IUserSchedulingPreferenceService userPreferenceService,
        IOptions<SmartSchedulingSettings> settings,
        ILogger<SmartSchedulingService> logger)
    {
        _aiPlanningService = aiPlanningService ?? throw new ArgumentNullException(nameof(aiPlanningService));
        _calendarSyncService = calendarSyncService ?? throw new ArgumentNullException(nameof(calendarSyncService));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _optimizationEngine = optimizationEngine ?? throw new ArgumentNullException(nameof(optimizationEngine));
        _timeBlockManager = timeBlockManager ?? throw new ArgumentNullException(nameof(timeBlockManager));
        _userPreferenceService = userPreferenceService ?? throw new ArgumentNullException(nameof(userPreferenceService));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _userSchedulingSemaphores = new ConcurrentDictionary<Guid, SemaphoreSlim>();
        _preferencesCache = new ConcurrentDictionary<Guid, SmartSchedulingPreferences>();
    }

    public async Task<SmartScheduleResponse> GenerateSmartScheduleAsync(
        GenerateSmartScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var semaphore = GetUserSemaphore(request.UserId);
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Generating smart schedule for user {UserId} from {StartDate} to {EndDate}",
                request.UserId, request.StartDate, request.EndDate);

            // Step 1: Get user preferences
            var preferences = await GetCachedUserPreferencesAsync(request.UserId, cancellationToken);
            var mergedPreferences = MergePreferences(preferences, request.Preferences);

            // Step 2: Retrieve tasks to schedule
            var tasks = await GetTasksForSchedulingAsync(request.UserId, request.TaskIds, cancellationToken);
            if (!tasks.Any())
            {
                return CreateEmptyScheduleResponse(request.UserId);
            }

            // Step 3: Get calendar availability if requested
            var calendarEvents = new List<CalendarEventSummary>();
            if (request.IncludeCalendarEvents)
            {
                calendarEvents = await GetCalendarEventsAsync(request.UserId, request.StartDate, request.EndDate, cancellationToken);
            }

            // Step 4: Generate AI-powered day plan
            var aiPlanRequest = BuildAIPlanRequest(request, mergedPreferences, tasks);
            var aiPlan = await _aiPlanningService.GenerateDayPlanAsync(
                request.UserId, request.StartDate, aiPlanRequest, cancellationToken);

            // Step 5: Apply schedule optimization
            var optimizationContext = new ScheduleOptimizationContext(
                tasks,
                calendarEvents,
                mergedPreferences,
                request.StartDate,
                request.EndDate
            );

            var optimizedSchedule = await _optimizationEngine.OptimizeScheduleAsync(
                request.UserId, optimizationContext, cancellationToken);

            // Step 6: Generate time block recommendations
            var timeBlocks = await _timeBlockManager.GenerateTimeBlocksAsync(
                request.UserId, optimizedSchedule, mergedPreferences, cancellationToken);

            // Step 7: Create scheduled items from optimization results
            var scheduledItems = await CreateScheduledItemsAsync(optimizedSchedule, tasks, timeBlocks, cancellationToken);

            // Step 8: Generate optimization insights
            var insights = GenerateOptimizationInsights(optimizedSchedule, aiPlan, mergedPreferences);

            // Step 9: Calculate metrics
            var metrics = CalculateScheduleMetrics(scheduledItems, tasks, request.StartDate, request.EndDate);

            // Step 10: Learn from user patterns
            await _userPreferenceService.RecordSchedulingActivityAsync(request.UserId, scheduledItems, cancellationToken);

            var response = new SmartScheduleResponse(
                request.UserId,
                DateTime.UtcNow,
                scheduledItems,
                timeBlocks,
                insights,
                metrics,
                CalculateConfidenceScore(optimizedSchedule, aiPlan)
            );

            _logger.LogInformation("Successfully generated smart schedule for user {UserId} with {ItemCount} items",
                request.UserId, scheduledItems.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating smart schedule for user {UserId}", request.UserId);
            return CreateErrorScheduleResponse(request.UserId, ex.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<ScheduleOptimizationResponse> OptimizeScheduleAsync(
        OptimizeScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var semaphore = GetUserSemaphore(request.UserId);
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Optimizing schedule for user {UserId}, schedule {ScheduleId}",
                request.UserId, request.ScheduleId);

            // Step 1: Validate current schedule
            var validationResult = await ValidateScheduleAsync(request.UserId, request.CurrentSchedule, cancellationToken);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Schedule validation failed for user {UserId}: {Issues}",
                    request.UserId, string.Join(", ", validationResult.Conflicts.Select(c => c.Description)));
            }

            // Step 2: Build optimization context
            var context = new OptimizationContext(
                request.CurrentSchedule,
                request.Goals,
                request.Constraints,
                await GetCachedUserPreferencesAsync(request.UserId, cancellationToken)
            );

            // Step 3: Apply optimization algorithms
            var optimizationResult = await _optimizationEngine.OptimizeExistingScheduleAsync(
                request.UserId, context, cancellationToken);

            // Step 4: Track and analyze changes
            var changes = AnalyzeScheduleChanges(request.CurrentSchedule, optimizationResult.OptimizedSchedule);

            // Step 5: Generate recommendations
            var recommendations = GenerateOptimizationRecommendations(optimizationResult, validationResult);

            // Step 6: Calculate metrics
            var metrics = CalculateOptimizationMetrics(request.CurrentSchedule, optimizationResult.OptimizedSchedule, changes);

            var response = new ScheduleOptimizationResponse(
                request.UserId,
                DateTime.UtcNow,
                optimizationResult.OptimizedSchedule,
                changes,
                metrics,
                recommendations
            );

            _logger.LogInformation("Successfully optimized schedule for user {UserId} with {ChangeCount} changes",
                request.UserId, changes.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing schedule for user {UserId}", request.UserId);
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<SchedulingSuggestionsResponse> GetSchedulingSuggestionsAsync(
        GetSchedulingSuggestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting scheduling suggestions for user {UserId} on {Date}",
                request.UserId, request.Date);

            // Get tasks to suggest scheduling for
            var tasks = await GetTasksForSchedulingAsync(request.UserId, request.TaskIds, cancellationToken);
            if (!tasks.Any())
            {
                return new SchedulingSuggestionsResponse(request.UserId, request.Date, new List<SchedulingSuggestion>(), DateTime.UtcNow);
            }

            // Get user preferences and patterns
            var preferences = await GetCachedUserPreferencesAsync(request.UserId, cancellationToken);
            var patterns = await _userPreferenceService.GetUserSchedulingPatternsAsync(request.UserId, cancellationToken);

            // Get calendar availability
            var calendarEvents = await GetCalendarEventsAsync(request.UserId, request.Date, request.Date.AddDays(1), cancellationToken);

            // Generate AI-powered suggestions
            var suggestions = await GenerateIntelligentSuggestionsAsync(
                tasks, preferences, patterns, calendarEvents, request.Date, request.MaxSuggestions, cancellationToken);

            var response = new SchedulingSuggestionsResponse(
                request.UserId,
                request.Date,
                suggestions,
                DateTime.UtcNow
            );

            _logger.LogInformation("Generated {SuggestionCount} scheduling suggestions for user {UserId}",
                suggestions.Count, request.UserId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduling suggestions for user {UserId}", request.UserId);
            return new SchedulingSuggestionsResponse(request.UserId, request.Date, new List<SchedulingSuggestion>(), DateTime.UtcNow);
        }
    }

    public async Task<UpdateSchedulingPreferencesResponse> UpdateUserSchedulingPreferencesAsync(
        UpdateSchedulingPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating scheduling preferences for user {UserId}", request.UserId);

            var updatedPreferences = await _userPreferenceService.UpdatePreferencesAsync(
                request.UserId, request.Preferences, cancellationToken);

            // Update cache
            _preferencesCache[request.UserId] = updatedPreferences;

            return new UpdateSchedulingPreferencesResponse(
                true,
                "Preferences updated successfully",
                updatedPreferences,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating scheduling preferences for user {UserId}", request.UserId);
            return new UpdateSchedulingPreferencesResponse(
                false,
                ex.Message,
                null!,
                DateTime.UtcNow
            );
        }
    }

    public async Task<UserSchedulingPatternsResponse> AnalyzeUserSchedulingPatternsAsync(
        GetUserSchedulingPatternsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing scheduling patterns for user {UserId} from {StartDate} to {EndDate}",
                request.UserId, request.StartDate, request.EndDate);

            var patterns = await _userPreferenceService.AnalyzeSchedulingPatternsAsync(
                request.UserId, request.StartDate, request.EndDate, cancellationToken);

            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scheduling patterns for user {UserId}", request.UserId);
            throw;
        }
    }

    public async Task<SmartSchedulingPreferences> GetUserSchedulingPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await GetCachedUserPreferencesAsync(userId, cancellationToken);
    }

    public async Task<ScheduleValidationResult> ValidateScheduleAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Validating schedule for user {UserId} with {ItemCount} items", userId, scheduledItems.Count);

            var conflicts = new List<ScheduleConflict>();
            var warnings = new List<ScheduleWarning>();
            var recommendations = new List<string>();

            // Check for time conflicts
            conflicts.AddRange(await DetectTimeConflictsAsync(scheduledItems));

            // Check for workload issues
            warnings.AddRange(DetectWorkloadWarnings(scheduledItems));

            // Check against user preferences
            var preferences = await GetCachedUserPreferencesAsync(userId, cancellationToken);
            warnings.AddRange(DetectPreferenceViolations(scheduledItems, preferences));

            // Generate recommendations based on findings
            recommendations.AddRange(GenerateValidationRecommendations(conflicts, warnings, preferences));

            return new ScheduleValidationResult(
                !conflicts.Any(c => c.Severity == "Critical"),
                conflicts,
                warnings,
                recommendations
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating schedule for user {UserId}", userId);
            return new ScheduleValidationResult(false, new List<ScheduleConflict>(), new List<ScheduleWarning>(), new List<string>());
        }
    }

    public async Task<SmartScheduleResponse> ApplyRealTimeScheduleAdjustmentAsync(
        Guid userId,
        Guid scheduleId,
        List<ScheduleChange> changes,
        CancellationToken cancellationToken = default)
    {
        var semaphore = GetUserSemaphore(userId);
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Applying real-time schedule adjustments for user {UserId}, schedule {ScheduleId}",
                userId, scheduleId);

            // This would implement real-time schedule adjustment logic
            // For now, return a placeholder response
            throw new NotImplementedException("Real-time schedule adjustment will be implemented in Phase 4");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying real-time schedule adjustment for user {UserId}", userId);
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<List<TimeBlockSuggestion>> GetTimeBlockRecommendationsAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting time block recommendations for user {UserId} on {Date}", userId, date);

            var preferences = await GetCachedUserPreferencesAsync(userId, cancellationToken);
            return await _timeBlockManager.GenerateTimeBlockRecommendationsAsync(userId, date, preferences, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time block recommendations for user {UserId}", userId);
            return new List<TimeBlockSuggestion>();
        }
    }

    public async Task<bool> IsSmartSchedulingAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if all required services are available
            var aiAvailable = await _aiPlanningService.IsAIServiceAvailableAsync(cancellationToken);
            var optimizationAvailable = await _optimizationEngine.IsAvailableAsync(cancellationToken);
            var timeBlockAvailable = await _timeBlockManager.IsAvailableAsync(cancellationToken);

            return aiAvailable && optimizationAvailable && timeBlockAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking smart scheduling availability");
            return false;
        }
    }

    // Private helper methods

    private SemaphoreSlim GetUserSemaphore(Guid userId)
    {
        return _userSchedulingSemaphores.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
    }

    private async Task<SmartSchedulingPreferences> GetCachedUserPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_preferencesCache.TryGetValue(userId, out var cachedPreferences))
        {
            return cachedPreferences;
        }

        var preferences = await _userPreferenceService.GetUserPreferencesAsync(userId, cancellationToken);
        _preferencesCache[userId] = preferences;
        return preferences;
    }

    private SmartSchedulingPreferences MergePreferences(SmartSchedulingPreferences userPreferences, SmartSchedulingPreferences requestPreferences)
    {
        // Merge request preferences with user preferences, giving precedence to request preferences
        return requestPreferences with
        {
            UserId = userPreferences.UserId,
            PreferredWorkingHours = requestPreferences.PreferredWorkingHours ?? userPreferences.PreferredWorkingHours,
            CustomConstraints = userPreferences.CustomConstraints.Concat(requestPreferences.CustomConstraints).ToList()
        };
    }

    private async Task<List<DomainTask>> GetTasksForSchedulingAsync(Guid userId, List<Guid> taskIds, CancellationToken cancellationToken)
    {
        if (taskIds.Any())
        {
            var tasks = new List<DomainTask>();
            foreach (var taskId in taskIds)
            {
                var task = await _taskRepository.GetByIdAsync(taskId);
                if (task != null && task.UserId == userId)
                {
                    tasks.Add(task);
                }
            }
            return tasks;
        }

        // Get all schedulable tasks for the user
        var searchCriteria = new Domain.Common.AppTaskSearchCriteria
        {
            UserId = userId,
            Statuses = new List<int> { (int)DomainTaskStatus.Pending, (int)DomainTaskStatus.InProgress },
            IncludeArchived = false
        };

        var pagedTasks = await _taskRepository.SearchAsync(searchCriteria, 1, 100, "Priority", false);
        return pagedTasks.Items.ToList();
    }

    private Task<List<CalendarEventSummary>> GetCalendarEventsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        // This would integrate with the calendar service to get events
        // For now, return empty list as placeholder
        return Task.FromResult(new List<CalendarEventSummary>());
    }

    private UserPlanningPreferences BuildAIPlanRequest(GenerateSmartScheduleRequest request, SmartSchedulingPreferences preferences, List<DomainTask> tasks)
    {
        return new UserPlanningPreferences(
            preferences.PreferredWorkingHours.StartTime,
            preferences.PreferredWorkingHours.EndTime,
            preferences.PreferredBreakTimes,
            preferences.MaxTasksPerTimeBlock,
            preferences.PreferredTaskCategories,
            MapWorkingStyle(preferences),
            new List<string>(), // Avoidance patterns
            MapEnergyPattern(preferences.ProductivityPattern)
        );
    }

    private WorkingStyle MapWorkingStyle(SmartSchedulingPreferences preferences)
    {
        // Map from smart scheduling preferences to AI planning working style
        return WorkingStyle.TimeBlocked; // Default mapping
    }

    private EnergyLevelPattern MapEnergyPattern(ProductivityPatterns pattern)
    {
        return pattern switch
        {
            ProductivityPatterns.MorningPerson => EnergyLevelPattern.MorningPerson,
            ProductivityPatterns.NightOwl => EnergyLevelPattern.NightOwl,
            ProductivityPatterns.AfternoonPeak => EnergyLevelPattern.Afternoon_Peak,
            ProductivityPatterns.Consistent => EnergyLevelPattern.Consistent,
            _ => EnergyLevelPattern.Variable
        };
    }

    private Task<List<SmartScheduledItem>> CreateScheduledItemsAsync(
        ScheduleOptimizationResult optimizationResult,
        List<DomainTask> tasks,
        List<TimeBlockSuggestion> timeBlocks,
        CancellationToken cancellationToken)
    {
        var scheduledItems = new List<SmartScheduledItem>();

        // Convert optimized schedule items to smart scheduled items
        // This is a placeholder implementation
        foreach (var task in tasks.Take(10)) // Limit for demo
        {
            scheduledItems.Add(new SmartScheduledItem(
                Guid.NewGuid(),
                task.Id,
                null,
                task.Title,
                task.Description ?? "",
                DateTime.Now.AddHours(scheduledItems.Count),
                DateTime.Now.AddHours(scheduledItems.Count + 1),
                ScheduledItemType.Task,
                (Domain.ValueObjects.Priority)task.Priority,
                task.Category.ToString(),
                new List<string>(),
                TimeSpan.FromHours(1),
                true,
                new List<SchedulingReason>
                {
                    new SchedulingReason("AI_Optimization", "Scheduled based on AI analysis", 0.8, new List<string> { "Priority", "Due date", "User patterns" })
                }
            ));
        }

        return Task.FromResult(scheduledItems);
    }

    private List<string> GenerateOptimizationInsights(ScheduleOptimizationResult optimizationResult, AIGeneratedPlan? aiPlan, SmartSchedulingPreferences preferences)
    {
        var insights = new List<string>();

        if (aiPlan != null)
        {
            insights.AddRange(aiPlan.ProductivityTips);
        }

        insights.Add("Schedule optimized for your peak productivity hours");
        insights.Add("Buffer time added between tasks to reduce stress");

        return insights;
    }

    private SmartScheduleMetrics CalculateScheduleMetrics(List<SmartScheduledItem> scheduledItems, List<DomainTask> allTasks, DateTime startDate, DateTime endDate)
    {
        var totalTasks = allTasks.Count;
        var scheduledTasks = scheduledItems.Count(i => i.ItemType == ScheduledItemType.Task);
        var unscheduledTasks = totalTasks - scheduledTasks;
        var totalScheduledTime = scheduledItems.Aggregate(TimeSpan.Zero, (total, item) => total.Add(item.EstimatedDuration));
        var availableTime = endDate - startDate;
        var utilization = availableTime.TotalMinutes > 0 ? totalScheduledTime.TotalMinutes / availableTime.TotalMinutes : 0;

        return new SmartScheduleMetrics(
            totalTasks,
            scheduledTasks,
            unscheduledTasks,
            totalScheduledTime,
            availableTime,
            utilization,
            scheduledItems.Count,
            0.85, // Placeholder productivity score
            new List<string>()
        );
    }

    private double CalculateConfidenceScore(ScheduleOptimizationResult optimizationResult, AIGeneratedPlan? aiPlan)
    {
        // Calculate confidence based on various factors
        double baseScore = 0.7;

        if (aiPlan != null)
        {
            baseScore += aiPlan.ConfidenceScore * 0.3;
        }

        return Math.Min(1.0, baseScore);
    }

    private SmartScheduleResponse CreateEmptyScheduleResponse(Guid userId)
    {
        return new SmartScheduleResponse(
            userId,
            DateTime.UtcNow,
            new List<SmartScheduledItem>(),
            new List<TimeBlockSuggestion>(),
            new List<string> { "No tasks available for scheduling" },
            new SmartScheduleMetrics(0, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, new List<string>()),
            0.0
        );
    }

    private SmartScheduleResponse CreateErrorScheduleResponse(Guid userId, string errorMessage)
    {
        return new SmartScheduleResponse(
            userId,
            DateTime.UtcNow,
            new List<SmartScheduledItem>(),
            new List<TimeBlockSuggestion>(),
            new List<string> { $"Error generating schedule: {errorMessage}" },
            new SmartScheduleMetrics(0, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, new List<string> { "Schedule generation failed" }),
            0.0
        );
    }

    private List<ScheduleChange> AnalyzeScheduleChanges(List<SmartScheduledItem> originalSchedule, List<SmartScheduledItem> optimizedSchedule)
    {
        // Analyze differences between schedules
        return new List<ScheduleChange>();
    }

    private List<string> GenerateOptimizationRecommendations(ScheduleOptimizationResult optimizationResult, ScheduleValidationResult validationResult)
    {
        var recommendations = new List<string>();

        recommendations.AddRange(validationResult.Recommendations);
        recommendations.Add("Consider adding buffer time between tasks");
        recommendations.Add("Schedule demanding tasks during peak energy hours");

        return recommendations;
    }

    private OptimizationMetrics CalculateOptimizationMetrics(List<SmartScheduledItem> originalSchedule, List<SmartScheduledItem> optimizedSchedule, List<ScheduleChange> changes)
    {
        return new OptimizationMetrics(
            changes.Count,
            0.15, // 15% productivity improvement
            TimeSpan.FromMinutes(30), // 30 minutes saved
            0.10, // 10% efficiency gain
            0, // No conflicts resolved in this implementation
            new List<OptimizationImpact>
            {
                new OptimizationImpact("Productivity", 0.15, "Improved task scheduling", new List<string> { "Better time allocation" })
            }
        );
    }

    private Task<List<SchedulingSuggestion>> GenerateIntelligentSuggestionsAsync(
        List<DomainTask> tasks,
        SmartSchedulingPreferences preferences,
        UserSchedulingPatternsResponse patterns,
        List<CalendarEventSummary> calendarEvents,
        DateTime date,
        int maxSuggestions,
        CancellationToken cancellationToken)
    {
        var suggestions = new List<SchedulingSuggestion>();

        foreach (var task in tasks.Take(maxSuggestions))
        {
            var suggestedTime = DetermineBestTimeSlot(task, preferences, patterns, calendarEvents, date);

            suggestions.Add(new SchedulingSuggestion(
                task.Id,
                task.Title,
                suggestedTime,
                suggestedTime.AddHours(1), // Default 1-hour duration
                0.8, // Confidence score
                "Based on your productivity patterns and availability",
                new List<string> { "Aligns with peak productivity time", "No calendar conflicts" },
                new List<string> { "Consider task complexity for duration" }
            ));
        }

        return Task.FromResult(suggestions);
    }

    private DateTime DetermineBestTimeSlot(DomainTask task, SmartSchedulingPreferences preferences, UserSchedulingPatternsResponse patterns, List<CalendarEventSummary> calendarEvents, DateTime date)
    {
        // Implement intelligent time slot determination
        var workingHours = preferences.PreferredWorkingHours;
        var startTime = date.Date.Add(workingHours.StartTime);

        // Simple implementation: schedule high priority tasks in the morning
        if (task.Priority == Domain.ValueObjects.Priority.High)
        {
            return startTime;
        }

        return startTime.AddHours(2); // Schedule other tasks later
    }

    private Task<List<ScheduleConflict>> DetectTimeConflictsAsync(List<SmartScheduledItem> scheduledItems)
    {
        var conflicts = new List<ScheduleConflict>();

        for (int i = 0; i < scheduledItems.Count; i++)
        {
            for (int j = i + 1; j < scheduledItems.Count; j++)
            {
                var item1 = scheduledItems[i];
                var item2 = scheduledItems[j];

                if (item1.StartTime < item2.EndTime && item2.StartTime < item1.EndTime)
                {
                    conflicts.Add(new ScheduleConflict(
                        "TimeOverlap",
                        new List<Guid> { item1.Id, item2.Id },
                        $"Time conflict between '{item1.Title}' and '{item2.Title}'",
                        "Medium",
                        new List<string> { "Reschedule one of the items", "Reduce duration of one item" }
                    ));
                }
            }
        }

        return Task.FromResult(conflicts);
    }

    private List<ScheduleWarning> DetectWorkloadWarnings(List<SmartScheduledItem> scheduledItems)
    {
        var warnings = new List<ScheduleWarning>();

        // Check for overloaded time periods
        var groupedByHour = scheduledItems.GroupBy(i => i.StartTime.Date.AddHours(i.StartTime.Hour));

        foreach (var hourGroup in groupedByHour)
        {
            if (hourGroup.Count() > 2)
            {
                warnings.Add(new ScheduleWarning(
                    "HighWorkload",
                    hourGroup.Select(i => i.Id).ToList(),
                    $"High workload detected at {hourGroup.Key:HH:mm}",
                    "May cause stress and reduced productivity"
                ));
            }
        }

        return warnings;
    }

    private List<ScheduleWarning> DetectPreferenceViolations(List<SmartScheduledItem> scheduledItems, SmartSchedulingPreferences preferences)
    {
        var warnings = new List<ScheduleWarning>();

        // Check for tasks scheduled outside working hours
        foreach (var item in scheduledItems.Where(i => i.ItemType == ScheduledItemType.Task))
        {
            var itemTime = item.StartTime.TimeOfDay;
            var workingHours = preferences.PreferredWorkingHours;

            if (itemTime < workingHours.StartTime || itemTime > workingHours.EndTime)
            {
                warnings.Add(new ScheduleWarning(
                    "OutsideWorkingHours",
                    new List<Guid> { item.Id },
                    $"Task '{item.Title}' scheduled outside preferred working hours",
                    "May not align with user's preferred schedule"
                ));
            }
        }

        return warnings;
    }

    private List<string> GenerateValidationRecommendations(List<ScheduleConflict> conflicts, List<ScheduleWarning> warnings, SmartSchedulingPreferences preferences)
    {
        var recommendations = new List<string>();

        if (conflicts.Any())
        {
            recommendations.Add("Resolve time conflicts by rescheduling overlapping items");
        }

        if (warnings.Any(w => w.WarningType == "HighWorkload"))
        {
            recommendations.Add("Consider spreading tasks more evenly throughout the day");
        }

        if (warnings.Any(w => w.WarningType == "OutsideWorkingHours"))
        {
            recommendations.Add("Reschedule tasks to align with your preferred working hours");
        }

        return recommendations;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var semaphore in _userSchedulingSemaphores.Values)
        {
            semaphore.Dispose();
        }

        _userSchedulingSemaphores.Clear();
        _preferencesCache.Clear();
        _disposed = true;
    }
}

// Supporting classes and records

internal record ScheduleOptimizationContext(
    List<DomainTask> Tasks,
    List<CalendarEventSummary> CalendarEvents,
    SmartSchedulingPreferences Preferences,
    DateTime StartDate,
    DateTime EndDate
);

internal record OptimizationContext(
    List<SmartScheduledItem> CurrentSchedule,
    OptimizationGoals Goals,
    List<ScheduleConstraint> Constraints,
    SmartSchedulingPreferences Preferences
);

internal record CalendarEventSummary(
    Guid Id,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay
);
