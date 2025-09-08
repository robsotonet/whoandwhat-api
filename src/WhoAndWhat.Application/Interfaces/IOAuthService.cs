using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Interfaces;

public interface IOAuthService
{
    public System.Threading.Tasks.Task<User?> AuthenticateWithOAuthAsync(string provider, string externalId, string? email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<OAuthAccount?> GetOAuthAccountAsync(string provider, string externalId, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<OAuthAccount> CreateOAuthAccountAsync(Guid userId, string provider, string externalId, string? email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<User> LinkOAuthAccountAsync(Guid userId, string provider, string externalId, string? email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task UnlinkOAuthAccountAsync(Guid userId, string provider, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<List<OAuthAccount>> GetUserOAuthAccountsAsync(Guid userId, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task<User> CreateUserFromOAuthAsync(string provider, string externalId, string email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task UpdateOAuthAccountProfileAsync(Guid userId, string provider, string? email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default);
    public System.Threading.Tasks.Task RecordOAuthLoginAsync(string provider, string externalId, CancellationToken cancellationToken = default);
}

public static class OAuthProviders
{
    public const string Google = "Google";
    public const string Facebook = "Facebook";
    public const string Apple = "Apple";
    public const string Microsoft = "Microsoft";

    public static readonly string[] SupportedProviders = { Google, Facebook, Apple, Microsoft };

    public static bool IsSupported(string provider)
    {
        return SupportedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase);
    }
}
