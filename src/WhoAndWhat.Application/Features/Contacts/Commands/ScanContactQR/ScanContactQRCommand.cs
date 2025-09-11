using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;

namespace WhoAndWhat.Application.Features.Contacts.Commands.ScanContactQR;

/// <summary>
/// Command to scan and process a QR code for adding a contact
/// </summary>
public record ScanContactQRCommand(
    string QRCodePayload,
    Guid UserId,
    string? CustomNotes = null
) : IRequest<Result<ContactDto>>;
