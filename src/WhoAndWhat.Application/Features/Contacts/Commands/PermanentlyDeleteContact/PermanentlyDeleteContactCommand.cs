using MediatR;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Features.Contacts.Commands.PermanentlyDeleteContact;

public record PermanentlyDeleteContactCommand(
    Guid ContactId,
    Guid UserId
) : IRequest<Result<bool>>;