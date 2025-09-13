using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.DTOs.SmartScheduling;

/// <summary>
/// Request to generate a smart schedule for a user
/// </summary>
public sealed record GenerateSmartScheduleRequest(
    Guid UserId,
    DateTime StartDate,
    DateTime EndDate,
    List<Guid> TaskIds,
    SmartSchedulingPreferences Preferences,
    bool IncludeCalendarEvents = true,
    bool OptimizeForProductivity = true
);

/// <summary>
/// Response containing a generated smart schedule
/// </summary>
public sealed record SmartScheduleResponse(
    Guid UserId,
    DateTime GeneratedAt,
    List<SmartScheduledItem> ScheduledItems,
    List<TimeBlockSuggestion> TimeBlocks,
    List<string> OptimizationInsights,
    SmartScheduleMetrics Metrics,
    double ConfidenceScore
);

/// <summary>
/// Request to optimize an existing schedule
/// </summary>
public sealed record OptimizeScheduleRequest(
    Guid UserId,
    Guid ScheduleId,
    List<SmartScheduledItem> CurrentSchedule,
    OptimizationGoals Goals,
    List<ScheduleConstraint> Constraints
);

/// <summary>
/// Response containing schedule optimization results
/// </summary>
public sealed record ScheduleOptimizationResponse(
    Guid UserId,
    DateTime OptimizedAt,
    List<SmartScheduledItem> OptimizedSchedule,
    List<ScheduleChange> Changes,
    OptimizationMetrics Metrics,
    List<string> Recommendations
);

/// <summary>
/// A scheduled item in a smart schedule
/// </summary>
public sealed record SmartScheduledItem(
    Guid Id,
    Guid? TaskId,
    Guid? CalendarEventId,
    string Title,
    string Description,
    DateTime StartTime,
    DateTime EndTime,
    ScheduledItemType ItemType,
    Priority Priority,
    string Category,
    List<string> Tags,
    TimeSpan EstimatedDuration,
    bool IsFlexible,
    List<SchedulingReason> SchedulingReasons
);

/// <summary>
/// Time block suggestion for productivity optimization
/// </summary>
public sealed record TimeBlockSuggestion(
    Guid Id,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    TimeBlockPurpose Purpose,
    string Description,
    List<string> SuggestedActivities,
    double ProductivityScore,
    string Reasoning
);

/// <summary>
/// User preferences for smart scheduling
/// </summary>
public sealed record SmartSchedulingPreferences(
    Guid UserId,
    WorkingHours PreferredWorkingHours,
    List<TimeSpan> PreferredBreakTimes,
    int MaxTasksPerTimeBlock,
    TimeSpan MinimumTaskDuration,
    TimeSpan MaximumTaskDuration,
    List<string> PreferredTaskCategories,
    ProductivityPatterns ProductivityPattern,
    bool AllowOverlappingTasks,
    bool PreferMorningTasks,
    bool RequireBufferTime,
    TimeSpan BufferDuration,
    List<SchedulingConstraint> CustomConstraints
);

/// <summary>
/// Working hours configuration
/// </summary>
public sealed record WorkingHours(
    TimeSpan StartTime,
    TimeSpan EndTime,
    List<DayOfWeek> WorkingDays,
    TimeSpan LunchBreakStart,
    TimeSpan LunchBreakDuration,
    bool FlexibleSchedule
);

/// <summary>
/// Optimization goals for schedule optimization
/// </summary>
public sealed record OptimizationGoals(
    OptimizationPriority Primary,
    List<OptimizationPriority> Secondary,
    double ProductivityWeight,
    double BalanceWeight,
    double EfficiencyWeight,
    bool MinimizeContextSwitching,
    bool RespectDeadlines,
    bool OptimizeEnergyLevels
);

/// <summary>
/// Schedule constraint for limiting optimization options
/// </summary>
public sealed record ScheduleConstraint(
    string Name,
    ConstraintType Type,
    DateTime? StartTime,
    DateTime? EndTime,
    List<Guid> AffectedTaskIds,
    string Description,
    bool IsHard,
    double Weight
);

/// <summary>
/// A change made during schedule optimization
/// </summary>
public sealed record ScheduleChange(
    string ChangeType,
    Guid ItemId,
    string ItemTitle,
    DateTime? OldStartTime,
    DateTime? NewStartTime,
    DateTime? OldEndTime,
    DateTime? NewEndTime,
    string Reason,
    double ImpactScore
);

/// <summary>
/// Metrics about a smart schedule
/// </summary>
public sealed record SmartScheduleMetrics(
    int TotalTasks,
    int ScheduledTasks,
    int UnscheduledTasks,
    TimeSpan TotalScheduledTime,
    TimeSpan AvailableTime,
    double UtilizationPercentage,
    int NumberOfTimeBlocks,
    double ProductivityScore,
    List<string> OptimizationWarnings
);

/// <summary>
/// Metrics about schedule optimization
/// </summary>
public sealed record OptimizationMetrics(
    int ChangesApplied,
    double ProductivityImprovement,
    TimeSpan TimeSaved,
    double EfficiencyGain,
    int ConflictsResolved,
    List<OptimizationImpact> Impacts
);

/// <summary>
/// Impact of a specific optimization
/// </summary>
public sealed record OptimizationImpact(
    string Area,
    double Impact,
    string Description,
    List<string> BenefitsRealized
);

/// <summary>
/// Reason why an item was scheduled at a specific time
/// </summary>
public sealed record SchedulingReason(
    string ReasonType,
    string Description,
    double InfluenceWeight,
    List<string> FactorsConsidered
);

/// <summary>
/// Request for scheduling suggestions
/// </summary>
public sealed record GetSchedulingSuggestionsRequest(
    Guid UserId,
    DateTime Date,
    List<Guid> TaskIds,
    int MaxSuggestions = 5
);

/// <summary>
/// Response containing scheduling suggestions
/// </summary>
public sealed record SchedulingSuggestionsResponse(
    Guid UserId,
    DateTime Date,
    List<SchedulingSuggestion> Suggestions,
    DateTime GeneratedAt
);

/// <summary>
/// A suggestion for scheduling a task
/// </summary>
public sealed record SchedulingSuggestion(
    Guid TaskId,
    string TaskTitle,
    DateTime SuggestedStartTime,
    DateTime SuggestedEndTime,
    double ConfidenceScore,
    string Reasoning,
    List<string> Benefits,
    List<string> Considerations
);

/// <summary>
/// Request to update user scheduling preferences
/// </summary>
public sealed record UpdateSchedulingPreferencesRequest(
    Guid UserId,
    SmartSchedulingPreferences Preferences
);

/// <summary>
/// Response for preference updates
/// </summary>
public sealed record UpdateSchedulingPreferencesResponse(
    bool Success,
    string Message,
    SmartSchedulingPreferences UpdatedPreferences,
    DateTime UpdatedAt
);

/// <summary>
/// Request to get user's scheduling patterns
/// </summary>
public sealed record GetUserSchedulingPatternsRequest(
    Guid UserId,
    DateTime StartDate,
    DateTime EndDate
);

/// <summary>
/// Response containing user's scheduling patterns
/// </summary>
public sealed record UserSchedulingPatternsResponse(
    Guid UserId,
    TimeSpan AnalysisPeriod,
    ProductivityPatterns DetectedPatterns,
    List<SchedulingPattern> Patterns,
    List<string> Insights,
    DateTime AnalyzedAt
);

/// <summary>
/// A detected scheduling pattern
/// </summary>
public sealed record SchedulingPattern(
    string PatternName,
    string Description,
    double Frequency,
    List<TimeSpan> PreferredTimes,
    List<string> AssociatedCategories,
    double ProductivityCorrelation
);

// Enums

public enum ScheduledItemType
{
    Task,
    CalendarEvent,
    Break,
    TimeBlock,
    Buffer,
    Meeting,
    FocusTime
}

public enum TimeBlockPurpose
{
    DeepWork,
    Administrative,
    Creative,
    Planning,
    Communication,
    Learning,
    Break,
    Buffer
}

public enum ProductivityPatterns
{
    MorningPerson,
    NightOwl,
    MidDay,
    Consistent,
    Variable,
    EarlyBird,
    AfternoonPeak
}

public enum OptimizationPriority
{
    Productivity,
    WorkLifeBalance,
    DeadlineAdherence,
    EnergyOptimization,
    ContextSwitchingMinimization,
    TimeEfficiency,
    StressReduction
}

public enum ConstraintType
{
    TimeBlock,
    Deadline,
    Availability,
    Priority,
    Dependencies,
    Category,
    Resource,
    Energy
}

public enum TimeSlotType
{
    Available,
    Busy,
    Tentative,
    OutOfOffice,
    WorkingTime,
    BreakTime,
    FocusTime,
    Buffer
}

// Additional types for smart scheduling infrastructure

/// <summary>
/// Context for schedule optimization operations
/// </summary>
public sealed record ScheduleOptimizationContext(
    List<SmartScheduledItem> CurrentSchedule,
    List<Guid> TaskIds,
    DateTime StartDate,
    DateTime EndDate,
    SmartSchedulingPreferences Preferences,
    List<ExternalCalendarEvent> CalendarEvents,
    OptimizationGoals Goals
);

/// <summary>
/// Result from schedule optimization
/// </summary>
public sealed record ScheduleOptimizationResult(
    List<SmartScheduledItem> OptimizedSchedule,
    List<ScheduleChange> Changes,
    double ImprovementScore,
    List<string> OptimizationInsights,
    Dictionary<string, double> Metrics,
    DateTime GeneratedAt
);

/// <summary>
/// Context for existing schedule optimization
/// </summary>
public sealed record OptimizationContext(
    List<SmartScheduledItem> ExistingSchedule,
    OptimizationGoals Goals,
    List<ScheduleConstraint> Constraints,
    SmartSchedulingPreferences Preferences,
    DateTime OptimizationDate
);

/// <summary>
/// Represents a time slot for scheduling
/// </summary>
public sealed record TimeSlot(
    DateTime StartTime,
    DateTime EndTime,
    TimeSlotType SlotType,
    bool IsAvailable,
    string Description = ""
);

/// <summary>
/// Data point for trend analysis
/// </summary>
public sealed record TrendDataPoint(
    DateTime Date,
    double Value,
    string Category,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Enhanced scheduling constraint with more detail
/// </summary>
public sealed record SchedulingConstraint(
    string Name,
    ConstraintType Type,
    DateTime? StartTime,
    DateTime? EndTime,
    List<Guid> AffectedTaskIds,
    string Description,
    bool IsHard,
    double Weight,
    Dictionary<string, object> Parameters
);

/// <summary>
/// External calendar event for integration
/// </summary>
public sealed record ExternalCalendarEvent(
    string Id,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string CalendarSource,
    bool IsBlockingTime
);


/// <summary>
/// Entity task type alias for compatibility
/// </summary>
public record TaskEntity(
    Guid Id,
    string Title,
    string Description,
    DateTime? DueDate,
    Priority Priority
);
