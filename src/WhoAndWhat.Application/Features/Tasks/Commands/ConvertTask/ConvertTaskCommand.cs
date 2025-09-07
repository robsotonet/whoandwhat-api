using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Commands.ConvertTask;

public record ConvertTaskCommand(
    Guid TaskId,
    int ToCategory,
    string? Reason,
    bool CreateSubtasks,
    Guid UserId
) : IRequest&lt;Result&lt;TaskDto&gt;&gt;;