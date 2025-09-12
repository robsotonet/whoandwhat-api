namespace WhoAndWhat.Application.DTOs.AI;

/// <summary>
/// AI-generated day plan with task scheduling and time block recommendations
/// </summary>
public sealed record AIGeneratedPlan(
    Guid UserId,
    DateTime PlanDate,
    List<ScheduledTaskBlock> ScheduledTasks,
    List<TimeBlockRecommendation> TimeBlocks,
    List<string> ProductivityTips,
    AIAnalysisMetadata AnalysisMetadata,
    double ConfidenceScore,
    DateTime GeneratedAt
);

/// <summary>
/// User preferences and constraints for AI planning
/// </summary>
public sealed record UserPlanningPreferences(
    TimeSpan WorkStartTime,
    TimeSpan WorkEndTime,
    List<TimeSpan> BreakTimes,
    int MaxTasksPerDay,
    List<string> PreferredTaskCategories,
    WorkingStyle WorkingStyle,
    List<string> AvoidancePatterns,
    EnergyLevelPattern EnergyPattern
);

/// <summary>
/// AI task priority suggestion with reasoning
/// </summary>
public sealed record TaskPrioritySuggestion(
    Guid TaskId,
    string SuggestedPriority,
    double ConfidenceScore,
    string AIReasoning,
    List<string> InfluencingFactors,
    DateTime SuggestionCreatedAt
);

/// <summary>
/// Context for task analysis and prioritization
/// </summary>
public sealed record TaskAnalysisContext(
    Guid TaskId,
    string TaskTitle,
    string TaskDescription,
    string CurrentCategory,
    string CurrentPriority,
    DateTime? DueDate,
    List<string> Tags,
    Dictionary<string, object> AdditionalContext
);

/// <summary>
/// Context for priority analysis
/// </summary>
public sealed record PriorityAnalysisContext(
    DateTime AnalysisDate,
    List<Guid> RelatedTaskIds,
    Dictionary<string, double> UserProductivityPatterns,
    List<string> CurrentGoals,
    WorkloadIntensity CurrentWorkload
);

/// <summary>
/// Schedule optimization result with suggestions and insights
/// </summary>
public sealed record ScheduleOptimizationResult(
    Guid UserId,
    DateTime OptimizationDate,
    List<ScheduleOptimization> Optimizations,
    List<ProductivityInsight> Insights,
    EstimatedImpact EstimatedImpact,
    DateTime GeneratedAt
);

/// <summary>
/// Time slot for scheduling
/// </summary>
public sealed record TimeSlot(
    DateTime StartTime,
    DateTime EndTime,
    bool IsAvailable,
    string? CurrentActivity,
    TimeSlotType SlotType
);

/// <summary>
/// Schedule optimization preferences
/// </summary>
public sealed record ScheduleOptimizationPreferences(
    OptimizationGoal PrimaryGoal,
    List<string> PreferredOptimizationTypes,
    int MaxScheduleChanges,
    bool AllowTaskReordering,
    bool PreserveBreakTimes
);

/// <summary>
/// Break recommendation with timing and activity suggestions
/// </summary>
public sealed record BreakRecommendation(
    TimeSpan RecommendedTime,
    TimeSpan Duration,
    BreakType BreakType,
    string ActivitySuggestion,
    string Reasoning,
    double ImportanceScore
);

/// <summary>
/// Workload analysis for break recommendations
/// </summary>
public sealed record WorkloadAnalysis(
    DateTime AnalysisDate,
    int TasksCompleted,
    int TasksRemaining,
    double StressLevel,
    TimeSpan ContinuousWorkTime,
    List<string> IntensityIndicators
);

/// <summary>
/// Productivity insights and patterns analysis
/// </summary>
public sealed record ProductivityInsights(
    Guid UserId,
    DateTime AnalysisDate,
    List<ProductivityPattern> IdentifiedPatterns,
    List<string> ActionableRecommendations,
    Dictionary<string, double> PerformanceMetrics,
    ProductivityTrendAnalysis TrendAnalysis
);

/// <summary>
/// Timeframe for productivity analysis
/// </summary>
public sealed record TimeframeAnalysis(
    DateTime StartDate,
    DateTime EndDate,
    AnalysisGranularity Granularity,
    List<string> FocusAreas
);

/// <summary>
/// Task categorization suggestion with confidence
/// </summary>
public sealed record CategorySuggestion(
    string SuggestedCategory,
    double ConfidenceScore,
    string Reasoning,
    List<string> AlternativeCategories,
    Dictionary<string, double> CategoryProbabilities
);

/// <summary>
/// User's historical categorization patterns
/// </summary>
public sealed record UserCategoryHistory(
    Guid UserId,
    Dictionary<string, int> CategoryFrequency,
    Dictionary<string, List<string>> KeywordPatterns,
    DateTime LastUpdated
);

/// <summary>
/// AI service health status with diagnostics
/// </summary>
public sealed record AIServiceHealthStatus(
    bool IsHealthy,
    TimeSpan ResponseTime,
    string ServiceVersion,
    List<string> AvailableCapabilities,
    List<HealthCheckResult> DetailedChecks,
    DateTime CheckTimestamp
);

/// <summary>
/// Task time estimation with confidence intervals
/// </summary>
public sealed record TaskTimeEstimate(
    Guid TaskId,
    TimeSpan EstimatedDuration,
    TimeSpan MinDuration,
    TimeSpan MaxDuration,
    double ConfidenceLevel,
    List<string> EstimationFactors
);

/// <summary>
/// Request for task time estimation
/// </summary>
public sealed record TaskEstimationRequest(
    Guid TaskId,
    string TaskTitle,
    string TaskDescription,
    string Category,
    string Priority,
    List<string> RequiredSkills,
    ComplexityLevel Complexity
);

/// <summary>
/// User's historical task completion performance
/// </summary>
public sealed record UserHistoricalPerformance(
    Guid UserId,
    Dictionary<string, TimeSpan> AverageTimesByCategory,
    Dictionary<string, double> AccuracyByComplexity,
    List<PerformanceDataPoint> HistoricalData,
    DateTime LastUpdated
);

// Supporting types and enums

/// <summary>
/// Scheduled task block with time allocation
/// </summary>
public sealed record ScheduledTaskBlock(
    Guid TaskId,
    string TaskTitle,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Category,
    string Priority,
    List<string> PreparationNeeded
);

/// <summary>
/// Time block recommendation
/// </summary>
public sealed record TimeBlockRecommendation(
    TimeSpan StartTime,
    TimeSpan EndTime,
    TimeBlockType BlockType,
    string Purpose,
    string Description
);

/// <summary>
/// AI analysis metadata
/// </summary>
public sealed record AIAnalysisMetadata(
    string ModelUsed,
    string ModelVersion,
    List<string> DataSourcesUsed,
    DateTime ProcessingStartTime,
    TimeSpan ProcessingDuration,
    Dictionary<string, object> ModelParameters
);

/// <summary>
/// Schedule optimization suggestion
/// </summary>
public sealed record ScheduleOptimization(
    string OptimizationType,
    string Description,
    TimeSpan OriginalTime,
    TimeSpan OptimizedTime,
    double ExpectedBenefit,
    List<string> RequiredActions
);

/// <summary>
/// Productivity insight
/// </summary>
public sealed record ProductivityInsight(
    string InsightType,
    string Description,
    double ImpactScore,
    List<string> ActionableSteps,
    string Category
);

/// <summary>
/// Estimated impact of optimization
/// </summary>
public sealed record EstimatedImpact(
    double ProductivityImprovement,
    TimeSpan TimeSaved,
    double StressReduction,
    List<string> QualitativeBenefits
);

/// <summary>
/// Productivity pattern identification
/// </summary>
public sealed record ProductivityPattern(
    string PatternName,
    string Description,
    double Frequency,
    List<string> Triggers,
    double ImpactOnPerformance
);

/// <summary>
/// Productivity trend analysis
/// </summary>
public sealed record ProductivityTrendAnalysis(
    TrendDirection OverallTrend,
    List<TrendDataPoint> DataPoints,
    List<string> TrendDrivers,
    List<string> Recommendations
);

/// <summary>
/// Health check result
/// </summary>
public sealed record HealthCheckResult(
    string CheckName,
    bool Passed,
    string? ErrorMessage,
    TimeSpan Duration,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Performance data point
/// </summary>
public sealed record PerformanceDataPoint(
    DateTime Date,
    string Category,
    TimeSpan EstimatedTime,
    TimeSpan ActualTime,
    double AccuracyScore
);

/// <summary>
/// Trend data point
/// </summary>
public sealed record TrendDataPoint(
    DateTime Date,
    double Value,
    string Metric,
    Dictionary<string, object> Context
);

// Enums

public enum WorkingStyle
{
    Sequential,
    Parallel,
    TimeBlocked,
    Flexible,
    Deadline_Driven
}

public enum EnergyLevelPattern
{
    MorningPerson,
    NightOwl,
    Consistent,
    Variable,
    Afternoon_Peak
}

public enum WorkloadIntensity
{
    Low,
    Moderate,
    High,
    Overwhelming
}

public enum TimeSlotType
{
    Work,
    Break,
    Meeting,
    Personal,
    Blocked
}

public enum OptimizationGoal
{
    Productivity,
    Work_Life_Balance,
    Stress_Reduction,
    Time_Efficiency,
    Quality_Focus
}

public enum BreakType
{
    Short,
    Long,
    Active,
    Passive,
    Creative
}

public enum AnalysisGranularity
{
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Quarterly
}

public enum ComplexityLevel
{
    Simple,
    Moderate,
    Complex,
    Expert_Level
}

public enum TimeBlockType
{
    Deep_Work,
    Administrative,
    Creative,
    Planning,
    Break,
    Buffer
}

public enum TrendDirection
{
    Improving,
    Declining,
    Stable,
    Volatile,
    Cyclical
}