using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for changing password while authenticated
/// </summary>
public record ChangePasswordRequest
{
    /// <summary>
    /// Current password for verification
    /// </summary>
    [Required(ErrorMessage = "Current password is required")]
    [StringLength(100, ErrorMessage = "Current password is invalid")]
    public string CurrentPassword { get; init; } = string.Empty;

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