namespace WhoAndWhat.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for smart scheduling functionality
/// </summary>
public class SmartSchedulingSettings
{
    /// <summary>
    /// Section name in configuration
    /// </summary>
    public const string SectionName = "SmartScheduling";

    /// <summary>
    /// Whether smart scheduling is enabled
    /// </summary>
    public bool EnableSmartScheduling { get; set; } = true;

    /// <summary>
    /// Whether schedule optimization is enabled
    /// </summary>
    public bool EnableOptimization { get; set; } = true;

    /// <summary>
    /// Whether time blocking feature is enabled
    /// </summary>
    public bool EnableTimeBlocking { get; set; } = true;

    /// <summary>
    /// Whether pattern learning from user behavior is enabled
    /// </summary>
    public bool EnablePatternLearning { get; set; } = true;

    /// <summary>
    /// Maximum number of deep work blocks per day
    /// </summary>
    public int MaxDeepWorkBlocksPerDay { get; set; } = 3;

    /// <summary>
    /// Maximum number of administrative blocks per day
    /// </summary>
    public int MaxAdminBlocksPerDay { get; set; } = 2;

    /// <summary>
    /// Default minimum task duration in minutes
    /// </summary>
    public int DefaultMinimumTaskDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Default maximum task duration in hours
    /// </summary>
    public int DefaultMaximumTaskDurationHours { get; set; } = 4;

    /// <summary>
    /// Default buffer time between tasks in minutes
    /// </summary>
    public int DefaultBufferTimeMinutes { get; set; } = 15;

    /// <summary>
    /// How often to analyze user patterns (in days)
    /// </summary>
    public int PatternAnalysisIntervalDays { get; set; } = 7;

    /// <summary>
    /// Number of days of history to consider for pattern analysis
    /// </summary>
    public int PatternAnalysisHistoryDays { get; set; } = 30;

    /// <summary>
    /// Maximum number of scheduling suggestions to return
    /// </summary>
    public int MaxSchedulingSuggestions { get; set; } = 10;

    /// <summary>
    /// Confidence threshold for automatic schedule optimizations
    /// </summary>
    public double AutoOptimizationConfidenceThreshold { get; set; } = 0.8;

    /// <summary>
    /// Cache settings for scheduling data
    /// </summary>
    public SchedulingCacheSettings Cache { get; set; } = new();

    /// <summary>
    /// Machine learning settings for pattern recognition
    /// </summary>
    public MachineLearningSettings MachineLearning { get; set; } = new();

    /// <summary>
    /// Productivity scoring settings
    /// </summary>
    public ProductivityScoringSettings ProductivityScoring { get; set; } = new();

    /// <summary>
    /// Time block optimization settings
    /// </summary>
    public TimeBlockSettings TimeBlocks { get; set; } = new();

    /// <summary>
    /// Background service settings
    /// </summary>
    public BackgroundServiceSettings BackgroundServices { get; set; } = new();
}

/// <summary>
/// Cache settings for smart scheduling
/// </summary>
public class SchedulingCacheSettings
{
    /// <summary>
    /// How long to cache user preferences (in minutes)
    /// </summary>
    public int UserPreferencesCacheMinutes { get; set; } = 60;

    /// <summary>
    /// How long to cache scheduling patterns (in minutes)
    /// </summary>
    public int SchedulingPatternsCacheMinutes { get; set; } = 120;

    /// <summary>
    /// How long to cache productivity insights (in minutes)
    /// </summary>
    public int ProductivityInsightsCacheMinutes { get; set; } = 240;

    /// <summary>
    /// How long to cache generated schedules (in minutes)
    /// </summary>
    public int GeneratedScheduleCacheMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum number of cached schedules per user
    /// </summary>
    public int MaxCachedSchedulesPerUser { get; set; } = 5;
}

/// <summary>
/// Machine learning settings for pattern recognition
/// </summary>
public class MachineLearningSettings
{
    /// <summary>
    /// Minimum number of scheduling activities required for pattern learning
    /// </summary>
    public int MinimumActivitiesForLearning { get; set; } = 10;

    /// <summary>
    /// Learning rate for preference adaptation
    /// </summary>
    public double LearningRate { get; set; } = 0.1;

    /// <summary>
    /// Weight given to user feedback in learning
    /// </summary>
    public double FeedbackWeight { get; set; } = 0.3;

    /// <summary>
    /// Weight given to behavioral patterns in learning
    /// </summary>
    public double BehavioralWeight { get; set; } = 0.7;

    /// <summary>
    /// Minimum confidence score for pattern-based suggestions
    /// </summary>
    public double MinimumPatternConfidence { get; set; } = 0.6;

    /// <summary>
    /// How often to update learned patterns (in days)
    /// </summary>
    public int PatternUpdateIntervalDays { get; set; } = 3;
}

/// <summary>
/// Productivity scoring algorithm settings
/// </summary>
public class ProductivityScoringSettings
{
    /// <summary>
    /// Weight for time utilization in productivity score
    /// </summary>
    public double TimeUtilizationWeight { get; set; } = 0.25;

    /// <summary>
    /// Weight for task balancing in productivity score
    /// </summary>
    public double TaskBalancingWeight { get; set; } = 0.20;

    /// <summary>
    /// Weight for priority alignment in productivity score
    /// </summary>
    public double PriorityAlignmentWeight { get; set; } = 0.30;

    /// <summary>
    /// Weight for context switching minimization in productivity score
    /// </summary>
    public double ContextSwitchingWeight { get; set; } = 0.15;

    /// <summary>
    /// Weight for work-life balance in productivity score
    /// </summary>
    public double WorkLifeBalanceWeight { get; set; } = 0.10;

    /// <summary>
    /// Minimum productivity score threshold for recommendations
    /// </summary>
    public double MinimumScoreThreshold { get; set; } = 0.5;

    /// <summary>
    /// Target productivity score for optimization
    /// </summary>
    public double TargetProductivityScore { get; set; } = 0.85;
}

/// <summary>
/// Time block configuration settings
/// </summary>
public class TimeBlockSettings
{
    /// <summary>
    /// Default deep work block duration in minutes
    /// </summary>
    public int DeepWorkBlockDurationMinutes { get; set; } = 90;

    /// <summary>
    /// Default administrative block duration in minutes
    /// </summary>
    public int AdministrativeBlockDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Default creative block duration in minutes
    /// </summary>
    public int CreativeBlockDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Default planning block duration in minutes
    /// </summary>
    public int PlanningBlockDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Default break block duration in minutes
    /// </summary>
    public int BreakBlockDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Minimum gap between time blocks in minutes
    /// </summary>
    public int MinimumGapBetweenBlocksMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum number of time blocks per day
    /// </summary>
    public int MaximumTimeBlocksPerDay { get; set; } = 12;

    /// <summary>
    /// Whether to automatically create buffer blocks
    /// </summary>
    public bool AutoCreateBufferBlocks { get; set; } = true;

    /// <summary>
    /// Whether to optimize time block sequence
    /// </summary>
    public bool OptimizeTimeBlockSequence { get; set; } = true;
}

/// <summary>
/// Schedule optimization algorithm settings
/// </summary>
public class OptimizationAlgorithmSettings
{
    /// <summary>
    /// Maximum number of optimization iterations
    /// </summary>
    public int MaxOptimizationIterations { get; set; } = 100;

    /// <summary>
    /// Optimization convergence tolerance
    /// </summary>
    public double ConvergenceTolerance { get; set; } = 0.001;

    /// <summary>
    /// Whether to use genetic algorithm for optimization
    /// </summary>
    public bool UseGeneticAlgorithm { get; set; } = false;

    /// <summary>
    /// Whether to use simulated annealing for optimization
    /// </summary>
    public bool UseSimulatedAnnealing { get; set; } = true;

    /// <summary>
    /// Population size for genetic algorithm
    /// </summary>
    public int GeneticAlgorithmPopulationSize { get; set; } = 50;

    /// <summary>
    /// Mutation rate for genetic algorithm
    /// </summary>
    public double GeneticAlgorithmMutationRate { get; set; } = 0.1;

    /// <summary>
    /// Initial temperature for simulated annealing
    /// </summary>
    public double SimulatedAnnealingInitialTemperature { get; set; } = 1000.0;

    /// <summary>
    /// Cooling rate for simulated annealing
    /// </summary>
    public double SimulatedAnnealingCoolingRate { get; set; } = 0.95;
}

/// <summary>
/// Performance monitoring settings
/// </summary>
public class SchedulingPerformanceSettings
{
    /// <summary>
    /// Maximum time allowed for schedule generation (in seconds)
    /// </summary>
    public int MaxScheduleGenerationTimeSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum time allowed for optimization (in seconds)
    /// </summary>
    public int MaxOptimizationTimeSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to enable performance monitoring
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Whether to log performance metrics
    /// </summary>
    public bool LogPerformanceMetrics { get; set; } = false;

    /// <summary>
    /// Performance alert threshold for schedule generation (in seconds)
    /// </summary>
    public int PerformanceAlertThresholdSeconds { get; set; } = 15;
}

/// <summary>
/// Background service configuration settings
/// </summary>
public class BackgroundServiceSettings
{
    /// <summary>
    /// Delay between processing batches in pattern learning service (milliseconds)
    /// </summary>
    public int PatternLearningBatchDelayMs { get; set; } = 100;

    /// <summary>
    /// Maximum number of users to process per pattern learning cycle
    /// </summary>
    public int PatternLearningMaxUsersPerCycle { get; set; } = 100;

    /// <summary>
    /// Delay between processing batches in optimization service (milliseconds)
    /// </summary>
    public int OptimizationBatchDelayMs { get; set; } = 200;

    /// <summary>
    /// Maximum number of users to process per optimization cycle
    /// </summary>
    public int OptimizationMaxUsersPerCycle { get; set; } = 50;

    /// <summary>
    /// Percentage chance a user will be selected for optimization per cycle (0.0 to 1.0)
    /// </summary>
    public double OptimizationSelectionProbability { get; set; } = 0.1;

    /// <summary>
    /// Maximum number of task records to keep per user for pattern analysis
    /// </summary>
    public int MaxTaskRecordsPerUser { get; set; } = 100;

    /// <summary>
    /// Maximum number of tasks to query for pattern analysis
    /// </summary>
    public int MaxTasksForPatternAnalysis { get; set; } = 1000;

    /// <summary>
    /// Default working hours start time (hours from midnight)
    /// </summary>
    public int DefaultWorkingHoursStartHour { get; set; } = 9;

    /// <summary>
    /// Default working hours end time (hours from midnight)
    /// </summary>
    public int DefaultWorkingHoursEndHour { get; set; } = 17;

    /// <summary>
    /// Default lunch time (hours from midnight)
    /// </summary>
    public int DefaultLunchTimeHour { get; set; } = 12;

    /// <summary>
    /// Default lunch duration in minutes
    /// </summary>
    public int DefaultLunchDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Default working hours change threshold in minutes
    /// </summary>
    public int WorkingHoursChangeThresholdMinutes { get; set; } = 30;
}
