namespace WhoAndWhat.Application.DTOs.Contacts;

/// <summary>
/// DTO representing a generated QR code for contact sharing
/// </summary>
public class ContactQRCodeDto
{
    /// <summary>
    /// The contact ID that this QR code represents
    /// </summary>
    public Guid ContactId { get; set; }
    
    /// <summary>
    /// Base64 encoded QR code image data
    /// </summary>
    public string QRCodeData { get; set; } = string.Empty;
    
    /// <summary>
    /// MIME type of the QR code image (image/png, image/svg+xml, image/jpeg)
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
    
    /// <summary>
    /// The secure payload encoded in the QR code
    /// </summary>
    public string EncodedPayload { get; set; } = string.Empty;
    
    /// <summary>
    /// When this QR code expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Custom message included in the QR code
    /// </summary>
    public string? CustomMessage { get; set; }
    
    /// <summary>
    /// QR code format (PNG, SVG, JPEG)
    /// </summary>
    public string Format { get; set; } = string.Empty;
    
    /// <summary>
    /// QR code size description (Small, Medium, Large, XLarge)
    /// </summary>
    public string Size { get; set; } = string.Empty;
    
    /// <summary>
    /// Actual pixel dimensions of the QR code
    /// </summary>
    public int PixelSize { get; set; }
    
    /// <summary>
    /// When this QR code was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}