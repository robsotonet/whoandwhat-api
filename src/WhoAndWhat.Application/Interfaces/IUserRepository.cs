
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

    /// <summary>
    /// Gets all active users (users who have been active recently)
    /// This method is inefficient for large datasets - use GetActiveUsersPagedAsync instead
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all active users</returns>
    [Obsolete("This method loads all users into memory. Use GetActiveUsersPagedAsync for better performance.")]
    public System.Threading.Tasks.Task<IEnumerable<User>> GetAllActiveUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active users with pagination support for better performance
    /// </summary>
    /// <param name="pageSize">Number of users to return per page</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged list of active users</returns>
    public System.Threading.Tasks.Task<IEnumerable<User>> GetActiveUsersPagedAsync(int pageSize, int pageNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of active users for pagination calculations
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total count of active users</returns>
    public System.Threading.Tasks.Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default);
}
