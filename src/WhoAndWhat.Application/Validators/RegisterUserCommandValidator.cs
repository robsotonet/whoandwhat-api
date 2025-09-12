using FluentValidation;
using WhoAndWhat.Application.Features.Auth.Commands.RegisterUser;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Validators;

/// <summary>
/// Validator for RegisterUserCommand
/// </summary>
public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Valid email address is required")
            .MaximumLength(254)
            .WithMessage("Email must not exceed 254 characters");

        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username is required")
            .MinimumLength(3)
            .WithMessage("Username must be at least 3 characters long")
            .MaximumLength(50)
            .WithMessage("Username must not exceed 50 characters")
            .Matches("^[a-zA-Z0-9_.-]+$")
            .WithMessage("Username can only contain letters, numbers, dots, hyphens, and underscores");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long")
            .MaximumLength(100)
            .WithMessage("Password must not exceed 100 characters")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)")
            .WithMessage("Password must contain at least one lowercase letter, one uppercase letter, and one digit");

        RuleFor(x => x.PreferredLanguage)
            .NotEmpty()
            .WithMessage("Preferred language is required")
            .Must(BeValidLanguage)
            .WithMessage("Invalid language. Supported languages are: en, es");

        RuleFor(x => x.AcceptTerms)
            .Equal(true)
            .WithMessage("You must accept the terms and conditions to register");
    }

    private static bool BeValidLanguage(string language)
    {
        return Enum.TryParse<Language>(language, true, out _);
    }
}
