using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Tasks.Commands.UnlinkContactFromTask;

public class UnlinkContactFromTaskCommandHandler : IRequestHandler<UnlinkContactFromTaskCommand, Result<bool>>
{
    private readonly IAppTaskRepository _taskRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ILogger<UnlinkContactFromTaskCommandHandler> _logger;

    public UnlinkContactFromTaskCommandHandler(
        IAppTaskRepository taskRepository,
        IContactRepository contactRepository,
        ILogger<UnlinkContactFromTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UnlinkContactFromTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Unlinking contact {ContactId} from task {TaskId} for user {UserId}",
                request.ContactId, request.TaskId, request.UserId);

            // Get and validate task
            var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
            if (task == null)
            {
                _logger.LogWarning("Task {TaskId} not found for user {UserId}", request.TaskId, request.UserId);
                return Result<bool>.Failure("Task not found");
            }

            // Verify task ownership
            if (task.UserId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to unlink contact from task {TaskId} owned by {OwnerId}",
                    request.UserId, request.TaskId, task.UserId);
                return Result<bool>.Failure("Task not found");
            }

            // Get and validate contact (to ensure user owns the contact)
            var contact = await _contactRepository.GetByIdAsync(request.ContactId, cancellationToken);
            if (contact == null)
            {
                _logger.LogWarning("Contact {ContactId} not found for user {UserId}", request.ContactId, request.UserId);
                return Result<bool>.Failure("Contact not found");
            }

            // Verify contact ownership
            if (contact.UserId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to unlink contact {ContactId} owned by {OwnerId}",
                    request.UserId, request.ContactId, contact.UserId);
                return Result<bool>.Failure("Contact not found");
            }

            // Find the existing relationship
            var existingLink = task.TaskContacts?.FirstOrDefault(tc => tc.ContactId == request.ContactId);
            if (existingLink == null)
            {
                _logger.LogWarning("No existing relationship found between contact {ContactId} and task {TaskId}",
                    request.ContactId, request.TaskId);
                return Result<bool>.Failure("Contact is not linked to this task");
            }

            // Remove the relationship
            if (task.TaskContacts != null)
            {
                task.TaskContacts.Remove(existingLink);
            }

            // Update task's modified timestamp
            task.UpdatedAt = DateTime.UtcNow;

            // Save changes
            await _taskRepository.UpdateAsync(task);
            await _taskRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully unlinked contact {ContactId} from task {TaskId} for user {UserId}. " +
                                 "Contact had role '{Role}'",
                request.ContactId, request.TaskId, request.UserId, existingLink.Role);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking contact {ContactId} from task {TaskId} for user {UserId}",
                request.ContactId, request.TaskId, request.UserId);
            return Result<bool>.Failure($"Error unlinking contact from task: {ex.Message}");
        }
    }
}
