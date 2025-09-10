using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Commands.LinkContactToTask;

public record LinkContactToTaskCommand(
    Guid TaskId,
    Guid ContactId,
    string Role,
    string? Notes,
    Guid UserId
) : IRequest<Result<TaskContactDto>>;