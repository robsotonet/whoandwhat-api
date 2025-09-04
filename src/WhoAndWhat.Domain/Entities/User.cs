using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Domain.Entities;

public class User
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

    public ICollection<Task> Tasks { get; set; } = new List<Task>();
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}