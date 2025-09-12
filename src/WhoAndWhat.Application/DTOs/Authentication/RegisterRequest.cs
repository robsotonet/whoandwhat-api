using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for user registration
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// User's email address (will be used as login)
    /// </summary>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's chosen username (display name)
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's password (min 8 characters, must contain uppercase, lowercase, and number)
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Password confirmation (must match password)
    /// </summary>
    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// User's preferred language (en or es)
    /// </summary>
    [Required]
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>
    /// User accepts terms of service
    /// </summary>
    [Required]
    public bool AcceptTerms { get; set; }
}
