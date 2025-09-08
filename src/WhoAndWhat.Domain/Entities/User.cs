using WhoAndWhat.Domain.ValueObjects;
using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; private set; } = null!;
    public string Username { get; private set; } = null!;
    public Language PreferredLanguage { get; private set; }
    public DateTime LastLoginAt { get; private set; }
    
    // Authentication properties
    public string PasswordHash { get; private set; } = null!;
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiryTime { get; private set; }
    public bool IsEmailVerified { get; private set; }
    
    // Account verification properties  
    public string? VerificationToken { get; private set; }
    
    // Password reset properties
    public string? ResetToken { get; private set; }
    public DateTime? ResetTokenExpires { get; private set; }

    public bool IsActive { get; private set; }
    public bool IsLocked { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public int FailedLoginAttempts { get; private set; }


    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    private readonly List<OAuthAccount> _oAuthAccounts = new();
    public IReadOnlyList<OAuthAccount> OAuthAccounts => _oAuthAccounts.AsReadOnly();

    // Navigation properties
    public ICollection<AppTask> Tasks { get; set; } = new List<AppTask>();
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();

    private User() 
    { 
        Email = string.Empty;
        Username = string.Empty;
        PreferredLanguage = Language.en;
    }

    // Protected constructor for testing purposes - allows setting specific Id
    protected User(Guid id, string email, string username, Language preferredLanguage, DateTime? createdAt = null) 
        : base(id, createdAt)
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
        MarkAsModified();
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
            MarkAsModified();
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
            MarkAsModified();
        }
    }

    public void VerifyEmail()
    {
        IsEmailVerified = true;
        MarkAsModified();
        AddDomainEvent(new UserEmailVerifiedEvent(Id));
    }

    public void LockAccount()
    {
        IsLocked = true;
        LockedUntil = DateTime.UtcNow.AddHours(24);
        MarkAsModified();
        AddDomainEvent(new UserLockedEvent(Id, LockedUntil.Value));
    }

    // Internal method for testing - allows setting a custom lock expiry time
    // Made internal and exposed via InternalsVisibleTo attribute for test assemblies
    internal void LockAccountUntil(DateTime lockedUntil)
    {
        IsLocked = true;
        LockedUntil = lockedUntil;
        MarkAsModified();
        AddDomainEvent(new UserLockedEvent(Id, LockedUntil.Value));
    }

    public void UnlockAccount()
    {
        IsLocked = false;
        LockedUntil = null;
        FailedLoginAttempts = 0;
        MarkAsModified();
        AddDomainEvent(new UserUnlockedEvent(Id));
    }

    public void DeactivateAccount()
    {
        IsActive = false;
        MarkAsModified();
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
        MarkAsModified();
        AddDomainEvent(new UserPreferencesUpdatedEvent(Id));
    }

    public void UpdateEmail(string newEmail)
    {
        if (string.IsNullOrWhiteSpace(newEmail))
        {
            throw new ArgumentNullException(nameof(newEmail));
        }
        
        if (!IsValidEmail(newEmail))
        {
            throw new ArgumentException("Invalid email format", nameof(newEmail));
        }

        Email = newEmail;
        IsEmailVerified = false; // Reset verification when email changes
        MarkAsModified();
    }

    public void UpdateUsername(string newUsername)
    {
        if (string.IsNullOrWhiteSpace(newUsername))
        {
            throw new ArgumentNullException(nameof(newUsername));
        }

        if (newUsername.Length < 3 || newUsername.Length > 50)
        {
            throw new ArgumentException("Username must be between 3 and 50 characters", nameof(newUsername));
        }

        Username = newUsername;
        MarkAsModified();
    }

    public void SetRefreshToken(string token, DateTime expiry)
    {
        RefreshToken = token;
        RefreshTokenExpiryTime = expiry;
        MarkAsModified();
    }

    public void ClearRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiryTime = null;
        MarkAsModified();
    }

    public void SetVerificationToken(string token)
    {
        VerificationToken = token;
        MarkAsModified();
    }

    public void ClearVerificationToken()
    {
        VerificationToken = null;
        MarkAsModified();
    }

    public void SetPasswordResetToken(string token, DateTime expiry)
    {
        ResetToken = token;
        ResetTokenExpires = expiry;
        MarkAsModified();
    }

    public void ClearPasswordResetToken()
    {
        ResetToken = null;
        ResetTokenExpires = null;
        MarkAsModified();
    }

    private static bool IsValidPassword(string password)
    {
        return password.Length >= 8 &&
               password.Any(char.IsUpper) &&
               password.Any(char.IsLower) &&
               password.Any(char.IsDigit);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var emailAddress = new System.Net.Mail.MailAddress(email);
            return emailAddress.Address == email;
        }
        catch
        {
            return false;
        }
    }
}