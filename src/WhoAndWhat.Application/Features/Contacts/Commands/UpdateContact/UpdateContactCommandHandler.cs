using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Contacts.Commands.UpdateContact;

public class UpdateContactCommandHandler : IRequestHandler<UpdateContactCommand, Result<ContactDto>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ContactValidator _contactValidator;
    private readonly ILogger<UpdateContactCommandHandler> _logger;

    public UpdateContactCommandHandler(
        IContactRepository contactRepository,
        ContactValidator contactValidator,
        ILogger<UpdateContactCommandHandler> logger)
    {
        _contactRepository = contactRepository;
        _contactValidator = contactValidator;
        _logger = logger;
    }

    public async Task<Result<ContactDto>> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate input parameters
            if (request.UserId == Guid.Empty)
            {
                return Result<ContactDto>.Failure("User ID is required");
            }

            if (request.ContactId == Guid.Empty)
            {
                return Result<ContactDto>.Failure("Contact ID is required");
            }

            _logger.LogInformation("Updating contact {ContactId} for user {UserId}", 
                request.ContactId, request.UserId);

            // Validate relationship type
            if (!Enum.IsDefined(typeof(ContactRelationType), request.RelationshipType))
            {
                return Result<ContactDto>.Failure("Invalid relationship type");
            }

            // Get the existing contact
            var existingContact = await _contactRepository.GetByIdAsync(request.ContactId, cancellationToken);
            if (existingContact == null)
            {
                _logger.LogWarning("Contact {ContactId} not found for user {UserId}", 
                    request.ContactId, request.UserId);
                return Result<ContactDto>.Failure("Contact not found");
            }

            // Verify ownership
            if (existingContact.UserId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to update contact {ContactId} owned by {OwnerId}", 
                    request.UserId, request.ContactId, existingContact.UserId);
                return Result<ContactDto>.Failure("Contact not found");
            }

            // Check if contact is soft deleted
            if (existingContact.IsDeleted)
            {
                _logger.LogWarning("Attempted to update soft deleted contact {ContactId} for user {UserId}", 
                    request.ContactId, request.UserId);
                return Result<ContactDto>.Failure("Contact not found");
            }

            // Update contact properties
            existingContact.Name = request.Name?.Trim() ?? string.Empty;
            existingContact.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
            existingContact.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            existingContact.RelationshipType = request.RelationshipType;
            existingContact.UpdatedAt = DateTime.UtcNow;

            // Validate the updated contact using FluentValidation
            var validationResult = await _contactValidator.ValidateAsync(existingContact, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Contact validation failed for contact {ContactId}: {Errors}", request.ContactId, errors);
                return Result<ContactDto>.Failure($"Contact validation failed: {errors}");
            }

            // Check for duplicate email within user's contacts (excluding current contact)
            if (!string.IsNullOrWhiteSpace(existingContact.Email))
            {
                var contactsWithSameEmail = await _contactRepository.FindContactsAsync(
                    existingContact.Email, 
                    request.UserId, 
                    false, 
                    cancellationToken);

                if (contactsWithSameEmail.Any(c => c.Id != request.ContactId))
                {
                    _logger.LogWarning("Another contact with email {Email} already exists for user {UserId}", 
                        existingContact.Email, request.UserId);
                    return Result<ContactDto>.Failure("A contact with this email already exists");
                }
            }

            // Save the updated contact
            await _contactRepository.UpdateAsync(existingContact, cancellationToken);
            await _contactRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Contact {ContactId} updated successfully for user {UserId}", 
                request.ContactId, request.UserId);

            // Map to DTO
            var contactDto = MapToContactDto(existingContact);

            return Result<ContactDto>.Success(contactDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contact {ContactId} for user {UserId}", 
                request.ContactId, request.UserId);
            return Result<ContactDto>.Failure($"Error updating contact: {ex.Message}");
        }
    }

    private static ContactDto MapToContactDto(Contact contact)
    {
        return new ContactDto
        {
            Id = contact.Id,
            Name = contact.Name,
            Email = contact.Email,
            Phone = contact.Phone,
            QRCode = contact.QRCode,
            InviteCode = contact.InviteCode,
            RelationshipType = contact.RelationshipType,
            RelationshipTypeName = ((ContactRelationType)contact.RelationshipType).ToString(),
            CreatedAt = contact.CreatedAt,
            UpdatedAt = contact.UpdatedAt,
            IsDeleted = contact.IsDeleted,
            DeletedAt = contact.DeletedAt,
            ActiveTaskCount = contact.Tasks?.Count(t => !t.IsDeleted) ?? 0,
            AssociatedTasks = contact.TaskContacts?.Select(tc => new ContactTaskDto
            {
                TaskId = tc.TaskId,
                TaskTitle = tc.Task?.Title ?? string.Empty,
                TaskStatus = tc.Task?.Status ?? 0,
                TaskStatusName = tc.Task != null ? ((WhoAndWhat.Domain.ValueObjects.AppTaskStatus)tc.Task.Status).ToString() : string.Empty,
                Role = tc.Role,
                LinkedAt = tc.LinkedAt,
                Notes = tc.Notes
            }).ToList() ?? new List<ContactTaskDto>()
        };
    }
}