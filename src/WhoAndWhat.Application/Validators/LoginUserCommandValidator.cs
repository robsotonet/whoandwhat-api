using FluentValidation;
using WhoAndWhat.Application.Features.Auth.Commands.LoginUser;

namespace WhoAndWhat.Application.Validators;

/// <summary>
/// Validator for LoginUserCommand
/// </summary>
public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Valid email address is required")
            .MaximumLength(254)
            .WithMessage("Email must not exceed 254 characters");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MaximumLength(100)
            .WithMessage("Password must not exceed 100 characters");
    }
}