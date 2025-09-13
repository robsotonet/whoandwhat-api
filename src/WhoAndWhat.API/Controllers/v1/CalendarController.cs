using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Application.Features.Calendar.Commands.ConnectProvider;
using WhoAndWhat.Application.Features.Calendar.Commands.ResolveConflict;
using WhoAndWhat.Application.Features.Calendar.Commands.TriggerSync;
using WhoAndWhat.Application.Features.Calendar.Queries.GetAvailableProviders;
using WhoAndWhat.Application.Features.Calendar.Queries.GetCalendarConflicts;
using WhoAndWhat.Application.Queries.GetCalendarView;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Calendar controller for managing calendar views, synchronization, and provider integration
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/calendar")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CalendarController> _logger;

    public CalendarController(IMediator mediator, ILogger<CalendarController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get monthly calendar view with tasks and events
    /// </summary>
    /// <param name="year">Year for the calendar view</param>
    /// <param name="month">Month for the calendar view (1-12)</param>
    /// <param name="includeEvents">Include external calendar events (default: true)</param>
    /// <param name="includeTasks">Include tasks in the view (default: true)</param>
    /// <returns>Monthly calendar view with events and tasks</returns>
    [HttpGet("{year:int}/{month:int}")]
    [ProducesResponseType(typeof(CalendarViewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalendarViewResponse>> GetMonthlyCalendar(
        [FromRoute] int year,
        [FromRoute] int month,
        [FromQuery] bool includeEvents = true,
        [FromQuery] bool includeTasks = true)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Getting monthly calendar for user {UserId} - {Year}/{Month}", userId, year, month);

            // Validate date parameters
            if (year < 2020 || year > 2030)
            {
                return BadRequest(new ErrorResponse("Year must be between 2020 and 2030"));
            }

            if (month < 1 || month > 12)
            {
                return BadRequest(new ErrorResponse("Month must be between 1 and 12"));
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var query = new GetCalendarViewQuery(
                userId,
                startDate,
                endDate,
                CalendarViewType.Monthly,
                includeEvents,
                includeTasks
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
            _logger.LogError(ex, "Error getting monthly calendar");
            return StatusCode(500, new ErrorResponse("An error occurred while getting the calendar view"));
        }
    }

    /// <summary>
    /// Get weekly calendar view with tasks and events
    /// </summary>
    /// <param name="date">Date within the week to display (YYYY-MM-DD)</param>
    /// <param name="includeEvents">Include external calendar events (default: true)</param>
    /// <param name="includeTasks">Include tasks in the view (default: true)</param>
    /// <returns>Weekly calendar view with events and tasks</returns>
    [HttpGet("week/{date}")]
    [ProducesResponseType(typeof(CalendarViewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalendarViewResponse>> GetWeeklyCalendar(
        [FromRoute] DateTime date,
        [FromQuery] bool includeEvents = true,
        [FromQuery] bool includeTasks = true)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Getting weekly calendar for user {UserId} - week of {Date}", userId, date.Date);

            // Calculate start of week (Monday)
            var startOfWeek = date.Date.AddDays(-(int)date.DayOfWeek + (int)DayOfWeek.Monday);
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                startOfWeek = startOfWeek.AddDays(-7);
            }
            var endOfWeek = startOfWeek.AddDays(6);

            var query = new GetCalendarViewQuery(
                userId,
                startOfWeek,
                endOfWeek,
                CalendarViewType.Weekly,
                includeEvents,
                includeTasks
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
            _logger.LogError(ex, "Error getting weekly calendar");
            return StatusCode(500, new ErrorResponse("An error occurred while getting the calendar view"));
        }
    }

    /// <summary>
    /// Get daily calendar view with tasks and events
    /// </summary>
    /// <param name="date">Date for the daily view (YYYY-MM-DD)</param>
    /// <param name="includeEvents">Include external calendar events (default: true)</param>
    /// <param name="includeTasks">Include tasks in the view (default: true)</param>
    /// <param name="includeTimeBlocks">Include time block suggestions (default: false)</param>
    /// <returns>Daily calendar view with events and tasks</returns>
    [HttpGet("day/{date}")]
    [ProducesResponseType(typeof(CalendarViewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalendarViewResponse>> GetDailyCalendar(
        [FromRoute] DateTime date,
        [FromQuery] bool includeEvents = true,
        [FromQuery] bool includeTasks = true,
        [FromQuery] bool includeTimeBlocks = false)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Getting daily calendar for user {UserId} - {Date}", userId, date.Date);

            var query = new GetCalendarViewQuery(
                userId,
                date.Date,
                date.Date,
                CalendarViewType.Daily,
                includeEvents,
                includeTasks,
                includeTimeBlocks
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
            _logger.LogError(ex, "Error getting daily calendar");
            return StatusCode(500, new ErrorResponse("An error occurred while getting the calendar view"));
        }
    }

    /// <summary>
    /// Trigger manual calendar synchronization with external providers
    /// </summary>
    /// <param name="request">Sync trigger parameters</param>
    /// <returns>Synchronization result with status and statistics</returns>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(CalendarSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalendarSyncResult>> TriggerSync([FromBody] TriggerSyncRequest request)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Triggering calendar sync for user {UserId} with provider {Provider}", 
                userId, request.Provider?.ToString() ?? "all");

            var command = new TriggerCalendarSyncCommand(
                userId,
                request.Provider,
                request.ForceFullSync,
                request.SyncDirection
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
            _logger.LogError(ex, "Error triggering calendar sync");
            return StatusCode(500, new ErrorResponse("An error occurred while triggering calendar sync"));
        }
    }

    /// <summary>
    /// Get available calendar providers and their configuration status
    /// </summary>
    /// <returns>List of available calendar providers with configuration details</returns>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(AvailableProvidersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AvailableProvidersResponse>> GetAvailableProviders()
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Getting available calendar providers for user {UserId}", userId);

            var query = new GetAvailableProvidersQuery(userId);
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available providers");
            return StatusCode(500, new ErrorResponse("An error occurred while getting available providers"));
        }
    }

    /// <summary>
    /// Connect to an external calendar provider
    /// </summary>
    /// <param name="request">Provider connection parameters</param>
    /// <returns>Connection result with authorization details if required</returns>
    [HttpPost("connect")]
    [ProducesResponseType(typeof(CalendarProviderConfigResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalendarProviderConfigResult>> ConnectProvider([FromBody] ConnectProviderRequest request)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Connecting to calendar provider {Provider} for user {UserId}", 
                request.Provider, userId);

            var command = new ConnectCalendarProviderCommand(
                userId,
                request.Provider,
                request.Configuration
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
            _logger.LogError(ex, "Error connecting calendar provider");
            return StatusCode(500, new ErrorResponse("An error occurred while connecting the calendar provider"));
        }
    }

    /// <summary>
    /// Get calendar conflicts that require user attention
    /// </summary>
    /// <param name="provider">Filter by specific provider (optional)</param>
    /// <param name="severityLevel">Minimum severity level to include (optional)</param>
    /// <param name="includeResolved">Include already resolved conflicts (default: false)</param>
    /// <returns>List of calendar conflicts with resolution options</returns>
    [HttpGet("conflicts")]
    [ProducesResponseType(typeof(CalendarConflictsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalendarConflictsResponse>> GetCalendarConflicts(
        [FromQuery] CalendarProvider? provider = null,
        [FromQuery] ConflictSeverity? severityLevel = null,
        [FromQuery] bool includeResolved = false)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Getting calendar conflicts for user {UserId}", userId);

            var filterOptions = new ConflictFilterOptions(
                provider.HasValue ? new List<CalendarProvider> { provider.Value } : null,
                severityLevel.HasValue ? new List<ConflictSeverity> { severityLevel.Value } : null,
                null, // FromDate
                null, // ToDate
                !includeResolved, // OnlyUnresolved
                provider
            );

            var query = new GetCalendarConflictsQuery(userId, filterOptions);
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
            {
                return BadRequest(new ErrorResponse(result.Error));
            }

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting calendar conflicts");
            return StatusCode(500, new ErrorResponse("An error occurred while getting calendar conflicts"));
        }
    }

    /// <summary>
    /// Resolve a calendar conflict with user-specified action
    /// </summary>
    /// <param name="request">Conflict resolution parameters</param>
    /// <returns>Resolution result with affected events</returns>
    [HttpPost("resolve")]
    [ProducesResponseType(typeof(ConflictResolutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConflictResolutionResult>> ResolveConflict([FromBody] ResolveConflictRequest request)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Resolving calendar conflict {ConflictId} for user {UserId} with action {Action}",
                request.ConflictId, userId, request.Resolution.Action);

            var command = new ResolveCalendarConflictCommand(
                userId,
                request.ConflictId,
                request.Resolution
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
            _logger.LogError(ex, "Error resolving calendar conflict");
            return StatusCode(500, new ErrorResponse("An error occurred while resolving the calendar conflict"));
        }
    }

    /// <summary>
    /// Get calendar service health status and sync information
    /// </summary>
    /// <returns>Calendar service health with provider status details</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(CalendarSyncHealthStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarSyncHealthStatus>> GetCalendarHealth()
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Checking calendar health for user {UserId}", userId);

            // For now, return mock health status
            // In a real implementation, this would check actual service health
            var healthStatus = new CalendarSyncHealthStatus(
                true,
                TimeSpan.FromMilliseconds(200),
                new Dictionary<CalendarProvider, ProviderHealthInfo>
                {
                    { CalendarProvider.Google, new ProviderHealthInfo(true, TimeSpan.FromMilliseconds(150), 1000, DateTime.UtcNow.AddHours(1), null, DateTime.UtcNow) },
                    { CalendarProvider.Outlook, new ProviderHealthInfo(true, TimeSpan.FromMilliseconds(180), 800, DateTime.UtcNow.AddHours(1), null, DateTime.UtcNow) },
                    { CalendarProvider.ICloud, new ProviderHealthInfo(true, TimeSpan.FromMilliseconds(220), 500, DateTime.UtcNow.AddHours(1), null, DateTime.UtcNow) }
                },
                3, // Active connections
                0, // Pending syncs
                new List<string>(), // No issues
                DateTime.UtcNow
            );

            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking calendar health");
            return Ok(new CalendarSyncHealthStatus(
                false,
                TimeSpan.Zero,
                new Dictionary<CalendarProvider, ProviderHealthInfo>(),
                0,
                0,
                new List<string> { "Health check failed" },
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

// Request DTOs for Calendar API

/// <summary>
/// Request for triggering calendar sync
/// </summary>
public record TriggerSyncRequest(
    CalendarProvider? Provider = null,
    bool ForceFullSync = false,
    SyncDirection SyncDirection = SyncDirection.Bidirectional
);

/// <summary>
/// Request for connecting to a calendar provider
/// </summary>
public record ConnectProviderRequest(
    CalendarProvider Provider,
    CalendarProviderConfiguration Configuration
);

/// <summary>
/// Request for resolving a calendar conflict
/// </summary>
public record ResolveConflictRequest(
    Guid ConflictId,
    ConflictResolution Resolution
);

// Response DTOs for Calendar API

/// <summary>
/// Response with calendar view data
/// </summary>
public record CalendarViewResponse(
    DateTime StartDate,
    DateTime EndDate,
    CalendarViewType ViewType,
    List<CalendarItem> Items,
    CalendarViewMetadata Metadata,
    DateTime GeneratedAt
);

/// <summary>
/// Calendar item for display in views
/// </summary>
public record CalendarItem(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    CalendarItemType Type,
    string? Category,
    string? Priority,
    CalendarItemStatus Status,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Calendar view metadata
/// </summary>
public record CalendarViewMetadata(
    int TotalItems,
    int TaskCount,
    int EventCount,
    int ConflictCount,
    List<string> ConnectedProviders,
    DateTime LastSyncTime
);

/// <summary>
/// Response with available providers
/// </summary>
public record AvailableProvidersResponse(
    List<AvailableCalendarProvider> Providers,
    List<CalendarProvider> ConnectedProviders,
    DateTime RetrievedAt
);

/// <summary>
/// Response with calendar conflicts
/// </summary>
public record CalendarConflictsResponse(
    List<CalendarSyncConflict> Conflicts,
    int TotalConflicts,
    int UnresolvedCount,
    ConflictStatistics Statistics,
    DateTime GeneratedAt
);

// Enums for Calendar API

public enum CalendarViewType
{
    Daily,
    Weekly,
    Monthly
}

public enum CalendarItemType
{
    Task,
    Event,
    TimeBlock,
    Reminder
}

public enum CalendarItemStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
    Overdue
}