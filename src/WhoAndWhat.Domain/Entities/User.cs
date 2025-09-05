using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

public class User : BaseEntity
{

    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    public Language PreferredLanguage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
    
    // Authentication properties
    public string PasswordHash { get; set; } = null!;
    public string Salt { get; set; } = null!;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    
    // Account verification properties
    public bool IsVerified { get; set; }
    public string? VerificationToken { get; set; }
    
    // Password reset properties
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpires { get; set; }

    public bool IsActive { get; private set; }
    public bool IsLocked { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public int FailedLoginAttempts { get; private set; }


    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    private readonly List<OAuthAccount> _oAuthAccounts = new();
    public IReadOnlyList<OAuthAccount> OAuthAccounts => _oAuthAccounts.AsReadOnly();

    // Navigation properties
    public ICollection<Task> Tasks { get; set; } = new List<Task>();
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();

    private User() 
    { 
        Email = string.Empty;
        Username = string.Empty;
        PreferredLanguage = Language.en;
    }

    public User(string email, string username, Language preferredLanguage)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Username = username ?? throw new ArgumentNullException(nameof(username));
        PreferredLanguage = preferredLanguage;
        IsActive = true;
        IsEmailVerified = false;
        IsLocked = false;
        FailedLoginAttempts = 0;
        
        AddDomainEvent(new UserCreatedEvent(Id, Email, Username));
    }

    public void SetPassword(string password)
    {
        if (!IsValidPassword(password))
        {
            throw new ArgumentException("Password does not meet requirements", nameof(password));
        }

        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12);
        AddDomainEvent(new UserPasswordChangedEvent(Id));
    }

    public bool VerifyPassword(string password)
    {
        if (string.IsNullOrEmpty(PasswordHash))
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
    }

    public void RecordLoginAttempt(bool successful)
    {
        if (successful)
        {
            LastLoginAt = DateTime.UtcNow;
            FailedLoginAttempts = 0;
            if (IsLocked && DateTime.UtcNow > LockedUntil)
            {
                IsLocked = false;
                LockedUntil = null;
            }
            AddDomainEvent(new UserLoggedInEvent(Id));
        }
        else
        {
            FailedLoginAttempts++;
            if (FailedLoginAttempts >= 5)
            {
                IsLocked = true;
                LockedUntil = DateTime.UtcNow.AddMinutes(30);
                AddDomainEvent(new UserLockedEvent(Id, LockedUntil.Value));
            }
        }
    }

    public void VerifyEmail()
    {
        IsEmailVerified = true;
        AddDomainEvent(new UserEmailVerifiedEvent(Id));
    }

    public void LockAccount()
    {
        IsLocked = true;
        LockedUntil = DateTime.UtcNow.AddHours(24);
        AddDomainEvent(new UserLockedEvent(Id, LockedUntil.Value));
    }

    public void UnlockAccount()
    {
        IsLocked = false;
        LockedUntil = null;
        FailedLoginAttempts = 0;
        AddDomainEvent(new UserUnlockedEvent(Id));
    }

    public void DeactivateAccount()
    {
        IsActive = false;
        AddDomainEvent(new UserDeactivatedEvent(Id));
    }

    public void AddRefreshToken(RefreshToken refreshToken)
    {
        _refreshTokens.Add(refreshToken);
    }

    public void RemoveRefreshToken(RefreshToken refreshToken)
    {
        _refreshTokens.Remove(refreshToken);
    }

    public void AddOAuthAccount(OAuthAccount oAuthAccount)
    {
        _oAuthAccounts.Add(oAuthAccount);
    }

    public void UpdatePreferredLanguage(Language language)
    {
        PreferredLanguage = language;
        AddDomainEvent(new UserPreferencesUpdatedEvent(Id));
    }

    private static bool IsValidPassword(string password)
    {
        return password.Length >= 8 &&
               password.Any(char.IsUpper) &&
               password.Any(char.IsLower) &&
               password.Any(char.IsDigit);
    }
}