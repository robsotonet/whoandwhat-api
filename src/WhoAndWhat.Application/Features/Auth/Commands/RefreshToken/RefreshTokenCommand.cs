using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;

namespace WhoAndWhat.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Command for refreshing JWT tokens
/// </summary>
public record RefreshTokenCommand(
    string RefreshToken
) : IRequest<Result<TokenResult>>;
