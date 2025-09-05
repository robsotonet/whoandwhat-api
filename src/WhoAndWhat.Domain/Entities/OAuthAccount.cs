namespace WhoAndWhat.Domain.Entities;

public class OAuthAccount : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Provider { get; private set; }
    public string ExternalId { get; private set; }
    public string? Email { get; private set; }
    public string? Name { get; private set; }
    public string? ProfileImageUrl { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation property
    public User User { get; set; } = null!;

    private OAuthAccount() 
    { 
        Provider = string.Empty;
        ExternalId = string.Empty;
    }

    public OAuthAccount(Guid userId, string provider, string externalId, string? email = null, string? name = null)
    {
        UserId = userId;
        Provider = string.IsNullOrEmpty(provider) ? throw new ArgumentNullException(nameof(provider)) : provider;
        ExternalId = string.IsNullOrEmpty(externalId) ? throw new ArgumentNullException(nameof(externalId)) : externalId;
        Email = email;
        Name = name;
        IsActive = true;
    }

    public void UpdateProfile(string? email, string? name, string? profileImageUrl)
    {
        Email = email;
        Name = name;
        ProfileImageUrl = profileImageUrl;
        MarkAsModified();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsModified();
    }

    public void Reactivate()
    {
        IsActive = true;
        MarkAsModified();
    }
}