
using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Data;

namespace WhoAndWhat.Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async System.Threading.Tasks.Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async System.Threading.Tasks.Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async System.Threading.Tasks.Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public new async System.Threading.Tasks.Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Username == username, cancellationToken);
    }

    [Obsolete("This method loads all users into memory. Use GetActiveUsersPagedAsync for better performance.")]
    public async System.Threading.Tasks.Task<IEnumerable<User>> GetAllActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Where(u => u.LastLoginDate >= DateTime.UtcNow.AddDays(-30)) // Active = logged in within last 30 days
            .ToListAsync(cancellationToken);
    }

    public async System.Threading.Tasks.Task<IEnumerable<User>> GetActiveUsersPagedAsync(int pageSize, int pageNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Where(u => u.LastLoginDate >= DateTime.UtcNow.AddDays(-30))
            .OrderBy(u => u.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async System.Threading.Tasks.Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Where(u => u.LastLoginDate >= DateTime.UtcNow.AddDays(-30))
            .CountAsync(cancellationToken);
    }
}
