using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for user logout
/// </summary>
public class LogoutRequest
{
    /// <summary>
    /// Refresh token to revoke (optional - if not provided, all user tokens will be revoked)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Revoke all refresh tokens for this user
    /// </summary>
    public bool RevokeAllTokens { get; set; } = false;
}