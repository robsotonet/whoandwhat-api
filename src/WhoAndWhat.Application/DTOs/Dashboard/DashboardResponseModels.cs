using WhoAndWhat.Application.Features.Dashboard.Commands.UpdateDashboardSettings;

namespace WhoAndWhat.Application.DTOs.Dashboard;

/// <summary>
/// Response model for dashboard metrics endpoint
/// </summary>
public sealed record DashboardMetricsResponseDto(
    DashboardOverviewDto Overview,
    List<CategoryStatsDto> CategoryStats,
    List<ProductivityTrendDto> ProductivityTrends,
    List<RecentActivityDto> RecentActivity,
    DashboardInsightsDto Insights
);

/// <summary>
/// Dashboard overview statistics
/// </summary>
public sealed record DashboardOverviewDto(
    int TotalTasks,
    int CompletedTasks,
    int PendingTasks,
    int OverdueTasks,
    double CompletionRate,
    double ProductivityScore,
    int CurrentStreak,
    DateTime LastActivityDate
);

/// <summary>
/// Category-specific statistics
/// </summary>
public sealed record CategoryStatsDto(
    string Category,
    int TotalTasks,
    int CompletedTasks,
    int PendingTasks,
    double CompletionRate,
    DateTime LastActivityDate,
    List<string> TopPriorities
);

/// <summary>
/// Productivity trend data point
/// </summary>
public sealed record ProductivityTrendDto(
    DateTime Date,
    int TasksCreated,
    int TasksCompleted,
    double CompletionRate,
    double ProductivityScore
);

/// <summary>
/// Recent activity item
/// </summary>
public sealed record RecentActivityDto(
    Guid TaskId,
    string Title,
    string Category,
    string Priority,
    string Action, // created, completed, updated, etc.
    DateTime Timestamp,
    Dictionary<string, object>? Metadata = null
);

/// <summary>
/// Dashboard insights and recommendations
/// </summary>
public sealed record DashboardInsightsDto(
    List<InsightDto> Insights,
    List<RecommendationDto> Recommendations,
    MotivationalContentDto? MotivationalContent = null
);

/// <summary>
/// Individual insight
/// </summary>
public sealed record InsightDto(
    string Title,
    string Description,
    string Type, // trend, warning, achievement, tip
    string Impact, // positive, negative, neutral
    Dictionary<string, object>? Data = null
);

/// <summary>
/// Recommendation for user
/// </summary>
public sealed record RecommendationDto(
    string Title,
    string Description,
    string Priority, // high, medium, low
    string Category, // productivity, time-management, balance, etc.
    List<string> ActionItems,
    DateTime? DueDate = null
);

/// <summary>
/// Motivational content
/// </summary>
public sealed record MotivationalContentDto(
    string Title,
    string Message,
    string Type, // quote, tip, achievement, milestone
    string Category, // productivity, motivation, celebration
    Dictionary<string, object>? CustomData = null
);

/// <summary>
/// Response model for productivity streak endpoint
/// </summary>
public sealed record ProductivityStreakResponseDto(
    int CurrentStreak,
    int LongestStreak,
    DateTime? LastActivityDate,
    List<StreakMilestoneDto> Milestones,
    List<StreakHistoryDto> StreakHistory,
    StreakInsightsDto Insights
);

/// <summary>
/// Streak milestone achievement
/// </summary>
public sealed record StreakMilestoneDto(
    int Days,
    string Title,
    string Description,
    bool IsAchieved,
    DateTime? AchievedDate = null
);

/// <summary>
/// Historical streak data
/// </summary>
public sealed record StreakHistoryDto(
    DateTime StartDate,
    DateTime EndDate,
    int Duration,
    int TasksCompleted,
    string StreakType // current, past, best
);

/// <summary>
/// Insights about streaks
/// </summary>
public sealed record StreakInsightsDto(
    double ConsistencyScore,
    string BestPeriod, // morning, afternoon, evening, weekend, etc.
    List<string> SuccessFactors,
    List<string> ImprovementAreas
);

/// <summary>
/// Response model for overdue tasks endpoint
/// </summary>
public sealed record OverdueTasksResponseDto(
    int TotalOverdueCount,
    List<OverdueTaskDto> OverdueTasks,
    OverdueAnalysisDto Analysis,
    List<string> Recommendations
);

/// <summary>
/// Overdue task details
/// </summary>
public sealed record OverdueTaskDto(
    Guid Id,
    string Title,
    string Category,
    string Priority,
    DateTime DueDate,
    int DaysOverdue,
    string UrgencyLevel, // critical, high, medium, low
    List<string>? Tags = null
);

/// <summary>
/// Analysis of overdue tasks
/// </summary>
public sealed record OverdueAnalysisDto(
    Dictionary<string, int> CategoryBreakdown,
    Dictionary<string, int> PriorityBreakdown,
    Dictionary<string, int> UrgencyBreakdown,
    double AverageOverdueDays,
    List<string> CommonPatterns
);

/// <summary>
/// Response model for completion stats endpoint
/// </summary>
public sealed record CompletionStatsResponseDto(
    CompletionOverviewDto Overview,
    List<CompletionTrendDto> Trends,
    Dictionary<string, CompletionBreakdownDto> Breakdowns,
    CompletionInsightsDto Insights
);

/// <summary>
/// Completion overview statistics
/// </summary>
public sealed record CompletionOverviewDto(
    int TotalCompleted,
    double CompletionRate,
    double AverageCompletionTime,
    int CompletedToday,
    int CompletedThisWeek,
    int CompletedThisMonth
);

/// <summary>
/// Completion trend over time
/// </summary>
public sealed record CompletionTrendDto(
    DateTime Period,
    int Completed,
    double CompletionRate,
    string TrendDirection // up, down, stable
);

/// <summary>
/// Breakdown statistics
/// </summary>
public sealed record CompletionBreakdownDto(
    Dictionary<string, int> Items,
    Dictionary<string, double> Rates,
    string TopPerformer,
    string NeedsAttention
);

/// <summary>
/// Completion insights and patterns
/// </summary>
public sealed record CompletionInsightsDto(
    List<string> PositivePatterns,
    List<string> AreasForImprovement,
    List<string> Recommendations,
    Dictionary<string, double> PerformanceMetrics
);

/// <summary>
/// Response model for dashboard settings endpoints
/// </summary>
public sealed record DashboardSettingsResponseDto(
    bool Success,
    DashboardSettingsDto Settings,
    List<string>? ValidationWarnings = null,
    DateTime LastUpdated = default
);

/// <summary>
/// Response model for resetting dashboard preferences
/// </summary>
public sealed record ResetDashboardPreferencesResponseDto(
    bool Success,
    DashboardSettingsDto DefaultSettings,
    List<string> ResetSettings,
    DateTime ResetTimestamp,
    string? Message = null
);

/// <summary>
/// Response model for export operations
/// </summary>
public sealed record ExportResponseDto(
    bool Success,
    string DownloadUrl,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTime ExpiresAt,
    ExportMetadataDto Metadata
);

/// <summary>
/// Export metadata information
/// </summary>
public sealed record ExportMetadataDto(
    DateTime ExportedAt,
    string ExportedBy,
    string ExportType,
    Dictionary<string, int> RecordCounts,
    string ChecksumHash,
    Dictionary<string, object>? CustomOptions = null
);

/// <summary>
/// Response model for report generation
/// </summary>
public sealed record ReportResponseDto(
    bool Success,
    string DownloadUrl,
    string ReportFileName,
    string ReportType,
    long FileSizeBytes,
    DateTime ExpiresAt,
    ReportMetadataDto Metadata
);

/// <summary>
/// Report metadata information
/// </summary>
public sealed record ReportMetadataDto(
    DateTime GeneratedAt,
    string GeneratedBy,
    string ReportType,
    Dictionary<string, object> Options,
    ReportSummaryDto Summary,
    string ChecksumHash
);

/// <summary>
/// Report summary statistics
/// </summary>
public sealed record ReportSummaryDto(
    int TotalTasks,
    int CompletedTasks,
    double CompletionRate,
    int ProductivityStreak,
    int InsightsGenerated,
    int RecommendationsProvided,
    DateTime PeriodStart,
    DateTime PeriodEnd
);