using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Contacts.Queries.GetContact;

public class GetContactQueryHandler : IRequestHandler<GetContactQuery, Result<ContactDto>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<GetContactQueryHandler> _logger;

    public GetContactQueryHandler(
        IContactRepository contactRepository,
        ILogger<GetContactQueryHandler> logger)
    {
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<ContactDto>> Handle(GetContactQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting contact {ContactId} for user {UserId}",
                request.ContactId, request.UserId);

            Domain.Entities.Contact? contact;

            if (request.IncludeTasks)
            {
                contact = await _contactRepository.GetContactWithTasksAsync(
                    request.ContactId,
                    request.UserId,
                    request.IncludeDeleted,
                    cancellationToken);
            }
            else if (request.IncludeDeleted)
            {
                contact = await _contactRepository.GetContactIncludingDeletedAsync(
                    request.ContactId,
                    request.UserId,
                    cancellationToken);
            }
            else
            {
                // Use the base repository method from Repository<Contact>
                contact = await _contactRepository.GetByIdAsync(request.ContactId, cancellationToken);

                // Verify the contact belongs to the user
                if (contact != null && contact.UserId != request.UserId)
                {
                    _logger.LogWarning("Contact {ContactId} does not belong to user {UserId}",
                        request.ContactId, request.UserId);
                    return Result<ContactDto>.Failure("Contact not found");
                }
            }

            if (contact == null)
            {
                _logger.LogWarning("Contact {ContactId} not found for user {UserId}",
                    request.ContactId, request.UserId);
                return Result<ContactDto>.Failure("Contact not found");
            }

            var contactDto = MapToContactDto(contact);

            _logger.LogInformation("Successfully retrieved contact {ContactId} for user {UserId}",
                request.ContactId, request.UserId);

            return Result<ContactDto>.Success(contactDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contact {ContactId} for user {UserId}",
                request.ContactId, request.UserId);
            return Result<ContactDto>.Failure($"Error retrieving contact: {ex.Message}");
        }
    }

    private static ContactDto MapToContactDto(Domain.Entities.Contact contact)
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
