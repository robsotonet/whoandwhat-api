using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;

namespace WhoAndWhat.Application.Features.Contacts.Queries.GenerateContactQR;

/// <summary>
/// Query to generate a QR code for sharing contact information
/// </summary>
public record GenerateContactQRQuery(
    Guid ContactId,
    Guid UserId,
    string? CustomMessage = null,
    int ExpirationHours = 24,
    QRCodeFormat Format = QRCodeFormat.PNG,
    QRCodeSize Size = QRCodeSize.Medium
) : IRequest<Result<ContactQRCodeDto>>;

/// <summary>
/// QR code format options
/// </summary>
public enum QRCodeFormat
{
    PNG,
    SVG,
    JPEG
}

/// <summary>
/// QR code size options
/// </summary>
public enum QRCodeSize
{
    Small,    // 100x100
    Medium,   // 200x200
    Large,    // 400x400
    XLarge    // 600x600
}