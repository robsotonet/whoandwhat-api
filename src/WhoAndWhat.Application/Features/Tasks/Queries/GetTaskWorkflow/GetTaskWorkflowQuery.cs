using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetTaskWorkflow;

public record GetTaskWorkflowQuery(
    Guid TaskId,
    Guid UserId
) : IRequest&lt;Result&lt;TaskWorkflowStateDto&gt;&gt;;