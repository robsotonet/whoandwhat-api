using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Repositories;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GetMotivationalContent;

/// <summary>
/// Handler for retrieving personalized motivational content for the user's dashboard
/// </summary>
public sealed class GetMotivationalContentQueryHandler
    : IRequestHandler<GetMotivationalContentQuery, Result<GetMotivationalContentResponse>>
{
    private readonly IMotivationalContentRepository _contentRepository;
    private readonly IUserContentPreferencesRepository _preferencesRepository;
    private readonly IOptimizedContentEngagementService _engagementService;
    private readonly ILogger<GetMotivationalContentQueryHandler> _logger;

    public GetMotivationalContentQueryHandler(
        IMotivationalContentRepository contentRepository,
        IUserContentPreferencesRepository preferencesRepository,
        IOptimizedContentEngagementService engagementService,
        ILogger<GetMotivationalContentQueryHandler> logger)
    {
        _contentRepository = contentRepository;
        _preferencesRepository = preferencesRepository;
        _engagementService = engagementService;
        _logger = logger;
    }

    public async Task<Result<GetMotivationalContentResponse>> Handle(
        GetMotivationalContentQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting motivational content for user {UserId}", request.UserId);

            // Get user preferences or create default ones
            var preferences = await _preferencesRepository.GetByUserIdAsync(request.UserId, cancellationToken)
                            ?? await CreateDefaultPreferencesAsync(request.UserId, cancellationToken);

            // Check if user can receive content now
            if (!preferences.CanDeliverContentNow(ContentDeliveryChannel.Dashboard, MotivationalContentType.Insight))
            {
                _logger.LogInformation("Content delivery blocked for user {UserId} - daily limit reached or outside delivery hours",
                    request.UserId);

                return Result<GetMotivationalContentResponse>.Success(
                    new GetMotivationalContentResponse(
                        Array.Empty<MotivationalContentDto>(),
                        0,
                        MapToPersonalizationInfo(preferences)
                    ));
            }

            // Get personalized content using the engagement service
            var personalizedContent = await _engagementService.GetPersonalizedContentAsync(
                request.UserId,
                request.Count ?? 3,
                cancellationToken);

            // Get total available content count
            var totalCount = await _contentRepository.GetActiveContentCountAsync(cancellationToken);

            var response = new GetMotivationalContentResponse(
                personalizedContent.Select(MapToDto).ToList(),
                totalCount,
                MapToPersonalizationInfo(preferences)
            );

            _logger.LogInformation("Successfully retrieved {Count} motivational contents for user {UserId}",
                personalizedContent.Count, request.UserId);

            return Result<GetMotivationalContentResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting motivational content for user {UserId}", request.UserId);
            return Result<GetMotivationalContentResponse>.Failure(
                "Failed to retrieve motivational content: " + ex.Message);
        }
    }

    private async Task<UserContentPreferences> CreateDefaultPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating default content preferences for user {UserId}", userId);

        var defaultPreferences = UserContentPreferences.CreateDefault(userId);
        await _preferencesRepository.AddAsync(defaultPreferences, cancellationToken);
        await _preferencesRepository.SaveChangesAsync(cancellationToken);

        return defaultPreferences;
    }

    private static MotivationalContentDto MapToDto(MotivationalContent content)
    {
        return new MotivationalContentDto(
            content.Id,
            content.Title,
            content.Message,
            content.ContentType.ToString(),
            content.Category.ToString(),
            content.Priority,
            content.StartDate, // Using StartDate instead of ScheduledFor
            content.TargetConditions,
            false, // IsPersonalized not implemented yet
            (double)content.Priority // Simple relevance score based on priority
        );
    }

    private static PersonalizationInfoDto MapToPersonalizationInfo(UserContentPreferences preferences)
    {
        return new PersonalizationInfoDto(
            0, // ContentDeliveredToday placeholder
            10, // MaxDailyContent placeholder
            new List<int> { 9, 10, 11, 12, 13, 14, 15, 16, 17 }, // OptimalDeliveryHours placeholder
            "Productivity, Motivation", // PreferredContentTypes placeholder
            0.5 // CalculateEngagementScore placeholder
        );
    }
}
