using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Authentication;

/// <summary>
/// Request model for updating user profile information
/// </summary>
public class UpdateProfileRequest
{
    /// <summary>
    /// User's username (optional update)
    /// </summary>
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username can only contain letters, numbers, underscore, and hyphen")]
    public string? Username { get; set; }

    /// <summary>
    /// User's preferred language (en, es)
    /// </summary>
    [RegularExpression(@"^(en|es)$", ErrorMessage = "Language must be 'en' or 'es'")]
    public string? PreferredLanguage { get; set; }
}
