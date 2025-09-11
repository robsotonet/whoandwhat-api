using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;

namespace WhoAndWhat.Application.Features.Contacts.Commands.CreateContact;

public record CreateContactCommand(
    string Name,
    string? Email,
    string? Phone,
    int RelationshipType,
    Guid UserId
) : IRequest<Result<ContactDto>>;