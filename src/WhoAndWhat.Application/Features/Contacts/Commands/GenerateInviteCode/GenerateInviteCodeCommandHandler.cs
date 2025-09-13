using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Contacts.Commands.GenerateInviteCode;

public class GenerateInviteCodeCommandHandler : IRequestHandler<GenerateInviteCodeCommand, Result<ContactInviteDto>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<GenerateInviteCodeCommandHandler> _logger;

    public GenerateInviteCodeCommandHandler(
        IContactRepository contactRepository,
        ILogger<GenerateInviteCodeCommandHandler> logger)
    {
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<ContactInviteDto>> Handle(GenerateInviteCodeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating invite code for contact {ContactId} for user {UserId}",
                request.ContactId, request.UserId);

            // Verify the contact exists and belongs to the user
            var contact = await _contactRepository.GetByIdAsync(request.ContactId, cancellationToken);

            if (contact == null)
            {
                _logger.LogWarning("Contact {ContactId} not found", request.ContactId);
                return Result<ContactInviteDto>.Failure("Contact not found");
            }

            if (contact.UserId != request.UserId)
            {
                _logger.LogWarning("Contact {ContactId} does not belong to user {UserId}",
                    request.ContactId, request.UserId);
                return Result<ContactInviteDto>.Failure("Contact not found");
            }

            // Generate unique invite code
            var inviteCode = GenerateUniqueInviteCode(contact.Id, contact.Name);
            var expiresAt = DateTime.UtcNow.AddHours(request.ExpirationHours);

            // Store the invite code in the contact
            contact.InviteCode = inviteCode;
            contact.UpdatedAt = DateTime.UtcNow;

            // Save the updated contact
            var saveResult = await _contactRepository.SaveChangesAsync(cancellationToken);
            if (saveResult == 0)
            {
                _logger.LogError("Failed to save invite code for contact {ContactId}", request.ContactId);
                return Result<ContactInviteDto>.Failure("Failed to generate invite code");
            }

            // Create contact DTO
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
                QRCode = contact.QRCode,
                InviteCode = contact.InviteCode,
                ActiveTaskCount = 0,
                AssociatedTasks = new List<ContactTaskDto>()
            };

            // Create shareable text
            var shareableText = CreateShareableText(inviteCode, contact.Name, request.CustomMessage);

            var result = new ContactInviteDto
            {
                ContactId = request.ContactId,
                InviteCode = inviteCode,
                ExpiresAt = expiresAt,
                CustomMessage = request.CustomMessage,
                AllowMultipleUses = request.AllowMultipleUses,
                UsageCount = 0, // Initial usage count
                GeneratedAt = DateTime.UtcNow,
                ContactInfo = contactDto,
                ShareableText = shareableText
            };

            _logger.LogInformation("Successfully generated invite code {InviteCode} for contact {ContactId}",
                inviteCode, request.ContactId);

            return Result<ContactInviteDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invite code for contact {ContactId} for user {UserId}",
                request.ContactId, request.UserId);
            return Result<ContactInviteDto>.Failure($"Error generating invite code: {ex.Message}");
        }
    }

    private string GenerateUniqueInviteCode(Guid contactId, string contactName)
    {
        // Create a unique string combining contact ID, name, and current timestamp
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var input = $"{contactId}:{contactName}:{timestamp}";

        // Generate hash using SHA256
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

        // Convert to base32-like string (using only uppercase letters and digits for better readability)
        var base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();

        // Take first 8 bytes for a reasonable length code
        for (int i = 0; i < Math.Min(8, hashBytes.Length); i++)
        {
            result.Append(base32Chars[hashBytes[i] % base32Chars.Length]);
        }

        // Add a random 4-character suffix for additional uniqueness using secure random generation
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        
        for (int i = 0; i < 4; i++)
        {
            result.Append(base32Chars[randomBytes[i] % base32Chars.Length]);
        }

        // Format as XXXX-XXXX-XXXX for readability
        var code = result.ToString();
        return $"{code.Substring(0, 4)}-{code.Substring(4, 4)}-{code.Substring(8, 4)}";
    }

    private string CreateShareableText(string inviteCode, string contactName, string? customMessage)
    {
        var text = new StringBuilder();

        text.AppendLine($"🤝 Connect with {contactName}!");

        if (!string.IsNullOrEmpty(customMessage))
        {
            text.AppendLine($"Message: {customMessage}");
        }

        text.AppendLine($"Use invite code: {inviteCode}");
        text.AppendLine("📱 Add this contact to your WhoAndWhat app");

        return text.ToString();
    }
}
