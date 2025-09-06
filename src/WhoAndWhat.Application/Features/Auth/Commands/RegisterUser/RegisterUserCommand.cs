using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;

namespace WhoAndWhat.Application.Features.Auth.Commands.RegisterUser;

/// <summary>
/// Command for registering a new user
/// </summary>
public record RegisterUserCommand(
    string Email,
    string Username,
    string Password,
    string PreferredLanguage,
    bool AcceptTerms
) : IRequest<Result<RegisterResponse>>;