using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTaskScheduling;

public record GetTaskSchedulingQuery(
    Guid UserId,
    DateTime? TargetDate = null,
    int MaxSuggestions = 20
) : IRequest<Result<TaskSchedulingResponse>>;
