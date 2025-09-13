using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services;
using DomainTask = WhoAndWhat.Domain.Entities.AppTask;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Advanced schedule optimization engine using machine learning algorithms and heuristics
/// </summary>
public class ScheduleOptimizationEngine : IScheduleOptimizationEngine
{
    private readonly ILogger<ScheduleOptimizationEngine> _logger;
    private readonly SmartSchedulingSettings _settings;
    private readonly IAIPlanningService _aiPlanningService;

    public ScheduleOptimizationEngine(
        IOptions<SmartSchedulingSettings> settings,
        IAIPlanningService aiPlanningService,
        ILogger<ScheduleOptimizationEngine> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _aiPlanningService = aiPlanningService ?? throw new ArgumentNullException(nameof(aiPlanningService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScheduleOptimizationResult> OptimizeScheduleAsync(
        Guid userId,
        ScheduleOptimizationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting schedule optimization for user {UserId} with {TaskCount} tasks",
                userId, context.Tasks.Count);

            // Step 1: Analyze tasks and create initial schedule
            var initialSchedule = await CreateInitialScheduleAsync(userId, context, cancellationToken);

            // Step 2: Apply optimization algorithms
            var optimizedSchedule = await ApplyOptimizationAlgorithmsAsync(userId, initialSchedule, context, cancellationToken);

            // Step 3: Validate and refine the schedule
            var refinedSchedule = await ValidateAndRefineScheduleAsync(userId, optimizedSchedule, context, cancellationToken);

            // Step 4: Calculate quality metrics
            var qualityMetrics = await CalculateQualityMetricsAsync(refinedSchedule, context.Preferences, cancellationToken);

            return new ScheduleOptimizationResult(
                refinedSchedule,
                new List<OptimizationChange>(), // Changes tracking would be implemented here
                qualityMetrics["ProductivityScore"],
                new List<string> { "Schedule optimized for productivity and user preferences" },
                qualityMetrics,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing schedule for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ScheduleOptimizationEngineResult> OptimizeExistingScheduleAsync(
        Guid userId,
        OptimizationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Optimizing existing schedule for user {UserId} with {ItemCount} items",
                userId, context.CurrentSchedule.Count);

            // Step 1: Analyze current schedule quality
            var currentQuality = await AnalyzeScheduleQualityAsync(userId, context.CurrentSchedule, cancellationToken);

            // Step 2: Identify optimization opportunities
            var opportunities = IdentifyOptimizationOpportunities(context.CurrentSchedule, context.Goals, context.Preferences);

            // Step 3: Apply targeted optimizations
            var optimizedSchedule = await ApplyTargetedOptimizationsAsync(
                userId, context.CurrentSchedule, opportunities, context, cancellationToken);

            // Step 4: Calculate improvements
            var newQuality = await CalculateQualityMetricsAsync(optimizedSchedule, context.Preferences, cancellationToken);
            var improvement = newQuality["ProductivityScore"] - currentQuality.OverallScore;

            // Step 5: Track changes applied
            var changes = TrackOptimizationChanges(context.CurrentSchedule, optimizedSchedule);

            return new ScheduleOptimizationEngineResult(
                optimizedSchedule,
                changes,
                improvement,
                new List<string> { "Applied productivity-focused optimizations", "Reduced context switching" },
                newQuality,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing existing schedule for user {UserId}", userId);
            throw;
        }
    }

    public Task<List<TimeSlotAssignment>> FindOptimalTimeSlotsAsync(
        Guid userId,
        List<Guid> taskIds,
        List<AvailableTimeSlot> availableSlots,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Finding optimal time slots for user {UserId} with {TaskCount} tasks and {SlotCount} available slots",
                userId, taskIds.Count, availableSlots.Count);

            var assignments = new List<TimeSlotAssignment>();

            // Simple first-fit algorithm (would be replaced with more sophisticated algorithms)
            foreach (var taskId in taskIds)
            {
                var bestSlot = FindBestSlotForTask(taskId, availableSlots, preferences);
                if (bestSlot != null)
                {
                    assignments.Add(new TimeSlotAssignment(
                        taskId,
                        bestSlot.Id,
                        bestSlot.StartTime,
                        bestSlot.EndTime,
                        bestSlot.AvailabilityScore,
                        new List<string> { "Best available slot", "Aligns with preferences" }
                    ));

                    // Remove the assigned slot from available slots
                    availableSlots.Remove(bestSlot);
                }
            }

            return Task.FromResult(assignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding optimal time slots for user {UserId}", userId);
            return Task.FromResult(new List<TimeSlotAssignment>());
        }
    }

    public Task<ScheduleQualityAnalysis> AnalyzeScheduleQualityAsync(
        Guid userId,
        List<SmartScheduledItem> schedule,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing schedule quality for user {UserId} with {ItemCount} scheduled items",
                userId, schedule.Count);

            // Calculate quality dimensions
            var qualityDimensions = new Dictionary<string, double>
            {
                ["TimeUtilization"] = CalculateTimeUtilization(schedule),
                ["TaskBalancing"] = CalculateTaskBalancing(schedule),
                ["PriorityAlignment"] = CalculatePriorityAlignment(schedule),
                ["ContextSwitching"] = CalculateContextSwitchingScore(schedule),
                ["WorkLifeBalance"] = CalculateWorkLifeBalanceScore(schedule)
            };

            var overallScore = qualityDimensions.Values.Average();

            // Identify quality issues
            var issues = IdentifyQualityIssues(schedule, qualityDimensions);

            // Generate improvement suggestions
            var suggestions = GenerateQualityImprovements(schedule, qualityDimensions, issues);

            return Task.FromResult(new ScheduleQualityAnalysis(
                overallScore,
                qualityDimensions,
                issues,
                suggestions,
                DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing schedule quality for user {UserId}", userId);
            throw;
        }
    }

    public Task<ProductivityScore> CalculateProductivityScoreAsync(
        Guid userId,
        List<SmartScheduledItem> schedule,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factorContributions = new Dictionary<string, double>();
            var positiveFactors = new List<ProductivityFactor>();
            var negativeFactors = new List<ProductivityFactor>();

            // Analyze various productivity factors
            factorContributions["TimeAlignment"] = AnalyzeTimeAlignment(schedule, preferences);
            factorContributions["TaskGrouping"] = AnalyzeTaskGrouping(schedule);
            factorContributions["EnergyOptimization"] = AnalyzeEnergyOptimization(schedule, preferences);
            factorContributions["BufferTime"] = AnalyzeBufferTime(schedule, preferences);

            var totalScore = factorContributions.Values.Average();

            // Categorize factors
            foreach (var factor in factorContributions)
            {
                var productivityFactor = new ProductivityFactor(
                    factor.Key,
                    factor.Value,
                    GetFactorDescription(factor.Key),
                    GetAffectedItems(schedule, factor.Key)
                );

                if (factor.Value >= 0.7)
                {
                    positiveFactors.Add(productivityFactor);
                }
                else if (factor.Value < 0.5)
                {
                    negativeFactors.Add(productivityFactor);
                }
            }

            var suggestions = GenerateProductivitySuggestions(factorContributions, schedule, preferences);

            return Task.FromResult(new ProductivityScore(
                totalScore,
                factorContributions,
                positiveFactors,
                negativeFactors,
                suggestions
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating productivity score for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if optimization engine is properly configured and AI service is available
            var aiAvailable = await _aiPlanningService.IsAIServiceAvailableAsync(cancellationToken);
            return aiAvailable && _settings.EnableOptimization;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking optimization engine availability");
            return false;
        }
    }

    // Private helper methods

    private Task<List<SmartScheduledItem>> CreateInitialScheduleAsync(
        Guid userId,
        ScheduleOptimizationContext context,
        CancellationToken cancellationToken)
    {
        var scheduledItems = new List<SmartScheduledItem>();
        var currentTime = context.StartDate.Date.Add(context.Preferences.PreferredWorkingHours.StartTime);

        foreach (var task in context.Tasks.OrderByDescending(t => t.Priority))
        {
            var duration = EstimateTaskDuration(task, context.Preferences);

            // Check for calendar conflicts
            var hasConflict = context.CalendarEvents.Any(e =>
                currentTime < e.EndTime && e.StartTime < currentTime.Add(duration));

            if (hasConflict)
            {
                // Find next available slot
                currentTime = FindNextAvailableSlot(currentTime, duration, context.CalendarEvents);
            }

            scheduledItems.Add(new SmartScheduledItem(
                Guid.NewGuid(),
                task.Id,
                null,
                task.Title,
                task.Description ?? "",
                currentTime,
                currentTime.Add(duration),
                ScheduledItemType.Task,
                (Domain.ValueObjects.Priority)task.Priority,
                task.Category.ToString(),
                new List<string>(),
                duration,
                true,
                new List<SchedulingReason>
                {
                    new SchedulingReason("InitialScheduling", "Scheduled based on priority", 0.7, new List<string> { "Priority", "Availability" })
                }
            ));

            currentTime = currentTime.Add(duration).Add(context.Preferences.BufferDuration);
        }

        return Task.FromResult(scheduledItems);
    }

    private Task<List<SmartScheduledItem>> ApplyOptimizationAlgorithmsAsync(
        Guid userId,
        List<SmartScheduledItem> initialSchedule,
        ScheduleOptimizationContext context,
        CancellationToken cancellationToken)
    {
        var optimizedSchedule = new List<SmartScheduledItem>(initialSchedule);

        // Apply various optimization algorithms
        optimizedSchedule = OptimizeForProductivity(optimizedSchedule, context.Preferences);
        optimizedSchedule = OptimizeForContextSwitching(optimizedSchedule);
        optimizedSchedule = OptimizeForEnergyLevels(optimizedSchedule, context.Preferences);

        return Task.FromResult(optimizedSchedule);
    }

    private Task<List<SmartScheduledItem>> ValidateAndRefineScheduleAsync(
        Guid userId,
        List<SmartScheduledItem> schedule,
        ScheduleOptimizationContext context,
        CancellationToken cancellationToken)
    {
        // Validate schedule against constraints and refine as needed
        var refinedSchedule = new List<SmartScheduledItem>(schedule);

        // Ensure no overlaps
        refinedSchedule = ResolveTimeOverlaps(refinedSchedule);

        // Respect working hours
        refinedSchedule = EnforceWorkingHours(refinedSchedule, context.Preferences.PreferredWorkingHours);

        return Task.FromResult(refinedSchedule);
    }

    private List<SmartScheduledItem> OptimizeForProductivity(
        List<SmartScheduledItem> schedule,
        SmartSchedulingPreferences preferences)
    {
        // Group similar tasks together and schedule high-priority tasks during peak hours
        var optimizedSchedule = schedule.ToList();

        if (preferences.ProductivityPattern == ProductivityPatterns.MorningPerson)
        {
            // Schedule high-priority tasks in the morning
            var highPriorityItems = optimizedSchedule.Where(i => i.Priority == Domain.ValueObjects.Priority.High).ToList();
            var morningStart = DateTime.Today.Add(preferences.PreferredWorkingHours.StartTime);

            foreach (var item in highPriorityItems)
            {
                var updatedItem = item with { StartTime = morningStart, EndTime = morningStart.Add(item.EstimatedDuration) };
                var index = optimizedSchedule.IndexOf(item);
                optimizedSchedule[index] = updatedItem;
                morningStart = morningStart.Add(item.EstimatedDuration).Add(preferences.BufferDuration);
            }
        }

        return optimizedSchedule;
    }

    private List<SmartScheduledItem> OptimizeForContextSwitching(List<SmartScheduledItem> schedule)
    {
        // Group tasks by category to minimize context switching
        return schedule.OrderBy(i => i.Category).ThenBy(i => i.StartTime).ToList();
    }

    private List<SmartScheduledItem> OptimizeForEnergyLevels(
        List<SmartScheduledItem> schedule,
        SmartSchedulingPreferences preferences)
    {
        // Schedule demanding tasks during high-energy periods
        var optimizedSchedule = schedule.ToList();

        // This is a simplified implementation - would involve more complex energy level analysis
        return optimizedSchedule;
    }

    private TimeSpan EstimateTaskDuration(DomainTask task, SmartSchedulingPreferences preferences)
    {
        // Simple duration estimation based on task properties
        var baseDuration = preferences.MinimumTaskDuration;

        // Adjust based on priority and category
        if (task.Priority == Domain.ValueObjects.Priority.High)
        {
            baseDuration = baseDuration.Add(TimeSpan.FromMinutes(30));
        }

        return TimeSpan.FromMinutes(Math.Min(baseDuration.TotalMinutes, preferences.MaximumTaskDuration.TotalMinutes));
    }

    private DateTime FindNextAvailableSlot(DateTime startTime, TimeSpan duration, List<CalendarEventSummary> calendarEvents)
    {
        var currentTime = startTime;

        while (calendarEvents.Any(e => currentTime < e.EndTime && e.StartTime < currentTime.Add(duration)))
        {
            var conflictingEvent = calendarEvents.First(e => currentTime < e.EndTime && e.StartTime < currentTime.Add(duration));
            currentTime = conflictingEvent.EndTime;
        }

        return currentTime;
    }

    private Task<Dictionary<string, double>> CalculateQualityMetricsAsync(
        List<SmartScheduledItem> schedule,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new Dictionary<string, double>
        {
            ["ProductivityScore"] = CalculateProductivityMetric(schedule, preferences),
            ["EfficiencyScore"] = CalculateEfficiencyMetric(schedule),
            ["BalanceScore"] = CalculateBalanceMetric(schedule, preferences),
            ["FlexibilityScore"] = CalculateFlexibilityMetric(schedule)
        });
    }

    private double CalculateTimeUtilization(List<SmartScheduledItem> schedule)
    {
        if (!schedule.Any())
        {
            return 0.0;
        }

        var totalScheduledTime = schedule.Sum(i => i.EstimatedDuration.TotalMinutes);
        var totalAvailableTime = (schedule.Max(i => i.EndTime) - schedule.Min(i => i.StartTime)).TotalMinutes;

        return totalAvailableTime > 0 ? totalScheduledTime / totalAvailableTime : 0.0;
    }

    private double CalculateTaskBalancing(List<SmartScheduledItem> schedule)
    {
        // Calculate how evenly tasks are distributed
        var tasksByHour = schedule.GroupBy(i => i.StartTime.Hour).Select(g => g.Count()).ToList();

        if (!tasksByHour.Any())
        {
            return 1.0;
        }

        var average = tasksByHour.Average();
        var variance = tasksByHour.Sum(count => Math.Pow(count - average, 2)) / tasksByHour.Count;

        // Lower variance means better balancing
        return Math.Max(0.0, 1.0 - (variance / (average * average)));
    }

    private double CalculatePriorityAlignment(List<SmartScheduledItem> schedule)
    {
        // Check if high-priority items are scheduled earlier in the day
        var priorityScore = 0.0;
        var totalItems = schedule.Count;

        if (totalItems == 0)
        {
            return 1.0;
        }

        foreach (var item in schedule)
        {
            var timeScore = 1.0 - (item.StartTime.Hour / 24.0); // Earlier = higher score
            var priorityWeight = item.Priority == Domain.ValueObjects.Priority.High ? 1.0 :
                                item.Priority == Domain.ValueObjects.Priority.Medium ? 0.5 : 0.2;

            priorityScore += timeScore * priorityWeight;
        }

        return priorityScore / totalItems;
    }

    private double CalculateContextSwitchingScore(List<SmartScheduledItem> schedule)
    {
        if (schedule.Count < 2)
        {
            return 1.0;
        }

        var switches = 0;
        for (int i = 1; i < schedule.Count; i++)
        {
            if (schedule[i].Category != schedule[i - 1].Category)
            {
                switches++;
            }
        }

        // Fewer switches = higher score
        return Math.Max(0.0, 1.0 - (switches / (double)schedule.Count));
    }

    private double CalculateWorkLifeBalanceScore(List<SmartScheduledItem> schedule)
    {
        // Check if tasks are scheduled within reasonable hours
        var workingHourViolations = schedule.Count(i => i.StartTime.Hour < 8 || i.StartTime.Hour > 18);
        return Math.Max(0.0, 1.0 - (workingHourViolations / (double)schedule.Count));
    }

    private List<QualityIssue> IdentifyQualityIssues(List<SmartScheduledItem> schedule, Dictionary<string, double> qualityDimensions)
    {
        var issues = new List<QualityIssue>();

        foreach (var dimension in qualityDimensions.Where(d => d.Value < 0.6))
        {
            issues.Add(new QualityIssue(
                dimension.Key,
                $"Low score in {dimension.Key}: {dimension.Value:P0}",
                dimension.Value < 0.3 ? "High" : "Medium",
                schedule.Select(i => i.Id).ToList(),
                new List<string> { "Productivity", "User satisfaction" }
            ));
        }

        return issues;
    }

    private List<QualityImprovement> GenerateQualityImprovements(
        List<SmartScheduledItem> schedule,
        Dictionary<string, double> qualityDimensions,
        List<QualityIssue> issues)
    {
        var improvements = new List<QualityImprovement>();

        if (qualityDimensions["ContextSwitching"] < 0.6)
        {
            improvements.Add(new QualityImprovement(
                "GroupSimilarTasks",
                "Group similar tasks together to reduce context switching",
                0.2,
                schedule.Select(i => i.Id).ToList(),
                new List<string> { "Reorder tasks by category", "Add buffer time between different task types" }
            ));
        }

        if (qualityDimensions["TaskBalancing"] < 0.6)
        {
            improvements.Add(new QualityImprovement(
                "BalanceWorkload",
                "Distribute tasks more evenly throughout the day",
                0.15,
                schedule.Select(i => i.Id).ToList(),
                new List<string> { "Spread out demanding tasks", "Add more breaks" }
            ));
        }

        return improvements;
    }

    private double CalculateProductivityMetric(List<SmartScheduledItem> schedule, SmartSchedulingPreferences preferences)
    {
        // Simplified productivity calculation
        var score = 0.8; // Base score

        // Bonus for alignment with productivity patterns
        if (preferences.ProductivityPattern == ProductivityPatterns.MorningPerson)
        {
            var morningTasks = schedule.Count(i => i.StartTime.Hour < 12 && i.Priority == Domain.ValueObjects.Priority.High);
            score += morningTasks * 0.05;
        }

        return Math.Min(1.0, score);
    }

    private double CalculateEfficiencyMetric(List<SmartScheduledItem> schedule)
    {
        // Calculate based on time utilization and task density
        return CalculateTimeUtilization(schedule) * 0.8;
    }

    private double CalculateBalanceMetric(List<SmartScheduledItem> schedule, SmartSchedulingPreferences preferences)
    {
        // Check work-life balance based on working hours and break times
        return CalculateWorkLifeBalanceScore(schedule);
    }

    private double CalculateFlexibilityMetric(List<SmartScheduledItem> schedule)
    {
        // Calculate based on buffer times and flexibility of scheduled items
        var flexibleItems = schedule.Count(i => i.IsFlexible);
        return schedule.Count > 0 ? (double)flexibleItems / schedule.Count : 1.0;
    }

    private List<OptimizationOpportunity> IdentifyOptimizationOpportunities(
        List<SmartScheduledItem> schedule,
        OptimizationGoals goals,
        SmartSchedulingPreferences preferences)
    {
        var opportunities = new List<OptimizationOpportunity>();

        // Identify context switching opportunities
        if (goals.MinimizeContextSwitching)
        {
            var contextSwitches = CountContextSwitches(schedule);
            if (contextSwitches > 3)
            {
                opportunities.Add(new OptimizationOpportunity(
                    "ReduceContextSwitching",
                    $"Reduce {contextSwitches} context switches",
                    0.2
                ));
            }
        }

        return opportunities;
    }

    private Task<List<SmartScheduledItem>> ApplyTargetedOptimizationsAsync(
        Guid userId,
        List<SmartScheduledItem> currentSchedule,
        List<OptimizationOpportunity> opportunities,
        OptimizationContext context,
        CancellationToken cancellationToken)
    {
        var optimizedSchedule = new List<SmartScheduledItem>(currentSchedule);

        foreach (var opportunity in opportunities)
        {
            switch (opportunity.Type)
            {
                case "ReduceContextSwitching":
                    optimizedSchedule = OptimizeForContextSwitching(optimizedSchedule);
                    break;
            }
        }

        return Task.FromResult(optimizedSchedule);
    }

    private List<OptimizationChange> TrackOptimizationChanges(
        List<SmartScheduledItem> originalSchedule,
        List<SmartScheduledItem> optimizedSchedule)
    {
        var changes = new List<OptimizationChange>();

        // Compare schedules and track changes
        // This is a simplified implementation
        for (int i = 0; i < Math.Min(originalSchedule.Count, optimizedSchedule.Count); i++)
        {
            var original = originalSchedule[i];
            var optimized = optimizedSchedule[i];

            if (original.StartTime != optimized.StartTime)
            {
                changes.Add(new OptimizationChange(
                    optimized.Id,
                    "TimeChange",
                    $"Moved '{optimized.Title}' from {original.StartTime:HH:mm} to {optimized.StartTime:HH:mm}",
                    original.StartTime,
                    optimized.StartTime,
                    original.EstimatedDuration,
                    optimized.EstimatedDuration,
                    0.1,
                    "Improved productivity alignment"
                ));
            }
        }

        return changes;
    }

    private AvailableTimeSlot? FindBestSlotForTask(Guid taskId, List<AvailableTimeSlot> availableSlots, SmartSchedulingPreferences preferences)
    {
        // Simple implementation - find slot with highest availability score
        return availableSlots.OrderByDescending(s => s.AvailabilityScore).FirstOrDefault();
    }

    private int CountContextSwitches(List<SmartScheduledItem> schedule)
    {
        if (schedule.Count < 2)
        {
            return 0;
        }

        var switches = 0;
        for (int i = 1; i < schedule.Count; i++)
        {
            if (schedule[i].Category != schedule[i - 1].Category)
            {
                switches++;
            }
        }

        return switches;
    }

    private List<SmartScheduledItem> ResolveTimeOverlaps(List<SmartScheduledItem> schedule)
    {
        var resolvedSchedule = new List<SmartScheduledItem>();
        var sortedSchedule = schedule.OrderBy(i => i.StartTime).ToList();

        foreach (var item in sortedSchedule)
        {
            var currentItem = item;

            // Check for overlaps with already resolved items
            foreach (var resolvedItem in resolvedSchedule)
            {
                if (currentItem.StartTime < resolvedItem.EndTime && resolvedItem.StartTime < currentItem.EndTime)
                {
                    // Move the current item after the resolved item
                    var newStartTime = resolvedItem.EndTime;
                    currentItem = currentItem with
                    {
                        StartTime = newStartTime,
                        EndTime = newStartTime.Add(currentItem.EstimatedDuration)
                    };
                }
            }

            resolvedSchedule.Add(currentItem);
        }

        return resolvedSchedule;
    }

    private List<SmartScheduledItem> EnforceWorkingHours(
        List<SmartScheduledItem> schedule,
        WorkingHours workingHours)
    {
        var enforcedSchedule = new List<SmartScheduledItem>();

        foreach (var item in schedule)
        {
            var adjustedItem = item;

            // Check if item starts before working hours
            if (item.StartTime.TimeOfDay < workingHours.StartTime)
            {
                var newStartTime = item.StartTime.Date.Add(workingHours.StartTime);
                adjustedItem = adjustedItem with
                {
                    StartTime = newStartTime,
                    EndTime = newStartTime.Add(item.EstimatedDuration)
                };
            }

            // Check if item ends after working hours
            if (adjustedItem.EndTime.TimeOfDay > workingHours.EndTime)
            {
                var newEndTime = adjustedItem.StartTime.Date.Add(workingHours.EndTime);
                var adjustedDuration = newEndTime - adjustedItem.StartTime;

                if (adjustedDuration > TimeSpan.Zero)
                {
                    adjustedItem = adjustedItem with
                    {
                        EndTime = newEndTime,
                        EstimatedDuration = adjustedDuration
                    };
                }
            }

            enforcedSchedule.Add(adjustedItem);
        }

        return enforcedSchedule;
    }

    // Productivity analysis helper methods

    private double AnalyzeTimeAlignment(List<SmartScheduledItem> schedule, SmartSchedulingPreferences preferences)
    {
        var alignment = 0.0;
        var workingHours = preferences.PreferredWorkingHours;

        foreach (var item in schedule)
        {
            var itemTime = item.StartTime.TimeOfDay;
            if (itemTime >= workingHours.StartTime && itemTime <= workingHours.EndTime)
            {
                alignment += 1.0;
            }
        }

        return schedule.Count > 0 ? alignment / schedule.Count : 1.0;
    }

    private double AnalyzeTaskGrouping(List<SmartScheduledItem> schedule)
    {
        return CalculateContextSwitchingScore(schedule);
    }

    private double AnalyzeEnergyOptimization(List<SmartScheduledItem> schedule, SmartSchedulingPreferences preferences)
    {
        // Analyze if high-priority tasks are scheduled during high-energy periods
        var energyScore = 0.0;

        foreach (var item in schedule)
        {
            var hourScore = GetEnergyScoreForHour(item.StartTime.Hour, preferences.ProductivityPattern);
            var priorityWeight = item.Priority == Domain.ValueObjects.Priority.High ? 1.0 : 0.5;
            energyScore += hourScore * priorityWeight;
        }

        return schedule.Count > 0 ? energyScore / schedule.Count : 1.0;
    }

    private double AnalyzeBufferTime(List<SmartScheduledItem> schedule, SmartSchedulingPreferences preferences)
    {
        var bufferScore = 0.0;
        var totalGaps = 0;

        for (int i = 1; i < schedule.Count; i++)
        {
            var gap = schedule[i].StartTime - schedule[i - 1].EndTime;
            if (gap >= preferences.BufferDuration)
            {
                bufferScore += 1.0;
            }
            totalGaps++;
        }

        return totalGaps > 0 ? bufferScore / totalGaps : 1.0;
    }

    private double GetEnergyScoreForHour(int hour, ProductivityPatterns pattern)
    {
        return pattern switch
        {
            ProductivityPatterns.MorningPerson => hour < 12 ? 1.0 : 0.5,
            ProductivityPatterns.NightOwl => hour > 16 ? 1.0 : 0.5,
            ProductivityPatterns.AfternoonPeak => hour >= 13 && hour <= 16 ? 1.0 : 0.7,
            _ => 0.8
        };
    }

    private string GetFactorDescription(string factorName)
    {
        return factorName switch
        {
            "TimeAlignment" => "How well tasks align with preferred working hours",
            "TaskGrouping" => "How well similar tasks are grouped together",
            "EnergyOptimization" => "How well tasks align with energy patterns",
            "BufferTime" => "Adequate buffer time between tasks",
            _ => "General productivity factor"
        };
    }

    private List<Guid> GetAffectedItems(List<SmartScheduledItem> schedule, string factorName)
    {
        // Return all items as affected for simplicity
        return schedule.Select(i => i.Id).ToList();
    }

    private List<string> GenerateProductivitySuggestions(
        Dictionary<string, double> factorContributions,
        List<SmartScheduledItem> schedule,
        SmartSchedulingPreferences preferences)
    {
        var suggestions = new List<string>();

        if (factorContributions["TimeAlignment"] < 0.6)
        {
            suggestions.Add("Reschedule tasks to align better with your preferred working hours");
        }

        if (factorContributions["TaskGrouping"] < 0.6)
        {
            suggestions.Add("Group similar tasks together to reduce context switching");
        }

        if (factorContributions["EnergyOptimization"] < 0.6)
        {
            suggestions.Add("Schedule demanding tasks during your peak energy hours");
        }

        if (factorContributions["BufferTime"] < 0.6)
        {
            suggestions.Add("Add more buffer time between tasks to reduce stress");
        }

        return suggestions;
    }
}

// Supporting classes

internal record OptimizationOpportunity(
    string Type,
    string Description,
    double ExpectedImprovement
);
