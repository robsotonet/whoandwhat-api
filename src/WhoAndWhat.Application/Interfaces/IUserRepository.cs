
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    public System.Threading.Tasks.Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an email address is already in use by another user
    /// </summary>
    /// <param name="email">The email address to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email exists, false otherwise</returns>
    public System.Threading.Tasks.Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a username is already in use by another user
    /// </summary>
    /// <param name="username">The username to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if username exists, false otherwise</returns>
    public System.Threading.Tasks.Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
}
