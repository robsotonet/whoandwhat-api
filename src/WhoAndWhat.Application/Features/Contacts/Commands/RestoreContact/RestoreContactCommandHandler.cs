using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Contacts.Commands.RestoreContact;

public class RestoreContactCommandHandler : IRequestHandler<RestoreContactCommand, Result<ContactDto>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<RestoreContactCommandHandler> _logger;

    public RestoreContactCommandHandler(
        IContactRepository contactRepository,
        ILogger<RestoreContactCommandHandler> logger)
    {
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<ContactDto>> Handle(RestoreContactCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Restoring contact {ContactId} for user {UserId}", request.ContactId, request.UserId);

            // Get the soft-deleted contact
            var contact = await _contactRepository.GetContactIncludingDeletedAsync(request.ContactId, request.UserId, cancellationToken);
            
            if (contact == null)
            {
                _logger.LogWarning("Contact {ContactId} not found for user {UserId}", request.ContactId, request.UserId);
                return Result<ContactDto>.Failure("Contact not found");
            }

            if (!contact.IsDeleted)
            {
                _logger.LogWarning("Contact {ContactId} is not soft-deleted", request.ContactId);
                return Result<ContactDto>.Failure("Contact is not deleted and cannot be restored");
            }

            // Restore the contact
            contact.IsDeleted = false;
            contact.DeletedAt = null;
            contact.UpdatedAt = DateTime.UtcNow;

            _contactRepository.Update(contact);
            await _contactRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully restored contact {ContactId} for user {UserId}", request.ContactId, request.UserId);

            // Return the restored contact
            var contactDto = new ContactDto
            {
                Id = contact.Id,
                Name = contact.Name,
                Email = contact.Email,
                Phone = contact.Phone,
                QRCode = contact.QRCode,
                InviteCode = contact.InviteCode,
                RelationshipType = contact.RelationshipType,
                CreatedAt = contact.CreatedAt,
                UpdatedAt = contact.UpdatedAt,
                IsDeleted = contact.IsDeleted,
                DeletedAt = contact.DeletedAt
            };

            return Result<ContactDto>.Success(contactDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring contact {ContactId} for user {UserId}", request.ContactId, request.UserId);
            return Result<ContactDto>.Failure("An error occurred while restoring the contact");
        }
    }
}