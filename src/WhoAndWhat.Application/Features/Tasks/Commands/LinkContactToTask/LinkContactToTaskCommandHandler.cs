using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;

namespace WhoAndWhat.Application.Features.Tasks.Commands.LinkContactToTask;

public class LinkContactToTaskCommandHandler : IRequestHandler<LinkContactToTaskCommand, Result<TaskContactDto>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<LinkContactToTaskCommandHandler> _logger;

    // Valid roles for task-contact relationships
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Owner",
        "Collaborator", 
        "Reviewer",
        "Observer"
    };

    public LinkContactToTaskCommandHandler(
        IAppTaskRepository taskRepository,
        IContactRepository contactRepository,
        ILogger<LinkContactToTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<TaskContactDto>> Handle(LinkContactToTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Linking contact {ContactId} to task {TaskId} with role '{Role}' for user {UserId}", 
                request.ContactId, request.TaskId, request.Role, request.UserId);

            // Validate role
            if (string.IsNullOrWhiteSpace(request.Role) || !ValidRoles.Contains(request.Role))
            {
                return Result<TaskContactDto>.Failure($"Invalid role '{request.Role}'. Valid roles are: {string.Join(", ", ValidRoles)}");
            }

            // Get and validate task
            var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
            if (task == null)
            {
                _logger.LogWarning("Task {TaskId} not found for user {UserId}", request.TaskId, request.UserId);
                return Result<TaskContactDto>.Failure("Task not found");
            }

            // Verify task ownership
            if (task.UserId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to link contact to task {TaskId} owned by {OwnerId}", 
                    request.UserId, request.TaskId, task.UserId);
                return Result<TaskContactDto>.Failure("Task not found");
            }

            // Check if task is archived or deleted
            if (task.IsArchived || task.IsDeleted)
            {
                _logger.LogWarning("Cannot link contact to archived or deleted task {TaskId}", request.TaskId);
                return Result<TaskContactDto>.Failure("Cannot link contact to archived or deleted task");
            }

            // Get and validate contact
            var contact = await _contactRepository.GetByIdAsync(request.ContactId, cancellationToken);
            if (contact == null)
            {
                _logger.LogWarning("Contact {ContactId} not found for user {UserId}", request.ContactId, request.UserId);
                return Result<TaskContactDto>.Failure("Contact not found");
            }

            // Verify contact ownership
            if (contact.UserId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to link contact {ContactId} owned by {OwnerId}", 
                    request.UserId, request.ContactId, contact.UserId);
                return Result<TaskContactDto>.Failure("Contact not found");
            }

            // Check if contact is soft deleted
            if (contact.IsDeleted)
            {
                _logger.LogWarning("Cannot link soft-deleted contact {ContactId} to task {TaskId}", 
                    request.ContactId, request.TaskId);
                return Result<TaskContactDto>.Failure("Cannot link deleted contact to task");
            }

            // Check for existing link
            var existingLink = task.TaskContacts?.FirstOrDefault(tc => tc.ContactId == request.ContactId);
            if (existingLink != null)
            {
                _logger.LogWarning("Contact {ContactId} is already linked to task {TaskId} with role '{ExistingRole}'", 
                    request.ContactId, request.TaskId, existingLink.Role);
                return Result<TaskContactDto>.Failure($"Contact is already linked to this task with role '{existingLink.Role}'");
            }

            // Create the task-contact relationship
            var taskContact = new TaskContact
            {
                TaskId = request.TaskId,
                ContactId = request.ContactId,
                Role = request.Role,
                LinkedAt = DateTime.UtcNow,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add to task's contact collection
            if (task.TaskContacts == null)
            {
                task.TaskContacts = new List<TaskContact>();
            }
            task.TaskContacts.Add(taskContact);

            // Update task's modified timestamp
            task.UpdatedAt = DateTime.UtcNow;

            // Save changes
            await _taskRepository.UpdateAsync(task);
            await _taskRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully linked contact {ContactId} to task {TaskId} with role '{Role}' for user {UserId}", 
                request.ContactId, request.TaskId, request.Role, request.UserId);

            // Create response DTO
            var taskContactDto = new TaskContactDto
            {
                TaskId = taskContact.TaskId,
                ContactId = taskContact.ContactId,
                ContactName = contact.Name,
                ContactEmail = contact.Email ?? string.Empty,
                Role = taskContact.Role
            };

            return Result<TaskContactDto>.Success(taskContactDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking contact {ContactId} to task {TaskId} for user {UserId}", 
                request.ContactId, request.TaskId, request.UserId);
            return Result<TaskContactDto>.Failure($"Error linking contact to task: {ex.Message}");
        }
    }
}