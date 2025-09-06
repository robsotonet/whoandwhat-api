
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
}
