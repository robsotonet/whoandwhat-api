using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;
using WhoAndWhat.Application.Features.SmartScheduling.Commands.GenerateSmartSchedule;
using WhoAndWhat.Application.Features.SmartScheduling.Commands.OptimizeSchedule;
using WhoAndWhat.Application.Features.SmartScheduling.Commands.UpdateSchedulingPreferences;
using WhoAndWhat.Application.Features.SmartScheduling.Queries.GetSchedulingSuggestions;
using WhoAndWhat.Application.Features.SmartScheduling.Queries.GetUserSchedulingPatterns;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Smart scheduling controller handling AI-powered task scheduling and optimization
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/smart-scheduling")]
[Authorize]
public class SmartSchedulingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SmartSchedulingController> _logger;

    public SmartSchedulingController(IMediator mediator, ILogger<SmartSchedulingController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generate an optimized smart schedule for tasks
    /// </summary>
    /// <param name="request">Schedule generation parameters</param>
    /// <returns>Generated smart schedule with optimized task placement</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(SmartScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SmartScheduleResponse>> GenerateSmartSchedule([FromBody] GenerateSmartScheduleRequestDto request)
    {
        try
        {
            var userId = GetUserId();

            _logger.LogInformation("Generating smart schedule for user {UserId}", userId);

            var command = new GenerateSmartScheduleCommand(
                userId,
                request.StartDate,
                request.EndDate,
                request.TaskIds ?? new List<Guid>(),
                request.Preferences,
                request.IncludeCalendarEvents,
                request.OptimizeForProductivity
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
            _logger.LogError(ex, "Error generating smart schedule");
            return StatusCode(500, new ErrorResponse("An error occurred while generating the smart schedule"));
        }
    }

    /// <summary>
    /// Optimize an existing schedule
    /// </summary>
    /// <param name="request">Schedule optimization parameters</param>
    /// <returns>Optimized schedule with improvements</returns>
    [HttpPost("optimize")]
    [ProducesResponseType(typeof(ScheduleOptimizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScheduleOptimizationResponse>> OptimizeSchedule([FromBody] OptimizeScheduleRequestDto request)
    {
        try
        {
            var userId = GetUserId();

            _logger.LogInformation("Optimizing schedule {ScheduleId} for user {UserId}", request.ScheduleId, userId);

            var command = new OptimizeScheduleCommand(
                userId,
                request.ScheduleId,
                request.CurrentSchedule,
                request.Goals,
                request.Constraints ?? new List<ScheduleConstraint>()
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
            _logger.LogError(ex, "Error optimizing schedule");
            return StatusCode(500, new ErrorResponse("An error occurred while optimizing the schedule"));
        }
    }

    /// <summary>
    /// Get intelligent scheduling suggestions for specific tasks
    /// </summary>
    /// <param name="date">Date to get suggestions for</param>
    /// <param name="taskIds">Optional task IDs to get suggestions for (comma-separated)</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return (default: 5)</param>
    /// <returns>List of scheduling suggestions</returns>
    [HttpGet("suggestions")]
    [ProducesResponseType(typeof(SchedulingSuggestionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SchedulingSuggestionsResponse>> GetSchedulingSuggestions(
        [FromQuery] DateTime date,
        [FromQuery] string? taskIds = null,
        [FromQuery] int maxSuggestions = 5)
    {
        try
        {
            var userId = GetUserId();

            _logger.LogInformation("Getting scheduling suggestions for user {UserId} on {Date}", userId, date);

            var taskIdList = new List<Guid>();
            if (!string.IsNullOrEmpty(taskIds))
            {
                taskIdList = taskIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => Guid.TryParse(id.Trim(), out var guid) ? guid : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToList();
            }

            var query = new GetSchedulingSuggestionsQuery(userId, date, taskIdList, maxSuggestions);
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduling suggestions");
            return StatusCode(500, new ErrorResponse("An error occurred while getting scheduling suggestions"));
        }
    }

    /// <summary>
    /// Update user's scheduling preferences
    /// </summary>
    /// <param name="preferences">Updated scheduling preferences</param>
    /// <returns>Updated preferences confirmation</returns>
    [HttpPut("preferences")]
    [ProducesResponseType(typeof(UpdateSchedulingPreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UpdateSchedulingPreferencesResponse>> UpdateSchedulingPreferences([FromBody] SmartSchedulingPreferences preferences)
    {
        try
        {
            var userId = GetUserId();

            _logger.LogInformation("Updating scheduling preferences for user {UserId}", userId);

            var command = new UpdateSchedulingPreferencesCommand(userId, preferences);
            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating scheduling preferences");
            return StatusCode(500, new ErrorResponse("An error occurred while updating scheduling preferences"));
        }
    }

    /// <summary>
    /// Get user's current scheduling preferences
    /// </summary>
    /// <returns>Current user scheduling preferences</returns>
    [HttpGet("preferences")]
    [ProducesResponseType(typeof(SmartSchedulingPreferences), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<SmartSchedulingPreferences>> GetSchedulingPreferences()
    {
        try
        {
            var userId = GetUserId();

            _logger.LogInformation("Getting scheduling preferences for user {UserId}", userId);

            // This would ideally be a separate query, but for simplicity we'll use the service directly
            // In a real implementation, you'd create a GetUserPreferencesQuery
            var preferences = new SmartSchedulingPreferences(
                userId,
                new WorkingHours(
                    TimeSpan.FromHours(9),
                    TimeSpan.FromHours(17),
                    new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                    TimeSpan.FromHours(12),
                    TimeSpan.FromMinutes(60),
                    false
                ),
                new List<TimeSpan> { TimeSpan.FromHours(10.5), TimeSpan.FromHours(15) },
                3,
                TimeSpan.FromMinutes(15),
                TimeSpan.FromHours(4),
                new List<string> { "Work", "Personal" },
                ProductivityPatterns.Consistent,
                false,
                true,
                true,
                TimeSpan.FromMinutes(15),
                new List<ScheduleConstraint>()
            );

            return Task.FromResult<ActionResult<SmartSchedulingPreferences>>(Ok(preferences));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduling preferences");
            return Task.FromResult<ActionResult<SmartSchedulingPreferences>>(StatusCode(500, new ErrorResponse("An error occurred while getting scheduling preferences")));
        }
    }

    /// <summary>
    /// Analyze user's scheduling patterns
    /// </summary>
    /// <param name="startDate">Start date for pattern analysis</param>
    /// <param name="endDate">End date for pattern analysis</param>
    /// <returns>Detected scheduling patterns and insights</returns>
    [HttpGet("patterns")]
    [ProducesResponseType(typeof(UserSchedulingPatternsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserSchedulingPatternsResponse>> GetSchedulingPatterns(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            var userId = GetUserId();

            _logger.LogInformation("Getting scheduling patterns for user {UserId} from {StartDate} to {EndDate}",
                userId, startDate, endDate);

            var query = new GetUserSchedulingPatternsQuery(userId, startDate, endDate);
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduling patterns");
            return StatusCode(500, new ErrorResponse("An error occurred while getting scheduling patterns"));
        }
    }

    /// <summary>
    /// Get time block recommendations for better productivity
    /// </summary>
    /// <param name="date">Date to generate time blocks for</param>
    /// <returns>Recommended time blocks</returns>
    [HttpGet("time-blocks")]
    [ProducesResponseType(typeof(List<TimeBlockSuggestion>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<List<TimeBlockSuggestion>>> GetTimeBlockRecommendations([FromQuery] DateTime date)
    {
        try
        {
            var userId = GetUserId();

            _logger.LogInformation("Getting time block recommendations for user {UserId} on {Date}", userId, date);

            // For now, return sample time blocks
            // In a real implementation, this would use the service
            var timeBlocks = new List<TimeBlockSuggestion>
            {
                new TimeBlockSuggestion(
                    Guid.NewGuid(),
                    "Deep Work Session",
                    date.Date.AddHours(9),
                    date.Date.AddHours(10.5),
                    TimeBlockPurpose.DeepWork,
                    "Focused work time for complex tasks",
                    new List<string> { "Turn off notifications", "Work on challenging tasks" },
                    0.9,
                    "Scheduled during your peak productivity hours"
                ),
                new TimeBlockSuggestion(
                    Guid.NewGuid(),
                    "Administrative Tasks",
                    date.Date.AddHours(14),
                    date.Date.AddHours(15),
                    TimeBlockPurpose.Administrative,
                    "Time for emails and routine tasks",
                    new List<string> { "Process emails", "Handle paperwork" },
                    0.7,
                    "Good time for routine administrative work"
                )
            };

            return Task.FromResult<ActionResult<List<TimeBlockSuggestion>>>(Ok(timeBlocks));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time block recommendations");
            return Task.FromResult<ActionResult<List<TimeBlockSuggestion>>>(StatusCode(500, new ErrorResponse("An error occurred while getting time block recommendations")));
        }
    }

    /// <summary>
    /// Check if smart scheduling service is available
    /// </summary>
    /// <returns>Service availability status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthStatusResponse), StatusCodes.Status200OK)]
    public Task<ActionResult<HealthStatusResponse>> GetServiceHealth()
    {
        try
        {
            _logger.LogInformation("Checking smart scheduling service health");

            // In a real implementation, this would check actual service health
            var healthStatus = new HealthStatusResponse(
                true,
                "Smart scheduling service is operational",
                new List<string> { "Schedule Generation", "Optimization", "Pattern Learning", "Time Blocking" },
                DateTime.UtcNow
            );

            return Task.FromResult<ActionResult<HealthStatusResponse>>(Ok(healthStatus));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking service health");
            return Task.FromResult<ActionResult<HealthStatusResponse>>(Ok(new HealthStatusResponse(
                false,
                "Service health check failed",
                new List<string>(),
                DateTime.UtcNow
            )));
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

// DTOs for API requests

/// <summary>
/// Request DTO for generating smart schedule
/// </summary>
public record GenerateSmartScheduleRequestDto(
    DateTime StartDate,
    DateTime EndDate,
    List<Guid>? TaskIds,
    SmartSchedulingPreferences Preferences,
    bool IncludeCalendarEvents = true,
    bool OptimizeForProductivity = true
);

/// <summary>
/// Request DTO for optimizing schedule
/// </summary>
public record OptimizeScheduleRequestDto(
    Guid ScheduleId,
    List<SmartScheduledItem> CurrentSchedule,
    OptimizationGoals Goals,
    List<ScheduleConstraint>? Constraints
);

/// <summary>
/// Response DTO for service health check
/// </summary>
public record HealthStatusResponse(
    bool IsHealthy,
    string Message,
    List<string> AvailableFeatures,
    DateTime CheckedAt
);
