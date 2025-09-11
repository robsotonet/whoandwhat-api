using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Tasks;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Tasks.Commands.UpdateContactRole;

public class UpdateContactRoleCommandHandler : IRequestHandler<UpdateContactRoleCommand, Result<TaskContactDto>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<UpdateContactRoleCommandHandler> _logger;

    // Valid roles for task-contact relationships
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Owner",
        "Collaborator",
        "Reviewer",
        "Observer"
    };

    public UpdateContactRoleCommandHandler(
        IAppTaskRepository taskRepository,
        IContactRepository contactRepository,
        ILogger<UpdateContactRoleCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<TaskContactDto>> Handle(UpdateContactRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating role for contact {ContactId} in task {TaskId} to '{NewRole}' for user {UserId}",
                request.ContactId, request.TaskId, request.NewRole, request.UserId);

            // Validate new role
            if (string.IsNullOrWhiteSpace(request.NewRole) || !ValidRoles.Contains(request.NewRole))
            {
                return Result<TaskContactDto>.Failure($"Invalid role '{request.NewRole}'. Valid roles are: {string.Join(", ", ValidRoles)}");
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
                _logger.LogWarning("User {UserId} attempted to update contact role in task {TaskId} owned by {OwnerId}",
                    request.UserId, request.TaskId, task.UserId);
                return Result<TaskContactDto>.Failure("Task not found");
            }

            // Check if task is archived or deleted
            if (task.IsArchived || task.IsDeleted)
            {
                _logger.LogWarning("Cannot update contact role in archived or deleted task {TaskId}", request.TaskId);
                return Result<TaskContactDto>.Failure("Cannot update contact role in archived or deleted task");
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
                _logger.LogWarning("User {UserId} attempted to update role for contact {ContactId} owned by {OwnerId}",
                    request.UserId, request.ContactId, contact.UserId);
                return Result<TaskContactDto>.Failure("Contact not found");
            }

            // Check if contact is soft deleted
            if (contact.IsDeleted)
            {
                _logger.LogWarning("Cannot update role for soft-deleted contact {ContactId} in task {TaskId}",
                    request.ContactId, request.TaskId);
                return Result<TaskContactDto>.Failure("Cannot update role for deleted contact");
            }

            // Find the existing relationship
            var existingLink = task.TaskContacts?.FirstOrDefault(tc => tc.ContactId == request.ContactId);
            if (existingLink == null)
            {
                _logger.LogWarning("No existing relationship found between contact {ContactId} and task {TaskId}",
                    request.ContactId, request.TaskId);
                return Result<TaskContactDto>.Failure("Contact is not linked to this task");
            }

            // Check if role is actually changing
            if (string.Equals(existingLink.Role, request.NewRole, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Contact {ContactId} already has role '{Role}' in task {TaskId}. No update needed.",
                    request.ContactId, request.NewRole, request.TaskId);

                // Return current state
                var currentDto = new TaskContactDto
                {
                    TaskId = existingLink.TaskId,
                    ContactId = existingLink.ContactId,
                    ContactName = contact.Name,
                    ContactEmail = contact.Email ?? string.Empty,
                    Role = existingLink.Role
                };
                return Result<TaskContactDto>.Success(currentDto);
            }

            // Log the role change for audit purposes
            var oldRole = existingLink.Role;
            _logger.LogInformation("Changing contact {ContactId} role in task {TaskId} from '{OldRole}' to '{NewRole}' for user {UserId}",
                request.ContactId, request.TaskId, oldRole, request.NewRole, request.UserId);

            // Update the relationship
            existingLink.Role = request.NewRole;
            existingLink.UpdatedAt = DateTime.UtcNow;

            // Update notes if provided
            if (request.Notes != null)
            {
                existingLink.Notes = request.Notes;
            }

            // Update task's modified timestamp
            task.UpdatedAt = DateTime.UtcNow;

            // Save changes
            await _taskRepository.UpdateAsync(task);
            await _taskRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully updated contact {ContactId} role in task {TaskId} from '{OldRole}' to '{NewRole}' for user {UserId}",
                request.ContactId, request.TaskId, oldRole, request.NewRole, request.UserId);

            // Create response DTO
            var taskContactDto = new TaskContactDto
            {
                TaskId = existingLink.TaskId,
                ContactId = existingLink.ContactId,
                ContactName = contact.Name,
                ContactEmail = contact.Email ?? string.Empty,
                Role = existingLink.Role
            };

            return Result<TaskContactDto>.Success(taskContactDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contact {ContactId} role in task {TaskId} for user {UserId}",
                request.ContactId, request.TaskId, request.UserId);
            return Result<TaskContactDto>.Failure($"Error updating contact role: {ex.Message}");
        }
    }
}
