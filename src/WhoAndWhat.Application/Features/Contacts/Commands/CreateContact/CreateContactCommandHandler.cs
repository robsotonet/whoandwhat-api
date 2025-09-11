using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Validators;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Contacts.Commands.CreateContact;

public class CreateContactCommandHandler : IRequestHandler<CreateContactCommand, Result<ContactDto>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ContactValidator _contactValidator;
    private readonly ILogger<CreateContactCommandHandler> _logger;

    public CreateContactCommandHandler(
        IContactRepository contactRepository,
        ContactValidator contactValidator,
        ILogger<CreateContactCommandHandler> logger)
    {
        _contactRepository = contactRepository;
        _contactValidator = contactValidator;
        _logger = logger;
    }

    public async Task<Result<ContactDto>> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate input parameters
            if (request.UserId == Guid.Empty)
            {
                return Result<ContactDto>.Failure("User ID is required");
            }

            _logger.LogInformation("Creating contact for user {UserId} with name '{Name}'",
                request.UserId, request.Name);

            // Validate relationship type
            if (!Enum.IsDefined(typeof(ContactRelationType), request.RelationshipType))
            {
                return Result<ContactDto>.Failure("Invalid relationship type");
            }

            // Create the contact entity
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Name = request.Name?.Trim() ?? string.Empty,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                RelationshipType = request.RelationshipType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Tasks = new List<AppTask>()
            };

            // Validate the contact using FluentValidation
            var validationResult = await _contactValidator.ValidateAsync(contact, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Contact validation failed for user {UserId}: {Errors}", request.UserId, errors);
                return Result<ContactDto>.Failure($"Contact validation failed: {errors}");
            }

            // Check for duplicate email within user's contacts
            if (!string.IsNullOrWhiteSpace(contact.Email))
            {
                var existingContacts = await _contactRepository.FindContactsAsync(
                    contact.Email,
                    request.UserId,
                    false,
                    cancellationToken);

                if (existingContacts.Any())
                {
                    _logger.LogWarning("Contact with email {Email} already exists for user {UserId}",
                        contact.Email, request.UserId);
                    return Result<ContactDto>.Failure("A contact with this email already exists");
                }
            }

            // Save the contact
            await _contactRepository.AddAsync(contact, cancellationToken);
            await _contactRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Contact {ContactId} created successfully for user {UserId}",
                contact.Id, request.UserId);

            // Map to DTO
            var contactDto = MapToContactDto(contact);

            return Result<ContactDto>.Success(contactDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contact for user {UserId}", request.UserId);
            return Result<ContactDto>.Failure($"Error creating contact: {ex.Message}");
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
            ActiveTaskCount = 0, // New contact has no tasks
            AssociatedTasks = new List<ContactTaskDto>()
        };
    }
}
