using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Tasks.Commands.DeleteTask;

public record DeleteTaskCommand(
    Guid TaskId,
    Guid UserId,
    bool HardDelete = false
) : IRequest&lt;Result&gt;;