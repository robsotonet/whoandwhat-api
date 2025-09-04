using FluentValidation;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Validators;

public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(user => user.Username).NotEmpty().MaximumLength(50);
        RuleFor(user => user.Email).NotEmpty().EmailAddress();
    }
}
