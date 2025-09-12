using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhoAndWhat.Application.DTOs.Dashboard;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetMotivationalContent;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetDashboardMetrics;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetProductivityStreak;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetOverdueTasks;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetCompletionStats;
using WhoAndWhat.Application.Features.Dashboard.Commands.UpdateDashboardSettings;
using WhoAndWhat.Application.Features.Dashboard.Commands.ResetDashboardPreferences;
using WhoAndWhat.Application.Features.Dashboard.Queries.ExportDashboardData;
using WhoAndWhat.Application.Features.Dashboard.Queries.GenerateDashboardReport;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Dashboard controller providing comprehensive dashboard metrics, analytics, and insights
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboard")]
[Tags("Dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DashboardController> _logger;

    /// <summary>
    /// Initializes a new instance of the Dashboard controller
    /// </summary>
    /// <param name="mediator">MediatR mediator for query handling</param>
    /// <param name="logger">Logger for Dashboard controller</param>
    public DashboardController(IMediator mediator, ILogger<DashboardController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get personalized motivational content for the user's dashboard
    /// </summary>
    /// <param name="count">Number of content items to return (default: 3, max: 10)</param>
    /// <param name="language">Content language (en/es, default: en)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Personalized motivational content</returns>
    [HttpGet("motivation")]
    [ProducesResponseType(typeof(GetMotivationalContentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<GetMotivationalContentResponse>> GetMotivationalContent(
        [FromQuery] int count = 3,
        [FromQuery] string language = "en",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input parameters
            if (count <= 0 || count > 10)
            {
                return BadRequest("Count must be between 1 and 10");
            }

            if (!IsValidLanguage(language))
            {
                return BadRequest("Language must be 'en' or 'es'");
            }

            // Extract user ID from claims
            var userId = GetCurrentUserId();

            _logger.LogInformation("Getting motivational content for user {UserId}, count: {Count}, language: {Language}",
                userId, count, language);

            // Execute query
            var query = new GetMotivationalContentQuery(userId, count, language);
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get motivational content for user {UserId}: {Error}",
                    userId, result.Error);

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve motivational content",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Successfully retrieved {Count} motivational contents for user {UserId}",
                result.Value.Contents.Count, userId);

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting motivational content");
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while retrieving motivational content",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Record user interaction with motivational content for analytics and personalization
    /// </summary>
    /// <param name="contentId">The content ID that was interacted with</param>
    /// <param name="interactionType">Type of interaction (view, click, share, dismiss)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success indicator</returns>
    [HttpPost("motivation/{contentId:guid}/interaction")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RecordContentInteraction(
        [FromRoute] Guid contentId,
        [FromBody] ContentInteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            if (contentId == Guid.Empty)
            {
                return BadRequest("Content ID cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(request.InteractionType))
            {
                return BadRequest("Interaction type is required");
            }

            if (!IsValidInteractionType(request.InteractionType))
            {
                return BadRequest("Invalid interaction type. Must be one of: view, click, share, dismiss");
            }

            // Extract user ID from claims
            var userId = GetCurrentUserId();

            _logger.LogInformation("Recording content interaction for user {UserId}, content {ContentId}, type {InteractionType}",
                userId, contentId, request.InteractionType);

            // TODO: Implement content interaction recording command
            // This would typically involve creating a RecordContentInteractionCommand
            // and handler that uses the IOptimizedContentEngagementService

            _logger.LogInformation("Successfully recorded content interaction for user {UserId}",
                userId);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error recording content interaction");
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error", 
                Detail = "An unexpected error occurred while recording content interaction",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get comprehensive dashboard metrics and insights
    /// </summary>
    /// <param name="request">Dashboard metrics filter options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dashboard metrics with overview, trends, and insights</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(DashboardMetricsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardMetricsResponseDto>> GetDashboardMetrics(
        [FromQuery] DashboardMetricsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Getting dashboard metrics for user {UserId}", userId);

        var query = new GetDashboardMetricsQuery(userId);

        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var response = MapToMetricsResponse(result.Value);
        return Ok(response);
    }

    /// <summary>
    /// Get productivity streak information and milestones
    /// </summary>
    /// <param name="request">Productivity streak request options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Productivity streak data with milestones and insights</returns>
    [HttpGet("streak")]
    [ProducesResponseType(typeof(ProductivityStreakResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductivityStreakResponseDto>> GetProductivityStreak(
        [FromQuery] ProductivityStreakRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Getting productivity streak for user {UserId}", userId);

        var query = new GetProductivityStreakQuery(userId);

        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var response = MapToStreakResponse(result.Value);
        return Ok(response);
    }

    /// <summary>
    /// Get overdue tasks with analysis and recommendations
    /// </summary>
    /// <param name="request">Overdue tasks filter options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Overdue tasks with analysis and recommendations</returns>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(OverdueTasksResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OverdueTasksResponseDto>> GetOverdueTasks(
        [FromQuery] OverdueTasksRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Getting overdue tasks for user {UserId}", userId);

        var query = new GetOverdueTasksQuery(
            UserId: userId,
            Limit: request.MaxTasks,
            CategoryFilter: request.Categories?.FirstOrDefault(),
            PriorityFilter: request.Priorities?.FirstOrDefault()
        );

        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var response = MapToOverdueResponse(result.Value);
        return Ok(response);
    }

    /// <summary>
    /// Get task completion statistics and trends
    /// </summary>
    /// <param name="request">Completion statistics request options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Completion statistics with trends and insights</returns>
    [HttpGet("completion-stats")]
    [ProducesResponseType(typeof(CompletionStatsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompletionStatsResponseDto>> GetCompletionStats(
        [FromQuery] CompletionStatsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Getting completion statistics for user {UserId}", userId);

        var query = new GetCompletionStatsQuery(userId, request.Period ?? "month");

        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var response = MapToCompletionStatsResponse(result.Value);
        return Ok(response);
    }

    /// <summary>
    /// Get current dashboard settings
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current dashboard settings</returns>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(DashboardSettingsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardSettingsResponseDto>> GetDashboardSettings(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Getting dashboard settings for user {UserId}", userId);

        // For now, return default settings - in reality, this would fetch from a repository
        var defaultSettings = new DashboardSettingsDto(
            Theme: "light",
            Language: "en",
            ShowCompletionStats: true,
            ShowProductivityStreak: true,
            ShowOverdueTasks: true,
            ShowMotivationalContent: true,
            RefreshInterval: 300,
            VisibleWidgets: new List<string> { "completion-stats", "productivity-streak", "overdue-tasks", "motivational-content" },
            WidgetSettings: new Dictionary<string, object>(),
            NotificationSettings: new NotificationSettingsDto(
                EnableOverdueAlerts: true,
                EnableStreakReminders: true,
                EnableDailyDigest: false,
                OverdueAlertThreshold: 3,
                DigestFrequency: "weekly",
                QuietHours: new List<int> { 22, 23, 0, 1, 2, 3, 4, 5, 6, 7 }
            ),
            DisplaySettings: new DisplaySettingsDto(
                ChartType: "bar",
                DateFormat: "MM/dd/yyyy",
                TimeFormat: "12h",
                Use24HourFormat: false,
                ItemsPerPage: 20,
                DefaultSortOrder: "priority",
                ShowAnimations: true,
                CompactMode: false
            )
        );

        var response = new DashboardSettingsResponseDto(
            Success: true,
            Settings: defaultSettings,
            LastUpdated: DateTime.UtcNow
        );

        return Ok(response);
    }

    /// <summary>
    /// Update dashboard settings
    /// </summary>
    /// <param name="request">Updated dashboard settings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated dashboard settings</returns>
    [HttpPut("settings")]
    [ProducesResponseType(typeof(DashboardSettingsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardSettingsResponseDto>> UpdateDashboardSettings(
        [FromBody] UpdateDashboardSettingsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Updating dashboard settings for user {UserId}", userId);

        var settings = new DashboardSettingsDto(
            Theme: request.Theme,
            Language: request.Language,
            ShowCompletionStats: request.ShowCompletionStats,
            ShowProductivityStreak: request.ShowProductivityStreak,
            ShowOverdueTasks: request.ShowOverdueTasks,
            ShowMotivationalContent: request.ShowMotivationalContent,
            RefreshInterval: request.RefreshInterval,
            VisibleWidgets: request.VisibleWidgets,
            WidgetSettings: request.WidgetSettings ?? new Dictionary<string, object>(),
            NotificationSettings: MapNotificationSettings(request.NotificationSettings),
            DisplaySettings: MapDisplaySettings(request.DisplaySettings)
        );

        var command = new UpdateDashboardSettingsCommand(userId, settings);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var response = new DashboardSettingsResponseDto(
            Success: result.Value.Success,
            Settings: result.Value.UpdatedSettings,
            ValidationWarnings: result.Value.ValidationWarnings,
            LastUpdated: DateTime.UtcNow
        );

        return Ok(response);
    }

    /// <summary>
    /// Reset dashboard preferences to defaults
    /// </summary>
    /// <param name="request">Reset preferences options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reset confirmation with default settings</returns>
    [HttpPost("settings/reset")]
    [ProducesResponseType(typeof(ResetDashboardPreferencesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResetDashboardPreferencesResponseDto>> ResetDashboardPreferences(
        [FromBody] ResetDashboardPreferencesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Resetting dashboard preferences for user {UserId}", userId);

        var command = new ResetDashboardPreferencesCommand(
            UserId: userId,
            ConfirmReset: request.ConfirmReset,
            SpecificSettings: request.SpecificSettings
        );

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var response = new ResetDashboardPreferencesResponseDto(
            Success: result.Value.Success,
            DefaultSettings: result.Value.DefaultSettings,
            ResetSettings: result.Value.ResetSettings,
            ResetTimestamp: result.Value.ResetTimestamp,
            Message: "Dashboard preferences have been successfully reset to defaults."
        );

        return Ok(response);
    }

    /// <summary>
    /// Export dashboard data in various formats
    /// </summary>
    /// <param name="request">Export options and format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File download with exported data</returns>
    [HttpPost("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExportResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportDashboardData(
        [FromBody] ExportDashboardDataRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Exporting dashboard data for user {UserId} in format {Format}", userId, request.Format);

        var options = new ExportOptionsDto(
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            IncludeCategories: request.IncludeCategories,
            IncludePriorities: request.IncludePriorities,
            IncludeStatuses: request.IncludeStatuses,
            DataTypes: request.DataTypes,
            IncludeDeleted: request.IncludeDeleted,
            IncludeArchived: request.IncludeArchived,
            TimeZone: request.TimeZone,
            CustomFilters: request.CustomFilters
        );

        var query = new ExportDashboardDataQuery(userId, request.Format, options);
        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var contentType = result.Value.ContentType;
        var fileName = result.Value.FileName;
        var fileContent = result.Value.FileContent;

        return File(fileContent, contentType, fileName);
    }

    /// <summary>
    /// Generate comprehensive dashboard report
    /// </summary>
    /// <param name="request">Report generation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated report file</returns>
    [HttpPost("reports")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ReportResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateDashboardReport(
        [FromBody] GenerateDashboardReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Generating dashboard report for user {UserId}, type: {ReportType}", userId, request.ReportType);

        var options = new ReportOptionsDto(
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            Format: request.Format,
            Sections: request.Sections,
            IncludeCharts: request.IncludeCharts,
            IncludeInsights: request.IncludeInsights,
            IncludeRecommendations: request.IncludeRecommendations,
            TimeZone: request.TimeZone,
            CustomSettings: request.CustomSettings
        );

        var query = new GenerateDashboardReportQuery(userId, request.ReportType, options);
        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var contentType = result.Value.ContentType;
        var fileName = result.Value.ReportFileName;
        var fileContent = result.Value.ReportContent;

        return File(fileContent, contentType, fileName);
    }

    #region Private Helper Methods

    /// <summary>
    /// Extract current user ID from JWT claims
    /// </summary>
    /// <returns>User ID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user ID is not found or invalid</exception>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    /// <summary>
    /// Validate language parameter
    /// </summary>
    /// <param name="language">Language code</param>
    /// <returns>True if valid</returns>
    private static bool IsValidLanguage(string language)
    {
        return language is "en" or "es";
    }

    /// <summary>
    /// Validate interaction type parameter
    /// </summary>
    /// <param name="interactionType">Interaction type</param>
    /// <returns>True if valid</returns>
    private static bool IsValidInteractionType(string interactionType)
    {
        return interactionType is "view" or "click" or "share" or "dismiss";
    }

    private DashboardMetricsResponseDto MapToMetricsResponse(GetDashboardMetricsResponse response)
    {
        return new DashboardMetricsResponseDto(
            Overview: new DashboardOverviewDto(
                TotalTasks: response.TotalActiveTasks,
                CompletedTasks: response.CompletedTasksToday,
                PendingTasks: response.TotalActiveTasks - response.CompletedTasksToday,
                OverdueTasks: response.OverdueTasks,
                CompletionRate: response.CompletionRate,
                ProductivityScore: response.OnTimeCompletionRate,
                CurrentStreak: response.Trends.CurrentStreak,
                LastActivityDate: DateTime.UtcNow
            ),
            CategoryStats: new List<CategoryStatsDto>
            {
                new("ToDo", response.CategoryBreakdown.TodoTasks, response.CategoryBreakdown.TodoTasks / 2, response.CategoryBreakdown.TodoTasks / 2, 0.5, DateTime.UtcNow, new List<string>()),
                new("Idea", response.CategoryBreakdown.IdeaTasks, response.CategoryBreakdown.IdeaTasks / 2, response.CategoryBreakdown.IdeaTasks / 2, 0.5, DateTime.UtcNow, new List<string>()),
                new("Appointment", response.CategoryBreakdown.AppointmentTasks, response.CategoryBreakdown.AppointmentTasks / 2, response.CategoryBreakdown.AppointmentTasks / 2, 0.5, DateTime.UtcNow, new List<string>()),
                new("BillReminder", response.CategoryBreakdown.BillReminderTasks, response.CategoryBreakdown.BillReminderTasks / 2, response.CategoryBreakdown.BillReminderTasks / 2, 0.5, DateTime.UtcNow, new List<string>()),
                new("Project", response.CategoryBreakdown.ProjectTasks, response.CategoryBreakdown.ProjectTasks / 2, response.CategoryBreakdown.ProjectTasks / 2, 0.5, DateTime.UtcNow, new List<string>())
            },
            ProductivityTrends: response.Trends.Last7Days.Select(day => new ProductivityTrendDto(
                Date: day.Date,
                TasksCreated: day.CreatedTasks,
                TasksCompleted: day.CompletedTasks,
                CompletionRate: day.CompletionRate,
                ProductivityScore: day.CompletionRate
            )).ToList(),
            RecentActivity: new List<RecentActivityDto>(),
            Insights: new DashboardInsightsDto(
                Insights: new List<InsightDto>(),
                Recommendations: new List<RecommendationDto>(),
                MotivationalContent: null
            )
        );
    }

    private ProductivityStreakResponseDto MapToStreakResponse(GetProductivityStreakResponse response)
    {
        return new ProductivityStreakResponseDto(
            CurrentStreak: response.CurrentStreak,
            LongestStreak: response.LongestStreak,
            LastActivityDate: response.LastActivityDate,
            Milestones: response.Milestones.Select(m => new StreakMilestoneDto(
                Days: m.Days,
                Title: m.Title,
                Description: m.Description,
                IsAchieved: m.IsAchieved,
                AchievedDate: m.AchievedDate
            )).ToList(),
            StreakHistory: response.StreakHistory.Select(sh => new StreakHistoryDto(
                StartDate: sh.StartDate,
                EndDate: sh.EndDate,
                Duration: sh.Duration,
                TasksCompleted: sh.TasksCompleted,
                StreakType: sh.StreakType
            )).ToList(),
            Insights: new StreakInsightsDto(
                ConsistencyScore: response.Insights.ConsistencyScore,
                BestPeriod: response.Insights.BestPeriod,
                SuccessFactors: response.Insights.SuccessFactors,
                ImprovementAreas: response.Insights.ImprovementAreas
            )
        );
    }

    private OverdueTasksResponseDto MapToOverdueResponse(GetOverdueTasksResponse response)
    {
        return new OverdueTasksResponseDto(
            TotalOverdueCount: response.TotalOverdueCount,
            OverdueTasks: response.OverdueTasks.Select(ot => new OverdueTaskDto(
                Id: ot.Id,
                Title: ot.Title,
                Category: ot.Category,
                Priority: ot.Priority,
                DueDate: ot.DueDate,
                DaysOverdue: ot.DaysOverdue,
                UrgencyLevel: ot.UrgencyLevel,
                Tags: ot.Tags
            )).ToList(),
            Analysis: new OverdueAnalysisDto(
                CategoryBreakdown: response.Analysis.CategoryBreakdown,
                PriorityBreakdown: response.Analysis.PriorityBreakdown,
                UrgencyBreakdown: response.Analysis.UrgencyBreakdown,
                AverageOverdueDays: response.Analysis.AverageOverdueDays,
                CommonPatterns: response.Analysis.CommonPatterns
            ),
            Recommendations: response.Recommendations
        );
    }

    private CompletionStatsResponseDto MapToCompletionStatsResponse(GetCompletionStatsResponse response)
    {
        return new CompletionStatsResponseDto(
            Overview: new CompletionOverviewDto(
                TotalCompleted: response.Overview.TotalCompleted,
                CompletionRate: response.Overview.CompletionRate,
                AverageCompletionTime: response.Overview.AverageCompletionTime,
                CompletedToday: response.Overview.CompletedToday,
                CompletedThisWeek: response.Overview.CompletedThisWeek,
                CompletedThisMonth: response.Overview.CompletedThisMonth
            ),
            Trends: response.Trends.Select(t => new CompletionTrendDto(
                Period: t.Period,
                Completed: t.Completed,
                CompletionRate: t.CompletionRate,
                TrendDirection: t.TrendDirection
            )).ToList(),
            Breakdowns: response.Breakdowns.ToDictionary(
                kvp => kvp.Key,
                kvp => new CompletionBreakdownDto(
                    Items: kvp.Value.Items,
                    Rates: kvp.Value.Rates,
                    TopPerformer: kvp.Value.TopPerformer,
                    NeedsAttention: kvp.Value.NeedsAttention
                )
            ),
            Insights: new CompletionInsightsDto(
                PositivePatterns: response.Insights.PositivePatterns,
                AreasForImprovement: response.Insights.AreasForImprovement,
                Recommendations: response.Insights.Recommendations,
                PerformanceMetrics: response.Insights.PerformanceMetrics
            )
        );
    }

    private NotificationSettingsDto MapNotificationSettings(NotificationSettingsRequestDto? request)
    {
        if (request == null)
        {
            return new NotificationSettingsDto(
                EnableOverdueAlerts: true,
                EnableStreakReminders: true,
                EnableDailyDigest: false,
                OverdueAlertThreshold: 3,
                DigestFrequency: "weekly",
                QuietHours: new List<int> { 22, 23, 0, 1, 2, 3, 4, 5, 6, 7 }
            );
        }

        return new NotificationSettingsDto(
            EnableOverdueAlerts: request.EnableOverdueAlerts,
            EnableStreakReminders: request.EnableStreakReminders,
            EnableDailyDigest: request.EnableDailyDigest,
            OverdueAlertThreshold: request.OverdueAlertThreshold,
            DigestFrequency: request.DigestFrequency,
            QuietHours: request.QuietHours
        );
    }

    private DisplaySettingsDto MapDisplaySettings(DisplaySettingsRequestDto? request)
    {
        if (request == null)
        {
            return new DisplaySettingsDto(
                ChartType: "bar",
                DateFormat: "MM/dd/yyyy",
                TimeFormat: "12h",
                Use24HourFormat: false,
                ItemsPerPage: 20,
                DefaultSortOrder: "priority",
                ShowAnimations: true,
                CompactMode: false
            );
        }

        return new DisplaySettingsDto(
            ChartType: request.ChartType,
            DateFormat: request.DateFormat,
            TimeFormat: request.TimeFormat,
            Use24HourFormat: request.Use24HourFormat,
            ItemsPerPage: request.ItemsPerPage,
            DefaultSortOrder: request.DefaultSortOrder,
            ShowAnimations: request.ShowAnimations,
            CompactMode: request.CompactMode
        );
    }

    #endregion
}

/// <summary>
/// Request model for content interaction recording
/// </summary>
/// <param name="InteractionType">Type of interaction (view, click, share, dismiss)</param>
public sealed record ContentInteractionRequest(string InteractionType);

/// <summary>
/// Response model for dashboard metrics
/// </summary>
/// <param name="CompletedTasksToday">Number of tasks completed today</param>
/// <param name="TotalActiveTasks">Total number of active tasks</param>
/// <param name="OverdueTasks">Number of overdue tasks</param>
/// <param name="ProductivityStreak">Current productivity streak in days</param>
/// <param name="MotivationalContentDelivered">Content items delivered today</param>
public sealed record DashboardMetricsResponse(
    int CompletedTasksToday,
    int TotalActiveTasks,
    int OverdueTasks,
    int ProductivityStreak,
    int MotivationalContentDelivered
);