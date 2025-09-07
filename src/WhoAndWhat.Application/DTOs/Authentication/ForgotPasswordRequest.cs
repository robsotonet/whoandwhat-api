using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for password reset initiation
/// </summary>
public record ForgotPasswordRequest
{
    /// <summary>
    /// Email address to send password reset instructions to
    /// </summary>
    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [StringLength(254, ErrorMessage = "Email address must not exceed 254 characters")]
    public string Email { get; init; } = string.Empty;
}