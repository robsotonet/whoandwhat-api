using FluentValidation;
using WhoAndWhat.Application.DTOs.Authentication;

namespace WhoAndWhat.Application.Validators;

/// <summary>
/// Validator for refresh token requests
/// </summary>
public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
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