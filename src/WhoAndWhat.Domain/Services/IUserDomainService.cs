
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Services;

public interface IUserDomainService
{
    public User CreateUser(string email, string username, string password, Language language);
}
