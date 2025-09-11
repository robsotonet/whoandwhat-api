using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Contacts.Commands.PermanentlyDeleteContact;

public class PermanentlyDeleteContactCommandHandler : IRequestHandler<PermanentlyDeleteContactCommand, Result<bool>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<PermanentlyDeleteContactCommandHandler> _logger;

    public PermanentlyDeleteContactCommandHandler(
        IContactRepository contactRepository,
        ILogger<PermanentlyDeleteContactCommandHandler> logger)
    {
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(PermanentlyDeleteContactCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Permanently deleting contact {ContactId} for user {UserId}", request.ContactId, request.UserId);

            // Get the soft-deleted contact
            var contact = await _contactRepository.GetContactIncludingDeletedAsync(request.ContactId, request.UserId, cancellationToken);

            if (contact == null)
            {
                _logger.LogWarning("Contact {ContactId} not found for user {UserId}", request.ContactId, request.UserId);
                return Result<bool>.Failure("Contact not found");
            }

            if (!contact.IsDeleted)
            {
                _logger.LogWarning("Contact {ContactId} is not soft-deleted and cannot be permanently deleted", request.ContactId);
                return Result<bool>.Failure("Contact must be soft-deleted before permanent deletion");
            }

            // Check if contact has any task associations that would prevent deletion
            if (contact.TaskContacts?.Any() == true)
            {
                _logger.LogWarning("Contact {ContactId} has task associations and cannot be permanently deleted", request.ContactId);
                return Result<bool>.Failure("Contact has task associations and cannot be permanently deleted");
            }

            // Permanently delete the contact
            _contactRepository.Remove(contact);
            await _contactRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully permanently deleted contact {ContactId} for user {UserId}", request.ContactId, request.UserId);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error permanently deleting contact {ContactId} for user {UserId}", request.ContactId, request.UserId);
            return Result<bool>.Failure("An error occurred while permanently deleting the contact");
        }
    }
}
