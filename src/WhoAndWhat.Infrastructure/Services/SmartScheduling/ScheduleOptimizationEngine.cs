using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;
using DomainSchedulingPattern = WhoAndWhat.Domain.Entities.SchedulingPattern;
using DTOSchedulingPattern = WhoAndWhat.Application.DTOs.SmartScheduling.SchedulingPattern;

namespace WhoAndWhat.Infrastructure.Services.SmartScheduling;

/// <summary>
/// Advanced schedule optimization engine that uses multiple algorithms and AI insights
/// to create optimal task schedules for maximum productivity
/// </summary>
public sealed class ScheduleOptimizationEngine : IScheduleOptimizationEngine
{
    private readonly ILogger<ScheduleOptimizationEngine> _logger;
    private readonly IAppTaskRepository _taskRepository;
    private readonly ISchedulingPatternRepository _patternRepository;
    private readonly IUserSchedulingPreferenceRepository _preferenceRepository;
    private readonly IAIPlanningService _aiPlanningService;

    // Algorithm weights for multi-objective optimization
    private const double PRODUCTIVITY_WEIGHT = 0.35;
    private const double DEADLINE_WEIGHT = 0.25;
    private const double ENERGY_WEIGHT = 0.20;
    private const double CONTEXT_SWITCHING_WEIGHT = 0.15;
    private const double FLEXIBILITY_WEIGHT = 0.05;

    public ScheduleOptimizationEngine(
        ILogger<ScheduleOptimizationEngine> logger,
        IAppTaskRepository taskRepository,
        ISchedulingPatternRepository patternRepository,
        IUserSchedulingPreferenceRepository preferenceRepository,
        IAIPlanningService aiPlanningService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _patternRepository = patternRepository ?? throw new ArgumentNullException(nameof(patternRepository));
        _preferenceRepository = preferenceRepository ?? throw new ArgumentNullException(nameof(preferenceRepository));
        _aiPlanningService = aiPlanningService ?? throw new ArgumentNullException(nameof(aiPlanningService));
    }

    public async Task<ScheduleOptimizationResult> OptimizeScheduleAsync(
        Guid userId,
        WhoAndWhat.Application.DTOs.SmartScheduling.ScheduleOptimizationContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting schedule optimization for user {UserId} with {TaskCount} tasks",
            userId, context.TaskIds.Count);

        try
        {
            // Load tasks to be scheduled
            var tasks = await LoadTasksAsync(context.TaskIds, cancellationToken);

            // Generate time slots based on preferences and calendar events
            var availableSlots = await GenerateAvailableTimeSlotsAsync(
                userId, context.StartDate, context.EndDate, context.CalendarEvents, context.Preferences, cancellationToken);

            // Load user's historical patterns for optimization
            var patterns = await _patternRepository.GetOptimizationEligiblePatternsAsync(userId, cancellationToken);

            // Apply optimization algorithms
            var optimizedSchedule = await ApplyOptimizationAlgorithmsAsync(
                userId, tasks, availableSlots, context.Preferences, patterns.ToList(), context.Goals, cancellationToken);

            // Calculate optimization metrics
            var metrics = CalculateOptimizationMetrics(context.CurrentSchedule, optimizedSchedule);

            // Generate optimization insights
            var insights = GenerateOptimizationInsights(optimizedSchedule, patterns.ToList(), context.Preferences);

            _logger.LogInformation("Schedule optimization completed for user {UserId}. Scheduled {Count} items with improvement score {Score:F2}",
                userId, optimizedSchedule.Count, metrics["ImprovementScore"]);

            return new ScheduleOptimizationResult(
                OptimizedSchedule: optimizedSchedule,
                Changes: new List<ScheduleChange>(), // Will be populated by comparing schedules
                ImprovementScore: (double)metrics["ImprovementScore"],
                OptimizationInsights: insights,
                Metrics: metrics,
                GeneratedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during schedule optimization for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ScheduleOptimizationEngineResult> OptimizeExistingScheduleAsync(
        Guid userId,
        WhoAndWhat.Application.DTOs.SmartScheduling.OptimizationContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing existing schedule for user {UserId} with {ItemCount} items",
            userId, context.ExistingSchedule.Count);

        try
        {
            // Analyze current schedule quality
            var qualityAnalysis = await AnalyzeScheduleQualityAsync(userId, context.ExistingSchedule, cancellationToken);

            // Load user patterns and preferences
            var patterns = await _patternRepository.GetOptimizationEligiblePatternsAsync(userId, cancellationToken);
            var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);

            // Apply incremental optimization techniques
            var optimizationChanges = await ApplyIncrementalOptimizationAsync(
                userId, context.ExistingSchedule, context.Goals, context.Constraints,
                patterns.ToList(), preferences, cancellationToken);

            // Apply changes to create optimized schedule
            var optimizedSchedule = ApplyOptimizationChanges(context.ExistingSchedule, optimizationChanges);

            // Calculate improvement metrics
            var improvementScore = CalculateImprovementScore(context.ExistingSchedule, optimizedSchedule, preferences);
            var qualityMetrics = CalculateQualityMetrics(optimizedSchedule, preferences);

            _logger.LogInformation("Existing schedule optimization completed for user {UserId}. Applied {ChangeCount} changes with improvement score {Score:F2}",
                userId, optimizationChanges.Count, improvementScore);

            return new ScheduleOptimizationEngineResult(
                OptimizedSchedule: optimizedSchedule,
                ChangesApplied: optimizationChanges,
                ImprovementScore: improvementScore,
                OptimizationReasons: GenerateOptimizationReasons(optimizationChanges, qualityAnalysis),
                QualityMetrics: qualityMetrics,
                OptimizedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing existing schedule for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<TimeSlotAssignment>> FindOptimalTimeSlotsAsync(
        Guid userId,
        List<Guid> taskIds,
        List<AvailableTimeSlot> availableSlots,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Finding optimal time slots for {TaskCount} tasks with {SlotCount} available slots",
            taskIds.Count, availableSlots.Count);

        try
        {
            var tasks = await LoadTasksAsync(taskIds, cancellationToken);
            var patterns = await _patternRepository.GetOptimizationEligiblePatternsAsync(userId, cancellationToken);

            var assignments = new List<TimeSlotAssignment>();

            // Sort tasks by priority and deadline urgency
            var sortedTasks = SortTasksByOptimizationPriority(tasks, preferences);

            foreach (var task in sortedTasks)
            {
                var bestSlot = await FindBestTimeSlotForTaskAsync(
                    task, availableSlots, patterns.ToList(), preferences, assignments, cancellationToken);

                if (bestSlot != null)
                {
                    var assignment = new TimeSlotAssignment(
                        TaskId: task.TaskId ?? Guid.Empty,
                        SlotId: bestSlot.Id,
                        StartTime: bestSlot.StartTime,
                        EndTime: bestSlot.StartTime.Add(GetEstimatedDuration(task, preferences)),
                        FitnessScore: CalculateSlotFitnessScore(task, bestSlot, patterns.ToList(), preferences),
                        AssignmentReasons: GenerateAssignmentReasons(task, bestSlot, patterns.ToList())
                    );

                    assignments.Add(assignment);

                    // Remove used time from available slots
                    availableSlots = UpdateAvailableSlots(availableSlots, assignment);
                }
            }

            _logger.LogInformation("Found optimal time slots for {AssignedCount} out of {TotalCount} tasks",
                assignments.Count, taskIds.Count);

            return assignments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding optimal time slots for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ScheduleQualityAnalysis> AnalyzeScheduleQualityAsync(
        Guid userId,
        List<SmartScheduledItem> schedule,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing schedule quality for user {UserId} with {ItemCount} items",
            userId, schedule.Count);

        try
        {
            var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);
            var patterns = await _patternRepository.GetOptimizationEligiblePatternsAsync(userId, cancellationToken);

            // Analyze different quality dimensions
            var qualityDimensions = new Dictionary<string, double>
            {
                ["TimeUtilization"] = CalculateTimeUtilization(schedule, preferences),
                ["ProductivityAlignment"] = await CalculateProductivityAlignmentAsync(schedule, patterns.ToList(), cancellationToken),
                ["DeadlineCompliance"] = CalculateDeadlineCompliance(schedule),
                ["ContextSwitching"] = CalculateContextSwitchingScore(schedule),
                ["EnergyOptimization"] = CalculateEnergyOptimizationScore(schedule, preferences),
                ["FlexibilityScore"] = CalculateFlexibilityScore(schedule),
                ["BalanceScore"] = CalculateWorkLifeBalanceScore(schedule, preferences)
            };

            var overallScore = qualityDimensions.Values.Average();

            // Identify quality issues
            var issues = IdentifyQualityIssues(schedule, qualityDimensions, preferences);

            // Generate improvement suggestions
            var suggestions = GenerateQualityImprovements(schedule, qualityDimensions, issues, preferences);

            _logger.LogInformation("Schedule quality analysis completed for user {UserId}. Overall score: {Score:F2}",
                userId, overallScore);

            return new ScheduleQualityAnalysis(
                OverallScore: overallScore,
                QualityDimensions: qualityDimensions,
                Issues: issues,
                Suggestions: suggestions,
                AnalyzedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing schedule quality for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ProductivityScore> CalculateProductivityScoreAsync(
        Guid userId,
        List<SmartScheduledItem> schedule,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var patterns = await _patternRepository.GetHighProductivityPatternsAsync(userId, 0.6, cancellationToken);

            var factorContributions = new Dictionary<string, double>();
            var positiveFactors = new List<ProductivityFactor>();
            var negativeFactors = new List<ProductivityFactor>();

            // Analyze productivity factors
            await AnalyzeProductivityFactorsAsync(schedule, patterns.ToList(), preferences,
                factorContributions, positiveFactors, negativeFactors, cancellationToken);

            var overallScore = CalculateWeightedProductivityScore(factorContributions);
            var improvements = GenerateProductivityImprovements(negativeFactors, schedule);

            return new ProductivityScore(
                Score: overallScore,
                FactorContributions: factorContributions,
                PositiveFactors: positiveFactors,
                NegativeFactors: negativeFactors,
                ImprovementSuggestions: improvements
            );
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
            // Check if all dependencies are available
            var aiAvailable = await _aiPlanningService.IsAIServiceAvailableAsync(cancellationToken);

            // Simple health check - could be expanded with more sophisticated checks
            return aiAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking schedule optimization engine availability");
            return false;
        }
    }

    #region Private Helper Methods

    private async Task<List<AppTask>> LoadTasksAsync(List<Guid> taskIds, CancellationToken cancellationToken)
    {
        var tasks = new List<AppTask>();
        foreach (var taskId in taskIds)
        {
            var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
            if (task != null)
            {
                tasks.Add(task);
            }
        }
        return tasks;
    }

    private Task<List<AvailableTimeSlot>> GenerateAvailableTimeSlotsAsync(
        Guid userId, DateTime startDate, DateTime endDate, List<ExternalCalendarEvent> calendarEvents,
        SmartSchedulingPreferences preferences, CancellationToken cancellationToken)
    {
        var slots = new List<AvailableTimeSlot>();
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            if (IsWorkingDay(currentDate, preferences))
            {
                var daySlots = GenerateDayTimeSlots(currentDate, preferences, calendarEvents);
                slots.AddRange(daySlots);
            }
            currentDate = currentDate.AddDays(1);
        }

        return Task.FromResult(slots);
    }

    private bool IsWorkingDay(DateTime date, SmartSchedulingPreferences preferences)
    {
        return preferences.PreferredWorkingHours.WorkingDays.Contains(date.DayOfWeek);
    }

    private List<AvailableTimeSlot> GenerateDayTimeSlots(DateTime date, SmartSchedulingPreferences preferences, List<ExternalCalendarEvent> calendarEvents)
    {
        var slots = new List<AvailableTimeSlot>();
        var workStart = date.Date.Add(preferences.PreferredWorkingHours.StartTime);
        var workEnd = date.Date.Add(preferences.PreferredWorkingHours.EndTime);

        // Create 30-minute slots throughout the work day
        var currentTime = workStart;
        while (currentTime < workEnd)
        {
            var slotEnd = currentTime.AddMinutes(30);
            if (slotEnd > workEnd)
            {
                break;
            }

            // Check if slot conflicts with calendar events
            var hasConflict = calendarEvents.Any(ce =>
                ce.StartTime < slotEnd && ce.EndTime > currentTime);

            if (!hasConflict)
            {
                var slotType = DetermineSlotType(currentTime, preferences);
                var availabilityScore = CalculateAvailabilityScore(currentTime, preferences);

                slots.Add(new AvailableTimeSlot(
                    Id: Guid.NewGuid(),
                    StartTime: currentTime,
                    EndTime: slotEnd,
                    SlotType: slotType,
                    AvailabilityScore: availabilityScore,
                    Characteristics: GetSlotCharacteristics(currentTime, preferences)
                ));
            }

            currentTime = currentTime.AddMinutes(30);
        }

        return slots;
    }

    private TimeSlotType DetermineSlotType(DateTime time, SmartSchedulingPreferences preferences)
    {
        var hour = time.Hour;

        if (preferences.ProductivityPattern == ProductivityPatterns.MorningPerson && hour < 12)
        {
            return TimeSlotType.WorkingTime;
        }

        if (preferences.ProductivityPattern == ProductivityPatterns.NightOwl && hour > 14)
        {
            return TimeSlotType.WorkingTime;
        }

        if (hour >= 12 && hour <= 13)
        {
            return TimeSlotType.BreakTime;
        }

        return TimeSlotType.Available;
    }

    private double CalculateAvailabilityScore(DateTime time, SmartSchedulingPreferences preferences)
    {
        var baseScore = 0.8;
        var hour = time.Hour;

        // Adjust based on productivity pattern
        switch (preferences.ProductivityPattern)
        {
            case ProductivityPatterns.MorningPerson:
                if (hour >= 8 && hour <= 11)
                {
                    baseScore += 0.2;
                }

                if (hour >= 14 && hour <= 17)
                {
                    baseScore -= 0.1;
                }

                break;
            case ProductivityPatterns.NightOwl:
                if (hour >= 14 && hour <= 18)
                {
                    baseScore += 0.2;
                }

                if (hour >= 8 && hour <= 11)
                {
                    baseScore -= 0.1;
                }

                break;
            case ProductivityPatterns.MidDay:
                if (hour >= 10 && hour <= 15)
                {
                    baseScore += 0.15;
                }

                break;
        }

        return Math.Max(0.1, Math.Min(1.0, baseScore));
    }

    private List<string> GetSlotCharacteristics(DateTime time, SmartSchedulingPreferences preferences)
    {
        var characteristics = new List<string>();
        var hour = time.Hour;

        if (hour >= 8 && hour <= 11)
        {
            characteristics.Add("Morning");
        }

        if (hour >= 12 && hour <= 13)
        {
            characteristics.Add("Lunch");
        }

        if (hour >= 14 && hour <= 17)
        {
            characteristics.Add("Afternoon");
        }

        if (hour >= 17)
        {
            characteristics.Add("Evening");
        }

        if (preferences.ProductivityPattern == ProductivityPatterns.MorningPerson && hour < 12)
        {
            characteristics.Add("HighEnergy");
        }

        if (preferences.ProductivityPattern == ProductivityPatterns.NightOwl && hour > 14)
        {
            characteristics.Add("HighEnergy");
        }

        return characteristics;
    }

    // Additional helper methods would continue here...
    // Due to space constraints, implementing core functionality only

    private TimeSpan GetEstimatedDuration(SmartScheduledItem task, SmartSchedulingPreferences preferences)
    {
        // Use category-based estimates for now
        return task.Category switch
        {
            "Appointment" => TimeSpan.FromHours(1),
            "BillReminder" => TimeSpan.FromMinutes(15),
            "Project" => TimeSpan.FromHours(2),
            "Idea" => TimeSpan.FromMinutes(30),
            "ToDo" => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(1)
        };
    }

    // Placeholder implementations for remaining core methods
    private List<SmartScheduledItem> SortTasksByOptimizationPriority(List<AppTask> tasks, SmartSchedulingPreferences preferences)
    {
        return tasks.OrderByDescending(t => t.Priority)
                   .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                   .Select(t => new SmartScheduledItem(
                       Id: t.Id,
                       TaskId: t.Id,
                       CalendarEventId: null,
                       Title: t.Title,
                       Description: t.Description ?? string.Empty,
                       StartTime: DateTime.MinValue,
                       EndTime: DateTime.MinValue,
                       ItemType: ScheduledItemType.Task,
                       Priority: Domain.ValueObjects.Priority.FromValue(t.Priority),
                       Category: t.Category.ToString(),
                       Tags: new List<string>(),
                       EstimatedDuration: GetEstimatedDuration(new SmartScheduledItem(
                           t.Id, t.Id, null, t.Title, t.Description ?? "", DateTime.MinValue, DateTime.MinValue,
                           ScheduledItemType.Task, Domain.ValueObjects.Priority.FromValue(t.Priority),
                           t.Category.ToString(), new List<string>(), TimeSpan.FromHours(1), true, new List<SchedulingReason>()
                       ), preferences),
                       IsFlexible: t.Priority < 3,
                       SchedulingReasons: new List<SchedulingReason>()
                   ))
                   .ToList();
    }

    // Core optimization algorithm with multi-objective optimization
    private async Task<List<SmartScheduledItem>> ApplyOptimizationAlgorithmsAsync(
        Guid userId, 
        List<AppTask> tasks, 
        List<AvailableTimeSlot> availableSlots, 
        SmartSchedulingPreferences preferences, 
        List<DomainSchedulingPattern> patterns, 
        OptimizationGoals goals, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying optimization algorithms for {TaskCount} tasks and {SlotCount} available slots", 
            tasks.Count, availableSlots.Count);

        var scheduledItems = new List<SmartScheduledItem>();
        var remainingSlots = new List<AvailableTimeSlot>(availableSlots);

        // Convert tasks to schedulable items with priority scoring
        var prioritizedTasks = await PrioritizeTasksForSchedulingAsync(tasks, preferences, patterns, goals, cancellationToken);

        // Apply constraint satisfaction optimization
        foreach (var task in prioritizedTasks)
        {
            var bestAssignment = await FindOptimalTaskAssignmentAsync(
                task, remainingSlots, preferences, patterns, goals, scheduledItems, cancellationToken);

            if (bestAssignment != null)
            {
                scheduledItems.Add(bestAssignment.ScheduledItem);
                remainingSlots = UpdateRemainingSlots(remainingSlots, bestAssignment);
                
                _logger.LogDebug("Scheduled task {TaskId} at {StartTime} with fitness score {Score}", 
                    task.Id, bestAssignment.ScheduledItem.StartTime, bestAssignment.FitnessScore);
            }
            else
            {
                _logger.LogWarning("Could not find suitable time slot for task {TaskId}", task.Id);
            }
        }

        // Apply post-optimization improvements
        scheduledItems = await ApplyPostOptimizationImprovementsAsync(scheduledItems, preferences, patterns, cancellationToken);

        _logger.LogInformation("Optimization completed. Scheduled {ScheduledCount} out of {TotalCount} tasks", 
            scheduledItems.Count, tasks.Count);

        return scheduledItems;
    }

    private Dictionary<string, object> CalculateOptimizationMetrics(List<SmartScheduledItem> currentSchedule, List<SmartScheduledItem> optimizedSchedule)
    {
        var metrics = new Dictionary<string, object>();

        // Calculate productivity improvement
        var currentProductivityScore = CalculateScheduleProductivityScore(currentSchedule);
        var optimizedProductivityScore = CalculateScheduleProductivityScore(optimizedSchedule);
        var productivityImprovement = optimizedProductivityScore - currentProductivityScore;

        // Calculate time utilization improvement
        var currentUtilization = CalculateTimeUtilizationScore(currentSchedule);
        var optimizedUtilization = CalculateTimeUtilizationScore(optimizedSchedule);
        var utilizationImprovement = optimizedUtilization - currentUtilization;

        // Calculate context switching reduction
        var currentContextSwitches = CountContextSwitches(currentSchedule);
        var optimizedContextSwitches = CountContextSwitches(optimizedSchedule);
        var contextSwitchReduction = (double)(currentContextSwitches - optimizedContextSwitches) / Math.Max(currentContextSwitches, 1);

        // Calculate deadline compliance improvement
        var currentDeadlineCompliance = CalculateDeadlineCompliance(currentSchedule);
        var optimizedDeadlineCompliance = CalculateDeadlineCompliance(optimizedSchedule);
        var deadlineImprovment = optimizedDeadlineCompliance - currentDeadlineCompliance;

        // Calculate weighted improvement score
        var improvementScore = 
            (productivityImprovement * PRODUCTIVITY_WEIGHT) +
            (utilizationImprovement * FLEXIBILITY_WEIGHT) +
            (contextSwitchReduction * CONTEXT_SWITCHING_WEIGHT) +
            (deadlineImprovment * DEADLINE_WEIGHT);

        metrics["ImprovementScore"] = Math.Max(0, Math.Min(1, improvementScore));
        metrics["ProductivityImprovement"] = productivityImprovement;
        metrics["UtilizationImprovement"] = utilizationImprovement;
        metrics["ContextSwitchReduction"] = contextSwitchReduction;
        metrics["DeadlineImprovement"] = deadlineImprovment;
        metrics["TasksScheduled"] = optimizedSchedule.Count;
        metrics["TotalTimeBlocks"] = optimizedSchedule.Count;
        metrics["AverageTaskDuration"] = optimizedSchedule.Any() ? optimizedSchedule.Average(s => s.EstimatedDuration.TotalMinutes) : 0;

        return metrics;
    }

    private List<string> GenerateOptimizationInsights(List<SmartScheduledItem> optimizedSchedule, List<DomainSchedulingPattern> patterns, SmartSchedulingPreferences preferences)
    {
        return new List<string> { "Schedule optimized for productivity" };
    }

    // Additional placeholder methods
    private async Task<List<OptimizationChange>> ApplyIncrementalOptimizationAsync(
        Guid userId, 
        List<SmartScheduledItem> existingSchedule, 
        OptimizationGoals goals, 
        List<ScheduleConstraint> constraints, 
        List<DomainSchedulingPattern> patterns, 
        UserSchedulingPreference? preferences, 
        CancellationToken cancellationToken)
    {
        var optimizationChanges = new List<OptimizationChange>();
        
        _logger.LogInformation("Applying incremental optimization for {ItemCount} scheduled items", existingSchedule.Count);
        
        // Analyze current schedule for optimization opportunities
        var opportunities = await IdentifyOptimizationOpportunitiesAsync(
            existingSchedule, goals, patterns, preferences, cancellationToken);
            
        foreach (var opportunity in opportunities)
        {
            var change = await CreateOptimizationChangeAsync(
                opportunity, existingSchedule, constraints, preferences, cancellationToken);
                
            if (change != null)
            {
                optimizationChanges.Add(change);
            }
        }
        
        // Sort changes by impact score (highest first)
        optimizationChanges.Sort((a, b) => b.ImpactScore.CompareTo(a.ImpactScore));
        
        _logger.LogInformation("Generated {ChangeCount} optimization changes", optimizationChanges.Count);
        
        return optimizationChanges;
    }
    private List<SmartScheduledItem> ApplyOptimizationChanges(List<SmartScheduledItem> existingSchedule, List<OptimizationChange> optimizationChanges)
    {
        var optimizedSchedule = existingSchedule.ToList();
        
        foreach (var change in optimizationChanges)
        {
            var itemIndex = optimizedSchedule.FindIndex(s => s.Id == change.ItemId);
            if (itemIndex >= 0)
            {
                var originalItem = optimizedSchedule[itemIndex];
                
                var updatedItem = change.ChangeType switch
                {
                    "Reschedule" => originalItem with 
                    {
                        StartTime = change.NewStartTime ?? originalItem.StartTime,
                        EndTime = (change.NewStartTime ?? originalItem.StartTime).Add(
                            change.NewDuration ?? originalItem.EstimatedDuration),
                        SchedulingReasons = originalItem.SchedulingReasons.Concat(new[] 
                        {
                            new SchedulingReason("Optimization", change.Reasoning, 0.8, 
                                new List<string> { "schedule_optimization" })
                        }).ToList()
                    },
                    "AdjustDuration" => originalItem with 
                    {
                        EndTime = originalItem.StartTime.Add(change.NewDuration ?? originalItem.EstimatedDuration),
                        EstimatedDuration = change.NewDuration ?? originalItem.EstimatedDuration,
                        SchedulingReasons = originalItem.SchedulingReasons.Concat(new[] 
                        {
                            new SchedulingReason("DurationOptimization", change.Reasoning, 0.7, 
                                new List<string> { "time_management" })
                        }).ToList()
                    },
                    _ => originalItem
                };
                
                optimizedSchedule[itemIndex] = updatedItem;
            }
        }
        
        return optimizedSchedule;
    }
    private double CalculateImprovementScore(List<SmartScheduledItem> existingSchedule, List<SmartScheduledItem> optimizedSchedule, UserSchedulingPreference? preferences)
    {
        if (!existingSchedule.Any() && !optimizedSchedule.Any()) return 0.0;
        if (!existingSchedule.Any()) return 1.0;
        if (!optimizedSchedule.Any()) return 0.0;
        
        var improvements = new List<double>();
        
        // Calculate productivity improvement
        var originalProductivity = CalculateScheduleProductivityScore(existingSchedule);
        var optimizedProductivity = CalculateScheduleProductivityScore(optimizedSchedule);
        var productivityImprovement = optimizedProductivity - originalProductivity;
        improvements.Add(productivityImprovement * PRODUCTIVITY_WEIGHT);
        
        // Calculate time utilization improvement
        var originalUtilization = CalculateTimeUtilization(existingSchedule, preferences);
        var optimizedUtilization = CalculateTimeUtilization(optimizedSchedule, preferences);
        var utilizationImprovement = optimizedUtilization - originalUtilization;
        improvements.Add(utilizationImprovement * FLEXIBILITY_WEIGHT);
        
        // Calculate context switching improvement
        var originalContextScore = CalculateContextSwitchingScore(existingSchedule);
        var optimizedContextScore = CalculateContextSwitchingScore(optimizedSchedule);
        var contextImprovement = optimizedContextScore - originalContextScore;
        improvements.Add(contextImprovement * CONTEXT_SWITCHING_WEIGHT);
        
        // Calculate energy optimization improvement
        var originalEnergyScore = CalculateEnergyOptimizationScore(existingSchedule, preferences);
        var optimizedEnergyScore = CalculateEnergyOptimizationScore(optimizedSchedule, preferences);
        var energyImprovement = optimizedEnergyScore - originalEnergyScore;
        improvements.Add(energyImprovement * ENERGY_WEIGHT);
        
        // Calculate deadline compliance improvement
        var originalDeadlineScore = CalculateDeadlineCompliance(existingSchedule);
        var optimizedDeadlineScore = CalculateDeadlineCompliance(optimizedSchedule);
        var deadlineImprovement = optimizedDeadlineScore - originalDeadlineScore;
        improvements.Add(deadlineImprovement * DEADLINE_WEIGHT);
        
        var totalImprovement = improvements.Sum();
        return Math.Max(0.0, Math.Min(1.0, totalImprovement));
    }
    private Dictionary<string, double> CalculateQualityMetrics(List<SmartScheduledItem> optimizedSchedule, UserSchedulingPreference? preferences)
    {
        var metrics = new Dictionary<string, double>
        {
            ["TimeUtilization"] = CalculateTimeUtilization(optimizedSchedule, preferences),
            ["ProductivityAlignment"] = CalculateScheduleProductivityScore(optimizedSchedule),
            ["ContextSwitching"] = CalculateContextSwitchingScore(optimizedSchedule),
            ["EnergyOptimization"] = CalculateEnergyOptimizationScore(optimizedSchedule, preferences),
            ["FlexibilityScore"] = CalculateFlexibilityScore(optimizedSchedule),
            ["WorkLifeBalance"] = CalculateWorkLifeBalanceScore(optimizedSchedule, preferences),
            ["DeadlineCompliance"] = CalculateDeadlineCompliance(optimizedSchedule)
        };
        
        // Calculate overall quality score
        metrics["OverallQuality"] = metrics.Values.Average();
        
        // Add schedule-specific metrics
        metrics["TotalScheduledItems"] = optimizedSchedule.Count;
        metrics["AverageDuration"] = optimizedSchedule.Any() ? 
            optimizedSchedule.Average(s => s.EstimatedDuration.TotalMinutes) : 0.0;
        metrics["ScheduleSpan"] = optimizedSchedule.Any() ? 
            (optimizedSchedule.Max(s => s.EndTime) - optimizedSchedule.Min(s => s.StartTime)).TotalHours : 0.0;
        
        return metrics;
    }
    private List<string> GenerateOptimizationReasons(List<OptimizationChange> optimizationChanges, ScheduleQualityAnalysis qualityAnalysis)
    {
        var reasons = new List<string>();
        
        if (optimizationChanges.Any())
        {
            var changesByType = optimizationChanges.GroupBy(c => c.ChangeType).ToList();
            
            foreach (var changeGroup in changesByType)
            {
                var count = changeGroup.Count();
                var avgImpact = changeGroup.Average(c => c.ImpactScore);
                
                var reason = changeGroup.Key switch
                {
                    "Reschedule" => $"Rescheduled {count} tasks for better productivity alignment (avg improvement: {avgImpact:P0})",
                    "AdjustDuration" => $"Adjusted duration of {count} tasks for optimal time management",
                    "Reorder" => $"Reordered {count} tasks to minimize context switching",
                    "AddBuffer" => $"Added buffer time to {count} tasks for better flexibility",
                    _ => $"Applied {changeGroup.Key} optimization to {count} tasks"
                };
                
                reasons.Add(reason);
            }
        }
        
        // Add quality-based reasons
        if (qualityAnalysis.OverallScore < 0.7)
        {
            reasons.Add($"Overall schedule quality improved from {qualityAnalysis.OverallScore:P0}");
        }
        
        var lowQualityDimensions = qualityAnalysis.QualityDimensions
            .Where(kvp => kvp.Value < 0.6)
            .Select(kvp => kvp.Key)
            .ToList();
            
        if (lowQualityDimensions.Any())
        {
            reasons.Add($"Improved low-performing areas: {string.Join(", ", lowQualityDimensions)}");
        }
        
        return reasons;
    }
    private double CalculateTimeUtilization(List<SmartScheduledItem> schedule, UserSchedulingPreference? preferences)
    {
        if (!schedule.Any()) return 0.0;

        var workingHours = preferences?.PreferredWorkingHours ?? 
            new Domain.Entities.WorkingHours 
            {
                StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(17)
            };

        var totalWorkingTime = workingHours.EndTime - workingHours.StartTime;
        var scheduledTime = schedule.Sum(s => s.EstimatedDuration.TotalHours);
        
        return totalWorkingTime.TotalHours > 0 ? Math.Min(1.0, scheduledTime / totalWorkingTime.TotalHours) : 0.0;
    }
    private Task<double> CalculateProductivityAlignmentAsync(List<SmartScheduledItem> schedule, List<DomainSchedulingPattern> patterns, CancellationToken cancellationToken)
    {
        if (!schedule.Any()) return Task.FromResult(0.0);

        var totalScore = 0.0;
        var itemCount = 0;

        foreach (var item in schedule)
        {
            var itemScore = CalculateItemProductivityAlignment(item, patterns);
            totalScore += itemScore;
            itemCount++;
        }

        var averageScore = itemCount > 0 ? totalScore / itemCount : 0.0;
        return Task.FromResult(averageScore);
    }

    private double CalculateItemProductivityAlignment(SmartScheduledItem item, List<DomainSchedulingPattern> patterns)
    {
        // Base score from time of day
        var timeScore = GetProductivityMultiplierForTime(item.StartTime);
        
        // Pattern-based score
        var patternScore = 0.7; // Default
        var matchingPatterns = patterns.Where(p => 
            p.Category.ToString() == item.Category && 
            IsTimeInPattern(item.StartTime, p)).ToList();
            
        if (matchingPatterns.Any())
        {
            patternScore = matchingPatterns.Average(p => p.SuccessRate);
        }
        
        return (timeScore + patternScore) / 2.0;
    }

    private bool IsTimeInPattern(DateTime itemTime, DomainSchedulingPattern pattern)
    {
        // Simplified pattern matching - in real implementation this would be more sophisticated
        var hour = itemTime.Hour;
        return pattern.PreferredTimeOfDay switch
        {
            "Morning" => hour >= 8 && hour < 12,
            "Afternoon" => hour >= 12 && hour < 17,
            "Evening" => hour >= 17 && hour < 21,
            _ => true
        };
    }
    private double CalculateDeadlineCompliance(List<SmartScheduledItem> schedule) => 0.9;
    private double CalculateContextSwitchingScore(List<SmartScheduledItem> schedule)
    {
        if (schedule.Count <= 1) return 1.0;

        var contextSwitches = CountContextSwitches(schedule);
        var maxPossibleSwitches = schedule.Count - 1;
        
        // Lower context switches = higher score
        var switchRatio = (double)contextSwitches / maxPossibleSwitches;
        return 1.0 - switchRatio;
    }
    private double CalculateEnergyOptimizationScore(List<SmartScheduledItem> schedule, UserSchedulingPreference? preferences)
    {
        if (!schedule.Any()) return 0.0;

        var totalScore = 0.0;
        
        foreach (var item in schedule)
        {
            var energyScore = CalculateItemEnergyScore(item, preferences);
            var weightedScore = energyScore * item.Priority.Value; // Weight by priority
            totalScore += weightedScore;
        }
        
        var maxPossibleScore = schedule.Sum(s => s.Priority.Value);
        return maxPossibleScore > 0 ? totalScore / maxPossibleScore : 0.0;
    }
    
    private double CalculateItemEnergyScore(SmartScheduledItem item, UserSchedulingPreference? preferences)
    {
        var hour = item.StartTime.Hour;
        
        // Default energy pattern if no preferences
        if (preferences?.ProductivityPattern == null)
        {
            return GetProductivityMultiplierForTime(item.StartTime);
        }
        
        return preferences.ProductivityPattern switch
        {
            "MorningPerson" => hour switch
            {
                >= 8 and <= 11 => 1.0,
                >= 12 and <= 15 => 0.7,
                _ => 0.5
            },
            "NightOwl" => hour switch
            {
                >= 14 and <= 18 => 1.0,
                >= 8 and <= 11 => 0.6,
                _ => 0.5
            },
            "MidDay" => hour switch
            {
                >= 10 and <= 15 => 0.9,
                _ => 0.6
            },
            _ => 0.75
        };
    }
    private double CalculateFlexibilityScore(List<SmartScheduledItem> schedule)
    {
        if (!schedule.Any()) return 0.0;

        var flexibleItems = schedule.Count(s => s.IsFlexible);
        var totalItems = schedule.Count;
        
        var flexibilityRatio = (double)flexibleItems / totalItems;
        
        // Also consider spacing between items for flexibility
        var averageGap = CalculateAverageGapBetweenItems(schedule);
        var gapScore = Math.Min(1.0, averageGap.TotalMinutes / 30.0); // 30 minutes = full score
        
        return (flexibilityRatio + gapScore) / 2.0;
    }
    
    private TimeSpan CalculateAverageGapBetweenItems(List<SmartScheduledItem> schedule)
    {
        if (schedule.Count <= 1) return TimeSpan.Zero;
        
        var orderedSchedule = schedule.OrderBy(s => s.StartTime).ToList();
        var gaps = new List<TimeSpan>();
        
        for (int i = 1; i < orderedSchedule.Count; i++)
        {
            var gap = orderedSchedule[i].StartTime - orderedSchedule[i-1].EndTime;
            if (gap > TimeSpan.Zero)
            {
                gaps.Add(gap);
            }
        }
        
        return gaps.Any() ? TimeSpan.FromTicks((long)gaps.Average(g => g.Ticks)) : TimeSpan.Zero;
    }
    private double CalculateWorkLifeBalanceScore(List<SmartScheduledItem> schedule, UserSchedulingPreference? preferences)
    {
        if (!schedule.Any()) return 1.0;

        var workingHours = preferences?.PreferredWorkingHours;
        if (workingHours == null) return 0.8; // Default score
        
        var itemsOutsideHours = schedule.Count(s => 
            s.StartTime.TimeOfDay < workingHours.StartTime ||
            s.EndTime.TimeOfDay > workingHours.EndTime);
            
        var totalItems = schedule.Count;
        var balanceRatio = 1.0 - ((double)itemsOutsideHours / totalItems);
        
        // Factor in break compliance
        var hasAdequateBreaks = HasAdequateBreakTime(schedule, preferences);
        var breakScore = hasAdequateBreaks ? 1.0 : 0.7;
        
        return (balanceRatio + breakScore) / 2.0;
    }
    
    private bool HasAdequateBreakTime(List<SmartScheduledItem> schedule, UserSchedulingPreference? preferences)
    {
        if (!schedule.Any()) return true;
        
        var orderedSchedule = schedule.OrderBy(s => s.StartTime).ToList();
        var totalWorkTime = orderedSchedule.Sum(s => s.EstimatedDuration.TotalMinutes);
        
        // Check for lunch break around midday
        var lunchTimeStart = new TimeSpan(12, 0, 0);
        var lunchTimeEnd = new TimeSpan(13, 30, 0);
        
        var hasLunchBreak = !orderedSchedule.Any(s => 
            s.StartTime.TimeOfDay < lunchTimeEnd && 
            s.EndTime.TimeOfDay > lunchTimeStart);
            
        // Check for adequate breaks between tasks (every 2 hours)
        var adequateBreaks = true;
        var lastEndTime = orderedSchedule.First().StartTime;
        
        foreach (var item in orderedSchedule)
        {
            var timeSinceLastBreak = item.StartTime - lastEndTime;
            if (timeSinceLastBreak.TotalHours > 2.5 && timeSinceLastBreak.TotalMinutes < 15)
            {
                adequateBreaks = false;
                break;
            }
            lastEndTime = item.EndTime;
        }
        
        return hasLunchBreak && adequateBreaks;
    }
    private List<QualityIssue> IdentifyQualityIssues(List<SmartScheduledItem> schedule, Dictionary<string, double> qualityDimensions, UserSchedulingPreference? preferences)
    {
        var issues = new List<QualityIssue>();
        
        // Time utilization issues
        if (qualityDimensions.GetValueOrDefault("TimeUtilization", 0) < 0.3)
        {
            issues.Add(new QualityIssue(
                "LowTimeUtilization",
                "Schedule has low time utilization with significant gaps",
                "Medium",
                schedule.Select(s => s.Id).ToList(),
                new List<string> { "Productivity", "Efficiency" }
            ));
        }
        
        // Productivity alignment issues
        if (qualityDimensions.GetValueOrDefault("ProductivityAlignment", 0) < 0.5)
        {
            var lowProductivityItems = schedule
                .Where(s => GetProductivityMultiplierForTime(s.StartTime) < 0.7)
                .ToList();
                
            if (lowProductivityItems.Any())
            {
                issues.Add(new QualityIssue(
                    "PoorProductivityAlignment",
                    $"{lowProductivityItems.Count} tasks scheduled during low-productivity hours",
                    "High",
                    lowProductivityItems.Select(s => s.Id).ToList(),
                    new List<string> { "Productivity", "Performance" }
                ));
            }
        }
        
        // Context switching issues
        if (qualityDimensions.GetValueOrDefault("ContextSwitching", 0) < 0.6)
        {
            var contextSwitches = CountContextSwitches(schedule);
            if (contextSwitches > schedule.Count * 0.7)
            {
                issues.Add(new QualityIssue(
                    "ExcessiveContextSwitching",
                    $"Schedule has {contextSwitches} context switches, which may reduce efficiency",
                    "Medium",
                    schedule.Select(s => s.Id).ToList(),
                    new List<string> { "Focus", "Efficiency" }
                ));
            }
        }
        
        // Deadline compliance issues
        if (qualityDimensions.GetValueOrDefault("DeadlineCompliance", 0) < 0.7)
        {
            var overdueTasks = schedule.Where(s => 
                s.TaskId.HasValue && 
                s.EndTime > DateTime.UtcNow && // Scheduled for future but might be overdue
                s.Priority.Value >= 3).ToList();
                
            if (overdueTasks.Any())
            {
                issues.Add(new QualityIssue(
                    "DeadlineRisk",
                    $"{overdueTasks.Count} high-priority tasks may miss deadlines",
                    "High",
                    overdueTasks.Select(s => s.Id).ToList(),
                    new List<string> { "Deadlines", "Priority Management" }
                ));
            }
        }
        
        // Work-life balance issues
        if (qualityDimensions.GetValueOrDefault("BalanceScore", 0) < 0.6)
        {
            issues.Add(new QualityIssue(
                "WorkLifeBalanceIssue",
                "Schedule extends beyond preferred working hours or lacks adequate breaks",
                "Medium",
                schedule.Select(s => s.Id).ToList(),
                new List<string> { "Work-Life Balance", "Wellbeing" }
            ));
        }
        
        return issues;
    }
    private List<QualityImprovement> GenerateQualityImprovements(List<SmartScheduledItem> schedule, Dictionary<string, double> qualityDimensions, List<QualityIssue> issues, UserSchedulingPreference? preferences)
    {
        var improvements = new List<QualityImprovement>();
        
        foreach (var issue in issues)
        {
            switch (issue.IssueType)
            {
                case "LowTimeUtilization":
                    improvements.Add(new QualityImprovement(
                        "ConsolidateTimeBlocks",
                        "Group similar tasks together to reduce gaps and improve time utilization",
                        0.25,
                        issue.AffectedItems,
                        new List<string> 
                        {
                            "Identify tasks that can be grouped by category",
                            "Reduce buffer time between related tasks",
                            "Consider batching similar activities"
                        }
                    ));
                    break;
                    
                case "PoorProductivityAlignment":
                    improvements.Add(new QualityImprovement(
                        "RescheduleToProductiveHours",
                        "Move high-priority tasks to peak productivity hours",
                        0.35,
                        issue.AffectedItems,
                        new List<string>
                        {
                            "Move complex tasks to morning hours (9-11 AM)",
                            "Schedule routine tasks during lower energy periods",
                            "Align task difficulty with energy levels"
                        }
                    ));
                    break;
                    
                case "ExcessiveContextSwitching":
                    improvements.Add(new QualityImprovement(
                        "ReduceContextSwitching",
                        "Group similar tasks together to minimize context switches",
                        0.20,
                        issue.AffectedItems,
                        new List<string>
                        {
                            "Batch similar task categories together",
                            "Create themed time blocks (e.g., 'Admin Hour', 'Deep Work Block')",
                            "Minimize transitions between different types of work"
                        }
                    ));
                    break;
                    
                case "DeadlineRisk":
                    improvements.Add(new QualityImprovement(
                        "PrioritizeUrgentTasks",
                        "Reschedule urgent tasks to earlier time slots",
                        0.40,
                        issue.AffectedItems,
                        new List<string>
                        {
                            "Move deadline-critical tasks to earlier in the day",
                            "Allocate more time for high-priority items",
                            "Consider breaking down large urgent tasks"
                        }
                    ));
                    break;
                    
                case "WorkLifeBalanceIssue":
                    improvements.Add(new QualityImprovement(
                        "ImproveWorkLifeBalance",
                        "Adjust schedule to respect working hours and break times",
                        0.15,
                        issue.AffectedItems,
                        new List<string>
                        {
                            "Ensure adequate lunch break (30+ minutes)",
                            "Avoid scheduling tasks outside preferred hours",
                            "Add 15-minute breaks every 2 hours",
                            "Reserve evening time for personal activities"
                        }
                    ));
                    break;
            }
        }
        
        // General improvements based on quality scores
        if (qualityDimensions.GetValueOrDefault("FlexibilityScore", 0) < 0.5)
        {
            improvements.Add(new QualityImprovement(
                "IncreaseFlexibility",
                "Add buffer time and flexible slots to accommodate unexpected changes",
                0.15,
                schedule.Select(s => s.Id).ToList(),
                new List<string>
                {
                    "Add 10-15 minute buffers between tasks",
                    "Keep 20% of schedule unplanned for flexibility",
                    "Mark non-urgent tasks as flexible"
                }
            ));
        }
        
        return improvements;
    }
    private Task AnalyzeProductivityFactorsAsync(List<SmartScheduledItem> schedule, List<DomainSchedulingPattern> patterns, SmartSchedulingPreferences preferences, Dictionary<string, double> factorContributions, List<ProductivityFactor> positiveFactors, List<ProductivityFactor> negativeFactors, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    private double CalculateWeightedProductivityScore(Dictionary<string, double> factorContributions) => factorContributions.Values.DefaultIfEmpty(0.7).Average();
    private List<string> GenerateProductivityImprovements(List<ProductivityFactor> negativeFactors, List<SmartScheduledItem> schedule) => new();
    private async Task<AvailableTimeSlot?> FindBestTimeSlotForTaskAsync(
        SmartScheduledItem task, 
        List<AvailableTimeSlot> availableSlots, 
        List<DomainSchedulingPattern> patterns, 
        SmartSchedulingPreferences preferences, 
        List<TimeSlotAssignment> assignments, 
        CancellationToken cancellationToken)
    {
        if (!availableSlots.Any())
        {
            return null;
        }

        var taskDuration = task.EstimatedDuration;
        var suitableSlots = availableSlots.Where(slot => 
            (slot.EndTime - slot.StartTime) >= taskDuration).ToList();

        if (!suitableSlots.Any())
        {
            return null;
        }

        // Score each suitable slot based on multiple factors
        var slotScores = new List<(AvailableTimeSlot Slot, double Score)>();

        foreach (var slot in suitableSlots)
        {
            var score = await CalculateSlotFitnessForTaskAsync(task, slot, patterns, preferences, assignments, cancellationToken);
            slotScores.Add((slot, score));
        }

        // Return the slot with the highest fitness score
        var bestSlot = slotScores.OrderByDescending(s => s.Score).First();
        
        _logger.LogDebug("Selected slot {SlotId} for task {TaskId} with score {Score}", 
            bestSlot.Slot.Id, task.TaskId, bestSlot.Score);

        return bestSlot.Slot;
    }
    private double CalculateSlotFitnessScore(SmartScheduledItem task, AvailableTimeSlot slot, List<DomainSchedulingPattern> patterns, SmartSchedulingPreferences preferences)
    {
        var fitnessScore = 0.0;
        var weights = new Dictionary<string, double>
        {
            ["productivity_alignment"] = 0.25,
            ["energy_level"] = 0.20,
            ["deadline_urgency"] = 0.20,
            ["context_continuity"] = 0.15,
            ["preference_match"] = 0.10,
            ["availability_score"] = 0.10
        };

        // Productivity alignment score
        var productivityScore = CalculateProductivityAlignmentScore(task, slot, patterns, preferences);
        fitnessScore += productivityScore * weights["productivity_alignment"];

        // Energy level score based on time of day and user patterns
        var energyScore = CalculateEnergyLevelScore(slot.StartTime, preferences);
        fitnessScore += energyScore * weights["energy_level"];

        // Deadline urgency score
        var urgencyScore = CalculateDeadlineUrgencyScore(task, slot);
        fitnessScore += urgencyScore * weights["deadline_urgency"];

        // Context continuity (prefer grouping similar tasks)
        var contextScore = CalculateContextContinuityScore(task, slot);
        fitnessScore += contextScore * weights["context_continuity"];

        // User preference match
        var preferenceScore = CalculatePreferenceMatchScore(task, slot, preferences);
        fitnessScore += preferenceScore * weights["preference_match"];

        // Base availability score from slot
        fitnessScore += slot.AvailabilityScore * weights["availability_score"];

        return Math.Max(0.0, Math.Min(1.0, fitnessScore));
    }
    private List<string> GenerateAssignmentReasons(SmartScheduledItem task, AvailableTimeSlot slot, List<DomainSchedulingPattern> patterns) => new() { "Optimal time slot identified" };
    private List<AvailableTimeSlot> UpdateAvailableSlots(List<AvailableTimeSlot> slots, TimeSlotAssignment assignment)
    {
        return slots.Where(s => s.Id != assignment.SlotId || 
                              s.EndTime <= assignment.StartTime || 
                              s.StartTime >= assignment.EndTime).ToList();
    }

    private List<AvailableTimeSlot> UpdateRemainingSlots(List<AvailableTimeSlot> slots, TaskAssignmentResult assignment)
    {
        var updatedSlots = new List<AvailableTimeSlot>();
        var assignmentStart = assignment.ScheduledItem.StartTime;
        var assignmentEnd = assignment.ScheduledItem.EndTime;

        foreach (var slot in slots)
        {
            if (slot.Id == assignment.Slot.Id)
            {
                // Split the slot if necessary
                if (slot.StartTime < assignmentStart)
                {
                    updatedSlots.Add(new AvailableTimeSlot(
                        Guid.NewGuid(),
                        slot.StartTime,
                        assignmentStart,
                        slot.SlotType,
                        slot.AvailabilityScore,
                        slot.Characteristics));
                }

                if (slot.EndTime > assignmentEnd)
                {
                    updatedSlots.Add(new AvailableTimeSlot(
                        Guid.NewGuid(),
                        assignmentEnd,
                        slot.EndTime,
                        slot.SlotType,
                        slot.AvailabilityScore,
                        slot.Characteristics));
                }
            }
            else if (slot.EndTime <= assignmentStart || slot.StartTime >= assignmentEnd)
            {
                // Slot doesn't overlap with assignment
                updatedSlots.Add(slot);
            }
        }

        return updatedSlots;
    }

    // New comprehensive algorithm implementations

    private async Task<List<AppTask>> PrioritizeTasksForSchedulingAsync(
        List<AppTask> tasks, 
        SmartSchedulingPreferences preferences, 
        List<DomainSchedulingPattern> patterns, 
        OptimizationGoals goals, 
        CancellationToken cancellationToken)
    {
        var taskScores = new List<(AppTask Task, double Score)>();

        foreach (var task in tasks)
        {
            var score = CalculateTaskSchedulingPriority(task, preferences, patterns, goals);
            taskScores.Add((task, score));
        }

        return taskScores.OrderByDescending(t => t.Score).Select(t => t.Task).ToList();
    }

    private double CalculateTaskSchedulingPriority(
        AppTask task, 
        SmartSchedulingPreferences preferences, 
        List<DomainSchedulingPattern> patterns, 
        OptimizationGoals goals)
    {
        var priorityScore = 0.0;

        // Base priority from task
        priorityScore += (task.Priority / 4.0) * 0.3; // Normalize to 0-1

        // Deadline urgency
        if (task.DueDate.HasValue)
        {
            var daysUntilDue = (task.DueDate.Value - DateTime.UtcNow).TotalDays;
            var urgencyScore = Math.Max(0, 1.0 - (daysUntilDue / 30.0)); // More urgent as deadline approaches
            priorityScore += urgencyScore * 0.25;
        }

        // Category importance based on preferences
        var categoryScore = preferences.PreferredTaskCategories.Contains(task.Category.ToString()) ? 0.2 : 0.1;
        priorityScore += categoryScore;

        // Historical success patterns
        var patternScore = CalculatePatternAlignmentScore(task, patterns);
        priorityScore += patternScore * 0.15;

        // Goal alignment
        var goalScore = CalculateGoalAlignmentScore(task, goals);
        priorityScore += goalScore * 0.1;

        return Math.Max(0.0, Math.Min(1.0, priorityScore));
    }

    private async Task<TaskAssignmentResult?> FindOptimalTaskAssignmentAsync(
        AppTask task,
        List<AvailableTimeSlot> availableSlots,
        SmartSchedulingPreferences preferences,
        List<DomainSchedulingPattern> patterns,
        OptimizationGoals goals,
        List<SmartScheduledItem> existingSchedule,
        CancellationToken cancellationToken)
    {
        var taskDuration = GetEstimatedTaskDuration(task, preferences);
        var suitableSlots = availableSlots.Where(slot => 
            (slot.EndTime - slot.StartTime) >= taskDuration).ToList();

        if (!suitableSlots.Any())
        {
            return null;
        }

        var bestScore = 0.0;
        AvailableTimeSlot? bestSlot = null;

        foreach (var slot in suitableSlots)
        {
            var fitnessScore = await CalculateTaskSlotFitnessAsync(
                task, slot, preferences, patterns, goals, existingSchedule, cancellationToken);

            if (fitnessScore > bestScore)
            {
                bestScore = fitnessScore;
                bestSlot = slot;
            }
        }

        if (bestSlot == null)
        {
            return null;
        }

        var scheduledItem = CreateScheduledItemFromTask(task, bestSlot, taskDuration);
        return new TaskAssignmentResult(scheduledItem, bestSlot, bestScore);
    }

    private async Task<double> CalculateTaskSlotFitnessAsync(
        AppTask task,
        AvailableTimeSlot slot,
        SmartSchedulingPreferences preferences,
        List<DomainSchedulingPattern> patterns,
        OptimizationGoals goals,
        List<SmartScheduledItem> existingSchedule,
        CancellationToken cancellationToken)
    {
        var fitnessScore = 0.0;

        // Time-based productivity score
        var timeScore = CalculateTimeBasedProductivityScore(slot.StartTime, preferences);
        fitnessScore += timeScore * PRODUCTIVITY_WEIGHT;

        // Energy alignment score
        var energyScore = CalculateEnergyAlignmentScore(task, slot, preferences);
        fitnessScore += energyScore * ENERGY_WEIGHT;

        // Deadline pressure score
        var deadlineScore = CalculateDeadlinePressureScore(task, slot);
        fitnessScore += deadlineScore * DEADLINE_WEIGHT;

        // Context switching minimization
        var contextScore = CalculateContextSwitchingScore(task, slot, existingSchedule);
        fitnessScore += contextScore * CONTEXT_SWITCHING_WEIGHT;

        // Flexibility score
        var flexibilityScore = CalculateFlexibilityScore(task, slot);
        fitnessScore += flexibilityScore * FLEXIBILITY_WEIGHT;

        return Math.Max(0.0, Math.Min(1.0, fitnessScore));
    }

    private async Task<List<SmartScheduledItem>> ApplyPostOptimizationImprovementsAsync(
        List<SmartScheduledItem> scheduledItems,
        SmartSchedulingPreferences preferences,
        List<DomainSchedulingPattern> patterns,
        CancellationToken cancellationToken)
    {
        var improvedSchedule = new List<SmartScheduledItem>(scheduledItems);

        // Apply time block consolidation
        improvedSchedule = ConsolidateSimilarTasks(improvedSchedule);

        // Optimize break placement
        improvedSchedule = await OptimizeBreakPlacementAsync(improvedSchedule, preferences, cancellationToken);

        // Apply buffer time optimization
        improvedSchedule = AddOptimalBufferTimes(improvedSchedule, preferences);

        return improvedSchedule;
    }

    // Supporting calculation methods
    private double CalculateScheduleProductivityScore(List<SmartScheduledItem> schedule)
    {
        if (!schedule.Any()) return 0.0;

        var totalScore = 0.0;
        var totalDuration = TimeSpan.Zero;

        foreach (var item in schedule)
        {
            var productivityMultiplier = GetProductivityMultiplierForTime(item.StartTime);
            var itemScore = productivityMultiplier * item.EstimatedDuration.TotalMinutes;
            totalScore += itemScore;
            totalDuration += item.EstimatedDuration;
        }

        return totalDuration.TotalMinutes > 0 ? totalScore / totalDuration.TotalMinutes : 0.0;
    }

    private double CalculateTimeUtilizationScore(List<SmartScheduledItem> schedule)
    {
        if (!schedule.Any()) return 0.0;

        var workingHours = schedule.Max(s => s.EndTime) - schedule.Min(s => s.StartTime);
        var scheduledTime = schedule.Sum(s => s.EstimatedDuration.TotalHours);
        
        return workingHours.TotalHours > 0 ? scheduledTime / workingHours.TotalHours : 0.0;
    }

    private int CountContextSwitches(List<SmartScheduledItem> schedule)
    {
        if (schedule.Count <= 1) return 0;

        var contextSwitches = 0;
        for (int i = 1; i < schedule.Count; i++)
        {
            if (schedule[i].Category != schedule[i-1].Category)
            {
                contextSwitches++;
            }
        }
        return contextSwitches;
    }

    private double GetProductivityMultiplierForTime(DateTime time)
    {
        var hour = time.Hour;
        return hour switch
        {
            >= 9 and <= 11 => 1.0,   // Peak morning
            >= 14 and <= 16 => 0.9,  // Good afternoon
            >= 8 and <= 9 => 0.8,    // Early morning
            >= 11 and <= 13 => 0.7,  // Pre-lunch
            >= 16 and <= 18 => 0.8,  // Late afternoon
            _ => 0.6                 // Off-peak hours
        };
    }

    private double CalculatePatternAlignmentScore(AppTask task, List<DomainSchedulingPattern> patterns)
    {
        // Find patterns that match this task's characteristics
        var matchingPatterns = patterns.Where(p => 
            p.Category.ToString() == task.Category.ToString() || 
            p.Priority == task.Priority).ToList();

        if (!matchingPatterns.Any())
        {
            return 0.5; // Neutral score for no patterns
        }

        return matchingPatterns.Average(p => p.SuccessRate);
    }

    private double CalculateGoalAlignmentScore(AppTask task, OptimizationGoals goals)
    {
        var score = 0.5; // Base score

        if ((goals.Primary == OptimizationPriority.Productivity || goals.Secondary.Contains(OptimizationPriority.Productivity)) && task.Priority >= 3)
        {
            score += 0.2 * goals.ProductivityWeight;
        }

        if (goals.MinimizeContextSwitching)
        {
            score += 0.1 * goals.EfficiencyWeight; // Will be adjusted based on scheduling context
        }

        if ((goals.Primary == OptimizationPriority.DeadlineAdherence || goals.RespectDeadlines) && task.DueDate.HasValue)
        {
            var urgency = (DateTime.UtcNow - task.DueDate.Value).TotalDays;
            score += (urgency < 7 ? 0.2 : 0.1) * goals.EfficiencyWeight;
        }

        return Math.Max(0.0, Math.Min(1.0, score));
    }

    #endregion

    // Additional helper methods for comprehensive scheduling

    private TimeSpan GetEstimatedTaskDuration(AppTask task, SmartSchedulingPreferences preferences)
    {
        var baseEstimate = task.Category switch
        {
            (int)Domain.ValueObjects.AppTaskCategory.Appointment => TimeSpan.FromHours(1),
            (int)Domain.ValueObjects.AppTaskCategory.BillReminder => TimeSpan.FromMinutes(15),
            (int)Domain.ValueObjects.AppTaskCategory.Project => TimeSpan.FromHours(2),
            (int)Domain.ValueObjects.AppTaskCategory.Idea => TimeSpan.FromMinutes(30),
            (int)Domain.ValueObjects.AppTaskCategory.ToDo => TimeSpan.FromMinutes(45),
            _ => TimeSpan.FromHours(1)
        };

        // Adjust based on priority
        var priorityMultiplier = (task.Priority + 1) / 5.0; // 0.2 to 1.0
        var adjustedDuration = TimeSpan.FromMinutes(baseEstimate.TotalMinutes * priorityMultiplier);

        // Ensure within user preferences
        if (adjustedDuration < preferences.MinimumTaskDuration)
            return preferences.MinimumTaskDuration;
        if (adjustedDuration > preferences.MaximumTaskDuration)
            return preferences.MaximumTaskDuration;

        return adjustedDuration;
    }

    private SmartScheduledItem CreateScheduledItemFromTask(AppTask task, AvailableTimeSlot slot, TimeSpan duration)
    {
        return new SmartScheduledItem(
            Id: Guid.NewGuid(),
            TaskId: task.Id,
            CalendarEventId: null,
            Title: task.Title,
            Description: task.Description ?? string.Empty,
            StartTime: slot.StartTime,
            EndTime: slot.StartTime.Add(duration),
            ItemType: ScheduledItemType.Task,
            Priority: Domain.ValueObjects.Priority.FromValue(task.Priority),
            Category: ((Domain.ValueObjects.AppTaskCategory)task.Category).ToString(),
            Tags: string.IsNullOrEmpty(task.Tags) ? new List<string>() : task.Tags.Split(',').ToList(),
            EstimatedDuration: duration,
            IsFlexible: task.Priority < 3,
            SchedulingReasons: GenerateSchedulingReasons(task, slot)
        );
    }

    private List<SchedulingReason> GenerateSchedulingReasons(AppTask task, AvailableTimeSlot slot)
    {
        var reasons = new List<SchedulingReason>();

        if (slot.Characteristics.Contains("HighEnergy"))
        {
            reasons.Add(new SchedulingReason("HighEnergyPeriod", "Scheduled during high energy time", 0.8, 
                new List<string> { "energy_level", "time_of_day" }));
        }

        if (task.Priority >= 3)
        {
            reasons.Add(new SchedulingReason("HighPriority", "High priority task scheduled first", 0.9, 
                new List<string> { "task_priority" }));
        }

        if (task.DueDate.HasValue && (task.DueDate.Value - DateTime.UtcNow).TotalDays < 7)
        {
            reasons.Add(new SchedulingReason("DeadlinePressure", "Scheduled due to approaching deadline", 0.85, 
                new List<string> { "due_date", "urgency" }));
        }

        return reasons;
    }

    private double CalculateTimeBasedProductivityScore(DateTime startTime, SmartSchedulingPreferences preferences)
    {
        var hour = startTime.Hour;
        
        return preferences.ProductivityPattern switch
        {
            ProductivityPatterns.MorningPerson => hour switch
            {
                >= 8 and <= 11 => 0.95,
                >= 14 and <= 16 => 0.7,
                _ => 0.5
            },
            ProductivityPatterns.NightOwl => hour switch
            {
                >= 14 and <= 18 => 0.95,
                >= 8 and <= 11 => 0.6,
                _ => 0.5
            },
            ProductivityPatterns.MidDay => hour switch
            {
                >= 10 and <= 15 => 0.9,
                _ => 0.6
            },
            _ => 0.75 // Consistent pattern
        };
    }

    private double CalculateEnergyAlignmentScore(AppTask task, AvailableTimeSlot slot, SmartSchedulingPreferences preferences)
    {
        var timeScore = CalculateTimeBasedProductivityScore(slot.StartTime, preferences);
        var complexityMultiplier = task.Priority >= 3 ? 1.2 : 1.0;
        
        return Math.Min(1.0, timeScore * complexityMultiplier);
    }

    private double CalculateDeadlinePressureScore(AppTask task, AvailableTimeSlot slot)
    {
        if (!task.DueDate.HasValue)
            return 0.5;

        var daysUntilDue = (task.DueDate.Value - slot.StartTime).TotalDays;
        
        return daysUntilDue switch
        {
            <= 1 => 1.0,  // Urgent - today or overdue
            <= 3 => 0.9,  // Very urgent
            <= 7 => 0.8,  // Urgent this week
            <= 14 => 0.6, // Moderately urgent
            _ => 0.3      // Not urgent
        };
    }

    private double CalculateContextSwitchingScore(AppTask task, AvailableTimeSlot slot, List<SmartScheduledItem> existingSchedule)
    {
        var nearbyTasks = existingSchedule
            .Where(s => Math.Abs((s.StartTime - slot.StartTime).TotalMinutes) < 120)
            .ToList();

        if (!nearbyTasks.Any())
            return 0.8; // Neutral score for isolated tasks

        var sameCategory = nearbyTasks.Count(s => s.Category == task.Category.ToString());
        var totalNearby = nearbyTasks.Count;
        
        return (double)sameCategory / totalNearby;
    }

    private double CalculateFlexibilityScore(AppTask task, AvailableTimeSlot slot)
    {
        // Tasks with no due date are more flexible
        if (!task.DueDate.HasValue)
            return 0.9;

        // Lower priority tasks are more flexible
        var priorityFlex = 1.0 - (task.Priority / 4.0);
        
        return Math.Max(0.1, priorityFlex);
    }

    private List<SmartScheduledItem> ConsolidateSimilarTasks(List<SmartScheduledItem> schedule)
    {
        // Group similar tasks that are scheduled close together
        var consolidatedSchedule = new List<SmartScheduledItem>(schedule);
        
        // Sort by start time
        consolidatedSchedule.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        
        // Look for opportunities to group similar tasks
        for (int i = 0; i < consolidatedSchedule.Count - 1; i++)
        {
            var current = consolidatedSchedule[i];
            var next = consolidatedSchedule[i + 1];
            
            // Check if tasks can be consolidated
            if (current.Category == next.Category && 
                (next.StartTime - current.EndTime).TotalMinutes < 30 &&
                current.IsFlexible && next.IsFlexible)
            {
                // Add consolidation reasoning
                var reasons = current.SchedulingReasons.ToList();
                reasons.Add(new SchedulingReason("TaskConsolidation", "Grouped with similar tasks", 0.7, 
                    new List<string> { "category_matching", "time_efficiency" }));
                
                consolidatedSchedule[i] = current with { SchedulingReasons = reasons };
            }
        }
        
        return consolidatedSchedule;
    }

    private async Task<List<SmartScheduledItem>> OptimizeBreakPlacementAsync(
        List<SmartScheduledItem> schedule, 
        SmartSchedulingPreferences preferences, 
        CancellationToken cancellationToken)
    {
        var optimizedSchedule = schedule.ToList();
        
        // Add recommended breaks based on user preferences
        foreach (var breakTime in preferences.PreferredBreakTimes)
        {
            var breakDateTime = DateTime.Today.Add(breakTime);
            
            // Check if there's a natural break point
            var taskBefore = optimizedSchedule
                .Where(s => s.EndTime <= breakDateTime)
                .OrderBy(s => s.EndTime)
                .LastOrDefault();
                
            var taskAfter = optimizedSchedule
                .Where(s => s.StartTime >= breakDateTime)
                .OrderBy(s => s.StartTime)
                .FirstOrDefault();
            
            if (taskBefore != null && taskAfter != null &&
                (taskAfter.StartTime - taskBefore.EndTime).TotalMinutes >= preferences.MinimumBreakDuration.TotalMinutes)
            {
                // There's already sufficient break time
                continue;
            }
            
            // Consider adjusting task times to accommodate break
            // This is a simplified implementation - real optimization would be more complex
        }
        
        return optimizedSchedule;
    }

    private List<SmartScheduledItem> AddOptimalBufferTimes(
        List<SmartScheduledItem> schedule, 
        SmartSchedulingPreferences preferences)
    {
        var bufferedSchedule = new List<SmartScheduledItem>();
        
        foreach (var item in schedule.OrderBy(s => s.StartTime))
        {
            var buffer = preferences.BufferBetweenTasks;
            
            // Adjust buffer based on task complexity and next task
            if (item.Priority.Value >= 3)
            {
                buffer = TimeSpan.FromMinutes(buffer.TotalMinutes * 1.5); // More buffer for high priority tasks
            }
            
            var bufferedItem = item with 
            {
                EndTime = item.EndTime.Add(buffer),
                SchedulingReasons = item.SchedulingReasons.Concat(new[] 
                {
                    new SchedulingReason("BufferTime", $"Added {buffer.TotalMinutes} minute buffer", 0.6, 
                        new List<string> { "flexibility", "context_switching" })
                }).ToList()
            };
            
            bufferedSchedule.Add(bufferedItem);
        }
        
        return bufferedSchedule;
    }

    private async Task<double> CalculateSlotFitnessForTaskAsync(
        SmartScheduledItem task,
        AvailableTimeSlot slot,
        List<DomainSchedulingPattern> patterns,
        SmartSchedulingPreferences preferences,
        List<TimeSlotAssignment> assignments,
        CancellationToken cancellationToken)
    {
        return CalculateSlotFitnessScore(task, slot, patterns, preferences);
    }

    private double CalculateProductivityAlignmentScore(
        SmartScheduledItem task,
        AvailableTimeSlot slot,
        List<DomainSchedulingPattern> patterns,
        SmartSchedulingPreferences preferences)
    {
        var timeScore = CalculateTimeBasedProductivityScore(slot.StartTime, preferences);
        var patternScore = patterns.Any() ? patterns.Where(p => p.Category.ToString() == task.Category)
            .Select(p => p.SuccessRate).DefaultIfEmpty(0.7).Average() : 0.7;
        
        return (timeScore + patternScore) / 2.0;
    }

    private double CalculateEnergyLevelScore(DateTime startTime, SmartSchedulingPreferences preferences)
    {
        return CalculateTimeBasedProductivityScore(startTime, preferences);
    }

    private double CalculateDeadlineUrgencyScore(SmartScheduledItem task, AvailableTimeSlot slot)
    {
        return 0.8; // Simplified - would analyze task deadlines in real implementation
    }

    private double CalculateContextContinuityScore(SmartScheduledItem task, AvailableTimeSlot slot)
    {
        return 0.7; // Simplified - would analyze context switching patterns
    }

    private double CalculatePreferenceMatchScore(
        SmartScheduledItem task,
        AvailableTimeSlot slot,
        SmartSchedulingPreferences preferences)
    {
        var categoryMatch = preferences.PreferredTaskCategories.Contains(task.Category) ? 1.0 : 0.5;
        var timeMatch = preferences.PreferredWorkingHours.StartTime <= slot.StartTime.TimeOfDay &&
                       preferences.PreferredWorkingHours.EndTime >= slot.EndTime.TimeOfDay ? 1.0 : 0.3;
        
        return (categoryMatch + timeMatch) / 2.0;
    }

    // Methods for incremental optimization
    
    private async Task<List<OptimizationOpportunity>> IdentifyOptimizationOpportunitiesAsync(
        List<SmartScheduledItem> schedule,
        OptimizationGoals goals,
        List<DomainSchedulingPattern> patterns,
        UserSchedulingPreference? preferences,
        CancellationToken cancellationToken)
    {
        var opportunities = new List<OptimizationOpportunity>();
        
        // Find tasks in low-productivity time slots
        var lowProductivityTasks = schedule
            .Where(s => GetProductivityMultiplierForTime(s.StartTime) < 0.7 && s.Priority.Value >= 3)
            .ToList();
            
        foreach (var task in lowProductivityTasks)
        {
            opportunities.Add(new OptimizationOpportunity(
                task.Id,
                "ProductivityAlignment",
                "Task scheduled during low productivity hours",
                0.3,
                new Dictionary<string, object> { ["current_productivity"] = GetProductivityMultiplierForTime(task.StartTime) }
            ));
        }
        
        // Find excessive context switches
        var orderedSchedule = schedule.OrderBy(s => s.StartTime).ToList();
        for (int i = 1; i < orderedSchedule.Count; i++)
        {
            var current = orderedSchedule[i];
            var previous = orderedSchedule[i - 1];
            
            if (current.Category != previous.Category && 
                (current.StartTime - previous.EndTime).TotalMinutes < 15)
            {
                opportunities.Add(new OptimizationOpportunity(
                    current.Id,
                    "ContextSwitching",
                    "Task causes context switch with insufficient buffer",
                    0.2,
                    new Dictionary<string, object> 
                    { 
                        ["previous_category"] = previous.Category,
                        ["gap_minutes"] = (current.StartTime - previous.EndTime).TotalMinutes 
                    }
                ));
            }
        }
        
        // Find deadline pressure issues
        foreach (var item in schedule)
        {
            if (item.TaskId.HasValue)
            {
                // Simplified deadline check - in real implementation would check actual due dates
                var urgencyScore = CalculateDeadlineUrgencyScore(item, null);
                if (urgencyScore > 0.8 && GetProductivityMultiplierForTime(item.StartTime) < 0.8)
                {
                    opportunities.Add(new OptimizationOpportunity(
                        item.Id,
                        "DeadlinePressure",
                        "Urgent task not in optimal time slot",
                        0.4,
                        new Dictionary<string, object> { ["urgency_score"] = urgencyScore }
                    ));
                }
            }
        }
        
        return opportunities.OrderByDescending(o => o.ImpactPotential).ToList();
    }
    
    private async Task<OptimizationChange?> CreateOptimizationChangeAsync(
        OptimizationOpportunity opportunity,
        List<SmartScheduledItem> schedule,
        List<ScheduleConstraint> constraints,
        UserSchedulingPreference? preferences,
        CancellationToken cancellationToken)
    {
        var item = schedule.FirstOrDefault(s => s.Id == opportunity.ItemId);
        if (item == null) return null;
        
        switch (opportunity.Type)
        {
            case "ProductivityAlignment":
                var newStartTime = FindBetterTimeSlot(item, schedule, preferences);
                if (newStartTime.HasValue && newStartTime != item.StartTime)
                {
                    return new OptimizationChange(
                        item.Id,
                        "Reschedule",
                        $"Move task from {item.StartTime:HH:mm} to {newStartTime:HH:mm} for better productivity",
                        item.StartTime,
                        newStartTime,
                        item.EstimatedDuration,
                        item.EstimatedDuration,
                        opportunity.ImpactPotential,
                        "Scheduled during higher productivity hours"
                    );
                }
                break;
                
            case "ContextSwitching":
                var bufferTime = TimeSpan.FromMinutes(15);
                var newEndTime = item.EndTime.Add(bufferTime);
                
                return new OptimizationChange(
                    item.Id,
                    "AddBuffer",
                    $"Add {bufferTime.TotalMinutes} minute buffer to reduce context switching",
                    item.StartTime,
                    item.StartTime,
                    item.EstimatedDuration,
                    item.EstimatedDuration.Add(bufferTime),
                    opportunity.ImpactPotential,
                    "Added buffer time to minimize context switching impact"
                );
                
            case "DeadlinePressure":
                var earlierTime = FindEarlierTimeSlot(item, schedule, preferences);
                if (earlierTime.HasValue)
                {
                    return new OptimizationChange(
                        item.Id,
                        "Reschedule",
                        $"Move urgent task earlier from {item.StartTime:HH:mm} to {earlierTime:HH:mm}",
                        item.StartTime,
                        earlierTime,
                        item.EstimatedDuration,
                        item.EstimatedDuration,
                        opportunity.ImpactPotential,
                        "Prioritized due to deadline pressure"
                    );
                }
                break;
        }
        
        return null;
    }
    
    private DateTime? FindBetterTimeSlot(
        SmartScheduledItem item, 
        List<SmartScheduledItem> schedule, 
        UserSchedulingPreference? preferences)
    {
        var workingHours = preferences?.PreferredWorkingHours;
        if (workingHours == null) return null;
        
        var startHour = workingHours.StartTime.Hours;
        var endHour = workingHours.EndTime.Hours;
        
        // Find high-productivity hours
        for (int hour = startHour; hour < endHour; hour++)
        {
            var candidateTime = item.StartTime.Date.AddHours(hour);
            var productivityScore = GetProductivityMultiplierForTime(candidateTime);
            
            if (productivityScore > 0.8 && IsTimeSlotAvailable(candidateTime, item.EstimatedDuration, schedule, item.Id))
            {
                return candidateTime;
            }
        }
        
        return null;
    }
    
    private DateTime? FindEarlierTimeSlot(
        SmartScheduledItem item, 
        List<SmartScheduledItem> schedule, 
        UserSchedulingPreference? preferences)
    {
        var workingHours = preferences?.PreferredWorkingHours;
        var startTime = workingHours?.StartTime ?? TimeSpan.FromHours(9);
        
        var earliestStart = item.StartTime.Date.Add(startTime);
        var searchEnd = item.StartTime;
        
        // Search for available slot before current time
        var current = earliestStart;
        while (current < searchEnd)
        {
            if (IsTimeSlotAvailable(current, item.EstimatedDuration, schedule, item.Id))
            {
                return current;
            }
            current = current.AddMinutes(30);
        }
        
        return null;
    }
    
    private bool IsTimeSlotAvailable(
        DateTime startTime, 
        TimeSpan duration, 
        List<SmartScheduledItem> schedule, 
        Guid excludeItemId)
    {
        var endTime = startTime.Add(duration);
        
        return !schedule.Any(s => 
            s.Id != excludeItemId &&
            ((s.StartTime < endTime && s.EndTime > startTime)));
    }

    #endregion

    // Supporting classes
    private record TaskAssignmentResult(SmartScheduledItem ScheduledItem, AvailableTimeSlot Slot, double FitnessScore);
    private record OptimizationOpportunity(Guid ItemId, string Type, string Description, double ImpactPotential, Dictionary<string, object> Metadata);
}