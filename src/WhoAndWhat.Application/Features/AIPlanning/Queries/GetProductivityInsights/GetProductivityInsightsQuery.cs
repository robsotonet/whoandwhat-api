using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.AI;

namespace WhoAndWhat.Application.Features.AIPlanning.Queries.GetProductivityInsights;

public record GetProductivityInsightsQuery(
    Guid UserId,
    TimeframeAnalysis Timeframe,
    string? AnalysisType
) : IRequest<Result<ProductivityInsights>>;