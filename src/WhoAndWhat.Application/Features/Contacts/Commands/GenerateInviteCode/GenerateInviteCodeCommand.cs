using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;

namespace WhoAndWhat.Application.Features.Contacts.Commands.GenerateInviteCode;

/// <summary>
/// Command to generate a unique invite code for contact sharing
/// </summary>
public record GenerateInviteCodeCommand(
    Guid ContactId,
    Guid UserId,
    int ExpirationHours = 24,
    string? CustomMessage = null,
    bool AllowMultipleUses = false
) : IRequest<Result<ContactInviteDto>>;