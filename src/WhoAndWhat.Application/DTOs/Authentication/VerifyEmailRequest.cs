using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for email verification
/// </summary>
public record VerifyEmailRequest
{
    /// <summary>
    /// User ID for the account being verified
    /// </summary>
    [Required(ErrorMessage = "User ID is required")]
    public Guid UserId { get; init; }

    /// <summary>
    /// Email verification token received via email
    /// </summary>
    [Required(ErrorMessage = "Verification token is required")]
    [StringLength(200, ErrorMessage = "Verification token is invalid")]
    public string Token { get; init; } = string.Empty;
}