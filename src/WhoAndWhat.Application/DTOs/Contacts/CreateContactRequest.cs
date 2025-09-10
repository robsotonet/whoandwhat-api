using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Contacts;

public class CreateContactRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(320)] // Maximum email length per RFC
    public string? Email { get; set; }

    [Phone]
    [StringLength(50)]
    public string? Phone { get; set; }

    [Required]
    [Range(0, 3)] // ContactRelationType enum values: Family=0, Friend=1, Colleague=2, Other=3
    public int RelationshipType { get; set; }
}

public class InviteContactRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Range(0, 3)] // ContactRelationType enum values
    public int RelationshipType { get; set; }

    [StringLength(500)]
    public string? InvitationMessage { get; set; }
}

public class QRContactRequest
{
    [Required]
    [StringLength(1000)] // QR code data can be quite long
    public string QRCodeData { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; set; }
}