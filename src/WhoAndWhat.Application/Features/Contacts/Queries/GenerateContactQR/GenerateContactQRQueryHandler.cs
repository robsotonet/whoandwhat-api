using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using QRCoder;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Contacts.Queries.GenerateContactQR;

public class GenerateContactQRQueryHandler : IRequestHandler<GenerateContactQRQuery, Result<ContactQRCodeDto>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<GenerateContactQRQueryHandler> _logger;

    public GenerateContactQRQueryHandler(
        IContactRepository contactRepository,
        ILogger<GenerateContactQRQueryHandler> logger)
    {
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<ContactQRCodeDto>> Handle(GenerateContactQRQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating QR code for contact {ContactId} for user {UserId}", 
                request.ContactId, request.UserId);

            // Verify the contact exists and belongs to the user
            var contact = await _contactRepository.GetByIdAsync(request.ContactId, cancellationToken);
            
            if (contact == null)
            {
                _logger.LogWarning("Contact {ContactId} not found", request.ContactId);
                return Result<ContactQRCodeDto>.Failure("Contact not found");
            }

            if (contact.UserId != request.UserId)
            {
                _logger.LogWarning("Contact {ContactId} does not belong to user {UserId}", 
                    request.ContactId, request.UserId);
                return Result<ContactQRCodeDto>.Failure("Contact not found");
            }

            // Generate expiration time
            var expiresAt = DateTime.UtcNow.AddHours(request.ExpirationHours);

            // Create secure payload
            var payload = new
            {
                ContactId = request.ContactId,
                Name = contact.Name,
                Email = contact.Email,
                Phone = contact.Phone,
                RelationshipType = contact.RelationshipType,
                CustomMessage = request.CustomMessage,
                ExpiresAt = expiresAt,
                Signature = GenerateSignature(request.ContactId.ToString(), expiresAt)
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonPayload));

            // Generate QR code
            var qrCodeData = GenerateQRCodeImage(encodedPayload, request.Format, request.Size);
            var contentType = GetContentType(request.Format);
            var pixelSize = GetPixelSize(request.Size);

            var result = new ContactQRCodeDto
            {
                ContactId = request.ContactId,
                QRCodeData = qrCodeData,
                ContentType = contentType,
                EncodedPayload = encodedPayload,
                ExpiresAt = expiresAt,
                CustomMessage = request.CustomMessage,
                Format = request.Format.ToString(),
                Size = request.Size.ToString(),
                PixelSize = pixelSize,
                GeneratedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Successfully generated QR code for contact {ContactId}", request.ContactId);
            return Result<ContactQRCodeDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating QR code for contact {ContactId} for user {UserId}", 
                request.ContactId, request.UserId);
            return Result<ContactQRCodeDto>.Failure($"Error generating QR code: {ex.Message}");
        }
    }

    private string GenerateQRCodeImage(string payload, QRCodeFormat format, QRCodeSize size)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        
        return format switch
        {
            QRCodeFormat.PNG => GeneratePngQRCode(qrCodeData, size),
            QRCodeFormat.SVG => GenerateSvgQRCode(qrCodeData, size),
            QRCodeFormat.JPEG => GenerateJpegQRCode(qrCodeData, size),
            _ => GeneratePngQRCode(qrCodeData, size)
        };
    }

    private string GeneratePngQRCode(QRCodeData qrCodeData, QRCodeSize size)
    {
        using var qrCode = new PngByteQRCode(qrCodeData);
        var pixelsPerModule = GetPixelsPerModule(size);
        var qrCodeImage = qrCode.GetGraphic(pixelsPerModule);
        return Convert.ToBase64String(qrCodeImage);
    }

    private string GenerateSvgQRCode(QRCodeData qrCodeData, QRCodeSize size)
    {
        using var qrCode = new SvgQRCode(qrCodeData);
        var pixelsPerModule = GetPixelsPerModule(size);
        var svgString = qrCode.GetGraphic(pixelsPerModule);
        var svgBytes = Encoding.UTF8.GetBytes(svgString);
        return Convert.ToBase64String(svgBytes);
    }

    private string GenerateJpegQRCode(QRCodeData qrCodeData, QRCodeSize size)
    {
        // QRCoder doesn't have native JPEG support, so we'll use PNG and note this limitation
        return GeneratePngQRCode(qrCodeData, size);
    }

    private int GetPixelsPerModule(QRCodeSize size) => size switch
    {
        QRCodeSize.Small => 5,   // ~100x100 for typical QR codes
        QRCodeSize.Medium => 10, // ~200x200
        QRCodeSize.Large => 20,  // ~400x400  
        QRCodeSize.XLarge => 30, // ~600x600
        _ => 10
    };

    private int GetPixelSize(QRCodeSize size) => size switch
    {
        QRCodeSize.Small => 100,
        QRCodeSize.Medium => 200,
        QRCodeSize.Large => 400,
        QRCodeSize.XLarge => 600,
        _ => 200
    };

    private string GetContentType(QRCodeFormat format) => format switch
    {
        QRCodeFormat.PNG => "image/png",
        QRCodeFormat.SVG => "image/svg+xml",
        QRCodeFormat.JPEG => "image/jpeg",
        _ => "image/png"
    };

    private string GenerateSignature(string contactId, DateTime expiresAt)
    {
        // Simple signature generation - in production, use a proper signing key
        var data = $"{contactId}:{expiresAt:yyyy-MM-ddTHH:mm:ssZ}";
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(dataBytes);
        return Convert.ToBase64String(hash);
    }
}