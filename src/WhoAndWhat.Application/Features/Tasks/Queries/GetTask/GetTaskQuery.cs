using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTask;

public record GetTaskQuery(
    Guid TaskId,
    Guid UserId,
    bool IncludeSubtasks = true
) : IRequest<Result<TaskDto>>;
