namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Response model for current user information
/// </summary>
public class CurrentUserResponse
{
    /// <summary>
    /// Unique user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's preferred language
    /// </summary>
    public string PreferredLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if the user's email is verified
    /// </summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>
    /// Indicates if the user account is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Account creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
}