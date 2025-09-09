using Microsoft.EntityFrameworkCore;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Data;

namespace WhoAndWhat.Infrastructure.Repositories;

public class OAuthAccountRepository : Repository<OAuthAccount>, IOAuthAccountRepository
{
    public OAuthAccountRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<OAuthAccount?> GetByProviderAndExternalIdAsync(string provider, string externalId, CancellationToken cancellationToken = default)
    {
        return await _context.OAuthAccounts
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Provider == provider && o.ExternalId == externalId && o.IsActive, cancellationToken);
    }

    public async Task<List<OAuthAccount>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.OAuthAccounts
            .Where(o => o.UserId == userId && o.IsActive)
            .OrderBy(o => o.Provider)
            .ToListAsync(cancellationToken);
    }

    public async Task<OAuthAccount?> GetByUserIdAndProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        return await _context.OAuthAccounts
            .FirstOrDefaultAsync(o => o.UserId == userId && o.Provider == provider && o.IsActive, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string provider, string externalId, CancellationToken cancellationToken = default)
    {
        return await _context.OAuthAccounts
            .AnyAsync(o => o.Provider == provider && o.ExternalId == externalId && o.IsActive, cancellationToken);
    }

    public async System.Threading.Tasks.Task RemoveByUserIdAndProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        var account = await GetByUserIdAndProviderAsync(userId, provider, cancellationToken);
        if (account != null)
        {
            account.Deactivate();
            await SaveChangesAsync(cancellationToken);
        }
    }
}
