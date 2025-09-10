using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;

namespace WhoAndWhat.Application.Features.Contacts.Queries.GetContact;

public record GetContactQuery(
    Guid ContactId,
    Guid UserId,
    bool IncludeDeleted = false,
    bool IncludeTasks = false
) : IRequest<Result<ContactDto>>;