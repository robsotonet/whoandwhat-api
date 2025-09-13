using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.AI;
using WhoAndWhat.Application.Features.AIPlanning.Commands.GenerateDayPlan;
using WhoAndWhat.Application.Features.AIPlanning.Commands.PrioritizeTasks;
using WhoAndWhat.Application.Features.AIPlanning.Queries.GetBreakRecommendations;
using WhoAndWhat.Application.Features.AIPlanning.Queries.GetProductivityInsights;
using WhoAndWhat.Application.Features.AIPlanning.Queries.GetTaskSuggestions;
using WhoAndWhat.Application.Features.AIPlanning.Queries.GetTaskTimeEstimates;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// AI Planning controller handling AI-powered task planning, suggestions, and productivity insights
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ai")]
[Authorize]
public class AIPlanningController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AIPlanningController> _logger;

    public AIPlanningController(IMediator mediator, ILogger<AIPlanningController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generate an AI-powered day plan with optimized task scheduling
    /// </summary>
    /// <param name="request">Day plan generation parameters</param>
    /// <returns>AI-generated day plan with scheduled tasks and time blocks</returns>
    [HttpPost("plan-day")]
    [ProducesResponseType(typeof(AIGeneratedPlan), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AIGeneratedPlan>> GenerateDayPlan([FromBody] GenerateDayPlanRequest request)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Generating AI day plan for user {UserId} on {Date}", userId, request.PlanDate);

            var command = new GenerateDayPlanCommand(
                userId,
                request.PlanDate,
                request.TaskIds ?? new List<Guid>(),
                request.Preferences,
                request.IncludeCalendarEvents,
                request.FocusMode
            );

            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI day plan");
            return StatusCode(500, new ErrorResponse("An error occurred while generating the day plan"));
        }
    }

    /// <summary>
    /// Get AI-powered task suggestions based on user patterns and current context
    /// </summary>
    /// <param name="contextType">Type of context for suggestions (optional)</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return (default: 10)</param>
    /// <param name="includeCategories">Comma-separated list of categories to include (optional)</param>
    /// <returns>List of AI-generated task suggestions</returns>
    [HttpGet("suggestions")]
    [ProducesResponseType(typeof(TaskSuggestionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TaskSuggestionsResponse>> GetTaskSuggestions(
        [FromQuery] string? contextType = null,
        [FromQuery] int maxSuggestions = 10,
        [FromQuery] string? includeCategories = null)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Getting AI task suggestions for user {UserId}", userId);

            var categories = new List<string>();
            if (!string.IsNullOrEmpty(includeCategories))
            {
                categories = includeCategories.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToList();
            }

            var query = new GetTaskSuggestionsQuery(userId, contextType, maxSuggestions, categories);
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task suggestions");
            return StatusCode(500, new ErrorResponse("An error occurred while getting task suggestions"));
        }
    }

    /// <summary>
    /// Use AI to analyze and suggest task prioritization
    /// </summary>
    /// <param name="request">Task prioritization parameters</param>
    /// <returns>AI-powered task priority suggestions with reasoning</returns>
    [HttpPost("prioritize")]
    [ProducesResponseType(typeof(TaskPrioritizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TaskPrioritizationResponse>> PrioritizeTasks([FromBody] TaskPrioritizationRequest request)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Running AI task prioritization for user {UserId} with {TaskCount} tasks", 
                userId, request.TaskAnalysisContexts.Count);

            var command = new PrioritizeTasksCommand(
                userId,
                request.TaskAnalysisContexts,
                request.PriorityContext,
                request.MaxPrioritySuggestions
            );

            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI task prioritization");
            return StatusCode(500, new ErrorResponse("An error occurred during task prioritization"));
        }
    }

    /// <summary>
    /// Get AI-powered time estimates for tasks based on historical data and complexity
    /// </summary>
    /// <param name="taskIds">Comma-separated list of task IDs to estimate (optional - if not provided, estimates recent tasks)</param>
    /// <param name="includeConfidenceInterval">Include confidence intervals in estimates (default: true)</param>
    /// <returns>AI-generated time estimates with confidence levels</returns>
    [HttpGet("time-estimates")]
    [ProducesResponseType(typeof(TaskTimeEstimatesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TaskTimeEstimatesResponse>> GetTaskTimeEstimates(
        [FromQuery] string? taskIds = null,
        [FromQuery] bool includeConfidenceInterval = true)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Getting AI time estimates for user {UserId}", userId);

            var taskIdList = new List<Guid>();
            if (!string.IsNullOrEmpty(taskIds))
            {
                taskIdList = taskIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => Guid.TryParse(id.Trim(), out var guid) ? guid : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToList();
            }

            var query = new GetTaskTimeEstimatesQuery(userId, taskIdList, includeConfidenceInterval);
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task time estimates");
            return StatusCode(500, new ErrorResponse("An error occurred while getting time estimates"));
        }
    }

    /// <summary>
    /// Get AI-generated productivity insights and patterns analysis
    /// </summary>
    /// <param name="startDate">Start date for analysis period</param>
    /// <param name="endDate">End date for analysis period</param>
    /// <param name="analysisType">Type of analysis to perform (optional)</param>
    /// <returns>Comprehensive productivity insights with actionable recommendations</returns>
    [HttpGet("insights")]
    [ProducesResponseType(typeof(ProductivityInsights), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductivityInsights>> GetProductivityInsights(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string? analysisType = null)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Getting productivity insights for user {UserId} from {StartDate} to {EndDate}", 
                userId, startDate, endDate);

            var timeframe = new TimeframeAnalysis(
                startDate,
                endDate,
                AnalysisGranularity.Daily,
                new List<string> { "productivity", "patterns", "efficiency" }
            );

            var query = new GetProductivityInsightsQuery(userId, timeframe, analysisType);
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting productivity insights");
            return StatusCode(500, new ErrorResponse("An error occurred while getting productivity insights"));
        }
    }

    /// <summary>
    /// Get AI-powered break recommendations based on current workload and patterns
    /// </summary>
    /// <param name="analysisDate">Date to analyze for break recommendations (default: today)</param>
    /// <param name="includeActivitySuggestions">Include specific activity suggestions for breaks (default: true)</param>
    /// <returns>Intelligent break recommendations with timing and activity suggestions</returns>
    [HttpPost("breaks")]
    [ProducesResponseType(typeof(BreakRecommendationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BreakRecommendationsResponse>> GetBreakRecommendations(
        [FromBody] BreakAnalysisRequest? request = null)
    {
        try
        {
            var userId = GetUserId();
            var analysisDate = request?.AnalysisDate ?? DateTime.Today;
            
            _logger.LogInformation("Getting break recommendations for user {UserId} on {Date}", userId, analysisDate);

            var workloadAnalysis = new WorkloadAnalysis(
                analysisDate,
                request?.TasksCompleted ?? 0,
                request?.TasksRemaining ?? 0,
                request?.StressLevel ?? 0.5,
                request?.ContinuousWorkTime ?? TimeSpan.FromHours(2),
                request?.IntensityIndicators ?? new List<string>()
            );

            var query = new GetBreakRecommendationsQuery(
                userId, 
                workloadAnalysis, 
                request?.IncludeActivitySuggestions ?? true
            );
            
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting break recommendations");
            return StatusCode(500, new ErrorResponse("An error occurred while getting break recommendations"));
        }
    }

    /// <summary>
    /// Check AI service health and availability
    /// </summary>
    /// <returns>AI service health status with diagnostics</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(AIServiceHealthStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<AIServiceHealthStatus>> GetServiceHealth()
    {
        try
        {
            _logger.LogInformation("Checking AI service health");

            // For now, return mock health status
            // In a real implementation, this would check actual AI service health
            var healthStatus = new AIServiceHealthStatus(
                true,
                TimeSpan.FromMilliseconds(150),
                "1.0.0",
                new List<string> 
                { 
                    "Day Planning", 
                    "Task Suggestions", 
                    "Priority Analysis", 
                    "Time Estimation", 
                    "Productivity Insights", 
                    "Break Recommendations" 
                },
                new List<HealthCheckResult>
                {
                    new HealthCheckResult("AI Model", true, null, TimeSpan.FromMilliseconds(50), new Dictionary<string, object>()),
                    new HealthCheckResult("Pattern Analysis", true, null, TimeSpan.FromMilliseconds(75), new Dictionary<string, object>()),
                    new HealthCheckResult("Time Estimation", true, null, TimeSpan.FromMilliseconds(25), new Dictionary<string, object>())
                },
                DateTime.UtcNow
            );

            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking AI service health");
            return Ok(new AIServiceHealthStatus(
                false,
                TimeSpan.Zero,
                "1.0.0",
                new List<string>(),
                new List<HealthCheckResult>
                {
                    new HealthCheckResult("Health Check", false, ex.Message, TimeSpan.Zero, new Dictionary<string, object>())
                },
                DateTime.UtcNow
            ));
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid or missing user ID");
        }
        return userId;
    }
}

// Request DTOs for AI Planning API

/// <summary>
/// Request for generating AI day plan
/// </summary>
public record GenerateDayPlanRequest(
    DateTime PlanDate,
    List<Guid>? TaskIds,
    UserPlanningPreferences Preferences,
    bool IncludeCalendarEvents = true,
    bool FocusMode = false
);

/// <summary>
/// Request for task prioritization
/// </summary>
public record TaskPrioritizationRequest(
    List<TaskAnalysisContext> TaskAnalysisContexts,
    PriorityAnalysisContext PriorityContext,
    int MaxPrioritySuggestions = 10
);

/// <summary>
/// Request for break analysis
/// </summary>
public record BreakAnalysisRequest(
    DateTime? AnalysisDate = null,
    int? TasksCompleted = null,
    int? TasksRemaining = null,
    double? StressLevel = null,
    TimeSpan? ContinuousWorkTime = null,
    List<string>? IntensityIndicators = null,
    bool? IncludeActivitySuggestions = null
);

// Response DTOs for AI Planning API

/// <summary>
/// Response with AI task suggestions
/// </summary>
public record TaskSuggestionsResponse(
    List<TaskPrioritySuggestion> Suggestions,
    int TotalSuggestions,
    string SuggestionContext,
    DateTime GeneratedAt
);

/// <summary>
/// Response with task prioritization results
/// </summary>
public record TaskPrioritizationResponse(
    List<TaskPrioritySuggestion> PrioritySuggestions,
    PriorityAnalysisContext AnalysisContext,
    double OverallConfidence,
    List<string> AnalysisNotes,
    DateTime GeneratedAt
);

/// <summary>
/// Response with time estimates
/// </summary>
public record TaskTimeEstimatesResponse(
    List<TaskTimeEstimate> TimeEstimates,
    UserHistoricalPerformance UserPerformance,
    List<string> EstimationNotes,
    DateTime GeneratedAt
);

/// <summary>
/// Response with break recommendations
/// </summary>
public record BreakRecommendationsResponse(
    List<BreakRecommendation> Recommendations,
    WorkloadAnalysis WorkloadAnalysis,
    List<string> GeneralTips,
    DateTime GeneratedAt
);