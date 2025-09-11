using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Contacts.Commands.ScanContactQR;

public class ScanContactQRCommandHandler : IRequestHandler<ScanContactQRCommand, Result<ContactDto>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ContactValidator _contactValidator;
    private readonly ILogger<ScanContactQRCommandHandler> _logger;

    public ScanContactQRCommandHandler(
        IContactRepository contactRepository,
        ContactValidator contactValidator,
        ILogger<ScanContactQRCommandHandler> logger)
    {
        _contactRepository = contactRepository;
        _contactValidator = contactValidator;
        _logger = logger;
    }

    public async Task<Result<ContactDto>> Handle(ScanContactQRCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing scanned QR code for user {UserId}", request.UserId);

            // Decode the QR payload
            var payloadResult = DecodeQRPayload(request.QRCodePayload);
            if (!payloadResult.IsValid)
            {
                _logger.LogWarning("Invalid QR code payload for user {UserId}: {Error}",
                    request.UserId, payloadResult.Error);
                return Result<ContactDto>.Failure(payloadResult.Error);
            }

            var payload = payloadResult.Payload;

            // Validate expiration
            if (payload.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("QR code expired for contact {ContactId}", payload.ContactId);
                return Result<ContactDto>.Failure("QR code has expired");
            }

            // Validate signature
            var expectedSignature = GenerateSignature(payload.ContactId.ToString(), payload.ExpiresAt);
            if (payload.Signature != expectedSignature)
            {
                _logger.LogWarning("Invalid QR code signature for contact {ContactId}", payload.ContactId);
                return Result<ContactDto>.Failure("Invalid QR code signature");
            }

            // Check if contact already exists for this user
            if (!string.IsNullOrEmpty(payload.Email))
            {
                var existingContacts = await _contactRepository.FindContactsAsync(payload.Email, request.UserId, false, cancellationToken);
                if (existingContacts.Any())
                {
                    _logger.LogInformation("Contact with email {Email} already exists for user {UserId}",
                        payload.Email, request.UserId);
                    return Result<ContactDto>.Failure("A contact with this email already exists");
                }
            }

            // Create new contact from QR payload
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                Name = payload.Name,
                Email = payload.Email,
                Phone = payload.Phone,
                RelationshipType = payload.RelationshipType,
                UserId = request.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            // Validate the contact
            var validationResult = await _contactValidator.ValidateAsync(contact);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Contact validation failed: {Errors}", errors);
                return Result<ContactDto>.Failure($"Invalid contact data: {errors}");
            }

            // Save the contact
            await _contactRepository.AddAsync(contact, cancellationToken);
            var saveResult = await _contactRepository.SaveChangesAsync(cancellationToken);

            if (saveResult == 0)
            {
                _logger.LogError("Failed to save contact from QR scan for user {UserId}", request.UserId);
                return Result<ContactDto>.Failure("Failed to create contact from QR code");
            }

            // Map to DTO and return
            var contactDto = new ContactDto
            {
                Id = contact.Id,
                Name = contact.Name,
                Email = contact.Email,
                Phone = contact.Phone,
                RelationshipType = contact.RelationshipType,
                RelationshipTypeName = ((ContactRelationType)contact.RelationshipType).ToString(),
                CreatedAt = contact.CreatedAt,
                UpdatedAt = contact.UpdatedAt,
                IsDeleted = contact.IsDeleted,
                DeletedAt = contact.DeletedAt,
                ActiveTaskCount = 0,
                AssociatedTasks = new List<ContactTaskDto>()
            };

            _logger.LogInformation("Successfully created contact {ContactId} from QR scan for user {UserId}",
                contact.Id, request.UserId);

            return Result<ContactDto>.Success(contactDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing QR code scan for user {UserId}", request.UserId);
            return Result<ContactDto>.Failure($"Error processing QR code: {ex.Message}");
        }
    }

    private (bool IsValid, string Error, QRPayload Payload) DecodeQRPayload(string encodedPayload)
    {
        try
        {
            // Decode base64
            var jsonBytes = Convert.FromBase64String(encodedPayload);
            var jsonString = Encoding.UTF8.GetString(jsonBytes);

            // Deserialize JSON
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var payload = JsonSerializer.Deserialize<QRPayload>(jsonString, options);

            if (payload == null)
            {
                return (false, "Invalid QR code payload format", null!);
            }

            // Basic validation
            if (payload.ContactId == Guid.Empty)
            {
                return (false, "Invalid contact ID in QR code", null!);
            }

            if (string.IsNullOrWhiteSpace(payload.Name))
            {
                return (false, "Contact name is required", null!);
            }

            return (true, string.Empty, payload);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to decode QR code payload: {ex.Message}", null!);
        }
    }

    private string GenerateSignature(string contactId, DateTime expiresAt)
    {
        // Use the same signature generation logic as the QR generation
        var data = $"{contactId}:{expiresAt:yyyy-MM-ddTHH:mm:ssZ}";
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(dataBytes);
        return Convert.ToBase64String(hash);
    }

    private class QRPayload
    {
        public Guid ContactId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int RelationshipType { get; set; }
        public string? CustomMessage { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Signature { get; set; } = string.Empty;
    }
}
