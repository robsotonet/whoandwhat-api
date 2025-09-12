namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Response model for refresh token operations
/// </summary>
public class RefreshTokenResponse
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// New access token
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// New refresh token
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Token type (typically "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Token expiration time in seconds
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Token issued timestamp
    /// </summary>
    public DateTime IssuedAt { get; set; }
}
