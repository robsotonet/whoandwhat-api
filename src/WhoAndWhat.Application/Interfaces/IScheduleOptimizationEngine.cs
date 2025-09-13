using WhoAndWhat.Application.DTOs.SmartScheduling;

namespace WhoAndWhat.Application.Interfaces;

/// <summary>
/// Engine for optimizing task schedules using advanced algorithms and AI insights
/// </summary>
public interface IScheduleOptimizationEngine
{
    /// <summary>
    /// Optimize a schedule based on tasks, calendar events, and user preferences
    /// </summary>
    /// <param name="userId">User identifier for personalization</param>
    /// <param name="context">Optimization context with tasks, calendar, and preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimized schedule with improved task placement</returns>
    Task<ScheduleOptimizationResult> OptimizeScheduleAsync(
        Guid userId, 
        ScheduleOptimizationContext context, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimize an existing schedule to improve efficiency and productivity
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="context">Optimization context with current schedule and goals</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimization result with improved schedule</returns>
    Task<ScheduleOptimizationEngineResult> OptimizeExistingScheduleAsync(
        Guid userId, 
        OptimizationContext context, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find optimal time slots for specific tasks
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="taskIds">Tasks to find slots for</param>
    /// <param name="availableSlots">Available time slots</param>
    /// <param name="preferences">User scheduling preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimal time slot assignments</returns>
    Task<List<TimeSlotAssignment>> FindOptimalTimeSlotsAsync(
        Guid userId,
        List<Guid> taskIds,
        List<AvailableTimeSlot> availableSlots,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze schedule quality and suggest improvements
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="schedule">Schedule to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Quality analysis with improvement suggestions</returns>
    Task<ScheduleQualityAnalysis> AnalyzeScheduleQualityAsync(
        Guid userId,
        List<SmartScheduledItem> schedule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate productivity score for a given schedule
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="schedule">Schedule to score</param>
    /// <param name="preferences">User preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Productivity score and contributing factors</returns>
    Task<ProductivityScore> CalculateProductivityScoreAsync(
        Guid userId,
        List<SmartScheduledItem> schedule,
        SmartSchedulingPreferences preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the optimization engine is available and properly configured
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if engine is ready for optimization operations</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from schedule optimization engine operations
/// </summary>
public sealed record ScheduleOptimizationEngineResult(
    List<SmartScheduledItem> OptimizedSchedule,
    List<OptimizationChange> ChangesApplied,
    double ImprovementScore,
    List<string> OptimizationReasons,
    Dictionary<string, double> QualityMetrics,
    DateTime OptimizedAt
);

/// <summary>
/// An optimization change applied to the schedule
/// </summary>
public sealed record OptimizationChange(
    Guid ItemId,
    string ChangeType,
    string Description,
    DateTime? OldStartTime,
    DateTime? NewStartTime,
    TimeSpan? OldDuration,
    TimeSpan? NewDuration,
    double ImpactScore,
    string Reasoning
);

/// <summary>
/// Time slot assignment for a task
/// </summary>
public sealed record TimeSlotAssignment(
    Guid TaskId,
    Guid SlotId,
    DateTime StartTime,
    DateTime EndTime,
    double FitnessScore,
    List<string> AssignmentReasons
);

/// <summary>
/// Available time slot for scheduling
/// </summary>
public sealed record AvailableTimeSlot(
    Guid Id,
    DateTime StartTime,
    DateTime EndTime,
    TimeSlotType SlotType,
    double AvailabilityScore,
    List<string> Characteristics
);

/// <summary>
/// Schedule quality analysis result
/// </summary>
public sealed record ScheduleQualityAnalysis(
    double OverallScore,
    Dictionary<string, double> QualityDimensions,
    List<QualityIssue> Issues,
    List<QualityImprovement> Suggestions,
    DateTime AnalyzedAt
);

/// <summary>
/// Quality issue in the schedule
/// </summary>
public sealed record QualityIssue(
    string IssueType,
    string Description,
    string Severity,
    List<Guid> AffectedItems,
    List<string> ImpactAreas
);

/// <summary>
/// Quality improvement suggestion
/// </summary>
public sealed record QualityImprovement(
    string ImprovementType,
    string Description,
    double ExpectedImpact,
    List<Guid> AffectedItems,
    List<string> ActionSteps
);

/// <summary>
/// Productivity score for a schedule
/// </summary>
public sealed record ProductivityScore(
    double Score,
    Dictionary<string, double> FactorContributions,
    List<ProductivityFactor> PositiveFactors,
    List<ProductivityFactor> NegativeFactors,
    List<string> ImprovementSuggestions
);

/// <summary>
/// Factor contributing to productivity score
/// </summary>
public sealed record ProductivityFactor(
    string FactorName,
    double Impact,
    string Description,
    List<Guid> AffectedItems
);