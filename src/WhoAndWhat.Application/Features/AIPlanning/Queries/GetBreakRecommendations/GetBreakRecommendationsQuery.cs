using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.AI;

namespace WhoAndWhat.Application.Features.AIPlanning.Queries.GetBreakRecommendations;

public record GetBreakRecommendationsQuery(
    Guid UserId,
    WorkloadAnalysis WorkloadAnalysis,
    bool IncludeActivitySuggestions
) : IRequest<Result<BreakRecommendationsResponse>>;

public record BreakRecommendationsResponse(
    List<BreakRecommendation> Recommendations,
    WorkloadAnalysis WorkloadAnalysis,
    List<string> GeneralTips,
    DateTime GeneratedAt
);