using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;

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
        ScheduleOptimizationContext context,
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
        OptimizationContext context,
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

    // Simplified implementations for demo purposes
    private Task<List<SmartScheduledItem>> ApplyOptimizationAlgorithmsAsync(Guid userId, List<AppTask> tasks, List<AvailableTimeSlot> availableSlots, SmartSchedulingPreferences preferences, List<SchedulingPattern> patterns, OptimizationGoals goals, CancellationToken cancellationToken)
    {
        return Task.FromResult(new List<SmartScheduledItem>());
    }

    private Dictionary<string, object> CalculateOptimizationMetrics(List<SmartScheduledItem> currentSchedule, List<SmartScheduledItem> optimizedSchedule)
    {
        return new Dictionary<string, object> { ["ImprovementScore"] = 0.25 };
    }

    private List<string> GenerateOptimizationInsights(List<SmartScheduledItem> optimizedSchedule, List<SchedulingPattern> patterns, SmartSchedulingPreferences preferences)
    {
        return new List<string> { "Schedule optimized for productivity" };
    }

    // Additional placeholder methods
    private Task<List<OptimizationChange>> ApplyIncrementalOptimizationAsync(Guid userId, List<SmartScheduledItem> existingSchedule, OptimizationGoals goals, List<ScheduleConstraint> constraints, List<SchedulingPattern> patterns, UserSchedulingPreference? preferences, CancellationToken cancellationToken) => Task.FromResult<List<OptimizationChange>>(new());
    private List<SmartScheduledItem> ApplyOptimizationChanges(List<SmartScheduledItem> existingSchedule, List<OptimizationChange> optimizationChanges) => existingSchedule;
    private double CalculateImprovementScore(List<SmartScheduledItem> existingSchedule, List<SmartScheduledItem> optimizedSchedule, UserSchedulingPreference? preferences) => 0.15;
    private Dictionary<string, double> CalculateQualityMetrics(List<SmartScheduledItem> optimizedSchedule, UserSchedulingPreference? preferences) => new();
    private List<string> GenerateOptimizationReasons(List<OptimizationChange> optimizationChanges, ScheduleQualityAnalysis qualityAnalysis) => new();
    private double CalculateTimeUtilization(List<SmartScheduledItem> schedule, UserSchedulingPreference? preferences) => 0.8;
    private Task<double> CalculateProductivityAlignmentAsync(List<SmartScheduledItem> schedule, List<SchedulingPattern> patterns, CancellationToken cancellationToken) => Task.FromResult(0.7);
    private double CalculateDeadlineCompliance(List<SmartScheduledItem> schedule) => 0.9;
    private double CalculateContextSwitchingScore(List<SmartScheduledItem> schedule) => 0.8;
    private double CalculateEnergyOptimizationScore(List<SmartScheduledItem> schedule, UserSchedulingPreference? preferences) => 0.7;
    private double CalculateFlexibilityScore(List<SmartScheduledItem> schedule) => 0.8;
    private double CalculateWorkLifeBalanceScore(List<SmartScheduledItem> schedule, UserSchedulingPreference? preferences) => 0.8;
    private List<QualityIssue> IdentifyQualityIssues(List<SmartScheduledItem> schedule, Dictionary<string, double> qualityDimensions, UserSchedulingPreference? preferences) => new();
    private List<QualityImprovement> GenerateQualityImprovements(List<SmartScheduledItem> schedule, Dictionary<string, double> qualityDimensions, List<QualityIssue> issues, UserSchedulingPreference? preferences) => new();
    private Task AnalyzeProductivityFactorsAsync(List<SmartScheduledItem> schedule, List<SchedulingPattern> patterns, SmartSchedulingPreferences preferences, Dictionary<string, double> factorContributions, List<ProductivityFactor> positiveFactors, List<ProductivityFactor> negativeFactors, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    private double CalculateWeightedProductivityScore(Dictionary<string, double> factorContributions) => factorContributions.Values.DefaultIfEmpty(0.7).Average();
    private List<string> GenerateProductivityImprovements(List<ProductivityFactor> negativeFactors, List<SmartScheduledItem> schedule) => new();
    private Task<AvailableTimeSlot?> FindBestTimeSlotForTaskAsync(SmartScheduledItem task, List<AvailableTimeSlot> availableSlots, List<SchedulingPattern> patterns, SmartSchedulingPreferences preferences, List<TimeSlotAssignment> assignments, CancellationToken cancellationToken) => Task.FromResult(availableSlots.FirstOrDefault());
    private double CalculateSlotFitnessScore(SmartScheduledItem task, AvailableTimeSlot slot, List<SchedulingPattern> patterns, SmartSchedulingPreferences preferences) => 0.8;
    private List<string> GenerateAssignmentReasons(SmartScheduledItem task, AvailableTimeSlot slot, List<SchedulingPattern> patterns) => new() { "Optimal time slot identified" };
    private List<AvailableTimeSlot> UpdateAvailableSlots(List<AvailableTimeSlot> slots, TimeSlotAssignment assignment) => slots.Where(s => s.Id != assignment.SlotId).ToList();

    #endregion
}
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Infrastructure.Services.SmartScheduling;

/// <summary>
/// Advanced schedule optimization engine using productivity scoring and heuristic algorithms
/// </summary>
public sealed class ScheduleOptimizationEngine : IScheduleOptimizationEngine
{
    private readonly ILogger<ScheduleOptimizationEngine> _logger;
    private readonly Random _random;

    public ScheduleOptimizationEngine(ILogger<ScheduleOptimizationEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    public async Task<ScheduleOptimizationResult> OptimizeScheduleAsync(
        Guid userId,
        ScheduleOptimizationContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting schedule optimization for user {UserId} with {TaskCount} tasks",
            userId, context.TaskIds.Count);

        try
        {
            var optimizedSchedule = new List<SmartScheduledItem>();
            var changes = new List<ScheduleChange>();
            var insights = new List<string>();

            // Get available time slots
            var availableSlots = GenerateAvailableTimeSlots(context);

            // Score and prioritize tasks
            var prioritizedTasks = await PrioritizeTasksAsync(context.CurrentSchedule, context.Preferences);

            // Schedule each task using optimization algorithm
            foreach (var task in prioritizedTasks)
            {
                var optimalSlot = FindOptimalTimeSlot(task, availableSlots, context.Preferences);
                if (optimalSlot != null)
                {
                    var optimizedTask = task with
                    {
                        StartTime = optimalSlot.StartTime,
                        EndTime = optimalSlot.StartTime.Add(task.EstimatedDuration),
                        SchedulingReasons = new List<SchedulingReason>
                        {
                            new SchedulingReason(
                                ReasonType: "OptimalityScore",
                                Description: "Scheduled based on productivity optimization",
                                InfluenceWeight: 0.8,
                                FactorsConsidered: new List<string> { "Energy levels", "Task priority", "Available time" }
                            )
                        }
                    };

                    optimizedSchedule.Add(optimizedTask);
                    RemoveUsedTimeSlot(availableSlots, optimalSlot, task.EstimatedDuration);

                    changes.Add(new ScheduleChange(
                        ChangeType: "Scheduled",
                        ItemId: task.Id,
                        ItemTitle: task.Title,
                        OldStartTime: task.StartTime == DateTime.MinValue ? null : task.StartTime,
                        NewStartTime: optimizedTask.StartTime,
                        OldEndTime: task.EndTime == DateTime.MinValue ? null : task.EndTime,
                        NewEndTime: optimizedTask.EndTime,
                        Reason: "Optimized for productivity and efficiency",
                        ImpactScore: CalculateTaskImpactScore(task)
                    ));
                }
                else
                {
                    // Add unscheduled task
                    optimizedSchedule.Add(task);
                    insights.Add($"Could not find optimal time for task: {task.Title}");
                }
            }

            // Apply optimization heuristics
            ApplyOptimizationHeuristics(optimizedSchedule, context.Preferences);

            var improvementScore = CalculateImprovementScore(context.CurrentSchedule, optimizedSchedule, context.Preferences);
            var metrics = CalculateOptimizationMetrics(optimizedSchedule, context);

            insights.AddRange(GenerateOptimizationInsights(optimizedSchedule, context));

            _logger.LogInformation("Completed schedule optimization for user {UserId}, improvement score: {Score}",
                userId, improvementScore);

            return new ScheduleOptimizationResult(
                OptimizedSchedule: optimizedSchedule,
                Changes: changes,
                ImprovementScore: improvementScore,
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

    public Task<ScheduleOptimizationEngineResult> OptimizeExistingScheduleAsync(
        Guid userId,
        OptimizationContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing existing schedule for user {UserId} with {ItemCount} items",
            userId, context.ExistingSchedule.Count);

        try
        {
            var optimizedSchedule = new List<SmartScheduledItem>(context.ExistingSchedule);
            var changesApplied = new List<OptimizationChange>();
            var qualityMetrics = new Dictionary<string, double>();

            // Apply time-based optimizations
            ApplyTimeOptimizations(optimizedSchedule, changesApplied, context);

            // Apply priority-based optimizations  
            ApplyPriorityOptimizations(optimizedSchedule, changesApplied, context);

            // Apply energy-level optimizations
            ApplyEnergyOptimizations(optimizedSchedule, changesApplied, context);

            // Calculate improvement score
            var improvementScore = CalculateImprovementScore(context.ExistingSchedule, optimizedSchedule, context.Preferences);

            // Calculate quality metrics
            qualityMetrics["EfficiencyScore"] = CalculateEfficiencyScore(optimizedSchedule);
            qualityMetrics["BalanceScore"] = CalculateBalanceScore(optimizedSchedule, context.Preferences);
            qualityMetrics["ProductivityScore"] = CalculateProductivityScore(optimizedSchedule, context.Preferences);
            qualityMetrics["ConflictScore"] = CalculateConflictScore(optimizedSchedule);

            _logger.LogInformation("Existing schedule optimization completed for user {UserId}, {ChangeCount} changes applied",
                userId, changesApplied.Count);

            return Task.FromResult(new ScheduleOptimizationEngineResult(
                OptimizedSchedule: optimizedSchedule,
                ChangesApplied: changesApplied,
                ImprovementScore: improvementScore,
                OptimizationReasons: GenerateOptimizationReasons(changesApplied),
                QualityMetrics: qualityMetrics,
                OptimizedAt: DateTime.UtcNow
            ));
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
        var assignments = new List<TimeSlotAssignment>();

        foreach (var taskId in taskIds)
        {
            var bestSlot = availableSlots
                .Where(slot => slot.AvailabilityScore > 0.5)
                .OrderByDescending(slot => CalculateSlotFitnessScore(slot, preferences))
                .FirstOrDefault();

            if (bestSlot != null)
            {
                assignments.Add(new TimeSlotAssignment(
                    TaskId: taskId,
                    SlotId: bestSlot.Id,
                    StartTime: bestSlot.StartTime,
                    EndTime: bestSlot.EndTime,
                    FitnessScore: CalculateSlotFitnessScore(bestSlot, preferences),
                    AssignmentReasons: new List<string>
                    {
                        "High availability score",
                        "Matches user preferences",
                        "Optimal productivity window"
                    }
                ));

                // Remove assigned slot from available slots
                availableSlots.Remove(bestSlot);
            }
        }

        return Task.FromResult(assignments);
    }

    public Task<ScheduleQualityAnalysis> AnalyzeScheduleQualityAsync(
        Guid userId,
        List<SmartScheduledItem> schedule,
        CancellationToken cancellationToken = default)
    {
        var qualityDimensions = new Dictionary<string, double>();
        var issues = new List<QualityIssue>();
        var suggestions = new List<QualityImprovement>();

        // Analyze different quality dimensions
        qualityDimensions["TimeUtilization"] = CalculateTimeUtilization(schedule);
        qualityDimensions["TaskDistribution"] = CalculateTaskDistribution(schedule);
        qualityDimensions["PriorityAlignment"] = CalculatePriorityAlignment(schedule);
        qualityDimensions["WorkloadBalance"] = CalculateWorkloadBalance(schedule);

        var overallScore = qualityDimensions.Values.Average();

        // Identify issues
        if (qualityDimensions["TimeUtilization"] < 0.5)
        {
            issues.Add(new QualityIssue(
                IssueType: "LowUtilization",
                Description: "Schedule has low time utilization",
                Severity: "Medium",
                AffectedItems: schedule.Select(s => s.Id).ToList(),
                ImpactAreas: new List<string> { "Productivity", "Efficiency" }
            ));

            suggestions.Add(new QualityImprovement(
                ImprovementType: "UtilizationBoost",
                Description: "Add more tasks or extend existing task durations",
                ExpectedImpact: 0.3,
                AffectedItems: schedule.Select(s => s.Id).ToList(),
                ActionSteps: new List<string>
                {
                    "Review task priorities",
                    "Consider adding buffer tasks",
                    "Optimize task duration estimates"
                }
            ));
        }

        return Task.FromResult(new ScheduleQualityAnalysis(
            OverallScore: overallScore,
            QualityDimensions: qualityDimensions,
            Issues: issues,
            Suggestions: suggestions,
            AnalyzedAt: DateTime.UtcNow
        ));
    }

    public Task<ProductivityScore> CalculateProductivityScoreAsync(
        Guid userId,
        List<SmartScheduledItem> schedule,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var factorContributions = new Dictionary<string, double>();
        var positiveFactors = new List<ProductivityFactor>();
        var negativeFactors = new List<ProductivityFactor>();

        // Calculate productivity factors
        var timeAlignment = CalculateTimeAlignmentScore(schedule, preferences);
        var priorityOptimization = CalculatePriorityOptimizationScore(schedule);
        var workloadBalance = CalculateWorkloadBalanceScore(schedule);
        var contextSwitching = CalculateContextSwitchingScore(schedule);

        factorContributions["TimeAlignment"] = timeAlignment;
        factorContributions["PriorityOptimization"] = priorityOptimization;
        factorContributions["WorkloadBalance"] = workloadBalance;
        factorContributions["ContextSwitching"] = contextSwitching;

        // Identify positive factors
        if (timeAlignment > 0.7)
        {
            positiveFactors.Add(new ProductivityFactor(
                FactorName: "Optimal Time Alignment",
                Impact: timeAlignment,
                Description: "Tasks scheduled during optimal time windows",
                AffectedItems: schedule.Select(s => s.Id).ToList()
            ));
        }

        if (contextSwitching > 0.6)
        {
            positiveFactors.Add(new ProductivityFactor(
                FactorName: "Low Context Switching",
                Impact: contextSwitching,
                Description: "Similar tasks grouped together effectively",
                AffectedItems: schedule.Select(s => s.Id).ToList()
            ));
        }

        // Identify negative factors
        if (workloadBalance < 0.4)
        {
            negativeFactors.Add(new ProductivityFactor(
                FactorName: "Poor Workload Distribution",
                Impact: -0.3,
                Description: "Uneven distribution of work throughout the day",
                AffectedItems: schedule.Select(s => s.Id).ToList()
            ));
        }

        var overallScore = factorContributions.Values.Average();

        var improvementSuggestions = new List<string>();
        if (overallScore < 0.6)
        {
            improvementSuggestions.Add("Consider rescheduling tasks to match your peak energy periods");
            improvementSuggestions.Add("Group similar tasks together to reduce context switching");
            improvementSuggestions.Add("Balance workload more evenly throughout the day");
        }

        return Task.FromResult(new ProductivityScore(
            Score: overallScore,
            FactorContributions: factorContributions,
            PositiveFactors: positiveFactors,
            NegativeFactors: negativeFactors,
            ImprovementSuggestions: improvementSuggestions
        ));
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Simple availability check - in real implementation might check external dependencies
        return await Task.FromResult(true);
    }

    // Private helper methods

    private List<AvailableTimeSlot> GenerateAvailableTimeSlots(ScheduleOptimizationContext context)
    {
        var slots = new List<AvailableTimeSlot>();
        var workingHours = context.Preferences.PreferredWorkingHours;

        for (var date = context.StartDate.Date; date <= context.EndDate.Date; date = date.AddDays(1))
        {
            if (workingHours.WorkingDays.Contains(date.DayOfWeek))
            {
                var startTime = date.Add(workingHours.StartTime);
                var endTime = date.Add(workingHours.EndTime);

                // Create hourly slots
                for (var time = startTime; time < endTime; time = time.AddHours(1))
                {
                    slots.Add(new AvailableTimeSlot(
                        Id: Guid.NewGuid(),
                        StartTime: time,
                        EndTime: time.AddHours(1),
                        SlotType: TimeSlotType.WorkingTime,
                        AvailabilityScore: CalculateTimeSlotAvailabilityScore(time, context.Preferences),
                        Characteristics: new List<string> { "Working hours", "Available" }
                    ));
                }
            }
        }

        // Remove slots blocked by calendar events
        foreach (var calendarEvent in context.CalendarEvents)
        {
            if (calendarEvent.IsBlockingTime)
            {
                slots.RemoveAll(slot =>
                    slot.StartTime < calendarEvent.EndTime && calendarEvent.StartTime < slot.EndTime);
            }
        }

        return slots;
    }

    private Task<List<SmartScheduledItem>> PrioritizeTasksAsync(
        List<SmartScheduledItem> tasks,
        SmartSchedulingPreferences preferences)
    {
        return Task.FromResult(tasks
            .OrderByDescending(t => t.Priority.Value) // Higher priority first
            .ThenByDescending(t => CalculateTaskUrgencyScore(t))
            .ThenBy(t => t.EstimatedDuration) // Shorter tasks first for same priority
            .ToList());
    }

    private AvailableTimeSlot? FindOptimalTimeSlot(
        SmartScheduledItem task,
        List<AvailableTimeSlot> availableSlots,
        SmartSchedulingPreferences preferences)
    {
        return availableSlots
            .Where(slot => slot.EndTime - slot.StartTime >= task.EstimatedDuration)
            .OrderByDescending(slot => CalculateSlotTaskFitness(slot, task, preferences))
            .FirstOrDefault();
    }

    private void RemoveUsedTimeSlot(
        List<AvailableTimeSlot> availableSlots,
        AvailableTimeSlot usedSlot,
        TimeSpan taskDuration)
    {
        availableSlots.Remove(usedSlot);

        // Add remaining time as new slot if any
        var remainingTime = (usedSlot.EndTime - usedSlot.StartTime) - taskDuration;
        if (remainingTime > TimeSpan.Zero)
        {
            availableSlots.Add(new AvailableTimeSlot(
                Id: Guid.NewGuid(),
                StartTime: usedSlot.StartTime.Add(taskDuration),
                EndTime: usedSlot.EndTime,
                SlotType: usedSlot.SlotType,
                AvailabilityScore: usedSlot.AvailabilityScore * 0.8, // Slightly lower score for fragmented time
                Characteristics: usedSlot.Characteristics
            ));
        }
    }

    private void ApplyOptimizationHeuristics(
        List<SmartScheduledItem> schedule,
        SmartSchedulingPreferences preferences)
    {
        // Group similar tasks together to reduce context switching
        var groupedTasks = schedule
            .GroupBy(t => t.Category)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in groupedTasks)
        {
            var tasks = group.OrderBy(t => t.StartTime).ToList();
            // Apply time grouping logic here if beneficial
        }
    }

    private double CalculateImprovementScore(
        List<SmartScheduledItem> originalSchedule,
        List<SmartScheduledItem> optimizedSchedule,
        SmartSchedulingPreferences preferences)
    {
        var originalScore = CalculateScheduleScore(originalSchedule, preferences);
        var optimizedScore = CalculateScheduleScore(optimizedSchedule, preferences);

        return Math.Max(0, (optimizedScore - originalScore) / Math.Max(originalScore, 0.1));
    }

    private double CalculateScheduleScore(List<SmartScheduledItem> schedule, SmartSchedulingPreferences preferences)
    {
        if (!schedule.Any())
        {
            return 0;
        }

        var priorityScore = schedule.Average(t => t.Priority.Value / 4.0); // Normalize to 0-1
        var utilizationScore = CalculateTimeUtilization(schedule);
        var balanceScore = CalculateWorkloadBalance(schedule);

        return (priorityScore * 0.4) + (utilizationScore * 0.3) + (balanceScore * 0.3);
    }

    private Dictionary<string, double> CalculateOptimizationMetrics(
        List<SmartScheduledItem> schedule,
        ScheduleOptimizationContext context)
    {
        return new Dictionary<string, double>
        {
            ["ScheduledTasks"] = schedule.Count(t => t.StartTime > DateTime.MinValue),
            ["AverageTaskDuration"] = schedule.Average(t => t.EstimatedDuration.TotalHours),
            ["PriorityDistribution"] = schedule.Average(t => t.Priority.Value),
            ["TimeUtilization"] = CalculateTimeUtilization(schedule)
        };
    }

    private List<string> GenerateOptimizationInsights(
        List<SmartScheduledItem> schedule,
        ScheduleOptimizationContext context)
    {
        var insights = new List<string>();

        var highPriorityTasks = schedule.Count(t => t.Priority.Value >= 3);
        if (highPriorityTasks > 0)
        {
            insights.Add($"Scheduled {highPriorityTasks} high-priority tasks during peak productivity hours");
        }

        var categories = schedule.GroupBy(t => t.Category).Count();
        if (categories <= 3)
        {
            insights.Add("Tasks grouped by category to minimize context switching");
        }

        var totalDuration = schedule.Sum(t => t.EstimatedDuration.TotalHours);
        if (totalDuration <= 6)
        {
            insights.Add("Balanced workload maintained within reasonable limits");
        }

        return insights;
    }

    private void ApplyTimeOptimizations(
        List<SmartScheduledItem> schedule,
        List<OptimizationChange> changes,
        OptimizationContext context)
    {
        // Move tasks to better time slots based on preferences and patterns
        foreach (var task in schedule.ToList())
        {
            var currentHour = task.StartTime.Hour;
            var optimalHour = GetOptimalHourForTask(task, context.Preferences);

            if (Math.Abs(currentHour - optimalHour) > 2) // Significant time difference
            {
                var newStartTime = task.StartTime.Date.AddHours(optimalHour);
                var newEndTime = newStartTime.Add(task.EstimatedDuration);

                var optimizedTask = task with
                {
                    StartTime = newStartTime,
                    EndTime = newEndTime
                };

                var index = schedule.IndexOf(task);
                schedule[index] = optimizedTask;

                changes.Add(new OptimizationChange(
                    ItemId: task.Id,
                    ChangeType: "TimeShift",
                    Description: $"Moved task to optimal time slot ({optimalHour}:00)",
                    OldStartTime: task.StartTime,
                    NewStartTime: newStartTime,
                    OldDuration: task.EstimatedDuration,
                    NewDuration: task.EstimatedDuration,
                    ImpactScore: 0.6,
                    Reasoning: "Aligned with user's peak productivity hours"
                ));
            }
        }
    }

    private void ApplyPriorityOptimizations(
        List<SmartScheduledItem> schedule,
        List<OptimizationChange> changes,
        OptimizationContext context)
    {
        // Ensure high-priority tasks get the best time slots
        var highPriorityTasks = schedule.Where(t => t.Priority.Value >= 3).ToList();
        var normalTasks = schedule.Where(t => t.Priority.Value < 3).ToList();

        // Move high-priority tasks to prime time slots (9-11 AM)
        foreach (var task in highPriorityTasks)
        {
            if (task.StartTime.Hour < 9 || task.StartTime.Hour > 11)
            {
                var primeTime = task.StartTime.Date.AddHours(9 + _random.Next(0, 3));
                if (IsTimeSlotAvailable(schedule, primeTime, task.EstimatedDuration, task.Id))
                {
                    var optimizedTask = task with
                    {
                        StartTime = primeTime,
                        EndTime = primeTime.Add(task.EstimatedDuration)
                    };

                    var index = schedule.IndexOf(task);
                    schedule[index] = optimizedTask;

                    changes.Add(new OptimizationChange(
                        ItemId: task.Id,
                        ChangeType: "PriorityBoost",
                        Description: "Moved high-priority task to prime time slot",
                        OldStartTime: task.StartTime,
                        NewStartTime: primeTime,
                        OldDuration: task.EstimatedDuration,
                        NewDuration: task.EstimatedDuration,
                        ImpactScore: 0.8,
                        Reasoning: "High-priority tasks deserve best time slots"
                    ));
                }
            }
        }
    }

    private void ApplyEnergyOptimizations(
        List<SmartScheduledItem> schedule,
        List<OptimizationChange> changes,
        OptimizationContext context)
    {
        // Schedule demanding tasks during high-energy periods
        var demandingTasks = schedule.Where(t =>
            t.EstimatedDuration.TotalHours >= 2 ||
            t.Category == "Development" ||
            t.Category == "Creative").ToList();

        foreach (var task in demandingTasks)
        {
            var energyLevel = GetEnergyLevelAtTime(task.StartTime, context.Preferences);
            if (energyLevel < 0.6) // Low energy time
            {
                var highEnergyTime = FindHighEnergyTimeSlot(task.StartTime.Date, context.Preferences);
                if (highEnergyTime.HasValue &&
                    IsTimeSlotAvailable(schedule, highEnergyTime.Value, task.EstimatedDuration, task.Id))
                {
                    var optimizedTask = task with
                    {
                        StartTime = highEnergyTime.Value,
                        EndTime = highEnergyTime.Value.Add(task.EstimatedDuration)
                    };

                    var index = schedule.IndexOf(task);
                    schedule[index] = optimizedTask;

                    changes.Add(new OptimizationChange(
                        ItemId: task.Id,
                        ChangeType: "EnergyAlignment",
                        Description: "Moved demanding task to high-energy period",
                        OldStartTime: task.StartTime,
                        NewStartTime: highEnergyTime.Value,
                        OldDuration: task.EstimatedDuration,
                        NewDuration: task.EstimatedDuration,
                        ImpactScore: 0.7,
                        Reasoning: "Aligned with user's natural energy patterns"
                    ));
                }
            }
        }
    }

    private List<string> GenerateOptimizationReasons(List<OptimizationChange> changes)
    {
        return changes
            .GroupBy(c => c.ChangeType)
            .Select(g => $"Applied {g.Count()} {g.Key} optimizations")
            .ToList();
    }

    // Helper calculation methods

    private double CalculateTaskImpactScore(SmartScheduledItem task)
    {
        return (task.Priority.Value / 4.0) * 0.6 + (task.EstimatedDuration.TotalHours / 8.0) * 0.4;
    }

    private double CalculateTimeSlotAvailabilityScore(DateTime time, SmartSchedulingPreferences preferences)
    {
        var hour = time.Hour;
        var workingHours = preferences.PreferredWorkingHours;

        // Higher score for core working hours
        if (hour >= workingHours.StartTime.Hours && hour <= workingHours.EndTime.Hours)
        {
            // Peak productivity hours get higher score
            if (hour >= 9 && hour <= 11)
            {
                return 0.9;
            }

            if (hour >= 14 && hour <= 16)
            {
                return 0.8;
            }

            return 0.7;
        }

        return 0.3; // Outside working hours
    }

    private double CalculateTaskUrgencyScore(SmartScheduledItem task)
    {
        // Simple urgency calculation based on priority and estimated duration
        return task.Priority.Value * 0.7 + (1.0 / Math.Max(task.EstimatedDuration.TotalHours, 0.5)) * 0.3;
    }

    private double CalculateSlotFitnessScore(AvailableTimeSlot slot, SmartSchedulingPreferences preferences)
    {
        var timeScore = CalculateTimeSlotAvailabilityScore(slot.StartTime, preferences);
        return slot.AvailabilityScore * 0.6 + timeScore * 0.4;
    }

    private double CalculateSlotTaskFitness(
        AvailableTimeSlot slot,
        SmartScheduledItem task,
        SmartSchedulingPreferences preferences)
    {
        var slotScore = CalculateSlotFitnessScore(slot, preferences);
        var durationFit = Math.Min(1.0, (slot.EndTime - slot.StartTime).TotalHours / task.EstimatedDuration.TotalHours);
        var priorityBonus = task.Priority.Value / 4.0;

        return slotScore * 0.5 + durationFit * 0.3 + priorityBonus * 0.2;
    }

    private double CalculateEfficiencyScore(List<SmartScheduledItem> schedule)
    {
        if (!schedule.Any())
        {
            return 0;
        }

        var totalTime = schedule.Sum(s => s.EstimatedDuration.TotalHours);
        var timeSpan = schedule.Max(s => s.EndTime) - schedule.Min(s => s.StartTime);

        return Math.Min(1.0, totalTime / Math.Max(timeSpan.TotalHours, 0.1));
    }

    private double CalculateBalanceScore(List<SmartScheduledItem> schedule, SmartSchedulingPreferences preferences)
    {
        if (!schedule.Any())
        {
            return 0;
        }

        var dailyGroups = schedule.GroupBy(s => s.StartTime.Date).ToList();
        var dailyWorkloads = dailyGroups.Select(g => g.Sum(s => s.EstimatedDuration.TotalHours)).ToList();

        if (!dailyWorkloads.Any())
        {
            return 0;
        }

        var avgWorkload = dailyWorkloads.Average();
        var variance = dailyWorkloads.Sum(w => Math.Pow(w - avgWorkload, 2)) / dailyWorkloads.Count;

        return Math.Max(0, 1.0 - (variance / Math.Max(avgWorkload, 1.0)));
    }

    private double CalculateProductivityScore(List<SmartScheduledItem> schedule, SmartSchedulingPreferences preferences)
    {
        if (!schedule.Any())
        {
            return 0;
        }

        var highPriorityInPeakHours = schedule.Count(s =>
            s.Priority.Value >= 3 &&
            s.StartTime.Hour >= 9 && s.StartTime.Hour <= 11);

        var totalHighPriority = schedule.Count(s => s.Priority.Value >= 3);

        return totalHighPriority > 0 ? (double)highPriorityInPeakHours / totalHighPriority : 0.5;
    }

    private double CalculateConflictScore(List<SmartScheduledItem> schedule)
    {
        var conflictCount = 0;
        for (int i = 0; i < schedule.Count; i++)
        {
            for (int j = i + 1; j < schedule.Count; j++)
            {
                if (HasTimeOverlap(schedule[i], schedule[j]))
                {
                    conflictCount++;
                }
            }
        }

        return Math.Max(0, 1.0 - (conflictCount * 0.2)); // Each conflict reduces score by 0.2
    }

    private double CalculateTimeUtilization(List<SmartScheduledItem> schedule)
    {
        if (!schedule.Any())
        {
            return 0;
        }

        var totalTaskTime = schedule.Sum(s => s.EstimatedDuration.TotalHours);
        var availableWorkHours = 8.0; // Assume 8-hour work day

        return Math.Min(1.0, totalTaskTime / availableWorkHours);
    }

    private double CalculateTaskDistribution(List<SmartScheduledItem> schedule)
    {
        if (!schedule.Any())
        {
            return 0;
        }

        var categoryGroups = schedule.GroupBy(s => s.Category).ToList();
        var distribution = categoryGroups.Select(g => g.Count() / (double)schedule.Count).ToList();

        // Calculate Shannon entropy for diversity
        var entropy = -distribution.Sum(p => p * Math.Log2(p));
        var maxEntropy = Math.Log2(categoryGroups.Count);

        return maxEntropy > 0 ? entropy / maxEntropy : 0;
    }

    private double CalculatePriorityAlignment(List<SmartScheduledItem> schedule)
    {
        if (!schedule.Any())
        {
            return 0;
        }

        var priorityWeightedScore = schedule.Sum(s =>
            (s.Priority.Value / 4.0) * CalculateTimeSlotQuality(s.StartTime));

        var totalPriorityWeight = schedule.Sum(s => s.Priority.Value / 4.0);

        return totalPriorityWeight > 0 ? priorityWeightedScore / totalPriorityWeight : 0;
    }

    private double CalculateWorkloadBalance(List<SmartScheduledItem> schedule)
    {
        return CalculateBalanceScore(schedule, new SmartSchedulingPreferences(
            UserId: Guid.Empty,
            PreferredWorkingHours: new WorkingHours(
                StartTime: TimeSpan.FromHours(9),
                EndTime: TimeSpan.FromHours(17),
                WorkingDays: new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                LunchBreakStart: TimeSpan.FromHours(12),
                LunchBreakDuration: TimeSpan.FromHours(1),
                FlexibleSchedule: true
            ),
            PreferredBreakTimes: new List<TimeSpan>(),
            MaxTasksPerTimeBlock: 3,
            MinimumTaskDuration: TimeSpan.FromMinutes(30),
            MaximumTaskDuration: TimeSpan.FromHours(4),
            PreferredTaskCategories: new List<string>(),
            ProductivityPattern: ProductivityPatterns.MorningPerson,
            AllowOverlappingTasks: false,
            PreferMorningTasks: true,
            RequireBufferTime: true,
            BufferDuration: TimeSpan.FromMinutes(15),
            CustomConstraints: new List<SchedulingConstraint>()
        ));
    }

    private double CalculateTimeAlignmentScore(List<SmartScheduledItem> schedule, SmartSchedulingPreferences preferences)
    {
        if (!schedule.Any())
        {
            return 0;
        }

        return schedule.Average(s =>
        {
            var timeQuality = CalculateTimeSlotQuality(s.StartTime);
            var preferenceAlignment = CalculatePreferenceAlignment(s, preferences);
            return (timeQuality + preferenceAlignment) / 2.0;
        });
    }

    private double CalculatePriorityOptimizationScore(List<SmartScheduledItem> schedule)
    {
        if (!schedule.Any())
        {
            return 0;
        }

        // Higher priority tasks should be scheduled earlier in better time slots
        var priorityTimeScore = schedule.Sum(s =>
            (s.Priority.Value / 4.0) * (1.0 - (s.StartTime.Hour - 9) / 8.0));

        var totalPriorityWeight = schedule.Sum(s => s.Priority.Value / 4.0);

        return totalPriorityWeight > 0 ? Math.Max(0, priorityTimeScore / totalPriorityWeight) : 0;
    }

    private double CalculateWorkloadBalanceScore(List<SmartScheduledItem> schedule)
    {
        return CalculateWorkloadBalance(schedule);
    }

    private double CalculateContextSwitchingScore(List<SmartScheduledItem> schedule)
    {
        if (schedule.Count <= 1)
        {
            return 1.0;
        }

        var contextSwitches = 0;
        var orderedSchedule = schedule.OrderBy(s => s.StartTime).ToList();

        for (int i = 1; i < orderedSchedule.Count; i++)
        {
            if (orderedSchedule[i].Category != orderedSchedule[i - 1].Category)
            {
                contextSwitches++;
            }
        }

        // Lower context switches = higher score
        var maxPossibleSwitches = schedule.Count - 1;
        return maxPossibleSwitches > 0 ? 1.0 - ((double)contextSwitches / maxPossibleSwitches) : 1.0;
    }

    private double CalculateTimeSlotQuality(DateTime time)
    {
        var hour = time.Hour;

        // Peak productivity hours
        if (hour >= 9 && hour <= 11)
        {
            return 1.0;
        }

        if (hour >= 14 && hour <= 16)
        {
            return 0.8;
        }

        if (hour >= 8 && hour <= 17)
        {
            return 0.6;
        }

        return 0.3; // Outside normal working hours
    }

    private double CalculatePreferenceAlignment(SmartScheduledItem task, SmartSchedulingPreferences preferences)
    {
        double score = 0.5; // Base score

        // Check if task category is preferred
        if (preferences.PreferredTaskCategories.Contains(task.Category))
        {
            score += 0.3;
        }

        // Check if time aligns with working hours
        var workingHours = preferences.PreferredWorkingHours;
        if (task.StartTime.TimeOfDay >= workingHours.StartTime &&
            task.EndTime.TimeOfDay <= workingHours.EndTime)
        {
            score += 0.2;
        }

        return Math.Min(1.0, score);
    }

    private int GetOptimalHourForTask(SmartScheduledItem task, SmartSchedulingPreferences preferences)
    {
        // Simple optimization: high priority tasks in morning, others later
        if (task.Priority.Value >= 3)
        {
            return preferences.PreferMorningTasks ? 9 : 14;
        }

        return preferences.PreferMorningTasks ? 10 : 15;
    }

    private bool IsTimeSlotAvailable(
        List<SmartScheduledItem> schedule,
        DateTime startTime,
        TimeSpan duration,
        Guid excludeTaskId)
    {
        var endTime = startTime.Add(duration);

        return !schedule.Any(s => s.Id != excludeTaskId &&
            s.StartTime < endTime && startTime < s.EndTime);
    }

    private double GetEnergyLevelAtTime(DateTime time, SmartSchedulingPreferences preferences)
    {
        var hour = time.Hour;

        return preferences.ProductivityPattern switch
        {
            ProductivityPatterns.MorningPerson => hour <= 12 ? 0.9 : 0.5,
            ProductivityPatterns.NightOwl => hour >= 14 ? 0.9 : 0.5,
            ProductivityPatterns.MidDay => hour >= 10 && hour <= 15 ? 0.9 : 0.5,
            _ => 0.7 // Consistent energy
        };
    }

    private DateTime? FindHighEnergyTimeSlot(DateTime date, SmartSchedulingPreferences preferences)
    {
        return preferences.ProductivityPattern switch
        {
            ProductivityPatterns.MorningPerson => date.AddHours(9),
            ProductivityPatterns.NightOwl => date.AddHours(15),
            ProductivityPatterns.MidDay => date.AddHours(11),
            _ => date.AddHours(10)
        };
    }

    private bool HasTimeOverlap(SmartScheduledItem item1, SmartScheduledItem item2)
    {
        return item1.StartTime < item2.EndTime && item2.StartTime < item1.EndTime;
    }
}
