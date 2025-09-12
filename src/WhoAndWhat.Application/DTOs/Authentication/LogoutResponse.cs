namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Response model for user logout
/// </summary>
public class LogoutResponse
{
    /// <summary>
    /// Success message
    /// </summary>
    public string Message { get; set; } = "Logged out successfully";

    /// <summary>
    /// Number of tokens revoked
    /// </summary>
    public int TokensRevoked { get; set; }

    /// <summary>
    /// Timestamp of logout
    /// </summary>
    public DateTime LogoutAt { get; set; } = DateTime.UtcNow;
}
