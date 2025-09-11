using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;

namespace WhoAndWhat.Application.Features.Tasks.Commands.UpdateContactRole;

public record UpdateContactRoleCommand(
    Guid TaskId,
    Guid ContactId,
    string NewRole,
    string? Notes,
    Guid UserId
) : IRequest<Result<TaskContactDto>>;
