namespace WhoAndWhat.Application.DTOs.Contacts;

/// <summary>
/// DTO representing a generated invite code for contact sharing
/// </summary>
public class ContactInviteDto
{
    /// <summary>
    /// The contact ID this invite code represents
    /// </summary>
    public Guid ContactId { get; set; }
    
    /// <summary>
    /// The unique invite code
    /// </summary>
    public string InviteCode { get; set; } = string.Empty;
    
    /// <summary>
    /// When this invite code expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Custom message included with the invite
    /// </summary>
    public string? CustomMessage { get; set; }
    
    /// <summary>
    /// Whether this code can be used multiple times
    /// </summary>
    public bool AllowMultipleUses { get; set; }
    
    /// <summary>
    /// How many times this code has been used
    /// </summary>
    public int UsageCount { get; set; }
    
    /// <summary>
    /// When this invite code was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }
    
    /// <summary>
    /// Contact information that will be shared
    /// </summary>
    public ContactDto ContactInfo { get; set; } = new();
    
    /// <summary>
    /// Shareable URL/text that includes the invite code
    /// </summary>
    public string ShareableText { get; set; } = string.Empty;
}