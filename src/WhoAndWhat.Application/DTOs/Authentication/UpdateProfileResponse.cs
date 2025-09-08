namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Response model for profile update operations
/// </summary>
public class UpdateProfileResponse
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
    /// User's updated username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's updated preferred language
    /// </summary>
    public string PreferredLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Success message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
