
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Services;

public class UserDomainService : IUserDomainService
{
    public User CreateUser(string email, string username, string password, Language language)
    {
        var user = new User(email, username, language);
        user.SetPassword(password);
        return user;
    }

    public Task<User> UpdateUserAsync(User user, string? email = null, string? username = null, string? firstName = null, string? lastName = null, Language? language = null)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        // Use domain methods to update user properties
        if (!string.IsNullOrWhiteSpace(email) && user.Email != email)
        {
            user.UpdateEmail(email);
        }

        if (!string.IsNullOrWhiteSpace(username) && user.Username != username)
        {
            user.UpdateUsername(username);
        }

        if (language != null && user.PreferredLanguage != language.Value)
        {
            user.UpdatePreferredLanguage(language.Value);
        }

        // Note: FirstName and LastName are not part of the User entity based on the current implementation
        // They would need to be added to the User entity if required

        return Task.FromResult(user);
    }

    public ValidationResult ValidatePassword(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required");
        }
        else
        {
            if (password.Length < 8)
            {
                errors.Add("Password must be at least 8 characters long");
            }

            if (!password.Any(char.IsUpper))
            {
                errors.Add("Password must contain at least one uppercase letter");
            }

            if (!password.Any(char.IsLower))
            {
                errors.Add("Password must contain at least one lowercase letter");
            }

            if (!password.Any(char.IsDigit))
            {
                errors.Add("Password must contain at least one number");
            }

            if (!password.Any(c => !char.IsLetterOrDigit(c)))
            {
                errors.Add("Password must contain at least one special character");
            }
        }

        return errors.Any()
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }
}
