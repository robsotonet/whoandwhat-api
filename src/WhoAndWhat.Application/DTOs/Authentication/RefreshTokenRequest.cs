using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for token refresh
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Refresh token to exchange for new access token
    /// </summary>
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
