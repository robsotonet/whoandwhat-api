
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Services;

public class UserDomainService : IUserDomainService
{
    public (string passwordHash, string salt) CreatePasswordHash(string password)
    {
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, salt);
        return (passwordHash, salt);
    }

    public bool VerifyPasswordHash(string password, string storedHash, string storedSalt)
    {
        return BCrypt.Net.BCrypt.Verify(password, storedHash);
    }

    public User CreateUser(string email, string username, string password, Language language)
    {
        var (passwordHash, salt) = CreatePasswordHash(password);

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            Salt = salt,
            PreferredLanguage = language,
            CreatedAt = DateTime.UtcNow,
            IsVerified = false, // Default to not verified
        };
    }
}
