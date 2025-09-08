using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for password reset completion
/// </summary>
public record ResetPasswordRequest
{
    /// <summary>
    /// Email address of the account
    /// </summary>
    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Password reset token received via email
    /// </summary>
    [Required(ErrorMessage = "Reset token is required")]
    [StringLength(200, ErrorMessage = "Reset token is invalid")]
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// New password for the account
    /// </summary>
    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).*$", 
        ErrorMessage = "Password must contain at least one lowercase letter, one uppercase letter, and one digit")]
    public string NewPassword { get; init; } = string.Empty;

    /// <summary>
    /// Confirmation of the new password
    /// </summary>
    [Required(ErrorMessage = "Password confirmation is required")]
    [Compare(nameof(NewPassword), ErrorMessage = "Password and confirmation do not match")]
    public string ConfirmPassword { get; init; } = string.Empty;
}