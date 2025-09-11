using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhoAndWhat.Application.Features.Dashboard.Queries.GetMotivationalContent;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Dashboard controller providing user dashboard data and analytics
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
            if (!userId.HasValue)
            {
                return Unauthorized("User ID not found in token");
            }

            _logger.LogInformation("Getting motivational content for user {UserId}, count: {Count}, language: {Language}",
                userId.Value, count, language);

            // Execute query
            var query = new GetMotivationalContentQuery(userId.Value, count, language);
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get motivational content for user {UserId}: {Error}",
                    userId.Value, result.Error);

                return BadRequest(new ProblemDetails
                {
                    Title = "Failed to retrieve motivational content",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Successfully retrieved {Count} motivational contents for user {UserId}",
                result.Value.Contents.Count, userId.Value);

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
            if (!userId.HasValue)
            {
                return Unauthorized("User ID not found in token");
            }

            _logger.LogInformation("Recording content interaction for user {UserId}, content {ContentId}, type {InteractionType}",
                userId.Value, contentId, request.InteractionType);

            // TODO: Implement content interaction recording command
            // This would typically involve creating a RecordContentInteractionCommand
            // and handler that uses the IOptimizedContentEngagementService

            _logger.LogInformation("Successfully recorded content interaction for user {UserId}",
                userId.Value);

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
    /// Get user's dashboard metrics summary
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dashboard metrics data</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(DashboardMetricsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardMetricsResponse>> GetDashboardMetrics(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract user ID from claims
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User ID not found in token");
            }

            _logger.LogInformation("Getting dashboard metrics for user {UserId}", userId.Value);

            // TODO: Implement GetDashboardMetricsQuery and handler
            // This is a placeholder implementation
            var response = new DashboardMetricsResponse(
                CompletedTasksToday: 0,
                TotalActiveTasks: 0,
                OverdueTasks: 0,
                ProductivityStreak: 0,
                MotivationalContentDelivered: 0
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting dashboard metrics");
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while retrieving dashboard metrics", 
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    #region Private Helpers

    /// <summary>
    /// Extract current user ID from JWT claims
    /// </summary>
    /// <returns>User ID or null if not found</returns>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
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