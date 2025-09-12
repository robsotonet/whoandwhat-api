using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;

namespace WhoAndWhat.Application.Features.Contacts.Commands.RestoreContact;

public record RestoreContactCommand(
    Guid ContactId,
    Guid UserId
) : IRequest<Result<ContactDto>>;
