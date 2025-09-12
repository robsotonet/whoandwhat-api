using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

public interface IOAuthAccountRepository : IRepository<OAuthAccount>
{
    public System.Threading.Tasks.Task<OAuthAccount?> GetByProviderAndExternalIdAsync(string provider, string externalId, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<List<OAuthAccount>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<OAuthAccount?> GetByUserIdAndProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<bool> ExistsAsync(string provider, string externalId, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task RemoveByUserIdAndProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default);
}
