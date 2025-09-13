using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Dashboard.Queries.GetMotivationalContent;

/// <summary>
/// Query to get personalized motivational content for the user's dashboard
/// </summary>
public sealed record GetMotivationalContentQuery(
    Guid UserId,
    int? Count = 3,
    string? Language = "en") : IRequest<Result<GetMotivationalContentResponse>>;

/// <summary>
/// Response containing personalized motivational content
/// </summary>
public sealed record GetMotivationalContentResponse(
    IReadOnlyList<MotivationalContentDto> Contents,
    int TotalAvailable,
    PersonalizationInfoDto PersonalizationInfo
);

/// <summary>
/// DTO for motivational content
/// </summary>
public sealed record MotivationalContentDto(
    Guid Id,
    string Title,
    string Message,
    string ContentType,
    string Category,
    int Priority,
    DateTime? ScheduledFor,
    Dictionary<string, object> TargetConditions,
    bool IsPersonalized,
    double RelevanceScore
);

/// <summary>
/// DTO for personalization information
/// </summary>
public sealed record PersonalizationInfoDto(
    int DeliveredToday,
    int MaxDailyContent,
    List<int> OptimalDeliveryHours,
    string PreferredContentTypes,
    double EngagementScore
);
