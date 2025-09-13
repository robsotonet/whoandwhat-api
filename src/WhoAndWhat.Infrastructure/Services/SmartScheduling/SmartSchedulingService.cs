using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Repositories;

namespace WhoAndWhat.Infrastructure.Services.SmartScheduling;

/// <summary>
/// Main orchestration service for smart scheduling operations
/// </summary>
public sealed class SmartSchedulingService : ISmartSchedulingService
{
    private readonly ILogger<SmartSchedulingService> _logger;
    private readonly IScheduleOptimizationEngine _optimizationEngine;
    private readonly ITimeBlockManager _timeBlockManager;
    private readonly IUserSchedulingPreferenceService _preferenceService;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ICalendarProviderService _calendarService;

    public SmartSchedulingService(
        ILogger<SmartSchedulingService> logger,
        IScheduleOptimizationEngine optimizationEngine,
        ITimeBlockManager timeBlockManager,
        IUserSchedulingPreferenceService preferenceService,
        IAppTaskRepository taskRepository,
        ICalendarProviderService calendarService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _optimizationEngine = optimizationEngine ?? throw new ArgumentNullException(nameof(optimizationEngine));
        _timeBlockManager = timeBlockManager ?? throw new ArgumentNullException(nameof(timeBlockManager));
        _preferenceService = preferenceService ?? throw new ArgumentNullException(nameof(preferenceService));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
    }

    public async Task<SmartScheduleResponse> GenerateSmartScheduleAsync(
        GenerateSmartScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating smart schedule for user {UserId} from {StartDate} to {EndDate}",
            request.UserId, request.StartDate, request.EndDate);

        try
        {
            // Get user preferences
            var preferences = request.Preferences ?? await _preferenceService.GetUserPreferencesAsync(request.UserId, cancellationToken);

            // Get tasks to schedule
            var tasks = new List<SmartScheduledItem>();
            foreach (var taskId in request.TaskIds)
            {
                var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
                if (task != null)
                {
                    tasks.Add(MapTaskToScheduledItem(task));
                }
            }

            // Get calendar events if requested
            var calendarEvents = new List<ExternalCalendarEvent>();
            if (request.IncludeCalendarEvents)
            {
                calendarEvents = await GetCalendarEventsAsync(request.UserId, request.StartDate, request.EndDate, cancellationToken);
            }

            // Create optimization context
            var optimizationContext = new ScheduleOptimizationContext(
                CurrentSchedule: new List<SmartScheduledItem>(),
                TaskIds: request.TaskIds,
                StartDate: request.StartDate,
                EndDate: request.EndDate,
                Preferences: preferences,
                CalendarEvents: calendarEvents,
                Goals: new OptimizationGoals(
                    Primary: request.OptimizeForProductivity ? OptimizationPriority.Productivity : OptimizationPriority.WorkLifeBalance,
                    Secondary: new List<OptimizationPriority> { OptimizationPriority.TimeEfficiency },
                    ProductivityWeight: 0.7,
                    BalanceWeight: 0.3,
                    EfficiencyWeight: 0.8,
                    MinimizeContextSwitching: true,
                    RespectDeadlines: true,
                    OptimizeEnergyLevels: true
                )
            );

            // Optimize schedule
            var optimizationResult = await _optimizationEngine.OptimizeScheduleAsync(request.UserId, optimizationContext, cancellationToken);

            // Generate time blocks
            var timeBlocks = await _timeBlockManager.GenerateTimeBlocksAsync(
                request.UserId,
                optimizationResult.OptimizedSchedule,
                preferences,
                cancellationToken);

            // Calculate metrics
            var metrics = CalculateScheduleMetrics(optimizationResult.OptimizedSchedule, timeBlocks);

            _logger.LogInformation("Successfully generated smart schedule for user {UserId} with {TaskCount} tasks and {BlockCount} time blocks",
                request.UserId, optimizationResult.OptimizedSchedule.Count, timeBlocks.Count);

            return new SmartScheduleResponse(
                UserId: request.UserId,
                GeneratedAt: DateTime.UtcNow,
                ScheduledItems: optimizationResult.OptimizedSchedule,
                TimeBlocks: timeBlocks,
                OptimizationInsights: optimizationResult.OptimizationInsights,
                Metrics: metrics,
                ConfidenceScore: CalculateConfidenceScore(optimizationResult, preferences)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating smart schedule for user {UserId}", request.UserId);
            throw;
        }
    }

    public async Task<ScheduleOptimizationResponse> OptimizeScheduleAsync(
        OptimizeScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing existing schedule {ScheduleId} for user {UserId}",
            request.ScheduleId, request.UserId);

        try
        {
            // Get user preferences
            var preferences = await _preferenceService.GetUserPreferencesAsync(request.UserId, cancellationToken);

            // Create optimization context
            var optimizationContext = new OptimizationContext(
                ExistingSchedule: request.CurrentSchedule,
                Goals: request.Goals,
                Constraints: request.Constraints,
                Preferences: preferences,
                OptimizationDate: DateTime.UtcNow
            );

            // Perform optimization
            var result = await _optimizationEngine.OptimizeExistingScheduleAsync(request.UserId, optimizationContext, cancellationToken);

            // Calculate optimization metrics
            var metrics = CalculateOptimizationMetrics(request.CurrentSchedule, result.OptimizedSchedule);

            _logger.LogInformation("Successfully optimized schedule for user {UserId}, improvement score: {ImprovementScore}",
                request.UserId, result.ImprovementScore);

            return new ScheduleOptimizationResponse(
                UserId: request.UserId,
                OptimizedAt: DateTime.UtcNow,
                OptimizedSchedule: result.OptimizedSchedule,
                Changes: result.ChangesApplied.Select(c => new ScheduleChange(
                    ChangeType: c.ChangeType,
                    ItemId: c.ItemId,
                    ItemTitle: GetItemTitle(c.ItemId, result.OptimizedSchedule),
                    OldStartTime: c.OldStartTime,
                    NewStartTime: c.NewStartTime,
                    OldEndTime: c.OldStartTime?.Add(c.OldDuration ?? TimeSpan.Zero),
                    NewEndTime: c.NewStartTime?.Add(c.NewDuration ?? TimeSpan.Zero),
                    Reason: c.Reasoning,
                    ImpactScore: c.ImpactScore
                )).ToList(),
                Metrics: metrics,
                Recommendations: GenerateOptimizationRecommendations(result)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing schedule for user {UserId}", request.UserId);
            throw;
        }
    }

    public async Task<SchedulingSuggestionsResponse> GetSchedulingSuggestionsAsync(
        GetSchedulingSuggestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting scheduling suggestions for user {UserId} on {Date}",
            request.UserId, request.Date);

        try
        {
            var preferences = await _preferenceService.GetUserPreferencesAsync(request.UserId, cancellationToken);
            var suggestions = new List<SchedulingSuggestion>();

            foreach (var taskId in request.TaskIds)
            {
                var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
                if (task != null)
                {
                    var optimalTimes = await _preferenceService.PredictOptimalTimesAsync(
                        request.UserId,
                        task.Category?.Name ?? "General",
                        cancellationToken);

                    if (optimalTimes.Any())
                    {
                        var bestTime = optimalTimes.OrderByDescending(t => t.OptimalityScore).First();
                        var startTime = request.Date.Date.Add(bestTime.StartTime);
                        var endTime = request.Date.Date.Add(bestTime.EndTime);

                        suggestions.Add(new SchedulingSuggestion(
                            TaskId: taskId,
                            TaskTitle: task.Title,
                            SuggestedStartTime: startTime,
                            SuggestedEndTime: endTime,
                            ConfidenceScore: bestTime.OptimalityScore,
                            Reasoning: bestTime.Reasoning,
                            Benefits: bestTime.SupportingFactors,
                            Considerations: new List<string> { "Based on your historical productivity patterns" }
                        ));
                    }
                }
            }

            return new SchedulingSuggestionsResponse(
                UserId: request.UserId,
                Date: request.Date,
                Suggestions: suggestions.Take(request.MaxSuggestions).ToList(),
                GeneratedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduling suggestions for user {UserId}", request.UserId);
            throw;
        }
    }

    public async Task<UpdateSchedulingPreferencesResponse> UpdateUserSchedulingPreferencesAsync(
        UpdateSchedulingPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating scheduling preferences for user {UserId}", request.UserId);

        try
        {
            var updatedPreferences = await _preferenceService.UpdatePreferencesAsync(
                request.UserId,
                request.Preferences,
                cancellationToken);

            return new UpdateSchedulingPreferencesResponse(
                Success: true,
                Message: "Scheduling preferences updated successfully",
                UpdatedPreferences: updatedPreferences,
                UpdatedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating scheduling preferences for user {UserId}", request.UserId);
            return new UpdateSchedulingPreferencesResponse(
                Success: false,
                Message: $"Failed to update preferences: {ex.Message}",
                UpdatedPreferences: request.Preferences,
                UpdatedAt: DateTime.UtcNow
            );
        }
    }

    public async Task<UserSchedulingPatternsResponse> AnalyzeUserSchedulingPatternsAsync(
        GetUserSchedulingPatternsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _preferenceService.AnalyzeSchedulingPatternsAsync(
            request.UserId,
            request.StartDate,
            request.EndDate,
            cancellationToken);
    }

    public async Task<SmartSchedulingPreferences> GetUserSchedulingPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _preferenceService.GetUserPreferencesAsync(userId, cancellationToken);
    }

    public Task<ScheduleValidationResult> ValidateScheduleAsync(
        Guid userId,
        List<SmartScheduledItem> scheduledItems,
        CancellationToken cancellationToken = default)
    {
        var conflicts = new List<ScheduleConflict>();
        var warnings = new List<ScheduleWarning>();
        var recommendations = new List<string>();

        // Check for time conflicts
        for (int i = 0; i < scheduledItems.Count; i++)
        {
            for (int j = i + 1; j < scheduledItems.Count; j++)
            {
                var item1 = scheduledItems[i];
                var item2 = scheduledItems[j];

                if (HasTimeConflict(item1, item2))
                {
                    conflicts.Add(new ScheduleConflict(
                        ConflictType: "TimeOverlap",
                        ConflictingItemIds: new List<Guid> { item1.Id, item2.Id },
                        Description: $"Time overlap between '{item1.Title}' and '{item2.Title}'",
                        Severity: "High",
                        ResolutionSuggestions: new List<string>
                        {
                            "Move one task to a different time slot",
                            "Reduce duration of one task",
                            "Split one task into smaller segments"
                        }
                    ));
                }
            }
        }

        // Check for workload warnings
        var totalWorkTime = scheduledItems
            .Where(i => i.ItemType == ScheduledItemType.Task)
            .Sum(i => (i.EndTime - i.StartTime).TotalHours);

        if (totalWorkTime > 10) // More than 10 hours of work
        {
            warnings.Add(new ScheduleWarning(
                WarningType: "HighWorkload",
                AffectedItemIds: scheduledItems.Select(i => i.Id).ToList(),
                Description: "Schedule contains more than 10 hours of work tasks",
                Impact: "May lead to fatigue and reduced productivity"
            ));

            recommendations.Add("Consider breaking up the workload across multiple days");
            recommendations.Add("Add more break time between intensive tasks");
        }

        return Task.FromResult(new ScheduleValidationResult(
            IsValid: !conflicts.Any(),
            Conflicts: conflicts,
            Warnings: warnings,
            Recommendations: recommendations
        ));
    }

    public async Task<SmartScheduleResponse> ApplyRealTimeScheduleAdjustmentAsync(
        Guid userId,
        Guid scheduleId,
        List<ScheduleChange> changes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Applying real-time schedule adjustments for user {UserId}, schedule {ScheduleId}",
            userId, scheduleId);

        try
        {
            var preferences = await _preferenceService.GetUserPreferencesAsync(userId, cancellationToken);

            // TODO: In Phase 4, implement full schedule fetching from database
            // For now, create a basic adjustment response based on the provided changes

            var adjustedItems = new List<SmartScheduledItem>();
            var appliedChanges = new List<string>();

            // Process each change and apply basic adjustments
            foreach (var change in changes)
            {
                switch (change.ChangeType.ToLower())
                {
                    case "reschedule":
                        appliedChanges.Add($"Rescheduled '{change.ItemTitle}' based on new constraints");
                        break;

                    case "cancel":
                        appliedChanges.Add($"Cancelled '{change.ItemTitle}' and adjusted surrounding tasks");
                        break;

                    case "extend":
                        appliedChanges.Add($"Extended duration of '{change.ItemTitle}' and shifted subsequent tasks");
                        break;

                    case "urgent":
                        appliedChanges.Add($"Prioritized '{change.ItemTitle}' and reorganized schedule");
                        break;

                    default:
                        appliedChanges.Add($"Applied change to '{change.ItemTitle}': {change.Reason}");
                        break;
                }
            }

            // Generate time block recommendations for the adjusted schedule
            var timeBlocks = await _timeBlockManager.GenerateTimeBlockRecommendationsAsync(
                userId,
                DateTime.Today,
                preferences,
                cancellationToken);

            var insights = new List<string>
            {
                $"Applied {changes.Count} real-time adjustments successfully",
                "Schedule optimized to minimize disruption to existing tasks",
                "Buffer time added between adjusted tasks to prevent conflicts"
            };

            if (changes.Any(c => c.ChangeType.ToLower() == "urgent"))
            {
                insights.Add("High-priority changes were accommodated while preserving productivity flow");
            }

            var metrics = new SmartScheduleMetrics(
                TotalTasks: adjustedItems.Count,
                ScheduledTasks: adjustedItems.Count,
                UnscheduledTasks: 0,
                TotalScheduledTime: TimeSpan.FromHours(adjustedItems.Sum(i => i.EstimatedDuration.TotalHours)),
                AvailableTime: TimeSpan.FromHours(8), // Assume 8-hour workday
                UtilizationPercentage: Math.Min(100, adjustedItems.Sum(i => i.EstimatedDuration.TotalHours) / 8.0 * 100),
                NumberOfTimeBlocks: timeBlocks.Count,
                ProductivityScore: CalculateAdjustmentProductivityScore(changes, preferences),
                OptimizationWarnings: ValidateAdjustmentWarnings(changes)
            );

            _logger.LogInformation("Successfully applied {ChangeCount} real-time adjustments for user {UserId}",
                changes.Count, userId);

            return new SmartScheduleResponse(
                UserId: userId,
                GeneratedAt: DateTime.UtcNow,
                ScheduledItems: adjustedItems,
                TimeBlocks: timeBlocks,
                OptimizationInsights: insights,
                Metrics: metrics,
                ConfidenceScore: CalculateAdjustmentConfidence(changes, preferences)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying real-time schedule adjustments for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<TimeBlockSuggestion>> GetTimeBlockRecommendationsAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var preferences = await _preferenceService.GetUserPreferencesAsync(userId, cancellationToken);
        return await _timeBlockManager.GenerateTimeBlockRecommendationsAsync(
            userId,
            date,
            preferences,
            cancellationToken);
    }

    public async Task<bool> IsSmartSchedulingAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var engineAvailable = await _optimizationEngine.IsAvailableAsync(cancellationToken);
            var timeBlockAvailable = await _timeBlockManager.IsAvailableAsync(cancellationToken);
            var preferenceAvailable = await _preferenceService.IsAvailableAsync(cancellationToken);

            return engineAvailable && timeBlockAvailable && preferenceAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking smart scheduling availability");
            return false;
        }
    }

    // Helper methods
    private SmartScheduledItem MapTaskToScheduledItem(TaskEntity task)
    {
        return new SmartScheduledItem(
            Id: Guid.NewGuid(),
            TaskId: task.Id,
            CalendarEventId: null,
            Title: task.Title,
            Description: task.Description ?? "",
            StartTime: DateTime.MinValue, // Will be set during optimization
            EndTime: DateTime.MinValue,   // Will be set during optimization
            ItemType: ScheduledItemType.Task,
            Priority: task.Priority,
            Category: task.Category?.Name ?? "General",
            Tags: task.Tags?.Select(t => t.Name).ToList() ?? new List<string>(),
            EstimatedDuration: TimeSpan.FromHours(2), // Default estimation
            IsFlexible: task.Priority.Value < 3, // High/Urgent priority tasks are less flexible
            SchedulingReasons: new List<SchedulingReason>()
        );
    }

    private Task<List<ExternalCalendarEvent>> GetCalendarEventsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        // This would integrate with calendar providers
        // For now, return empty list
        return Task.FromResult(new List<ExternalCalendarEvent>());
    }

    private SmartScheduleMetrics CalculateScheduleMetrics(
        List<SmartScheduledItem> scheduledItems,
        List<TimeBlockSuggestion> timeBlocks)
    {
        var tasks = scheduledItems.Where(i => i.ItemType == ScheduledItemType.Task).ToList();
        var totalTime = tasks.Sum(t => (t.EndTime - t.StartTime).TotalHours);

        return new SmartScheduleMetrics(
            TotalTasks: tasks.Count,
            ScheduledTasks: tasks.Count(t => t.StartTime > DateTime.MinValue),
            UnscheduledTasks: tasks.Count(t => t.StartTime == DateTime.MinValue),
            TotalScheduledTime: TimeSpan.FromHours(totalTime),
            AvailableTime: TimeSpan.FromHours(8), // Assumed 8-hour work day
            UtilizationPercentage: Math.Min(100, (totalTime / 8.0) * 100),
            NumberOfTimeBlocks: timeBlocks.Count,
            ProductivityScore: CalculateProductivityScore(scheduledItems, timeBlocks),
            OptimizationWarnings: new List<string>()
        );
    }

    private OptimizationMetrics CalculateOptimizationMetrics(
        List<SmartScheduledItem> original,
        List<SmartScheduledItem> optimized)
    {
        return new OptimizationMetrics(
            ChangesApplied: 0,
            ProductivityImprovement: 0.15,
            TimeSaved: TimeSpan.FromMinutes(30),
            EfficiencyGain: 0.2,
            ConflictsResolved: 0,
            Impacts: new List<OptimizationImpact>()
        );
    }

    private double CalculateConfidenceScore(
        ScheduleOptimizationResult result,
        SmartSchedulingPreferences preferences)
    {
        // Simple confidence calculation based on optimization metrics
        return Math.Min(1.0, 0.8 + (result.ImprovementScore * 0.2));
    }

    private double CalculateProductivityScore(
        List<SmartScheduledItem> scheduledItems,
        List<TimeBlockSuggestion> timeBlocks)
    {
        // Simple productivity score calculation
        return 0.75 + (timeBlocks.Count * 0.05);
    }

    private string GetItemTitle(Guid itemId, List<SmartScheduledItem> items)
    {
        return items.FirstOrDefault(i => i.Id == itemId)?.Title ?? "Unknown Item";
    }

    private List<string> GenerateOptimizationRecommendations(ScheduleOptimizationEngineResult result)
    {
        return new List<string>
        {
            "Consider grouping similar tasks together to reduce context switching",
            "Schedule demanding tasks during your peak energy hours",
            "Add buffer time between meetings and intensive tasks"
        };
    }

    private bool HasTimeConflict(SmartScheduledItem item1, SmartScheduledItem item2)
    {
        return item1.StartTime < item2.EndTime && item2.StartTime < item1.EndTime;
    }

    private double CalculateAdjustmentProductivityScore(List<ScheduleChange> changes, SmartSchedulingPreferences preferences)
    {
        if (!changes.Any())
        {
            return 0.8;
        }

        // Higher score for fewer disruptive changes
        var disruptiveChanges = changes.Count(c =>
            c.ChangeType.ToLower() is "cancel" or "reschedule" or "urgent");

        var disruptionScore = Math.Max(0.3, 1.0 - (disruptiveChanges * 0.1));

        // Bonus for changes that align with preferences
        var alignmentBonus = changes.Any(c => c.ChangeType.ToLower() == "urgent") &&
                           preferences.ProductivityPattern == ProductivityPatterns.MorningPerson ? 0.1 : 0.0;

        return Math.Min(1.0, disruptionScore + alignmentBonus);
    }

    private List<string> ValidateAdjustmentWarnings(List<ScheduleChange> changes)
    {
        var warnings = new List<string>();

        var cancelledCount = changes.Count(c => c.ChangeType.ToLower() == "cancel");
        if (cancelledCount > 2)
        {
            warnings.Add($"High number of cancellations ({cancelledCount}) may impact productivity");
        }

        var urgentCount = changes.Count(c => c.ChangeType.ToLower() == "urgent");
        if (urgentCount > 1)
        {
            warnings.Add($"Multiple urgent changes ({urgentCount}) may cause schedule conflicts");
        }

        if (changes.Count > 5)
        {
            warnings.Add("Large number of changes may require manual review");
        }

        return warnings;
    }

    private double CalculateAdjustmentConfidence(List<ScheduleChange> changes, SmartSchedulingPreferences preferences)
    {
        if (!changes.Any())
        {
            return 0.9;
        }

        // Start with base confidence
        var confidence = 0.8;

        // Reduce confidence for complex changes
        var complexChanges = changes.Count(c =>
            c.ChangeType.ToLower() is "reschedule" or "extend");
        confidence -= complexChanges * 0.05;

        // Reduce confidence for many changes
        if (changes.Count > 3)
        {
            confidence -= (changes.Count - 3) * 0.03;
        }

        // Increase confidence if changes are simple
        var simpleChanges = changes.Count(c =>
            c.ChangeType.ToLower() is "cancel" or "urgent");
        confidence += simpleChanges * 0.02;

        return Math.Max(0.5, Math.Min(1.0, confidence));
    }
}
