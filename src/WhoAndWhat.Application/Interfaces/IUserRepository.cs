
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
}
