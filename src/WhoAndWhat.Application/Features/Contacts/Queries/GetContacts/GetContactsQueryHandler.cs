using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Contacts.Queries.GetContacts;

public class GetContactsQueryHandler : IRequestHandler<GetContactsQuery, Result<ContactSearchResult>>
{
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<GetContactsQueryHandler> _logger;

    public GetContactsQueryHandler(
        IContactRepository contactRepository,
        ILogger<GetContactsQueryHandler> logger)
    {
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<ContactSearchResult>> Handle(GetContactsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate input parameters
            if (request.UserId == Guid.Empty)
            {
                return Result<ContactSearchResult>.Failure("User ID is required");
            }

            if (request.PageSize <= 0 || request.PageSize > 100)
            {
                return Result<ContactSearchResult>.Failure("Page size must be between 1 and 100");
            }

            if (request.PageNumber <= 0)
            {
                return Result<ContactSearchResult>.Failure("Page number must be greater than 0");
            }

            _logger.LogInformation("Getting contacts for user {UserId} with search: '{Search}'",
                request.UserId, request.Search);

            IEnumerable<Domain.Entities.Contact> contacts;
            int totalCount;

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                // Use search functionality
                contacts = await _contactRepository.FindContactsAsync(
                    request.Search,
                    request.UserId,
                    request.IncludeDeleted,
                    cancellationToken);

                totalCount = contacts.Count();
            }
            else
            {
                // Get all contacts (this will need to be implemented in the repository for pagination)
                contacts = await GetAllContactsAsync(request, cancellationToken);
                totalCount = contacts.Count();
            }

            // Filter by relationship types if specified
            if (request.RelationshipTypes?.Any() == true)
            {
                contacts = contacts.Where(c => request.RelationshipTypes.Contains(c.RelationshipType));
                totalCount = contacts.Count();
            }

            // Apply sorting
            contacts = ApplySorting(contacts, request.SortBy, request.SortDescending);

            // Apply pagination
            var pagedContacts = contacts
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Map to DTOs
            var contactDtos = pagedContacts.Select(MapToContactDto).ToList();

            var result = new ContactSearchResult
            {
                Contacts = contactDtos,
                TotalCount = totalCount,
                Page = request.PageNumber,
                PageSize = request.PageSize,
                SearchQuery = request.Search,
                RelationshipTypes = request.RelationshipTypes,
                IncludeDeleted = request.IncludeDeleted
            };

            _logger.LogInformation("Retrieved {Count} contacts for user {UserId}",
                contactDtos.Count, request.UserId);

            return Result<ContactSearchResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contacts for user {UserId}", request.UserId);
            return Result<ContactSearchResult>.Failure($"Error retrieving contacts: {ex.Message}");
        }
    }

    private async Task<IEnumerable<Domain.Entities.Contact>> GetAllContactsAsync(
        GetContactsQuery request,
        CancellationToken cancellationToken)
    {
        // For now, we'll use a simple approach since we don't have a GetAllForUser method
        // In a real implementation, you'd want to add this to the repository with proper pagination
        return await _contactRepository.FindContactsAsync(
            string.Empty, // Empty search returns all
            request.UserId,
            request.IncludeDeleted,
            cancellationToken) ?? Enumerable.Empty<Domain.Entities.Contact>();
    }

    private static IEnumerable<Domain.Entities.Contact> ApplySorting(
        IEnumerable<Domain.Entities.Contact> contacts,
        string? sortBy,
        bool sortDescending)
    {
        var orderedContacts = sortBy?.ToLower() switch
        {
            "name" => sortDescending
                ? contacts.OrderByDescending(c => c.Name)
                : contacts.OrderBy(c => c.Name),
            "email" => sortDescending
                ? contacts.OrderByDescending(c => c.Email ?? string.Empty)
                : contacts.OrderBy(c => c.Email ?? string.Empty),
            "relationshiptype" => sortDescending
                ? contacts.OrderByDescending(c => c.RelationshipType)
                : contacts.OrderBy(c => c.RelationshipType),
            "createdat" => sortDescending
                ? contacts.OrderByDescending(c => c.CreatedAt)
                : contacts.OrderBy(c => c.CreatedAt),
            _ => sortDescending
                ? contacts.OrderByDescending(c => c.Name)
                : contacts.OrderBy(c => c.Name)
        };

        return orderedContacts;
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
