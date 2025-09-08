using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;

namespace WhoAndWhat.Application.Features.Auth.Commands.LogoutUser;

/// <summary>
/// Command for user logout
/// </summary>
public record LogoutUserCommand(
    Guid UserId,
    string? RefreshToken = null,
    bool RevokeAllTokens = false
) : IRequest<Result<LogoutResponse>>;
