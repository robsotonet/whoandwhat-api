using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;

namespace WhoAndWhat.Application.Features.Auth.Commands.LoginUser;

/// <summary>
/// Command for user authentication
/// </summary>
public record LoginUserCommand(
    string Email,
    string Password,
    bool RememberMe
) : IRequest<Result<LoginResponse>>;
