using FluentValidation;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Domain.Validators;

public class ContactValidator : AbstractValidator<Contact>
{
    public ContactValidator()
    {
        RuleFor(contact => contact.Name).NotEmpty().MaximumLength(100);
        RuleFor(contact => contact.Email).EmailAddress().When(contact => !string.IsNullOrEmpty(contact.Email));
    }
}
