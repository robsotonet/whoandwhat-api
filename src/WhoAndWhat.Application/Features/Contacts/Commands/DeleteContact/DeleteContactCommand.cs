using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Contacts.Commands.DeleteContact;

public record DeleteContactCommand(
    Guid ContactId,
    Guid UserId
) : IRequest<Result<bool>>;
