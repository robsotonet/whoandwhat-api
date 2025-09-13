using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Dashboard;

/// <summary>
/// Request model for dashboard metrics with filtering options
/// </summary>
public sealed record DashboardMetricsRequestDto(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    [StringLength(50)]
    string? TimeZone = "UTC",
    List<string>? IncludeCategories = null,
    List<string>? IncludePriorities = null,
    bool IncludeInsights = true,
    bool IncludeRecommendations = true,
    bool IncludeMotivationalContent = true
);

/// <summary>
/// Request model for productivity streak with period options
/// </summary>
public sealed record ProductivityStreakRequestDto(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    bool IncludeMilestones = true,
    bool IncludeHistory = true,
    bool IncludeInsights = true,
    [Range(1, 365)]
    int MaxHistoryDays = 90
);

/// <summary>
/// Request model for overdue tasks with filtering
/// </summary>
public sealed record OverdueTasksRequestDto(
    List<string>? Categories = null,
    List<string>? Priorities = null,
    List<string>? UrgencyLevels = null, // critical, high, medium, low
    [Range(1, 1000)]
    int MaxTasks = 50,
    bool IncludeAnalysis = true,
    bool IncludeRecommendations = true,
    string SortBy = "daysOverdue", // daysOverdue, priority, category, title
    string SortOrder = "desc" // asc, desc
);

/// <summary>
/// Request model for completion statistics
/// </summary>
public sealed record CompletionStatsRequestDto(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    [StringLength(20)]
    string Period = "month", // day, week, month, quarter, year
    List<string>? IncludeCategories = null,
    bool IncludeTrends = true,
    bool IncludeBreakdowns = true,
    bool IncludeInsights = true
);

/// <summary>
/// Request model for updating dashboard settings
/// </summary>
public sealed record UpdateDashboardSettingsRequestDto(
    [StringLength(20)]
    string Theme = "light",
    [StringLength(10)]
    string Language = "en",
    bool ShowCompletionStats = true,
    bool ShowProductivityStreak = true,
    bool ShowOverdueTasks = true,
    bool ShowMotivationalContent = true,
    [Range(30, 3600)]
    int RefreshInterval = 300,
    List<string> VisibleWidgets = default!,
    Dictionary<string, object>? WidgetSettings = null,
    NotificationSettingsRequestDto? NotificationSettings = null,
    DisplaySettingsRequestDto? DisplaySettings = null
)
{
    public UpdateDashboardSettingsRequestDto() : this("light", "en", true, true, true, true, 300,
        new List<string> { "completion-stats", "productivity-streak", "overdue-tasks", "motivational-content" },
        null, null, null)
    {
    }
}

/// <summary>
/// Request model for notification settings
/// </summary>
public sealed record NotificationSettingsRequestDto(
    bool EnableOverdueAlerts = true,
    bool EnableStreakReminders = true,
    bool EnableDailyDigest = false,
    [Range(0, 30)]
    int OverdueAlertThreshold = 3,
    [StringLength(20)]
    string DigestFrequency = "weekly",
    List<int> QuietHours = default!
)
{
    public NotificationSettingsRequestDto() : this(true, true, false, 3, "weekly",
        new List<int> { 22, 23, 0, 1, 2, 3, 4, 5, 6, 7 })
    {
    }
}

/// <summary>
/// Request model for display settings
/// </summary>
public sealed record DisplaySettingsRequestDto(
    [StringLength(20)]
    string ChartType = "bar",
    [StringLength(20)]
    string DateFormat = "MM/dd/yyyy",
    [StringLength(10)]
    string TimeFormat = "12h",
    bool Use24HourFormat = false,
    [Range(5, 100)]
    int ItemsPerPage = 20,
    [StringLength(20)]
    string DefaultSortOrder = "priority",
    bool ShowAnimations = true,
    bool CompactMode = false
);

/// <summary>
/// Request model for resetting dashboard preferences
/// </summary>
public sealed record ResetDashboardPreferencesRequestDto(
    bool ConfirmReset = false,
    List<string>? SpecificSettings = null
);

/// <summary>
/// Request model for exporting dashboard data
/// </summary>
public sealed record ExportDashboardDataRequestDto(
    [Required]
    [StringLength(10)]
    string Format = "json", // csv, json, excel
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    List<string>? IncludeCategories = null,
    List<string>? IncludePriorities = null,
    List<string>? IncludeStatuses = null,
    List<string>? DataTypes = null, // tasks, metrics, streaks, analytics
    bool IncludeDeleted = false,
    bool IncludeArchived = false,
    [StringLength(50)]
    string TimeZone = "UTC",
    Dictionary<string, object>? CustomFilters = null
);

/// <summary>
/// Request model for generating dashboard reports
/// </summary>
public sealed record GenerateDashboardReportRequestDto(
    [Required]
    [StringLength(20)]
    string ReportType = "summary", // summary, detailed, analytical
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    [StringLength(10)]
    string Format = "pdf", // pdf, html, markdown
    List<string>? Sections = null, // overview, tasks, productivity, trends, recommendations
    bool IncludeCharts = true,
    bool IncludeInsights = true,
    bool IncludeRecommendations = true,
    [StringLength(50)]
    string TimeZone = "UTC",
    Dictionary<string, object>? CustomSettings = null
);

/// <summary>
/// Request model for bulk operations on tasks
/// </summary>
public sealed record BulkTaskOperationRequestDto(
    [Required]
    List<Guid> TaskIds = default!,
    [Required]
    [StringLength(20)]
    string Operation = "", // mark_complete, mark_pending, delete, archive, update_priority, update_category
    Dictionary<string, object>? OperationData = null,
    bool ValidatePermissions = true,
    string? Reason = null
);

/// <summary>
/// Request model for task search with dashboard context
/// </summary>
public sealed record DashboardTaskSearchRequestDto(
    [StringLength(200)]
    string? Query = null,
    List<string>? Categories = null,
    List<string>? Priorities = null,
    List<string>? Statuses = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    bool IncludeCompleted = true,
    bool IncludeArchived = false,
    [Range(1, 100)]
    int PageSize = 20,
    [Range(1, int.MaxValue)]
    int PageNumber = 1,
    [StringLength(20)]
    string SortBy = "createdAt", // createdAt, dueDate, priority, title, status
    [StringLength(4)]
    string SortOrder = "desc" // asc, desc
);

/// <summary>
/// Request model for getting task statistics
/// </summary>
public sealed record TaskStatisticsRequestDto(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    [StringLength(20)]
    string GroupBy = "category", // category, priority, status, date, week, month
    bool IncludeCompletionRates = true,
    bool IncludeTrends = true,
    bool IncludeComparisons = false, // compare with previous period
    [StringLength(50)]
    string TimeZone = "UTC"
);

/// <summary>
/// Request model for productivity insights
/// </summary>
public sealed record ProductivityInsightsRequestDto(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    [StringLength(20)]
    string AnalysisType = "standard", // standard, detailed, predictive
    List<string>? FocusAreas = null, // completion, timing, patterns, streaks
    bool IncludeRecommendations = true,
    bool IncludeComparisons = false,
    [Range(1, 10)]
    int MaxInsights = 5
);
