using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Contacts.Commands.DeleteContact;

public class DeleteContactCommandHandler : IRequestHandler<DeleteContactCommand, Result<bool>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<DeleteContactCommandHandler> _logger;

    public DeleteContactCommandHandler(
        IContactRepository contactRepository,
        ILogger<DeleteContactCommandHandler> logger)
    {
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteContactCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Attempting to delete contact {ContactId} for user {UserId}", 
                request.ContactId, request.UserId);

            // Get the existing contact
            var existingContact = await _contactRepository.GetByIdAsync(request.ContactId, cancellationToken);
            if (existingContact == null)
            {
                _logger.LogWarning("Contact {ContactId} not found for user {UserId}", 
                    request.ContactId, request.UserId);
                return Result<bool>.Failure("Contact not found");
            }

            // Verify ownership
            if (existingContact.UserId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to delete contact {ContactId} owned by {OwnerId}", 
                    request.UserId, request.ContactId, existingContact.UserId);
                return Result<bool>.Failure("Contact not found");
            }

            // Check if contact is already soft deleted
            if (existingContact.IsDeleted)
            {
                _logger.LogWarning("Contact {ContactId} is already deleted for user {UserId}", 
                    request.ContactId, request.UserId);
                return Result<bool>.Failure("Contact not found");
            }

            // Check if contact has active task associations
            var activeTaskCount = await _contactRepository.CountActiveTaskAssociationsAsync(
                request.ContactId, request.UserId, cancellationToken);

            if (activeTaskCount > 0)
            {
                _logger.LogWarning("Cannot delete contact {ContactId} - it has {ActiveTaskCount} active task associations", 
                    request.ContactId, activeTaskCount);
                return Result<bool>.Failure($"Cannot delete contact because it is associated with {activeTaskCount} active task(s). Please remove the contact from all active tasks first.");
            }

            // Perform soft deletion using repository method
            var deleteResult = await _contactRepository.SoftDeleteContactAsync(
                request.ContactId, request.UserId, cancellationToken);

            if (!deleteResult)
            {
                _logger.LogError("Failed to soft delete contact {ContactId} for user {UserId}", 
                    request.ContactId, request.UserId);
                return Result<bool>.Failure("Failed to delete contact");
            }

            // Save changes
            await _contactRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Contact {ContactId} successfully soft deleted for user {UserId}", 
                request.ContactId, request.UserId);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contact {ContactId} for user {UserId}", 
                request.ContactId, request.UserId);
            return Result<bool>.Failure($"Error deleting contact: {ex.Message}");
        }
    }
}