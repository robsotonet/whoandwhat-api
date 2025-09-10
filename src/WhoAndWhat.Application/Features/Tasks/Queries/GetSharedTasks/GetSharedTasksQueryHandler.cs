using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.ValueObjects;
using Priority = WhoAndWhat.Domain.ValueObjects.Priority;

namespace WhoAndWhat.Application.Features.Tasks.Queries.GetSharedTasks;

public class GetSharedTasksQueryHandler : IRequestHandler<GetSharedTasksQuery, Result<PagedResult<SharedTaskDto>>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<GetSharedTasksQueryHandler> _logger;

    public GetSharedTasksQueryHandler(
        IAppTaskRepository taskRepository,
        IContactRepository contactRepository,
        ILogger<GetSharedTasksQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<PagedResult<SharedTaskDto>>> Handle(GetSharedTasksQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting shared tasks for user {UserId}", request.UserId);

            // Get tasks where the user is linked as a contact
            var sharedTasks = await GetTasksSharedWithUserAsync(request, cancellationToken);

            // Apply filters
            var filteredTasks = ApplyFilters(sharedTasks, request);

            // Apply search if provided
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                filteredTasks = ApplySearch(filteredTasks, request.SearchTerm);
            }

            // Apply pagination
            var totalCount = filteredTasks.Count();
            var paginatedTasks = filteredTasks
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var result = PagedResult<SharedTaskDto>.Create(
                paginatedTasks,
                totalCount,
                request.PageNumber,
                request.PageSize);

            _logger.LogInformation("Found {Count} shared tasks for user {UserId}", 
                result.Items.Count(), request.UserId);

            return Result<PagedResult<SharedTaskDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shared tasks for user {UserId}", request.UserId);
            return Result<PagedResult<SharedTaskDto>>.Failure($"Error retrieving shared tasks: {ex.Message}");
        }
    }

    private async Task<List<SharedTaskDto>> GetTasksSharedWithUserAsync(GetSharedTasksQuery request, CancellationToken cancellationToken)
    {
        // This is a simplified approach. In a real implementation, you might want to create a specific repository method
        // for better performance with joins and filtering at the database level
        
        var sharedTasks = new List<SharedTaskDto>();
        
        // Find all contacts that belong to the user
        var userContacts = await GetUserContactsAsync(request.UserId, cancellationToken);
        
        foreach (var contact in userContacts)
        {
            // Get tasks where this contact is linked (tasks shared with this user through the contact)
            var contactTasks = await GetTasksForContactAsync(contact.Id, cancellationToken);
            
            foreach (var taskContact in contactTasks)
            {
                var sharedTask = MapToSharedTaskDto(taskContact, contact);
                
                // Apply authorization logic based on role
                ApplyAuthorizationRules(sharedTask);
                
                sharedTasks.Add(sharedTask);
            }
        }

        return sharedTasks;
    }

    private async Task<List<Domain.Entities.Contact>> GetUserContactsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Get all contacts for the user
        var contacts = await _contactRepository.FindContactsAsync("", userId, false, cancellationToken);
        return contacts.ToList();
    }

    private Task<List<Domain.Entities.TaskContact>> GetTasksForContactAsync(Guid contactId, CancellationToken cancellationToken)
    {
        // This is a limitation of the current repository interface
        // In a real implementation, you would have a method like:
        // return await _taskRepository.GetTaskContactsByContactIdAsync(contactId, cancellationToken);
        
        // For now, return empty list as we need to enhance the repository interface
        _logger.LogWarning("GetTasksForContactAsync not fully implemented - repository interface limitation");
        return Task.FromResult(new List<Domain.Entities.TaskContact>());
    }

    private SharedTaskDto MapToSharedTaskDto(Domain.Entities.TaskContact taskContact, Domain.Entities.Contact contact)
    {
        var task = taskContact.Task;
        
        return new SharedTaskDto
        {
            TaskId = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            StatusName = ((AppTaskStatus)task.Status).ToString(),
            Category = task.Category,
            CategoryName = ((AppTaskCategory)task.Category).ToString(),
            Priority = task.Priority,
            PriorityName = ((Priority)task.Priority).ToString(),
            DueDate = task.DueDate,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            ContactId = contact.Id,
            ContactName = contact.Name,
            ContactRole = taskContact.Role,
            LinkedAt = taskContact.LinkedAt,
            ContactNotes = taskContact.Notes,
            TaskOwnerId = task.UserId,
            TaskOwnerName = "Task Owner" // User entity doesn't have Name property
        };
    }

    private void ApplyAuthorizationRules(SharedTaskDto sharedTask)
    {
        // Apply authorization based on the contact's role
        switch (sharedTask.ContactRole.ToLower())
        {
            case "owner":
                sharedTask.CanEdit = true;
                sharedTask.CanDelete = true;
                sharedTask.CanComment = true;
                sharedTask.CanViewDetails = true;
                break;
                
            case "collaborator":
                sharedTask.CanEdit = true;
                sharedTask.CanDelete = false;
                sharedTask.CanComment = true;
                sharedTask.CanViewDetails = true;
                break;
                
            case "reviewer":
                sharedTask.CanEdit = false;
                sharedTask.CanDelete = false;
                sharedTask.CanComment = true;
                sharedTask.CanViewDetails = true;
                break;
                
            case "observer":
                sharedTask.CanEdit = false;
                sharedTask.CanDelete = false;
                sharedTask.CanComment = false;
                sharedTask.CanViewDetails = true;
                break;
                
            default:
                // Default to observer permissions
                sharedTask.CanEdit = false;
                sharedTask.CanDelete = false;
                sharedTask.CanComment = false;
                sharedTask.CanViewDetails = true;
                break;
        }
    }

    private IEnumerable<SharedTaskDto> ApplyFilters(List<SharedTaskDto> tasks, GetSharedTasksQuery request)
    {
        var filtered = tasks.AsEnumerable();

        if (!string.IsNullOrEmpty(request.Role))
        {
            filtered = filtered.Where(t => t.ContactRole.Equals(request.Role, StringComparison.OrdinalIgnoreCase));
        }

        if (request.ContactId.HasValue)
        {
            filtered = filtered.Where(t => t.ContactId == request.ContactId.Value);
        }

        if (request.Status.HasValue)
        {
            filtered = filtered.Where(t => t.Status == request.Status.Value);
        }

        if (request.Category.HasValue)
        {
            filtered = filtered.Where(t => t.Category == request.Category.Value);
        }

        return filtered;
    }

    private IEnumerable<SharedTaskDto> ApplySearch(IEnumerable<SharedTaskDto> tasks, string searchTerm)
    {
        var normalizedSearch = searchTerm.ToLower().Trim();
        
        return tasks.Where(t => 
            t.Title.ToLower().Contains(normalizedSearch) ||
            (t.Description?.ToLower().Contains(normalizedSearch) ?? false) ||
            t.ContactName.ToLower().Contains(normalizedSearch) ||
            t.TaskOwnerName.ToLower().Contains(normalizedSearch));
    }
}