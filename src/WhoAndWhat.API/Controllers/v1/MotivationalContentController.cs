using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.API.Controllers.v1;

/// <summary>
/// Controller for managing motivational content, A/B testing, and content analytics
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/content")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class MotivationalContentController : ControllerBase
{
    private readonly IMotivationalContentService _contentService;
    private readonly IContentABTestingService _abTestingService;
    private readonly IRepository<MotivationalContent> _contentRepository;
    private readonly IRepository<UserContentPreferences> _preferencesRepository;
    private readonly ILogger<MotivationalContentController> _logger;

    public MotivationalContentController(
        IMotivationalContentService contentService,
        IContentABTestingService abTestingService,
        IRepository<MotivationalContent> contentRepository,
        IRepository<UserContentPreferences> preferencesRepository,
        ILogger<MotivationalContentController> logger)
    {
        _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));
        _abTestingService = abTestingService ?? throw new ArgumentNullException(nameof(abTestingService));
        _contentRepository = contentRepository ?? throw new ArgumentNullException(nameof(contentRepository));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get personalized motivational content for the current user
    /// </summary>
    /// <param name="contentType">Preferred content type</param>
    /// <param name="category">Preferred category</param>
    /// <param name="maxItems">Maximum number of items to return</param>
    /// <returns>Personalized content items</returns>
    [HttpGet("personalized")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<IEnumerable<PersonalizedContentResult>>> GetPersonalizedContent(
        [FromQuery] MotivationalContentType? contentType = null,
        [FromQuery] ContentCategory? category = null,
        [FromQuery] int maxItems = 1)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var context = new ContentSelectionContext
            {
                PreferredType = contentType,
                PreferredCategory = category,
                DeliveryChannel = ContentDeliveryChannel.API,
                TriggerEvent = "api_request"
            };

            IEnumerable<PersonalizedContentResult> results;

            if (maxItems <= 1)
            {
                var singleResult = await _contentService.GetPersonalizedContentAsync(userId.Value, context);
                results = singleResult != null ? new[] { singleResult } : Enumerable.Empty<PersonalizedContentResult>();
            }
            else
            {
                results = await _contentService.GetPersonalizedContentBatchAsync(userId.Value, maxItems, context);
            }

            if (!results.Any())
            {
                return NoContent();
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting personalized content for user");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving content");
        }
    }

    /// <summary>
    /// Record user engagement with motivational content
    /// </summary>
    /// <param name="request">Engagement recording request</param>
    /// <returns>Success status</returns>
    [HttpPost("engagement")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RecordEngagement([FromBody] ContentEngagementRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var success = await _contentService.LogContentEngagementAsync(
                userId.Value,
                request.ContentId,
                request.EngagementType,
                request.DeliveryLogId,
                request.EngagementMetadata,
                request.ViewDuration);

            if (success)
            {
                return Ok(new { message = "Engagement recorded successfully", timestamp = DateTime.UtcNow });
            }

            return BadRequest("Failed to record engagement");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording content engagement");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while recording engagement");
        }
    }

    /// <summary>
    /// Get user's content preferences
    /// </summary>
    /// <returns>User content preferences</returns>
    [HttpGet("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserContentPreferencesDto>> GetContentPreferences()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var preferences = await _preferencesRepository.GetByConditionAsync(
                p => p.UserId == userId.Value);

            if (preferences == null)
            {
                preferences = UserContentPreferences.CreateDefault(userId.Value);
                await _preferencesRepository.AddAsync(preferences);
            }

            return Ok(MapToPreferencesDto(preferences));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content preferences");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving preferences");
        }
    }

    /// <summary>
    /// Update user's content preferences
    /// </summary>
    /// <param name="request">Updated preferences</param>
    /// <returns>Updated preferences</returns>
    [HttpPut("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserContentPreferencesDto>> UpdateContentPreferences([FromBody] UpdateContentPreferencesRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var preferences = await _preferencesRepository.GetByConditionAsync(
                p => p.UserId == userId.Value);

            if (preferences == null)
            {
                preferences = UserContentPreferences.CreateDefault(userId.Value);
            }

            // Update preferences based on request
            if (request.IsContentEnabled.HasValue)
            {
                preferences.SetContentEnabled(request.IsContentEnabled.Value);
            }

            if (request.PreferredFrequency.HasValue)
            {
                preferences.SetPreferredFrequency(request.PreferredFrequency.Value);
            }

            if (request.PreferredContentTypes?.Any() == true)
            {
                preferences.SetPreferredContentTypes(request.PreferredContentTypes);
            }

            if (request.PreferredCategories?.Any() == true)
            {
                preferences.SetPreferredCategories(request.PreferredCategories);
            }

            if (request.PreferredChannels?.Any() == true)
            {
                preferences.SetPreferredChannels(request.PreferredChannels);
            }

            if (request.MaxDailyContent.HasValue && request.MaxWeeklyContent.HasValue)
            {
                preferences.SetContentLimits(request.MaxDailyContent.Value, request.MaxWeeklyContent.Value);
            }

            if (request.AllowWeekends.HasValue || request.AllowAfterHours.HasValue)
            {
                preferences.SetSchedulingPreferences(
                    request.AllowWeekends ?? preferences.AllowWeekends,
                    request.AllowAfterHours ?? preferences.AllowAfterHours);
            }

            if (!string.IsNullOrWhiteSpace(request.TimeZone))
            {
                preferences.SetTimeZone(request.TimeZone);
            }

            await _preferencesRepository.UpdateAsync(preferences);

            return Ok(MapToPreferencesDto(preferences));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating content preferences");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating preferences");
        }
    }

    /// <summary>
    /// Temporarily pause content delivery
    /// </summary>
    /// <param name="request">Pause request with duration</param>
    /// <returns>Success status</returns>
    [HttpPost("preferences/pause")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> PauseContent([FromBody] PauseContentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var preferences = await _preferencesRepository.GetByConditionAsync(
                p => p.UserId == userId.Value);

            if (preferences == null)
            {
                return NotFound("User preferences not found");
            }

            var pauseUntil = request.PauseUntil ?? DateTime.UtcNow.AddHours(request.PauseHours ?? 24);
            preferences.PauseContentUntil(pauseUntil);
            
            await _preferencesRepository.UpdateAsync(preferences);

            return Ok(new { 
                message = "Content paused successfully", 
                pausedUntil = pauseUntil,
                timestamp = DateTime.UtcNow 
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing content");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while pausing content");
        }
    }

    /// <summary>
    /// Resume content delivery
    /// </summary>
    /// <returns>Success status</returns>
    [HttpPost("preferences/resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResumeContent()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var preferences = await _preferencesRepository.GetByConditionAsync(
                p => p.UserId == userId.Value);

            if (preferences == null)
            {
                return NotFound("User preferences not found");
            }

            preferences.ResumeContent();
            await _preferencesRepository.UpdateAsync(preferences);

            return Ok(new { message = "Content resumed successfully", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming content");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while resuming content");
        }
    }

    /// <summary>
    /// Get content performance analytics (Admin only)
    /// </summary>
    /// <param name="contentId">Content ID</param>
    /// <param name="startDate">Start date for analytics</param>
    /// <param name="endDate">End date for analytics</param>
    /// <returns>Content performance metrics</returns>
    [HttpGet("analytics/{contentId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentPerformanceMetrics>> GetContentAnalytics(
        Guid contentId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;

            var metrics = await _contentService.GetContentPerformanceAsync(contentId, start, end);
            
            if (metrics.TotalDeliveries == 0)
            {
                return NotFound("No performance data found for the specified content and date range");
            }

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content analytics for content {ContentId}", contentId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving analytics");
        }
    }

    /// <summary>
    /// Get all motivational content (Admin only)
    /// </summary>
    /// <param name="pageNumber">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="contentType">Filter by content type</param>
    /// <param name="category">Filter by category</param>
    /// <param name="isActive">Filter by active status</param>
    /// <returns>List of motivational content</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<MotivationalContentDto>>> GetAllContent(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] MotivationalContentType? contentType = null,
        [FromQuery] ContentCategory? category = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var content = await _contentRepository.GetAllAsync();
            
            // Apply filters
            if (contentType.HasValue)
            {
                content = content.Where(c => c.ContentType == contentType.Value);
            }

            if (category.HasValue)
            {
                content = content.Where(c => c.Category == category.Value);
            }

            if (isActive.HasValue)
            {
                content = content.Where(c => c.IsActive == isActive.Value);
            }

            var totalCount = content.Count();
            var pagedContent = content
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToContentDto)
                .ToList();

            var result = new PaginatedResult<MotivationalContentDto>
            {
                Items = pagedContent,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all motivational content");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving content");
        }
    }

    /// <summary>
    /// Create new motivational content (Admin only)
    /// </summary>
    /// <param name="request">Content creation request</param>
    /// <returns>Created content</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MotivationalContentDto>> CreateContent([FromBody] CreateMotivationalContentRequest request)
    {
        try
        {
            var content = MotivationalContent.Create(
                request.Title,
                request.Message,
                request.ContentType,
                request.Category,
                request.TargetConditions,
                request.Priority);

            if (!string.IsNullOrEmpty(request.ImageUrl))
            {
                content.SetImageUrl(request.ImageUrl);
            }

            if (!string.IsNullOrEmpty(request.ActionUrl))
            {
                content.SetAction(request.ActionUrl, request.ActionText);
            }

            if (request.StartDate.HasValue || request.EndDate.HasValue)
            {
                content.SetActivePeriod(request.StartDate, request.EndDate);
            }

            if (request.SchedulingRules?.Any() == true)
            {
                content.SetSchedulingRules(request.SchedulingRules);
            }

            await _contentRepository.AddAsync(content);

            var result = MapToContentDto(content);
            return CreatedAtAction(nameof(GetContent), new { id = content.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating motivational content");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating content");
        }
    }

    /// <summary>
    /// Get specific motivational content (Admin only)
    /// </summary>
    /// <param name="id">Content ID</param>
    /// <returns>Content details</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MotivationalContentDto>> GetContent(Guid id)
    {
        try
        {
            var content = await _contentRepository.GetByIdAsync(id);
            if (content == null)
            {
                return NotFound();
            }

            return Ok(MapToContentDto(content));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting motivational content {ContentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving content");
        }
    }

    /// <summary>
    /// Update motivational content (Admin only)
    /// </summary>
    /// <param name="id">Content ID</param>
    /// <param name="request">Update request</param>
    /// <returns>Updated content</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MotivationalContentDto>> UpdateContent(Guid id, [FromBody] UpdateMotivationalContentRequest request)
    {
        try
        {
            var content = await _contentRepository.GetByIdAsync(id);
            if (content == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(request.Message))
            {
                content.UpdateContent(request.Title, request.Message);
            }

            if (request.Priority.HasValue)
            {
                content.SetPriority(request.Priority.Value);
            }

            if (request.TargetConditions != null)
            {
                content.SetTargetConditions(request.TargetConditions);
            }

            if (request.SchedulingRules != null)
            {
                content.SetSchedulingRules(request.SchedulingRules);
            }

            if (request.StartDate.HasValue || request.EndDate.HasValue)
            {
                content.SetActivePeriod(
                    request.StartDate ?? content.StartDate,
                    request.EndDate ?? content.EndDate);
            }

            if (!string.IsNullOrEmpty(request.ImageUrl))
            {
                content.SetImageUrl(request.ImageUrl);
            }

            if (request.ActionUrl != null)
            {
                content.SetAction(request.ActionUrl, request.ActionText);
            }

            if (request.IsActive.HasValue)
            {
                if (request.IsActive.Value)
                {
                    content.Activate();
                }
                else
                {
                    content.Deactivate();
                }
            }

            await _contentRepository.UpdateAsync(content);

            return Ok(MapToContentDto(content));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating motivational content {ContentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating content");
        }
    }

    /// <summary>
    /// Delete motivational content (Admin only)
    /// </summary>
    /// <param name="id">Content ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteContent(Guid id)
    {
        try
        {
            var content = await _contentRepository.GetByIdAsync(id);
            if (content == null)
            {
                return NotFound();
            }

            await _contentRepository.SoftDeleteAsync(content);

            return Ok(new { message = "Content deleted successfully", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting motivational content {ContentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting content");
        }
    }

    // A/B Testing endpoints

    /// <summary>
    /// Create new A/B test (Admin only)
    /// </summary>
    /// <param name="request">A/B test creation request</param>
    /// <returns>A/B test configuration</returns>
    [HttpPost("ab-tests")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ABTestConfiguration>> CreateABTest([FromBody] CreateABTestRequest request)
    {
        try
        {
            var configuration = await _abTestingService.CreateABTestAsync(
                request.TestName,
                request.Description,
                request.ContentIds,
                request.GroupWeights,
                request.TestDuration);

            return CreatedAtAction(nameof(GetABTestResults), new { testName = request.TestName }, configuration);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating A/B test '{TestName}'", request.TestName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating A/B test");
        }
    }

    /// <summary>
    /// Get A/B test results (Admin only)
    /// </summary>
    /// <param name="testName">Test name</param>
    /// <param name="includeStatisticalAnalysis">Include statistical analysis</param>
    /// <returns>A/B test results</returns>
    [HttpGet("ab-tests/{testName}/results")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ABTestResults>> GetABTestResults(
        string testName,
        [FromQuery] bool includeStatisticalAnalysis = true)
    {
        try
        {
            var results = await _abTestingService.AnalyzeABTestAsync(testName, includeStatisticalAnalysis);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting A/B test results for '{TestName}'", testName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving A/B test results");
        }
    }

    /// <summary>
    /// Get all active A/B tests (Admin only)
    /// </summary>
    /// <returns>List of active A/B tests</returns>
    [HttpGet("ab-tests")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ABTestSummary>>> GetActiveABTests()
    {
        try
        {
            var tests = await _abTestingService.GetActiveABTestsAsync();
            return Ok(tests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active A/B tests");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving A/B tests");
        }
    }

    /// <summary>
    /// Promote A/B test winner (Admin only)
    /// </summary>
    /// <param name="testName">Test name</param>
    /// <param name="request">Winner promotion request</param>
    /// <returns>Success status</returns>
    [HttpPost("ab-tests/{testName}/promote")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> PromoteABTestWinner(string testName, [FromBody] PromoteWinnerRequest? request = null)
    {
        try
        {
            var success = await _abTestingService.PromoteWinnerAsync(testName, request?.WinnerGroup);
            
            if (success)
            {
                return Ok(new { 
                    message = "Winner promoted successfully", 
                    testName, 
                    winnerGroup = request?.WinnerGroup ?? "Auto-determined",
                    timestamp = DateTime.UtcNow 
                });
            }

            return BadRequest("Failed to promote winner - check test results for issues");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error promoting A/B test winner for '{TestName}'", testName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while promoting winner");
        }
    }

    /// <summary>
    /// Stop A/B test (Admin only)
    /// </summary>
    /// <param name="testName">Test name</param>
    /// <returns>Success status</returns>
    [HttpPost("ab-tests/{testName}/stop")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> StopABTest(string testName)
    {
        try
        {
            var success = await _abTestingService.StopABTestAsync(testName);
            
            if (success)
            {
                return Ok(new { message = "A/B test stopped successfully", testName, timestamp = DateTime.UtcNow });
            }

            return BadRequest("Failed to stop A/B test");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping A/B test '{TestName}'", testName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while stopping A/B test");
        }
    }

    // Private helper methods

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private MotivationalContentDto MapToContentDto(MotivationalContent content)
    {
        return new MotivationalContentDto
        {
            Id = content.Id,
            Title = content.Title,
            Message = content.Message,
            ContentType = content.ContentType,
            Category = content.Category,
            Priority = content.Priority,
            IsActive = content.IsActive,
            IsABTestEnabled = content.IsABTestEnabled,
            ABTestGroup = content.ABTestGroup,
            ImageUrl = content.ImageUrl,
            ActionUrl = content.ActionUrl,
            ActionText = content.ActionText,
            StartDate = content.StartDate,
            EndDate = content.EndDate,
            TargetConditions = content.TargetConditions,
            SchedulingRules = content.SchedulingRules,
            Metadata = content.Metadata,
            CreatedAt = content.CreatedAt,
            UpdatedAt = content.UpdatedAt
        };
    }

    private UserContentPreferencesDto MapToPreferencesDto(UserContentPreferences preferences)
    {
        return new UserContentPreferencesDto
        {
            UserId = preferences.UserId,
            IsContentEnabled = preferences.IsContentEnabled,
            PreferredFrequency = preferences.PreferredFrequency,
            PreferredContentTypes = preferences.PreferredContentTypes.ToList(),
            PreferredCategories = preferences.PreferredCategories.ToList(),
            PreferredChannels = preferences.PreferredChannels.ToList(),
            MaxDailyContent = preferences.MaxDailyContent,
            MaxWeeklyContent = preferences.MaxWeeklyContent,
            AllowWeekends = preferences.AllowWeekends,
            AllowAfterHours = preferences.AllowAfterHours,
            TimeZone = preferences.TimeZone,
            LastContentDelivery = preferences.LastContentDelivery,
            ContentPausedUntil = preferences.ContentPausedUntil,
            PreferredDeliveryTimes = preferences.PreferredDeliveryTimes
        };
    }
}

// DTOs and Request Models

public class ContentEngagementRequest
{
    [Required]
    public Guid ContentId { get; set; }

    [Required]
    public ContentEngagementType EngagementType { get; set; }

    public Guid? DeliveryLogId { get; set; }
    public Dictionary<string, object>? EngagementMetadata { get; set; }
    public TimeSpan? ViewDuration { get; set; }
}

public class UserContentPreferencesDto
{
    public Guid UserId { get; set; }
    public bool IsContentEnabled { get; set; }
    public ContentFrequency PreferredFrequency { get; set; }
    public List<MotivationalContentType> PreferredContentTypes { get; set; } = new();
    public List<ContentCategory> PreferredCategories { get; set; } = new();
    public List<ContentDeliveryChannel> PreferredChannels { get; set; } = new();
    public int MaxDailyContent { get; set; }
    public int MaxWeeklyContent { get; set; }
    public bool AllowWeekends { get; set; }
    public bool AllowAfterHours { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public DateTime? LastContentDelivery { get; set; }
    public DateTime? ContentPausedUntil { get; set; }
    public Dictionary<string, TimeSpan> PreferredDeliveryTimes { get; set; } = new();
}

public class UpdateContentPreferencesRequest
{
    public bool? IsContentEnabled { get; set; }
    public ContentFrequency? PreferredFrequency { get; set; }
    public List<MotivationalContentType>? PreferredContentTypes { get; set; }
    public List<ContentCategory>? PreferredCategories { get; set; }
    public List<ContentDeliveryChannel>? PreferredChannels { get; set; }
    public int? MaxDailyContent { get; set; }
    public int? MaxWeeklyContent { get; set; }
    public bool? AllowWeekends { get; set; }
    public bool? AllowAfterHours { get; set; }
    public string? TimeZone { get; set; }
}

public class PauseContentRequest
{
    public DateTime? PauseUntil { get; set; }
    public double? PauseHours { get; set; }
}

public class MotivationalContentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public MotivationalContentType ContentType { get; set; }
    public ContentCategory Category { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public bool IsABTestEnabled { get; set; }
    public string ABTestGroup { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Dictionary<string, object> TargetConditions { get; set; } = new();
    public Dictionary<string, object> SchedulingRules { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateMotivationalContentRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public MotivationalContentType ContentType { get; set; }

    [Required]
    public ContentCategory Category { get; set; }

    public Dictionary<string, object>? TargetConditions { get; set; }
    public int Priority { get; set; } = 0;
    public string? ImageUrl { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Dictionary<string, object>? SchedulingRules { get; set; }
}

public class UpdateMotivationalContentRequest
{
    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(1000)]
    public string? Message { get; set; }

    public int? Priority { get; set; }
    public Dictionary<string, object>? TargetConditions { get; set; }
    public Dictionary<string, object>? SchedulingRules { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ImageUrl { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateABTestRequest
{
    [Required]
    [StringLength(100)]
    public string TestName { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    public List<Guid> ContentIds { get; set; } = new();

    public Dictionary<string, double>? GroupWeights { get; set; }
    public TimeSpan? TestDuration { get; set; }
}

public class PromoteWinnerRequest
{
    public string? WinnerGroup { get; set; }
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}