
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    public System.Threading.Tasks.Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
