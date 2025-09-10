using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Tasks.Commands.UnlinkContactFromTask;

public record UnlinkContactFromTaskCommand(
    Guid TaskId,
    Guid ContactId,
    Guid UserId
) : IRequest<Result<bool>>;