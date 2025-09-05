using FluentValidation;
using WhoAndWhat.Application.Features.Auth.Commands.RefreshToken;

namespace WhoAndWhat.Application.Validators;

/// <summary>
/// Validator for RefreshTokenCommand
/// </summary>
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required")
            .MinimumLength(10)
            .WithMessage("Invalid refresh token format")
            .MaximumLength(500)
            .WithMessage("Refresh token is too long");
    }
}