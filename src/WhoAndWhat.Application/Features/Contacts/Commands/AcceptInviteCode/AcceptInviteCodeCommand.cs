using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;

namespace WhoAndWhat.Application.Features.Contacts.Commands.AcceptInviteCode;

/// <summary>
/// Command to accept an invite code and add a contact
/// </summary>
public record AcceptInviteCodeCommand(
    string InviteCode,
    Guid UserId,
    string? CustomNotes = null
) : IRequest<Result<ContactDto>>;
