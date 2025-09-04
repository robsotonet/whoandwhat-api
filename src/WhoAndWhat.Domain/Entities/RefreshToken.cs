namespace WhoAndWhat.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByToken { get; private set; }
    public string CreatedByIp { get; private set; }
    public string? RevokedByIp { get; private set; }

    // Navigation property
    public User User { get; set; } = null!;

    private RefreshToken() 
    { 
        Token = string.Empty;
        CreatedByIp = string.Empty;
    }

    public RefreshToken(Guid userId, string token, DateTime expiresAt, string createdByIp)
    {
        UserId = userId;
        Token = string.IsNullOrEmpty(token) ? throw new ArgumentNullException(nameof(token)) : token;
        ExpiresAt = expiresAt;
        CreatedByIp = string.IsNullOrEmpty(createdByIp) ? throw new ArgumentNullException(nameof(createdByIp)) : createdByIp;
        IsRevoked = false;
    }

    public void Revoke(string revokedByIp, string? replacedByToken = null)
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByToken = replacedByToken;
        MarkAsModified();
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;
}