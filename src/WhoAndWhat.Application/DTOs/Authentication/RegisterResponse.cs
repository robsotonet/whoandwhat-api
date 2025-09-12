namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Response model for user registration
/// </summary>
public class RegisterResponse
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
    /// Indicates if email verification is required
    /// </summary>
    public bool RequiresEmailVerification { get; set; } = true;

    /// <summary>
    /// JWT access token (if email verification is not required)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// JWT refresh token (if email verification is not required)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token expiration time in seconds (if tokens provided)
    /// </summary>
    public int? ExpiresIn { get; set; }

    /// <summary>
    /// Registration success message
    /// </summary>
    public string Message { get; set; } = "User registered successfully. Please verify your email.";
}
