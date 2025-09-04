
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Services;

public interface IUserDomainService
{
    public (string passwordHash, string salt) CreatePasswordHash(string password);
    public bool VerifyPasswordHash(string password, string storedHash, string storedSalt);
    public User CreateUser(string email, string username, string password, Language language);
}
