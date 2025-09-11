using System.ComponentModel.DataAnnotations;

namespace WhoAndWhat.Application.DTOs.Contacts;

public class UpdateContactRequest
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