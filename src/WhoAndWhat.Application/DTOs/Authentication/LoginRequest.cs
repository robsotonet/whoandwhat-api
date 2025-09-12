using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for user authentication
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// User's email address
    /// </summary>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's password
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Remember user session for extended period
    /// </summary>
    public bool RememberMe { get; set; } = false;
}
