using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Commands.ExecuteTaskAction;

public record ExecuteTaskActionCommand(
    Guid TaskId,
    string ActionId,
    Dictionary<string, object> Parameters,
    Guid UserId
) : IRequest&lt;Result&lt;TaskDto&gt;&gt;;