using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.AI;

namespace WhoAndWhat.Application.Features.AIPlanning.Queries.GetTaskTimeEstimates;

public record GetTaskTimeEstimatesQuery(
    Guid UserId,
    List<Guid> TaskIds,
    bool IncludeConfidenceInterval
) : IRequest<Result<TaskTimeEstimatesResponse>>;

public record TaskTimeEstimatesResponse(
    List<TaskTimeEstimate> TimeEstimates,
    UserHistoricalPerformance UserPerformance,
    List<string> EstimationNotes,
    DateTime GeneratedAt
);