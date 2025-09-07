using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for account deactivation
/// </summary>
public class DeactivateAccountRequest
{
    /// <summary>
    /// Current password for verification
    /// </summary>
    [Required(ErrorMessage = "Current password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// Reason for account deactivation (optional)
    /// </summary>
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string? Reason { get; set; }

    /// <summary>
    /// Confirmation that user understands the consequences
    /// </summary>
    [Required]
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must confirm account deactivation")]
    public bool ConfirmDeactivation { get; set; }
}