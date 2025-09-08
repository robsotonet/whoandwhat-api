
using WhoAndWhat.Domain.Common;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Services;

public interface IUserDomainService
{
    public User CreateUser(string email, string username, string password, Language language);
    public Task<User> UpdateUserAsync(User user, string? email = null, string? username = null, string? firstName = null, string? lastName = null, Language? language = null);
    public ValidationResult ValidatePassword(string password);
}
