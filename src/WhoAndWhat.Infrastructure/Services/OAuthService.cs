using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Infrastructure.Services;

public class OAuthService : IOAuthService
{
    private readonly IOAuthAccountRepository _oauthAccountRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserDomainService _userDomainService;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        IOAuthAccountRepository oauthAccountRepository,
        IUserRepository userRepository,
        IUserDomainService userDomainService,
        ILogger<OAuthService> logger)
    {
        _oauthAccountRepository = oauthAccountRepository;
        _userRepository = userRepository;
        _userDomainService = userDomainService;
        _logger = logger;
    }

    public async Task<User?> AuthenticateWithOAuthAsync(string provider, string externalId, string? email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default)
    {
        if (!OAuthProviders.IsSupported(provider))
        {
            _logger.LogWarning("Unsupported OAuth provider: {Provider}", provider);
            return null;
        }

        var oauthAccount = await _oauthAccountRepository.GetByProviderAndExternalIdAsync(provider, externalId, cancellationToken);

        if (oauthAccount != null)
        {
            // Update profile information and record login
            oauthAccount.UpdateProfile(email, name, profileImageUrl);
            oauthAccount.RecordLogin();
            await _oauthAccountRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("OAuth authentication successful for existing account. Provider: {Provider}, ExternalId: {ExternalId}, UserId: {UserId}",
                provider, externalId, oauthAccount.UserId);

            return oauthAccount.User;
        }

        // Check if user exists with the same email
        if (!string.IsNullOrEmpty(email))
        {
            var existingUser = await _userRepository.GetUserByEmailAsync(email, cancellationToken);
            if (existingUser != null)
            {
                // Link OAuth account to existing user
                await CreateOAuthAccountAsync(existingUser.Id, provider, externalId, email, name, profileImageUrl, cancellationToken);

                _logger.LogInformation("OAuth account linked to existing user. Provider: {Provider}, Email: {Email}, UserId: {UserId}",
                    provider, email, existingUser.Id);

                return existingUser;
            }
        }

        // Create new user if email is provided
        if (!string.IsNullOrEmpty(email))
        {
            var newUser = await CreateUserFromOAuthAsync(provider, externalId, email, name, profileImageUrl, cancellationToken);

            _logger.LogInformation("New user created from OAuth. Provider: {Provider}, Email: {Email}, UserId: {UserId}",
                provider, email, newUser.Id);

            return newUser;
        }

        _logger.LogWarning("OAuth authentication failed - no email provided. Provider: {Provider}, ExternalId: {ExternalId}",
            provider, externalId);

        return null;
    }

    public async Task<OAuthAccount?> GetOAuthAccountAsync(string provider, string externalId, CancellationToken cancellationToken = default)
    {
        return await _oauthAccountRepository.GetByProviderAndExternalIdAsync(provider, externalId, cancellationToken);
    }

    public async Task<OAuthAccount> CreateOAuthAccountAsync(Guid userId, string provider, string externalId, string? email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default)
    {
        var oauthAccount = new OAuthAccount(userId, provider, externalId, email, name);
        if (!string.IsNullOrEmpty(profileImageUrl))
        {
            oauthAccount.UpdateProfile(email, name, profileImageUrl);
        }
        oauthAccount.RecordLogin();

        await _oauthAccountRepository.AddAsync(oauthAccount, cancellationToken);
        await _oauthAccountRepository.SaveChangesAsync(cancellationToken);

        return oauthAccount;
    }

    public async Task<User> LinkOAuthAccountAsync(Guid userId, string provider, string externalId, string? email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default)
    {
        // Check if account already exists for this provider
        var existingAccount = await _oauthAccountRepository.GetByUserIdAndProviderAsync(userId, provider, cancellationToken);
        if (existingAccount != null)
        {
            throw new InvalidOperationException($"User already has a linked {provider} account");
        }

        // Check if this OAuth account is already linked to another user
        var existingOAuthAccount = await _oauthAccountRepository.GetByProviderAndExternalIdAsync(provider, externalId, cancellationToken);
        if (existingOAuthAccount != null)
        {
            throw new InvalidOperationException($"This {provider} account is already linked to another user");
        }

        await CreateOAuthAccountAsync(userId, provider, externalId, email, name, profileImageUrl, cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        _logger.LogInformation("OAuth account linked successfully. UserId: {UserId}, Provider: {Provider}, ExternalId: {ExternalId}",
            userId, provider, externalId);

        return user;
    }

    public async System.Threading.Tasks.Task UnlinkOAuthAccountAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        await _oauthAccountRepository.RemoveByUserIdAndProviderAsync(userId, provider, cancellationToken);

        _logger.LogInformation("OAuth account unlinked. UserId: {UserId}, Provider: {Provider}", userId, provider);
    }

    public async Task<List<OAuthAccount>> GetUserOAuthAccountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _oauthAccountRepository.GetByUserIdAsync(userId, cancellationToken);
    }

    public async System.Threading.Tasks.Task<User> CreateUserFromOAuthAsync(string provider, string externalId, string email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default)
    {
        // Generate username from email or name
        var username = GenerateUsername(email, name);

        // Create user without password (OAuth-only user)
        var user = new User(email, username, Language.en);

        // Mark email as verified since it comes from OAuth provider
        user.VerifyEmail();

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // Create OAuth account
        await CreateOAuthAccountAsync(user.Id, provider, externalId, email, name, profileImageUrl, cancellationToken);

        return user;
    }

    public async System.Threading.Tasks.Task UpdateOAuthAccountProfileAsync(Guid userId, string provider, string? email, string? name, string? profileImageUrl, CancellationToken cancellationToken = default)
    {
        var oauthAccount = await _oauthAccountRepository.GetByUserIdAndProviderAsync(userId, provider, cancellationToken);
        if (oauthAccount == null)
        {
            throw new InvalidOperationException($"No {provider} account found for user {userId}");
        }

        oauthAccount.UpdateProfile(email, name, profileImageUrl);
        await _oauthAccountRepository.SaveChangesAsync(cancellationToken);
    }

    public async System.Threading.Tasks.Task RecordOAuthLoginAsync(string provider, string externalId, CancellationToken cancellationToken = default)
    {
        var oauthAccount = await _oauthAccountRepository.GetByProviderAndExternalIdAsync(provider, externalId, cancellationToken);
        if (oauthAccount != null)
        {
            oauthAccount.RecordLogin();
            await _oauthAccountRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private string GenerateUsername(string email, string? name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            // Remove spaces and special characters from name
            var cleanName = new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray());
            if (cleanName.Length >= 3)
            {
                return cleanName.ToLowerInvariant();
            }
        }

        // Use email prefix as username
        var emailPrefix = email.Split('@')[0];
        var cleanEmailPrefix = new string(emailPrefix.Where(c => char.IsLetterOrDigit(c)).ToArray());

        return cleanEmailPrefix.ToLowerInvariant();
    }
}
