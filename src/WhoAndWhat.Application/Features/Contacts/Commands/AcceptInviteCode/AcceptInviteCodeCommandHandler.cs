using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Contacts.Commands.AcceptInviteCode;

public class AcceptInviteCodeCommandHandler : IRequestHandler<AcceptInviteCodeCommand, Result<ContactDto>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ContactValidator _contactValidator;
    private readonly ILogger<AcceptInviteCodeCommandHandler> _logger;

    public AcceptInviteCodeCommandHandler(
        IContactRepository contactRepository,
        ContactValidator contactValidator,
        ILogger<AcceptInviteCodeCommandHandler> logger)
    {
        _contactRepository = contactRepository;
        _contactValidator = contactValidator;
        _logger = logger;
    }

    public async Task<Result<ContactDto>> Handle(AcceptInviteCodeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing invite code {InviteCode} for user {UserId}",
                request.InviteCode, request.UserId);

            // Validate invite code format
            if (string.IsNullOrWhiteSpace(request.InviteCode))
            {
                return Result<ContactDto>.Failure("Invite code is required");
            }

            // Find contact with matching invite code
            var originalContact = await FindContactByInviteCodeAsync(request.InviteCode, cancellationToken);
            if (originalContact == null)
            {
                _logger.LogWarning("Invite code {InviteCode} not found", request.InviteCode);
                return Result<ContactDto>.Failure("Invalid or expired invite code");
            }

            // Cannot accept your own invite code
            if (originalContact.UserId == request.UserId)
            {
                _logger.LogWarning("User {UserId} tried to accept their own invite code {InviteCode}",
                    request.UserId, request.InviteCode);
                return Result<ContactDto>.Failure("You cannot add yourself as a contact");
            }

            // Check if contact already exists for this user
            var existingContact = await CheckForExistingContactAsync(originalContact, request.UserId, cancellationToken);
            if (existingContact != null)
            {
                _logger.LogInformation("Contact already exists for user {UserId} with email {Email}",
                    request.UserId, originalContact.Email);
                return Result<ContactDto>.Failure("You already have this contact in your list");
            }

            // Create new contact for the requesting user
            var newContact = new Contact
            {
                Id = Guid.NewGuid(),
                Name = originalContact.Name,
                Email = originalContact.Email,
                Phone = originalContact.Phone,
                RelationshipType = (int)ContactRelationType.Friend, // Default relationship type
                UserId = request.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            // Validate the new contact
            var validationResult = await _contactValidator.ValidateAsync(newContact);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Contact validation failed: {Errors}", errors);
                return Result<ContactDto>.Failure($"Invalid contact data: {errors}");
            }

            // Save the new contact
            await _contactRepository.AddAsync(newContact, cancellationToken);
            var saveResult = await _contactRepository.SaveChangesAsync(cancellationToken);

            if (saveResult == 0)
            {
                _logger.LogError("Failed to save contact from invite code {InviteCode} for user {UserId}",
                    request.InviteCode, request.UserId);
                return Result<ContactDto>.Failure("Failed to add contact from invite code");
            }

            // Map to DTO and return
            var contactDto = new ContactDto
            {
                Id = newContact.Id,
                Name = newContact.Name,
                Email = newContact.Email,
                Phone = newContact.Phone,
                RelationshipType = newContact.RelationshipType,
                RelationshipTypeName = ((ContactRelationType)newContact.RelationshipType).ToString(),
                CreatedAt = newContact.CreatedAt,
                UpdatedAt = newContact.UpdatedAt,
                IsDeleted = newContact.IsDeleted,
                DeletedAt = newContact.DeletedAt,
                ActiveTaskCount = 0,
                AssociatedTasks = new List<ContactTaskDto>()
            };

            _logger.LogInformation("Successfully added contact {ContactId} from invite code {InviteCode} for user {UserId}",
                newContact.Id, request.InviteCode, request.UserId);

            return Result<ContactDto>.Success(contactDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing invite code {InviteCode} for user {UserId}",
                request.InviteCode, request.UserId);
            return Result<ContactDto>.Failure($"Error processing invite code: {ex.Message}");
        }
    }

    private async Task<Contact?> FindContactByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken)
    {
        try
        {
            return await _contactRepository.FindContactByInviteCodeAsync(inviteCode, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding contact by invite code {InviteCode}", inviteCode);
            return null;
        }
    }

    private async Task<Contact?> CheckForExistingContactAsync(Contact originalContact, Guid userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(originalContact.Email))
        {
            return null;
        }

        var existingContacts = await _contactRepository.FindContactsAsync(originalContact.Email, userId, false, cancellationToken);
        return existingContacts.FirstOrDefault();
    }
}
